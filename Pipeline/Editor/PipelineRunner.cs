using System.Collections.Generic;
using System.IO;
using UnityEngine;

// =====================================================================================
// Pipeline — 编排入口（导入区 1 入库 → 处理区③④⑤ → 输出区⑥）
// =====================================================================================

/// <summary>
/// 流程编排。设置自动不主动调用——依赖导入时 Unity AssetPostprocessor。
/// </summary>
public static class PipelineRunner
{
    /// <summary>按选项跑一遍。</summary>
    public static PipelineResult Run(PipelineOptions options)
    {
        var result = new PipelineResult();
        if (options == null)
        {
            result.Fail(PipelineErrorCodes.BadArgs, "PipelineOptions 为 null");
            return result;
        }

        bool quiet = options.Quiet;

        if (!ImportFromBindings(options, result))
        {
            LogResult(result);
            return result;
        }

        // ③ Prefab
        List<string> prefabPaths = options.PrefabPaths != null
            ? new List<string>(options.PrefabPaths)
            : new List<string>();

        if (options.RunPrefab)
        {
            if (options.ModelPaths == null || options.ModelPaths.Count == 0)
            {
                result.Fail(PipelineErrorCodes.BadArgs, "RunPrefab=true 但无模型路径");
                LogResult(result);
                return result;
            }

            prefabPaths = BuildPrefabsPerBinding(options, result);
            if (prefabPaths == null)
            {
                LogResult(result);
                return result;
            }

            result.PrefabOutputs.AddRange(prefabPaths);
            result.Info("[Pipeline] ③ Prefab 成功 " + prefabPaths.Count + " / " + options.ModelPaths.Count);

            if (prefabPaths.Count == 0)
            {
                result.Fail(PipelineErrorCodes.PrefabFailed, "未生成任何 Prefab");
                LogResult(result);
                return result;
            }
        }
        else if (options.RunAb && prefabPaths.Count == 0)
        {
            result.Fail(PipelineErrorCodes.BadArgs, "RunPrefab=false 且 PrefabPaths 为空，无法打 AB");
            LogResult(result);
            return result;
        }

        // ④ 依赖③：无 Prefab 则强制跳过（面板连锁；此处防手组 Options / CLI）
        if (options.RunFlatten && !options.RunPrefab)
        {
            result.Info("[Pipeline] ④ 已请求但③未开，已跳过（④依赖③）");
            options.RunFlatten = false;
        }

        // ④ 平铺（可选）→ 成功后⑥改打 Art Prefab
        if (options.RunFlatten)
        {
            if (prefabPaths.Count == 0)
            {
                result.Fail(PipelineErrorCodes.BadArgs, "RunFlatten=true 但无 Prefab 路径");
                LogResult(result);
                return result;
            }

            List<string> artPrefabPaths = FlattenPerPrefab(options, prefabPaths, quiet, result);
            if (artPrefabPaths == null)
            {
                LogResult(result);
                return result;
            }

            prefabPaths = artPrefabPaths;
            result.Info("[Pipeline] ④→⑥ 改用 Art Prefab × " + prefabPaths.Count);
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                result.Info("  Art: " + prefabPaths[i]);
            }

            // D17：⑤ 扫本次 Art 单元（含 Model/），不读 L1 Prefs。
            if (options.RunPostProcess &&
                (options.PostProcessFolderPaths == null || options.PostProcessFolderPaths.Count == 0))
            {
                List<string> artUnits = CollectArtUnitFolders(artPrefabPaths);
                if (artUnits.Count > 0)
                {
                    options.PostProcessFolderPaths = artUnits;
                    result.Info("[Pipeline] ④→⑤ 扫描 Art 单元 × " + artUnits.Count);
                    for (int i = 0; i < artUnits.Count; i++)
                    {
                        result.Info("  ⑤: " + artUnits[i]);
                    }
                }
            }
        }

        // ⑤ 依赖④：未平铺则强制跳过
        if (options.RunPostProcess && !options.RunFlatten)
        {
            result.Info("[Pipeline] ⑤ 已请求但④未开，已跳过（⑤依赖④）");
            options.RunPostProcess = false;
        }

