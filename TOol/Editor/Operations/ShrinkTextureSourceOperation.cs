using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   这个操作只回答一个问题——"这张贴图的源文件超标了，该缩到多大写回去"。
//   格式怎么解、怎么编，交给 Codec 层；缩放怎么做，交给 TextureScaler；
//   阈值多少、JPG 质量多少、最小边长多少，全部从配置资产读，这里不写死任何数值。
//
// 为什么用二分搜索而不是"每轮减半"：
//   减半是一种很粗的步长。一张 4096 的图如果 4096 超标、3000 就达标，减半会直接
//   跳到 2048——白扔掉一半分辨率。二分是在 [最小边长, 原始最长边] 这个区间上找
//   "最大的、编码后仍然达标的尺寸"，最终结果是恰好卡在阈值下沿，画质损失最小。
//   代价是要多编码几次（上限由配置里的 maxSearchIterations 控制，默认 12 次
//   足够在 8192 到 64 之间定位到 1 像素精度），所以每一步都会上报进度。
//
// 但二分默认不用于二的幂贴图（重要，别当成性能倒退改回去）：
//   连续二分算出来的尺寸几乎一定不是二的幂（4096 可能停在 2913）。而交付打包工具
//   会对每张非二的幂贴图记一条问题项、并在 Console 打警告——也就是说"压缩成功"会
//   直接换来"交付报告告警"，两个插件的目标互相打架。
//   所以源图长宽都是二的幂时，改在对折阶梯（2048→1024→512…）上找最大的达标档位：
//   对折同时作用于长宽，长宽比精确不变，结果仍是二的幂。代价是最多浪费一档分辨率，
//   换来的是交付端零告警，这个取舍对交付流程是划算的。
//   源图本来就不是二的幂时没有什么可保的，仍然走连续二分。
//
// 两种搜索共同的前提假设：尺寸越小、编码后的文件越小。PNG / JPG / TGA-RLE 都满足。
// 如果以后接入某种"尺寸变小反而变大"的怪格式，搜索会退化成找不到解，
// 那时会走下面的最小边长兜底并给出明确报错，不会静默出错。
//
// 关于第一步"原尺寸重新编码"：
//   这一步经常直接命中，尤其是 TGA——很多 DCC 软件默认导出未压缩 TGA，
//   一张 2048x2048 未压缩就是 16 MB，但换成 RLE 可能只有 3 MB。
//   这种情况完全不需要降分辨率，画质零损失，所以必须先试这一步。
// =====================================================================================
public class ShrinkTextureSourceOperation : ITextureAssetOperation
{
    public string Id
    {
        get { return "shrink_source_file"; }
    }

    public string DisplayName
    {
        get { return "压缩超标的贴图源文件"; }
    }

    public string Description
    {
        get
        {
            return "磁盘上的贴图源文件超过配置阈值时，先尝试原尺寸重新编码；仍然超标则找出达标的最大尺寸" +
                   "（二的幂贴图走对折阶梯，其余走连续二分），等比缩放后覆盖源文件并重新导入。" +
                   "会直接改写源文件，不做备份。";
        }
    }

    public int Order
    {
        get { return 100; }
    }

    public bool CanProcess(string assetPath, TextureProcessSettings settings)
    {
        // 这里必须足够便宜：只看扩展名和文件长度，不解码。
        // 导入回调会对一整批（可能上千个）资产逐个调用它。
        if (!TextureCodecRegistry.IsSupported(assetPath))
        {
            return false;
        }

        long length = TextureAssetPathUtility.GetFileLength(assetPath);
        return length > 0 && settings != null && length > settings.MaxSourceBytes;
    }