        // ⑤ = 代跑 L1「按批量路径执行全部」（手动内核），不是导入期自动流。
        if (options.RunPostProcess)
        {
            ToolPostProcessResult post = ToolPostProcessApi.RunMasterBatch(options.PostProcessFolderPaths);
            ApplyPostProcessResult(result, post, "[Pipeline] ⑤ PostProcess");

            // 与 UnityGLTF 菜单导出解耦：⑤ 结束后立刻报告工程内 Mesh 是否全白。
            string diagnose = ModelVertexColorDiagnose.DiagnosePaths(options.PostProcessFolderPaths);
            result.Info(diagnose);
            Debug.Log(diagnose);
        }

        // ⑥ AB（Options：输出根 / 可选 UP）
        if (options.RunAb)
        {
            RetinarAbBuildOptions abOpt = options.AbBuildOptions;
            if (abOpt == null)
            {
                abOpt = RetinarAbBuildOptions.FromExportSettings(
                    RetinarExportSettings.Current,
                    quietOverride: options.Quiet);
            }
            else
            {
                abOpt.Quiet = options.Quiet;
            }

            RetinarAbBuildResult ab = RetinarAbApi.Build(prefabPaths, abOpt);
            if (ab.BuiltBundleFiles != null)
            {
                result.AbOutputs.AddRange(ab.BuiltBundleFiles);
            }

            result.Info("[Pipeline] ⑥ Ab 成功 " + ab.OkNames.Count +
                        " 失败行 " + ab.FailLines.Count +
                        " 交付根=" + abOpt.NormalizedDeliverableRoot +
                        (abOpt.ExportUnityPackage ? " +UP" : string.Empty));
            for (int i = 0; i < ab.FailLines.Count; i++)
            {
                result.Info("  AB fail: " + ab.FailLines[i]);
            }

            if (!ab.PartialOk)
            {
                result.Fail(PipelineErrorCodes.AbFailed, "Build AB 全部失败");
                LogResult(result);
                return result;
            }

            // ⑥ 重导冲色：仍用通道 3（同一 RunMasterBatch 只跑模型），不走导入钩子打 Art。
            if (options.RunPostProcess &&
                options.PostProcessFolderPaths != null &&
                options.PostProcessFolderPaths.Count > 0)
            {
                string postAbDiag = ModelVertexColorDiagnose.DiagnosePaths(options.PostProcessFolderPaths);
                result.Info("[Pipeline] ⑥ 后\n" + postAbDiag);
                Debug.Log(postAbDiag);

                if (!ModelVertexColorDiagnose.AreAllWhite(options.PostProcessFolderPaths))
                {
                    result.Info("[Pipeline] ⑥ 后顶点色被冲掉 → 仅重跑模型刷白并重打 AB");
                    ToolPostProcessResult reWhite = ToolPostProcessApi.RunMasterBatch(
                        options.PostProcessFolderPaths,
                        includeTexture: false,
                        includeMaterial: false,
                        includeModel: true);
                    ApplyPostProcessResult(result, reWhite, "[Pipeline] ⑥后重刷白");

                    string afterWhite = ModelVertexColorDiagnose.DiagnosePaths(options.PostProcessFolderPaths);
                    result.Info(afterWhite);
                    Debug.Log(afterWhite);

                    if (ModelVertexColorDiagnose.AreAllWhite(options.PostProcessFolderPaths))
                    {
                        result.AbOutputs.Clear();
                        RetinarAbBuildResult ab2 = RetinarAbApi.Build(prefabPaths, abOpt);
                        if (ab2.BuiltBundleFiles != null)
                        {
                            result.AbOutputs.AddRange(ab2.BuiltBundleFiles);
                        }

                        result.Info("[Pipeline] ⑥ 重打 Ab 成功 " + ab2.OkNames.Count +
                                    " 失败行 " + ab2.FailLines.Count);
                        for (int i = 0; i < ab2.FailLines.Count; i++)
                        {
                            result.Info("  AB fail: " + ab2.FailLines[i]);
                        }

                        if (!ab2.PartialOk)
                        {
                            result.Fail(PipelineErrorCodes.AbFailed, "重打 AB 全部失败");
                            LogResult(result);
                            return result;
                        }
                    }
                    else
                    {
                        result.Info("[Pipeline] 重刷白后仍非全白，未重打 AB（交付物可能仍含非白顶点色）");
                    }
                }
            }
        }

        LogResult(result);
        return result;
    }

    /// <summary>
    /// 读 Bindings（空则用 SourcePath+MaterialId 合成一行）。逐行 1 入库 + 2.5 ctx。
    /// 无表且已有 ModelPaths 时沿用（不入库）。
    /// </summary>
    private static bool ImportFromBindings(PipelineOptions options, PipelineResult result)
    {
        List<PipelineSourceBinding> rows = NormalizeBindings(options);
        if (rows.Count == 0)
        {
            if (options.ModelPaths != null && options.ModelPaths.Count > 0)
            {
                AttachContextsFromModelPaths(options, result);
                return true;
            }

            if (options.RunPrefab ||
                (options.RunAb && (options.PrefabPaths == null || options.PrefabPaths.Count == 0)))
            {
                result.Fail(PipelineErrorCodes.BadArgs, "[Pipeline] 未提供 SourceBindings / SourcePath / ModelPaths");
                return false;
            }

            return true;
        }

        options.SourceBindings = rows;
        options.SourcePath = rows[0].SourcePath;
        options.MaterialId = rows[0].MaterialId;
        options.ModelPaths = new List<string>();
        options.JobContexts = new List<PipelineJobContext>();
        options.JobContext = null;

        result.Info("[Pipeline] Bindings × " + rows.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            PipelineSourceBinding row = rows[i];
            string assetPath;
            if (!ImportOne(options, row, i, result, out assetPath))
            {
                return false;
            }

            options.ModelPaths.Add(assetPath);
            AttachOneContext(options, assetPath, result);
        }

        return true;
    }

    private static List<PipelineSourceBinding> NormalizeBindings(PipelineOptions options)
    {
        var rows = new List<PipelineSourceBinding>();
        if (options.SourceBindings != null)
        {
            for (int i = 0; i < options.SourceBindings.Count; i++)
            {
                PipelineSourceBinding src = options.SourceBindings[i];
                if (src == null || string.IsNullOrWhiteSpace(src.SourcePath))
                {
                    continue;
                }

                rows.Add(src.CloneWith(src.SourcePath.Trim().Replace("\\", "/")));
            }
        }

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(options.SourcePath))
        {
            rows.Add(new PipelineSourceBinding(
                options.SourcePath.Trim().Replace("\\", "/"),
                options.MaterialId));
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i].MaterialId))
            {
                rows[i].MaterialId = PipelineMaterialId.SuggestDefault(rows[i].SourcePath);
            }
            else
            {
                rows[i].MaterialId = rows[i].MaterialId.Trim();
            }
        }

        return rows;
    }

    private static bool ImportOne(
        PipelineOptions options,
        PipelineSourceBinding row,
        int index,
        PipelineResult result,
        out string assetPath)
    {
        assetPath = null;
        string source = row.SourcePath;
        string id2 = row.MaterialId;
        string label = "[Pipeline] 1 入库 [" + (index + 1) + "] ";

        if (options.RunImport)
        {
            string importMsg;
            if (!ToolImportApi.ImportSingleModel(source, id2, out assetPath, out importMsg))
            {
                result.Fail(PipelineErrorCodes.ImportFailed, label + "失败: " + importMsg);
                return false;
            }

            result.Info(label + importMsg + " ID2=" + id2);
            return true;
        }

        if (IsExternalDiskPath(source))
        {
            result.Fail(PipelineErrorCodes.BadArgs,
                "[Pipeline] 工程外路径需要打开 1 入库: " + source);
            return false;
        }

        string msg;
        if (!ToolImportApi.ImportSingleModel(source, id2, out assetPath, out msg))
        {
            result.Fail(PipelineErrorCodes.BadArgs, "[Pipeline] 无法解析工程内模型: " + msg);
            return false;
        }

        result.Info(label + "跳过拷贝: " + assetPath + " ID2=" + id2);
        return true;
    }

    private static void AttachContextsFromModelPaths(PipelineOptions options, PipelineResult result)
    {
        options.JobContexts = new List<PipelineJobContext>();
        options.JobContext = null;
        if (options.ModelPaths == null)
        {
            return;
        }

        for (int i = 0; i < options.ModelPaths.Count; i++)
        {
            AttachOneContext(options, options.ModelPaths[i], result);
        }
    }

    private static void AttachOneContext(PipelineOptions options, string assetPath, PipelineResult result)
    {
        if (options.JobContexts == null)
        {
            options.JobContexts = new List<PipelineJobContext>();
        }

        PipelineJobContext ctx = PipelineJobContext.Build(assetPath);
        options.JobContexts.Add(ctx);
        if (options.JobContext == null)
        {
            options.JobContext = ctx;
        }

        if (ctx == null)
        {
            return;
        }

        result.Info(ctx.ToLogString());
        if (!ctx.MainAssetOk && options.RunPrefab)
        {
            result.Info("[Pipeline] ctx.MainAssetOk=false → ③ 空列表仍映射 PrefabFailed(30)（默认，不改 20）");
        }
    }

    /// <summary>③ 每行自己的 ID2；不把一个 Id 罩整表（避免内核再追加 stem）。</summary>
    private static List<string> BuildPrefabsPerBinding(PipelineOptions options, PipelineResult result)
    {
        var prefabPaths = new List<string>();
        IList<PipelineSourceBinding> rows = options.SourceBindings;
        for (int i = 0; i < options.ModelPaths.Count; i++)
        {
            string id2 = null;
            if (rows != null && i < rows.Count)
            {
                id2 = rows[i].MaterialId;
            }
            else if (i == 0)
            {
                id2 = options.MaterialId;
            }

            List<string> written = ToolPrefabApi.BuildPrefabs(
                new[] { options.ModelPaths[i] },
                id2);
            if (written == null || written.Count == 0)
            {
                result.Fail(
                    PipelineErrorCodes.PrefabFailed,
                    "[Pipeline] ③ [" + (i + 1) + "] 未生成 Prefab: " + options.ModelPaths[i]);
                return null;
            }

            prefabPaths.AddRange(written);
            result.Info("[Pipeline] ③ [" + (i + 1) + "] " + written[0] + " ID2=" + id2);
        }

        return prefabPaths;
    }

    /// <summary>
    /// ④ 可配对行数。只按下标；禁止用 ctx[0] 或 JobContext 填缺。
    /// 返回 true 表示数量一致。pairedCount 始终是两者较小值。
    /// </summary>
    public static bool TryAlignFlattenRows(
        int prefabCount,
        int contextCount,
        out int pairedCount,
        out string mismatchMessage)
    {
        if (prefabCount < 0)
        {
            prefabCount = 0;
        }

        if (contextCount < 0)
        {
            contextCount = 0;
        }

        pairedCount = prefabCount < contextCount ? prefabCount : contextCount;
        if (prefabCount == contextCount)
        {
            mismatchMessage = null;
            return true;
        }

        mismatchMessage = "④ Prefab 与 ctx 数量不一致: prefab=" + prefabCount + " ctx=" + contextCount;
        return false;
    }

    /// <summary>④ 按下标把 ctx 译成 plan 再 Run。禁止走菜单直平铺入口。</summary>
    private static List<string> FlattenPerPrefab(
        PipelineOptions options,
        List<string> prefabPaths,
        bool quiet,
        PipelineResult result)
    {
        var allArt = new List<string>();
        IList<PipelineJobContext> contexts = options.JobContexts;
        int contextCount = contexts == null ? 0 : contexts.Count;
        bool loggedClear = false;

        result.Info("[Pipeline] ④ 平铺 SO 快照：" +
                    (options.FlattenPolicy == null
                        ? "<null，使用管线默认>"
                        : options.FlattenPolicy.ToLogString()));

        bool countsMatch = TryAlignFlattenRows(
            prefabPaths.Count, contextCount, out int pairedCount, out string mismatchMessage);

        for (int i = 0; i < pairedCount; i++)
        {
            PipelineJobContext ctx = contexts[i];

            PipelineSourceBinding binding = null;
            if (options.SourceBindings != null && i < options.SourceBindings.Count)
            {
                binding = options.SourceBindings[i];
            }

            ToolFlattenRequest request = ToolFlattenRequest.ForPipeline(options.FlattenPolicy);
            request.ConvertZUpToYUp = binding != null && binding.ConvertZUpToYUp;

            FlattenPlan plan = ToolFlattenApi.FromContext(ctx, request, prefabPaths[i]);

            if (request.ConvertZUpToYUp)
            {
                result.Info("[Pipeline] ④ [" + (i + 1) + "] 轴向修正：内容节点叠 −90°X");
            }
            else if (ctx != null && !string.IsNullOrEmpty(ctx.ZUpExporterNote))
            {
                result.Info("[Pipeline] ④ [" + (i + 1) + "] 未开轴向修正（该源导出器默认 Z-up）");
            }

            if (!loggedClear && request.ClearDestinationArtFolder)
            {
                result.Info("[Pipeline] ④ 清空本次 Art/<名>/ 再写（不扫整棵 Art）");
                loggedClear = true;
            }

            if (plan.Branch == FlattenBranch.RelocateAtomic)
            {
                result.Info("[Pipeline] ④ [" + (i + 1) + "] SkipDependencySplit + B′ 原子搬迁");
            }

            if (plan.MissingUris != null && plan.MissingUris.Count > 0)
            {
                result.Info("[Pipeline] ④ [" + (i + 1) + "] glTF 缺必需伴生 × " +
                            plan.MissingUris.Count + "，停止本行平铺");
                for (int missingIndex = 0; missingIndex < plan.MissingUris.Count; missingIndex++)
                {
                    result.Info("  missing URI: " + plan.MissingUris[missingIndex]);
                }
            }

            FlattenRowResult row = ToolFlattenApi.Run(plan);
            if (!row.Ok)
            {
                result.Fail(
                    PipelineErrorCodes.FlattenFailed,
                    string.IsNullOrEmpty(row.Message)
                        ? ("平铺失败: " + prefabPaths[i])
                        : row.Message);
                return null;
            }

            result.Info("[Pipeline] ④ [" + (i + 1) + "] Flatten 1 ← " + prefabPaths[i]);
            PipelineFlattenQuality.Apply(result, row, i + 1);
            allArt.Add(row.ArtPrefabPath);
        }

        if (!countsMatch)
        {
            result.Fail(PipelineErrorCodes.FlattenFailed, mismatchMessage);
            return null;
        }

        return allArt;
    }

    private static bool IsExternalDiskPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string p = path.Replace("\\", "/");
        if (p.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            string full = Path.GetFullPath(path).Replace("\\", "/");
            string data = Application.dataPath.Replace("\\", "/");
            if (full.StartsWith(data + "/", System.StringComparison.OrdinalIgnoreCase) ||
                full.Equals(data, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    /// <summary>
    /// 从 Art Prefab 路径取出单元根（如 Assets/Art/某模型），供⑤扫 Model/ 与 Prefab 依赖。
    /// </summary>
    private static List<string> CollectArtUnitFolders(IList<string> artPrefabPaths)
    {
        var folders = new List<string>();
        if (artPrefabPaths == null)
        {
            return folders;
        }

        string prefix = RetinarPaths.ArtRoot + "/";
        for (int i = 0; i < artPrefabPaths.Count; i++)
        {
            string path = (artPrefabPaths[i] ?? string.Empty).Replace("\\", "/");
            if (!path.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = path.Substring(prefix.Length);
            int slash = relative.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            string unit = RetinarPaths.ArtRoot + "/" + relative.Substring(0, slash);
            if (!folders.Contains(unit))
            {
                folders.Add(unit);
            }
        }

        return folders;
    }

    /// <summary>
    /// D16：报告进 Messages；FailedCount&gt;0 且当前仍 Ok 或仅 41/42 时 Fail(50)。
    /// 不覆盖 20/30/40 等硬停码。取消进度条不算硬失败。60 随后仍可覆盖 50。
    /// </summary>
    private static void ApplyPostProcessResult(
        PipelineResult result,
        ToolPostProcessResult post,
        string label)
    {
        if (post == null)
        {
            return;
        }

        result.Info(label + " 失败条=" + post.FailedCount +
                    (post.Canceled ? " 已取消" : string.Empty) +
                    "\n" + post.Report);
        if (post.HasHardFailure &&
            PipelineFlattenQuality.CanEscalateToPostProcess(result.ExitCode))
        {
            result.Fail(
                PipelineErrorCodes.PostProcessFailed,
                "[Pipeline] ⑤ 有 " + post.FailedCount + " 条处理失败（细节见报告）");
        }
    }

    private static void LogResult(PipelineResult result)
    {
        string text = result.ToString();
        if (result.Ok)
        {
            Debug.Log(text);
        }
        else
        {
            Debug.LogError(text);
        }
    }
}