    /// <summary>
    /// 总体流程：读源文件字节 → 解码成像素 → 二分找出达标的最大尺寸 → 写回源文件并重新导入。
    /// </summary>
    public TextureOperationResult Execute(TextureOperationContext context)
    {
        // 手动在窗口里选中 .fbm 里的贴图也拦下来，但要说清楚原因——
        // 静默跳过会让人以为工具坏了，而直接执行又是白做（见 IsInsideEmbeddedMediaFolder）。
        if (TextureAssetPathUtility.IsInsideEmbeddedMediaFolder(context.AssetPath))
        {
            return TextureOperationResult.Skipped(
                "这是 Unity 从 FBX 抽取内嵌贴图生成的 .fbm 缓存目录，模型下次重新导入就会被原始数据覆盖，" +
                "压了也留不住。请改为压缩打包工具平铺到 Assets/Art/<模型>/Texture/ 之后的那一份。");
        }

        string fullPath = TextureAssetPathUtility.ToFullPath(context.AssetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return TextureOperationResult.Failed("磁盘上找不到这个文件，可能刚刚被移动或删除。");
        }

        ITextureFileCodec codec = TextureCodecRegistry.FindByAssetPath(context.AssetPath);
        if (codec == null)
        {
            return TextureOperationResult.Skipped("没有支持这种扩展名的编解码器。");
        }

        byte[] originalBytes = File.ReadAllBytes(fullPath);
        if (originalBytes.LongLength <= context.Settings.MaxSourceBytes)
        {
            return TextureOperationResult.Skipped("体积已在阈值以内。");
        }

        context.ReportSubProgress(0f, "解码源文件…");
        Texture2D decoded;
        string decodeError;
        if (!codec.TryDecode(originalBytes, out decoded, out decodeError))
        {
            return TextureOperationResult.Failed("解码失败（" + codec.DisplayName + "）: " + decodeError);
        }

        try
        {
            return ShrinkAndWriteBack(context, codec, decoded, originalBytes, fullPath);
        }
        finally
        {
            // 解码出来的 Texture2D 是脱离 AssetDatabase 的临时对象，
            // 不手动销毁会一直留在编辑器内存里，批量处理时很快就上 GB。
            Object.DestroyImmediate(decoded);
        }
    }

    private TextureOperationResult ShrinkAndWriteBack(
        TextureOperationContext context,
        ITextureFileCodec codec,
        Texture2D decoded,
        byte[] originalBytes,
        string fullPath)
    {
        EncodeAttempt best = FindLargestSizeUnderLimit(context, codec, decoded);
        if (!best.Succeeded)
        {
            return TextureOperationResult.Failed(best.FailureReason);
        }

        if (best.Bytes.LongLength >= originalBytes.LongLength)
        {
            // 极少见：源文件本身已经是高度优化过的编码，我们重编只会更大。
            // 这时候覆盖回去纯亏——既没减小体积，又白搭一次画质损失。
            return TextureOperationResult.Skipped(
                "重新编码后体积没有变小（" + TextureAssetPathUtility.FormatBytes(originalBytes.LongLength) +
                " -> " + TextureAssetPathUtility.FormatBytes(best.Bytes.LongLength) + "），保留原文件。");
        }

        File.WriteAllBytes(fullPath, best.Bytes);

        // 源文件字节已经变了，必须强制重新导入，否则编辑器里显示的、打进包里的
        // 都还是旧的导入缓存。这里能安全调用 ImportAsset 的前提是：
        // 本操作永远由 TextureSourceFileProcessor 推迟到导入流程【结束之后】才执行，
        // 不是在 OnPostprocessAllAssets 的调用栈里，所以不构成嵌套导入。
        AssetDatabase.ImportAsset(context.AssetPath, ImportAssetOptions.ForceUpdate);

        return TextureOperationResult.Changed(
            TextureAssetPathUtility.FormatBytes(originalBytes.LongLength) + " -> " +
            TextureAssetPathUtility.FormatBytes(best.Bytes.LongLength) + "，尺寸 " +
            decoded.width + "x" + decoded.height + " -> " + best.Width + "x" + best.Height +
            "（编码 " + best.EncodeCount + " 次）");
    }

    // ---------------------------------------------------------------------------
    // 尺寸搜索：找"最大的、编码后仍然达标的尺寸"
    // ---------------------------------------------------------------------------

    /// <summary>
    /// 总体流程：先试原尺寸（无损，命中就不用降分辨率）→ 仍超标则按源图是否为二的幂
    /// 选一种搜索策略 → 两种策略都找不到解时统一走最小边长兜底。
    /// </summary>
    private EncodeAttempt FindLargestSizeUnderLimit(
        TextureOperationContext context,
        ITextureFileCodec codec,
        Texture2D decoded)
    {
        int encodeCount = 0;

        context.ReportSubProgress(0.05f, "尝试原尺寸重新编码…");
        EncodeAttempt originalSizeAttempt = TryEncodeAtLongestSide(
            codec, context.Settings, decoded, Mathf.Max(decoded.width, decoded.height), ref encodeCount);
        if (!originalSizeAttempt.Succeeded)
        {
            return originalSizeAttempt;
        }

        if (originalSizeAttempt.Bytes.LongLength <= context.Settings.MaxSourceBytes)
        {
            return originalSizeAttempt;
        }

        EncodeAttempt best = ShouldSearchOnPowerOfTwoLadder(context.Settings, decoded)
            ? SearchPowerOfTwoLadder(context, codec, decoded, ref encodeCount)
            : SearchByBisection(context, codec, decoded, ref encodeCount);

        if (best.Succeeded)
        {
            best.EncodeCount = encodeCount;
            return best;
        }

        // 区分两种"没拿到结果"：编码器真的报错要原样上报，
        // 只是区间里没找到达标尺寸才值得再试一次最小边长。
        if (best.Failed)
        {
            return best;
        }

        return BuildMinimumSizeFallback(context, codec, decoded, encodeCount);
    }

    /// <summary>
    /// 只有"配置要求保持二的幂"且"源图长宽本来就都是二的幂"时才走阶梯搜索。
    /// 源图已经是 1023x512 这种尺寸的话，对折也变不成二的幂，强行阶梯只会白丢分辨率。
    /// </summary>
    private static bool ShouldSearchOnPowerOfTwoLadder(TextureProcessSettings settings, Texture2D decoded)
    {
        return settings.preservePowerOfTwo && IsPowerOfTwo(decoded.width) && IsPowerOfTwo(decoded.height);
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    /// <summary>
    /// 在对折阶梯上从大到小逐档试，第一个达标的档位就是最大的达标档位，直接返回。
    /// 阶梯本身很短（8192 到 64 只有 8 档），不需要二分，顺序扫反而更省编码次数。
    /// </summary>
    private EncodeAttempt SearchPowerOfTwoLadder(
        TextureOperationContext context,
        ITextureFileCodec codec,
        Texture2D decoded,
        ref int encodeCount)
    {
        int minimum = Mathf.Max(1, context.Settings.minDimension);
        int sourceLongestSide = Mathf.Max(decoded.width, decoded.height);

        // 原尺寸已经在上一步证明超标，所以从对折后的那一档开始。
        int candidate = sourceLongestSide / 2;
        int step = 0;
        int totalSteps = CountLadderSteps(sourceLongestSide, minimum);

        while (candidate >= minimum && candidate >= 1)
        {
            step++;
            context.ReportSubProgress(
                0.1f + 0.85f * step / Mathf.Max(1, totalSteps),
                "对折到最长边 " + candidate + " 像素（第 " + step + "/" + totalSteps + " 档）");

            EncodeAttempt attempt = TryEncodeAtLongestSide(codec, context.Settings, decoded, candidate, ref encodeCount);
            if (!attempt.Succeeded || attempt.Bytes.LongLength <= context.Settings.MaxSourceBytes)
            {
                return attempt;
            }

            candidate /= 2;
        }

        return EncodeAttempt.NotFound();
    }

    private static int CountLadderSteps(int sourceLongestSide, int minimum)
    {
        int count = 0;
        for (int size = sourceLongestSide / 2; size >= minimum && size >= 1; size /= 2)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// 连续二分：上界排除原尺寸（刚证明它超标），下界是配置的最小边长。
    /// 结果最贴近阈值下沿、画质损失最小，但尺寸通常不是二的幂。
    /// </summary>
    private EncodeAttempt SearchByBisection(
        TextureOperationContext context,
        ITextureFileCodec codec,
        Texture2D decoded,
        ref int encodeCount)
    {
        long limit = context.Settings.MaxSourceBytes;
        int sourceLongestSide = Mathf.Max(decoded.width, decoded.height);
        int low = Mathf.Min(context.Settings.minDimension, sourceLongestSide - 1);
        int high = sourceLongestSide - 1;
        EncodeAttempt best = EncodeAttempt.NotFound();
        int maxIterations = Mathf.Max(1, context.Settings.maxSearchIterations);

        for (int iteration = 0; iteration < maxIterations && low <= high; iteration++)
        {
            int candidateLongestSide = low + (high - low) / 2;
            context.ReportSubProgress(
                0.1f + 0.85f * (iteration + 1) / maxIterations,
                "二分尝试最长边 " + candidateLongestSide + " 像素（第 " + (iteration + 1) + "/" + maxIterations + " 次）");

            EncodeAttempt attempt = TryEncodeAtLongestSide(codec, context.Settings, decoded, candidateLongestSide, ref encodeCount);
            if (!attempt.Succeeded)
            {
                return attempt;
            }

            if (attempt.Bytes.LongLength <= limit)
            {
                // 达标了，记为当前最优，然后往"更大"的一半继续找，看能不能少损失一点。
                best = attempt;
                low = candidateLongestSide + 1;
            }
            else
            {
                high = candidateLongestSide - 1;
            }
        }

        return best;
    }

    /// <summary>
    /// 搜索区间里一个达标的尺寸都没找到（比如阈值设得极小，或者迭代次数用完了）。
    /// 这时候用配置的最小边长再编一次：能达标就用它，仍然超标就明确报错，
    /// 绝不静默把一张超标的图写回去让它蒙混过关。
    /// </summary>
    private EncodeAttempt BuildMinimumSizeFallback(
        TextureOperationContext context,
        ITextureFileCodec codec,
        Texture2D decoded,
        int encodeCount)
    {
        context.ReportSubProgress(0.95f, "二分未找到达标尺寸，回退到最小边长…");
        EncodeAttempt fallback = TryEncodeAtLongestSide(
            codec, context.Settings, decoded, context.Settings.minDimension, ref encodeCount);

        if (!fallback.Succeeded)
        {
            return fallback;
        }

        if (fallback.Bytes.LongLength <= context.Settings.MaxSourceBytes)
        {
            fallback.EncodeCount = encodeCount;
            return fallback;
        }

        return EncodeAttempt.Failure(
            "即使缩到配置的最小边长 " + context.Settings.minDimension + " 像素，编码后仍有 " +
            TextureAssetPathUtility.FormatBytes(fallback.Bytes.LongLength) + "，超过阈值 " +
            TextureAssetPathUtility.FormatBytes(context.Settings.MaxSourceBytes) + "。" +
            "请确认阈值是否设得过小；若是 PNG，可考虑改用 JPG 或减少通道数。");
    }

    private EncodeAttempt TryEncodeAtLongestSide(
        ITextureFileCodec codec,
        TextureProcessSettings settings,
        Texture2D decoded,
        int targetLongestSide,
        ref int encodeCount)
    {
        int targetWidth;
        int targetHeight;
        TextureScaler.CalculateProportionalSize(
            decoded.width, decoded.height, targetLongestSide, out targetWidth, out targetHeight);

        bool needsScaling = targetWidth != decoded.width || targetHeight != decoded.height;
        Texture2D encodeSource = needsScaling ? TextureScaler.Scale(decoded, targetWidth, targetHeight) : decoded;
        try
        {
            byte[] bytes;
            string encodeError;
            encodeCount++;
            if (!codec.TryEncode(encodeSource, settings, out bytes, out encodeError))
            {
                return EncodeAttempt.Failure("编码失败（" + codec.DisplayName + "）: " + encodeError);
            }

            return EncodeAttempt.Success(bytes, encodeSource.width, encodeSource.height, encodeCount);
        }
        finally
        {
            if (needsScaling)
            {
                Object.DestroyImmediate(encodeSource);
            }
        }
    }

    private struct EncodeAttempt
    {
        public byte[] Bytes;
        public int Width;
        public int Height;
        public int EncodeCount;
        public string FailureReason;

        /// <summary>没有 FailureReason 且拿到了字节，就算这一次尝试成功（不代表体积达标）。</summary>
        public bool Succeeded
        {
            get { return string.IsNullOrEmpty(FailureReason) && Bytes != null; }
        }

        /// <summary>编解码器明确报错。和"搜索区间里没找到达标尺寸"是两回事，不能混。</summary>
        public bool Failed
        {
            get { return !string.IsNullOrEmpty(FailureReason); }
        }

        public static EncodeAttempt Success(byte[] bytes, int width, int height, int encodeCount)
        {
            return new EncodeAttempt { Bytes = bytes, Width = width, Height = height, EncodeCount = encodeCount };
        }

        public static EncodeAttempt Failure(string reason)
        {
            return new EncodeAttempt { FailureReason = reason };
        }

        /// <summary>二分过程中"还没找到达标解"的初始状态，不是错误。</summary>
        public static EncodeAttempt NotFound()
        {
            return new EncodeAttempt();
        }
    }
}
