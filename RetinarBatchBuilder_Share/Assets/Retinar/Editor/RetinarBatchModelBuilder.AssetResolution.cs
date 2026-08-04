using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 本文件是 RetinarBatchModelBuilder 的 partial 分文件，专门负责两件事：
//   1) “源资产发现”——给定一个 FBX/OBJ，去哪里找它的贴图、材质（CollectSourceAssets 等）。
//   2) “打包前校验”——外部依赖白名单检查、Model 目录纯净度检查（Validate* 系列）。
//
// 为什么单独拆出来：
//   用户反馈“有时只是移动文件位置，就会导致打包终止”。追下来是这两块代码共同造成的：
//
//   a) 原 CollectSourceAssets 只在模型【当前所在目录】及其【上一级目录】下，
//      按固定的几个文件夹名字（Materials / Texture / Textures / <模型名>.fbm）做
//      非递归查找。只要用户把 FBX 或者贴图文件夹挪了地方（哪怕只是挪到子文件夹，
//      或者改了个文件夹名字），贴图/材质就“悄悄”找不到了，不会报错，只是没被复制。
//
//   b) 因为 a) 没找到，材质仍然引用着工程里原来那个外部贴图路径
//      （RemapMaterialTexturesToArtFolder 原来只有“已经复制过”才重定向）。
//
//   c) 原 ValidateExternalDependencies 一旦发现预制体的任何依赖不在
//      Assets/Art/<模型名>/ 或者 4 个写死的运行时白名单目录下，就直接判定
//      “不支持的外部依赖”，整批 AssetBundle/交付物打包全部终止——而且报错信息
//      只有一行资产路径，看不出这个文件“应该”在哪、现在实际在哪，非常难排查。
//
//   d) MoveAssetToExactPath 在 AssetDatabase.MoveAsset 失败时只是 LogWarning
//      然后静默返回原路径，导致文件仍留在 Model 目录下，被后面更严格的
//      ValidateModelFoldersAreClean 判定为“Model 目录里有非模型文件”而失败。
//
// 这个文件的修复思路：
//   - 贴图/材质查找改成递归、支持更多命名，且会把“搜索了哪些目录、找到了什么”
//     打印出来，方便一眼看出是不是因为挪了文件夹。
//   - 校验函数在报错前先尝试“自愈”：把游离在外的贴图/材质复制并重定向进该模型
//     自己的 Art 目录，成功了就不算失败；只有自愈不了（比如缺了脚本、动画控制器
//     这类没法简单复制重定向的资产）才真正终止打包。
//   - 报错信息里带上磁盘绝对路径、最后修改时间，能直接定位是哪个文件、什么时候
//     被动过。
// =====================================================================================

public static partial class RetinarBatchModelBuilder
{
    // ---------------------------------------------------------------------------
    // 资产类型判断
    // ---------------------------------------------------------------------------

    private static bool IsModelAsset(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        return extension == ".fbx" || extension == ".obj";
    }

    private static bool IsMaterialAsset(string assetPath)
    {
        return Path.GetExtension(assetPath).ToLowerInvariant() == ".mat";
    }

    private static bool IsTextureAsset(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" ||
               extension == ".tga" || extension == ".tif" || extension == ".tiff";
    }

    private static bool IsTextAsset(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        return extension == ".txt" || extension == ".bytes" || extension == ".json" ||
               extension == ".xml" || extension == ".csv";
    }

    // 伴生文件夹的常见命名——比原来多覆盖几种常见叫法（大小写、复数、中文习惯）。
    // 如果你们团队还有别的命名习惯，直接往这个数组里加就行，不用改查找逻辑。
    private static readonly string[] CompanionMaterialFolderNames =
    {
        "Materials", "Material", "Mats"
    };

    private static readonly string[] CompanionTextureFolderNames =
    {
        "Texture", "Textures", "Maps", "Tex"
    };

    // ---------------------------------------------------------------------------
    // 源资产发现：给定 FBX/OBJ，找它的贴图和材质
    // ---------------------------------------------------------------------------

    private static void CollectSourceAssets(string sourcePath, HashSet<string> modelPaths, HashSet<string> materialPaths, HashSet<string> texturePaths)
    {
        AddTypedAssetPath(sourcePath, modelPaths, materialPaths, texturePaths);

        // 1) FBX/OBJ 自身在 AssetDatabase 里登记过的依赖（如果材质是外部 .mat 且已经
        //    被这个模型引用，Unity 通常能识别到）。
        foreach (string dependency in AssetDatabase.GetDependencies(sourcePath, true))
        {
            AddTypedAssetPath(dependency, modelPaths, materialPaths, texturePaths);
        }

        // 2) 按命名习惯，在模型所在目录、其父目录、以及“模型名.fbm”目录里递归查找。
        //    —— 这里改成递归（SearchOption.AllDirectories），并且同时检查
        //    模型当前目录与父目录，这样即使贴图文件夹被挪到了子文件夹里，或者
        //    模型本身被挪了一层目录，多数情况下还是能找到。
        string sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/") ?? string.Empty;
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        string parentDirectory = Path.GetDirectoryName(sourceDirectory)?.Replace("\\", "/") ?? string.Empty;

        var searchedFolders = new List<string>();
        var candidateFolders = new List<string> { sourceDirectory, parentDirectory };

        foreach (string materialFolderName in CompanionMaterialFolderNames)
        {
            foreach (string baseFolder in candidateFolders)
            {
                searchedFolders.Add(AddAssetsFromFolder(baseFolder + "/" + materialFolderName, materialPaths, IsMaterialAsset));
            }
        }

        foreach (string textureFolderName in CompanionTextureFolderNames)
        {
            foreach (string baseFolder in candidateFolders)
            {
                searchedFolders.Add(AddAssetsFromFolder(baseFolder + "/" + textureFolderName, texturePaths, IsTextureAsset));
            }
        }

        // FBX 内嵌贴图导出后 Unity 默认使用的 "<模型名>.fbm" 目录。
        foreach (string baseFolder in candidateFolders)
        {
            searchedFolders.Add(AddAssetsFromFolder(baseFolder + "/" + sourceName + ".fbm", texturePaths, IsTextureAsset));
        }

        if (materialPaths.Count == 0 && texturePaths.Count == 0)
        {
            // 没找到任何贴图/材质，不代表一定有问题（模型可能确实没有外部贴图），
            // 但如果预期应该有，这行日志能第一时间告诉你“去这些地方找过了，都没有”，
            // 而不是等到后面打包失败才去猜。
            Debug.Log("[Retinar] 未在以下目录中找到贴图/材质，如果模型本应带贴图，请确认文件是否被移动：\n" +
                string.Join("\n", searchedFolders.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray()));
        }
    }

    private static void AddTypedAssetPath(string assetPath, HashSet<string> modelPaths, HashSet<string> materialPaths, HashSet<string> texturePaths)
    {
        if (IsModelAsset(assetPath))
        {
            modelPaths.Add(assetPath);
        }
        else if (IsMaterialAsset(assetPath))
        {
            materialPaths.Add(assetPath);
        }
        else if (IsTextureAsset(assetPath))
        {
            texturePaths.Add(assetPath);
        }
    }

    /// <summary>
    /// 在指定文件夹（递归）下查找符合条件的资产并加入 target。
    /// 返回实际搜索的文件夹路径（供调用方汇总打印诊断信息），文件夹不存在时返回 null。
    /// </summary>
    private static string AddAssetsFromFolder(string folderPath, HashSet<string> target, Func<string, bool> predicate)
    {
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            return null;
        }

        string fullFolderPath = AssetPathToFullPath(folderPath);
        foreach (string filePath in Directory.GetFiles(fullFolderPath, "*.*", SearchOption.AllDirectories))
        {
            string assetPath = FullPathToAssetPath(filePath);
            if (!string.IsNullOrEmpty(assetPath) && predicate(assetPath))
            {
                target.Add(assetPath);
            }
        }

        return folderPath;
    }

    // ---------------------------------------------------------------------------
    // 资产搬移（供 FlattenModelCompanionFolders 等调用）
    // ---------------------------------------------------------------------------

    private static string MoveAssetToExactPath(string sourcePath, string requestedDestinationPath)
    {
        sourcePath = sourcePath.Replace("\\", "/");
        requestedDestinationPath = requestedDestinationPath.Replace("\\", "/");

        if (sourcePath.Equals(requestedDestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(requestedDestinationPath) != null &&
            sourcePath.StartsWith(ArtRoot + "/", StringComparison.OrdinalIgnoreCase) &&
            requestedDestinationPath.StartsWith(ArtRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.DeleteAsset(sourcePath);
            return requestedDestinationPath;
        }

        // 磁盘上已经有文件、但 AssetDatabase 还不认识它时，MoveAsset 会失败并顺带触发一次
        // Unity 内部断言（Console 里表现为 Assertion failed on expression: 'm_hasValue'，
        // 后面才跟着 "Asset to move is not in asset database"）。
        // 典型来源：Unity 在导入 FBX 时会把内嵌贴图抽取到 <FBX名>.fbm/ 目录，文件立刻落盘，
        // 但要等下一次 Refresh 才会成为资产；而调用方是用 Directory.GetFiles 按磁盘枚举的，
        // 于是拿到了一个"存在但还没导入"的路径。这里按需补一次导入，让搬移不依赖调用方刷新。
        if (!EnsureAssetIsInDatabase(sourcePath))
        {
            Debug.LogError("[Retinar] 待搬移的文件不在 AssetDatabase 中，且按需导入也没能注册它，已跳过：\n" +
                "  源路径: " + sourcePath + "\n" +
                "  目标路径: " + requestedDestinationPath + "\n" +
                "  文件会留在原位置，可能导致后续目录清洁度校验失败。");
            return sourcePath;
        }

        string destinationPath = AssetDatabase.GenerateUniqueAssetPath(requestedDestinationPath);
        string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
        if (!string.IsNullOrEmpty(error))
        {
            // 原来这里只 LogWarning 然后静默放弃，文件会留在错误的位置，
            // 直到后面 ValidateModelFoldersAreClean 才会因为“Model 目录里有非模型文件”
            // 报错——但那时候已经看不出来是这里移动失败了。改成 LogError 并把
            // 源/目标路径和原始错误都打出来，方便直接定位。
            Debug.LogError("[Retinar] 移动资产失败，文件将保留在原位置，这可能导致后续目录清洁度校验失败：\n" +
                "  源路径: " + sourcePath + "\n" +
                "  目标路径: " + destinationPath + "\n" +
                "  Unity 返回的错误: " + error);
            return sourcePath;
        }

        return destinationPath;
    }

    /// <summary>
    /// 确认某个路径已经是 AssetDatabase 里的资产；只在磁盘上存在的话补一次同步导入。
    /// 返回 false 表示文件根本不存在，或者 Unity 拒绝把它当作资产（例如被 .gitignore 之外的
    /// 导入器规则排除），这两种情况都不该再往下调用 MoveAsset。
    /// </summary>
    private static bool EnsureAssetIsInDatabase(string assetPath)
    {
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
        {
            return true;
        }

        string fullPath = AssetPathToFullPath(assetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            return false;
        }

        // ForceSynchronousImport：搬移紧接着就要发生，不能等 Unity 自己排队。
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
    }

    // ---------------------------------------------------------------------------
    // 打包前校验的总调度：逐个资产判定，失败的只排除自己
    // ---------------------------------------------------------------------------

    /// <summary>
    /// 对每个生成出来的资产分别跑三道校验，返回通过的那些；没通过的把原因写进
    /// excludedReports，并清掉它的 AssetBundle 名字，避免它被打进 AssetBundle 输出里。
    ///
    /// 为什么要改成逐个判定：
    ///   原来三道校验是"对整批一起做，任一条不通过就整批 return"。实际使用中，
    ///   十个模型里有一个贴图放错位置，另外九个已经生成好、也完全合规的模型
    ///   同样不会出包，用户只能去 Assets/Art 里把生成好的预设体一个个重新选中再打一遍。
    ///   逐个判定之后，坏的那一个被单独挑出来写进报告，好的九个正常交付。
    /// </summary>
    private static List<GeneratedAsset> PartitionAssetsThatPassValidation(
        List<GeneratedAsset> generated,
        List<string> excludedReports)
    {
        var passed = new List<GeneratedAsset>();
        foreach (GeneratedAsset asset in generated)
        {
            List<string> reasons = CollectValidationFailures(asset);
            if (reasons.Count == 0)
            {
                passed.Add(asset);
                continue;
            }

            // 这个资产的预设体如果还挂着 AssetBundle 名字，BuildPipeline 会照样把它
            // 打进包里——校验都没过的东西不应该出现在交付物里，这里主动摘掉。
            ClearBundleName(asset.PrefabPath);

            excludedReports.Add(
                "【" + asset.AssetName + "】未通过校验，已从本次出包中排除\n" +
                "  预设体: " + asset.PrefabPath + "\n" +
                "  资产目录: " + asset.AssetFolder + "\n" +
                string.Join("\n", reasons.ToArray()));
        }

        return passed;
    }

    /// <summary>
    /// 单个资产的三道校验。复用原来那三个接收 List 的校验函数，传进去只装一个元素的
    /// 列表——这样校验逻辑本身完全没动，只是调用粒度从"整批"变成"单个"。
    /// </summary>
    private static List<string> CollectValidationFailures(GeneratedAsset asset)
    {
        var single = new List<GeneratedAsset> { asset };
        var reasons = new List<string>();
        string error;

        if (!ValidateModelFoldersAreClean(single, out error))
        {
            reasons.Add("  [Model 目录不纯净] Model 目录只允许放模型文件、不允许有子文件夹\n" + Indent(error));
        }

        if (!ValidatePrefabSpatialPlacement(single, out error))
        {
            reasons.Add("  [SafeZone 位置校验未通过]\n" + Indent(error));
        }

        if (!ValidateExternalDependencies(single, out error))
        {
            reasons.Add("  [存在不支持的外部依赖] 自动自愈已经尝试过一次，下面是自愈之后仍然存在的问题\n" + Indent(error));
        }

        return reasons;
    }

    private static string Indent(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return "    " + string.Join("\n    ", lines);
    }

    /// <summary>
    /// 三种校验失败合并写成一份报告。
    /// 原来是每种校验各写一个固定文件名的报告（model_folder_not_clean.txt /
    /// prefab_spatial_placement_failed.txt / unsupported_external_dependencies.txt），
    /// 而且因为一失败就整批终止，后两份永远不会和第一份同时出现——排查时要挨个文件去翻。
    /// 现在一次打包只产出一份，按资产分段，一眼能看完这批里所有问题。
    /// </summary>
    private static string WriteValidationFailureReport(List<string> excludedReports)
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), DeliverableRoot, "_diagnostics");
        EnsureDiskDirectory(outputDir);
        string reportPath = Path.Combine(outputDir, "validation_failures.txt");

        var lines = new List<string>
        {
            "Retinar Batch Builder - 未通过校验、已排除出本次打包的资产",
            "Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "",
            "说明：名单里的资产只影响自己，同一批里通过校验的资产已经正常出包。",
            "处理完下面的问题后，重新选中【原始 FBX】或【Assets/Art/<名字>/Prefab 里的预设体】再执行一次即可；",
            "重新选中生成目录里的预设体不会再新建 Assets/Art/<名字>_prefab 这类多余目录。",
            ""
        };
        lines.AddRange(excludedReports);

        File.WriteAllLines(reportPath, lines.ToArray(), new UTF8Encoding(false));
        return Path.GetFullPath(reportPath);
    }

    // ---------------------------------------------------------------------------
    // 打包前校验：外部依赖白名单
    // ---------------------------------------------------------------------------

    // 允许作为“公共运行时依赖”而不必复制进模型自己文件夹的路径前缀。
    // 注意：这本身也是一份写死的白名单——如果以后有人把运行时脚本/插件目录挪了地方
    // 或改了名字，这里同样需要跟着改，否则会复现一模一样的“移动文件导致打包终止”问题。
    private static readonly string[] ApprovedRuntimeDependencyPrefixes =
    {
        "Assets/Retinar/Scripts/",
        "Assets/Retinar/XLua/",
        "Assets/Retinar/Plugins/",
        "Assets/RetinarRuntime/",
    };

    private static bool IsApprovedRuntimeDependency(string assetPath)
    {
        foreach (string prefix in ApprovedRuntimeDependencyPrefixes)
        {
            if (assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ValidateExternalDependencies(List<GeneratedAsset> assets, out string errorText)
    {
        var errors = new List<string>();

        foreach (GeneratedAsset asset in assets)
        {
            // 先尝试自愈：把游离在外的贴图/材质复制并重定向进模型自己的目录。
            // 多数“移动了文件导致打包失败”的情况会在这一步被悄悄修好，不会影响本次打包。
            if (TryHealExternalDependencies(asset, out List<string> healedPaths))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (healedPaths.Count > 0)
                {
                    Debug.Log("[Retinar] 自动修复了 " + asset.AssetName + " 的以下外部依赖引用：\n" +
                        string.Join("\n", healedPaths.ToArray()));
                }
            }

            // 自愈后再扫一遍；若仍挂着导入区 .fbm，再强制 Extract+remap 一次后重取依赖。
            string[] dependencies = AssetDatabase.GetDependencies(asset.PrefabPath, true);
            List<string> externalFbmBefore = CollectExternalFbmPathsFromDependencies(asset, dependencies);
            if (externalFbmBefore.Count > 0)
            {
                Debug.LogWarning("[Retinar] " + asset.AssetName + "：自愈后仍有外部 .fbm 依赖 " +
                    externalFbmBefore.Count + " 条，开始校验阶段强制 Extract+remap：\n" +
                    string.Join("\n", externalFbmBefore.ToArray()));
                ExtractAndBindPackagedModelTextures(asset.AssetFolder);
                RemapAllArtMaterialsToLocalTextures(asset.AssetFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                dependencies = AssetDatabase.GetDependencies(asset.PrefabPath, true);
                List<string> externalFbmAfter = CollectExternalFbmPathsFromDependencies(asset, dependencies);
                if (externalFbmAfter.Count == 0)
                {
                    Debug.Log("[Retinar] " + asset.AssetName + "：校验阶段强制 Extract+remap 后，外部 .fbm 依赖已清零");
                }
                else
                {
                    Debug.LogError("[Retinar] " + asset.AssetName + "：校验阶段强制 Extract+remap 后仍剩 " +
                        externalFbmAfter.Count + " 条外部 .fbm 依赖：\n" +
                        string.Join("\n", externalFbmAfter.ToArray()));
                }
            }
            else
            {
                Debug.Log("[Retinar] " + asset.AssetName + "：自愈后无外部 .fbm 依赖");
            }

            foreach (string rawDependency in dependencies)
            {
                string dependency = rawDependency.Replace("\\", "/");
                if (!dependency.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    dependency.Equals(asset.PrefabPath, StringComparison.OrdinalIgnoreCase) ||
                    dependency.StartsWith(asset.AssetFolder + "/", StringComparison.OrdinalIgnoreCase) ||
                    IsApprovedRuntimeDependency(dependency))
                {
                    continue;
                }

                errors.Add(BuildDependencyDiagnosticLine(asset, dependency));
            }
        }

        errorText = string.Join("\n\n", errors.Distinct().OrderBy(line => line).ToArray());
        return errors.Count == 0;
    }

    private static bool HasExternalEmbeddedMediaDependency(GeneratedAsset asset, string[] dependencies)
    {
        return CollectExternalFbmPathsFromDependencies(asset, dependencies).Count > 0;
    }

    private static List<string> CollectExternalFbmPathsFromDependencies(GeneratedAsset asset, string[] dependencies)
    {
        var result = new List<string>();
        if (dependencies == null)
        {
            return result;
        }

        string assetFolderPrefix = asset.AssetFolder + "/";
        foreach (string rawDependency in dependencies)
        {
            string dependency = rawDependency.Replace("\\", "/");
            if (!dependency.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                dependency.StartsWith(assetFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsTextureAsset(dependency) && IsInsideEmbeddedMediaFolderPath(dependency))
            {
                result.Add(dependency);
            }
        }

        return result;
    }

    /// <summary>打包流程用：按预制体路径收集仍落在外部 .fbm 的贴图依赖。</summary>
    private static List<string> CollectExternalFbmTextureDependencies(string assetFolder, string prefabPath)
    {
        var asset = new GeneratedAsset(
            Path.GetFileName(assetFolder),
            assetFolder,
            string.Empty,
            string.Empty,
            prefabPath,
            string.Empty,
            default(AssetStats));
        return CollectExternalFbmPathsFromDependencies(asset, AssetDatabase.GetDependencies(prefabPath, true));
    }

    private static bool IsInsideEmbeddedMediaFolderPath(string assetPath)
    {
        string[] segments = assetPath.Replace("\\", "/").Split('/');
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].EndsWith(".fbm", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 尝试把预制体里指向"资产文件夹之外"的贴图/材质依赖，复制并重定向进该模型自己的
    /// Art 目录。
    ///
    /// 重要：材质【已经】在 Art/Material 里时也必须重映射贴图。
    /// 旧逻辑在"材质已在 Art"时直接 continue，导致材质仍引用导入区 .fbm 的情况
    /// 完全得不到自愈（Plane_Jian31：Texture 已拷进 Art，但依赖校验仍报 AAA/.../fbx.fbm）。
    /// </summary>
    private static bool TryHealExternalDependencies(GeneratedAsset asset, out List<string> healedPaths)
    {
        healedPaths = new List<string>();
        string materialFolder = asset.AssetFolder + "/Material";
        string textureFolder = asset.AssetFolder + "/Texture";
        EnsureAssetFolder(materialFolder);
        EnsureAssetFolder(textureFolder);

        List<string> fbmBeforeHeal = CollectExternalFbmTextureDependencies(asset.AssetFolder, asset.PrefabPath);
        Debug.Log("[Retinar] " + asset.AssetName + "：开始自愈外部依赖（自愈前外部 .fbm=" +
            fbmBeforeHeal.Count + "）" +
            (fbmBeforeHeal.Count > 0 ? "：\n" + string.Join("\n", fbmBeforeHeal.ToArray()) : string.Empty));

        GameObject instance = PrefabUtility.LoadPrefabContents(asset.PrefabPath);
        bool changed = false;
        try
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material).Replace("\\", "/");
                    if (string.IsNullOrEmpty(materialPath) || !IsMaterialAsset(materialPath))
                    {
                        continue;
                    }

                    Material workingMaterial = material;
                    if (!materialPath.StartsWith(asset.AssetFolder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        string targetMaterialPath = materialFolder + "/" + Path.GetFileName(materialPath);
                        string copiedMaterialPath = CopyAssetToExactPath(materialPath, targetMaterialPath);
                        Material copiedMaterial = AssetDatabase.LoadAssetAtPath<Material>(copiedMaterialPath);
                        if (copiedMaterial == null)
                        {
                            continue;
                        }

                        workingMaterial = copiedMaterial;
                        materials[i] = copiedMaterial;
                        rendererChanged = true;
                        healedPaths.Add(materialPath + "  ->  " + copiedMaterialPath);
                    }

                    if (RemapMaterialTexturesToArtFolder(workingMaterial, textureFolder))
                    {
                        EditorUtility.SetDirty(workingMaterial);
                        changed = true;
                        healedPaths.Add("贴图重映射: " + AssetDatabase.GetAssetPath(workingMaterial));
                    }
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, asset.PrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }

        // 兜底：Art/Material 下所有材质再扫一遍贴图（含未被 Renderer 引用到的）。
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }

            if (RemapMaterialTexturesToArtFolder(material, textureFolder))
            {
                EditorUtility.SetDirty(material);
                changed = true;
                healedPaths.Add("贴图重映射: " + path);
            }
        }

        // 切断 Model FBX 对导入区 .fbm 的依赖：
        // materialSearch=Local 不够——内嵌贴图提取仍会复用工程里已有的同名 .fbm。
        // 必须 ExtractTextures 到本模型 Texture/，再按文件名把外部依赖 remap 回来。
        if (ExtractAndBindPackagedModelTextures(asset.AssetFolder))
        {
            changed = true;
            healedPaths.Add("ExtractTextures + remap -> " + asset.AssetFolder + "/Texture");
        }

        if (RemapAllArtMaterialsToLocalTextures(asset.AssetFolder))
        {
            changed = true;
            healedPaths.Add("Art/Material 贴图全部收到本模型 Texture/");
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        List<string> fbmAfterHeal = CollectExternalFbmTextureDependencies(asset.AssetFolder, asset.PrefabPath);
        Debug.Log("[Retinar] " + asset.AssetName + "：自愈结束 changed=" + changed +
            " 修复条目=" + healedPaths.Count + " 自愈后外部 .fbm=" + fbmAfterHeal.Count +
            (fbmAfterHeal.Count > 0 ? "：\n" + string.Join("\n", fbmAfterHeal.ToArray()) : string.Empty));

        return changed;
    }

    /// <summary>
    /// 把交付区 FBX 的内嵌贴图强制抽到 Assets/Art/&lt;模型&gt;/Texture，
    /// 并把 ModelImporter 上仍指向外部（尤其是导入区 .fbm）的贴图 remap 到本地副本。
    ///
    /// 根因：Copy/Flatten 之后 Art/Texture 里已有副本，但 FBX 再导入时 Unity 会按
    /// 贴图名复用工程里先存在的 Assets/AAA/.../fbx.fbm，GetDependencies 于是一直挂外部路径。
    /// materialSearch=Local 只影响材质搜索，管不到这层“同名贴图复用”。
    /// </summary>
    private static bool ExtractAndBindPackagedModelTextures(string assetFolder)
    {
        string modelFolder = assetFolder + "/Model";
        string textureFolder = assetFolder + "/Texture";
        if (!AssetDatabase.IsValidFolder(modelFolder))
        {
            Debug.Log("[Retinar] ExtractAndBind 跳过：无 Model 目录 " + modelFolder);
            return false;
        }

        EnsureAssetFolder(textureFolder);
        bool changed = false;
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });
        Debug.Log("[Retinar] ExtractAndBind 开始 assetFolder=" + assetFolder +
            " 模型数=" + modelGuids.Length + " -> " + textureFolder);
        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsModelAsset(modelPath))
            {
                continue;
            }

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Retinar] ExtractAndBind：无 ModelImporter " + modelPath);
                continue;
            }

            List<string> externalBefore = CollectModelExternalFbmTextures(modelPath, assetFolder);
            Debug.Log("[Retinar] ExtractAndBind 模型=" + modelPath +
                " Extract 前外部 .fbm 贴图=" + externalBefore.Count +
                (externalBefore.Count > 0 ? "：\n" + string.Join("\n", externalBefore.ToArray()) : string.Empty));

            // 已无外部 .fbm 时不必再 Extract（会盖贴图、触发重导冲顶点色）。
            // 仅在材质搜索设置不合规时做一次保留顶点色的重导。
            if (externalBefore.Count == 0)
            {
                bool settingsDirty =
                    importer.materialLocation != ModelImporterMaterialLocation.InPrefab ||
                    importer.materialSearch != ModelImporterMaterialSearch.Local ||
                    importer.materialName != ModelImporterMaterialName.BasedOnMaterialName;
                if (settingsDirty)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.materialSearch = ModelImporterMaterialSearch.Local;
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    Debug.Log("[Retinar] ExtractAndBind 跳过 Extract（无外部 .fbm），仅校正材质搜索并重导: " + modelPath);
                    SaveAndReimportPreservingMeshVertexColors(importer);
                    changed = true;
                }
                else
                {
                    Debug.Log("[Retinar] ExtractAndBind 跳过: 无外部 .fbm 且材质搜索已是 Local — " + modelPath);
                }

                continue;
            }

            // ExtractTextures 会把 FBX 内嵌原始大图写进 Texture/，覆盖两遍流程里已压缩的同名文件。
            // 先快照再抽取，抽取后把“被放大”的文件恢复成压缩版；缺失的仍用抽取结果补齐。
            Dictionary<string, byte[]> preservedTextures = SnapshotTextureFolderFiles(textureFolder);

            try
            {
                importer.ExtractTextures(textureFolder);
                changed = true;
                Debug.Log("[Retinar] ExtractTextures 已调用: " + modelPath + " -> " + textureFolder);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Retinar] ExtractTextures 失败: " + modelPath + " -> " + exception.Message);
            }

            AssetDatabase.Refresh();
            int restored = RestorePreservedTexturesIfExtractGrewThem(preservedTextures);
            if (restored > 0)
            {
                changed = true;
                Debug.Log("[Retinar] ExtractTextures 后已恢复 " + restored +
                    " 张更小的 Art 贴图（避免盖掉已压缩结果）");
            }

            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;

            // 先按当前依赖表 remap 一次，再导入；导入后若仍挂外部 .fbm，再 remap + 导入一次。
            int remapPass1 = RemapModelImporterTexturesToArtFolder(importer, assetFolder, textureFolder);
            if (remapPass1 > 0)
            {
                changed = true;
            }

            Debug.Log("[Retinar] ExtractAndBind 第 1 次 SaveAndReimport: " + modelPath +
                "（本轮 AddRemap 贴图数=" + remapPass1 + "）");
            SaveAndReimportPreservingMeshVertexColors(importer);
            AssetDatabase.Refresh();

            importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer != null)
            {
                int remapPass2 = RemapModelImporterTexturesToArtFolder(importer, assetFolder, textureFolder);
                if (remapPass2 > 0)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.materialSearch = ModelImporterMaterialSearch.Local;
                    Debug.Log("[Retinar] ExtractAndBind 第 2 次 SaveAndReimport: " + modelPath +
                        "（本轮 AddRemap 贴图数=" + remapPass2 + "）");
                    SaveAndReimportPreservingMeshVertexColors(importer);
                    changed = true;
                }
                else
                {
                    Debug.Log("[Retinar] ExtractAndBind 第 2 轮无需再 AddRemap: " + modelPath);
                    changed = true;
                }
            }

            List<string> externalAfter = CollectModelExternalFbmTextures(modelPath, assetFolder);
            if (externalAfter.Count == 0)
            {
                Debug.Log("[Retinar] ExtractAndBind 完成: " + modelPath + " 外部 .fbm 贴图已清零");
            }
            else
            {
                Debug.LogWarning("[Retinar] ExtractAndBind 完成仍剩外部 .fbm 贴图 " +
                    externalAfter.Count + " 条: " + modelPath + "\n" +
                    string.Join("\n", externalAfter.ToArray()));
            }
        }

        FlattenModelCompanionFolders(assetFolder);
        Debug.Log("[Retinar] ExtractAndBind 结束 assetFolder=" + assetFolder + " changed=" + changed);
        return changed;
    }

    private static Dictionary<string, byte[]> SnapshotTextureFolderFiles(string textureFolder)
    {
        var snapshot = new Dictionary<string, byte[]>(System.StringComparer.OrdinalIgnoreCase);
        string textureFullPath = AssetPathToFullPath(textureFolder);
        if (!Directory.Exists(textureFullPath))
        {
            return snapshot;
        }

        foreach (string filePath in Directory.GetFiles(textureFullPath, "*.*", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(filePath).Equals(".meta", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetPath = FullPathToAssetPath(filePath);
            if (string.IsNullOrEmpty(assetPath) || !IsTextureAsset(assetPath))
            {
                continue;
            }

            snapshot[assetPath] = File.ReadAllBytes(filePath);
        }

        return snapshot;
    }

    /// <summary>
    /// ExtractTextures 之后：若同名文件变大（典型为未压缩内嵌大图盖掉已压缩 Art），写回快照。
    /// 文件被删则也恢复。变小或等大则保留当前磁盘内容。
    /// </summary>
    private static int RestorePreservedTexturesIfExtractGrewThem(Dictionary<string, byte[]> preservedTextures)
    {
        if (preservedTextures == null || preservedTextures.Count == 0)
        {
            return 0;
        }

        int restored = 0;
        foreach (KeyValuePair<string, byte[]> pair in preservedTextures)
        {
            string assetPath = pair.Key;
            byte[] preservedBytes = pair.Value;
            if (preservedBytes == null || preservedBytes.Length == 0)
            {
                continue;
            }

            string fullPath = AssetPathToFullPath(assetPath);
            bool missing = !File.Exists(fullPath);
            long currentLength = missing ? long.MaxValue : new FileInfo(fullPath).Length;
            if (!missing && currentLength <= preservedBytes.LongLength)
            {
                continue;
            }

            File.WriteAllBytes(fullPath, preservedBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            restored++;
            Debug.Log("[Retinar] 保留已压缩 Art 贴图: " + assetPath +
                (missing
                    ? "（抽取后缺失，已写回）"
                    : "（" + FormatBytes(currentLength) + " -> " + FormatBytes(preservedBytes.LongLength) + "）"));
        }

        return restored;
    }

    /// <returns>本次成功 AddRemap 的外部贴图数量。</returns>
    private static int RemapModelImporterTexturesToArtFolder(
        ModelImporter importer,
        string assetFolder,
        string textureFolder)
    {
        int remapCount = 0;
        string modelPath = importer.assetPath;
        string[] dependencies = AssetDatabase.GetDependencies(modelPath, true);
        foreach (string rawDependency in dependencies)
        {
            string dependency = rawDependency.Replace("\\", "/");
            if (!IsTextureAsset(dependency) ||
                dependency.StartsWith(assetFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fileName = Path.GetFileName(dependency);
            string targetPath = textureFolder + "/" + fileName;
            bool copied = false;
            if (AssetDatabase.LoadMainAssetAtPath(targetPath) == null)
            {
                CopyAssetToExactPath(dependency, targetPath);
                copied = true;
            }

            Texture artTexture = AssetDatabase.LoadAssetAtPath<Texture>(targetPath);
            if (artTexture == null)
            {
                Debug.LogWarning("[Retinar] AddRemap 跳过：Art 贴图加载失败 " + dependency + " -> " + targetPath);
                continue;
            }

            string textureName = Path.GetFileNameWithoutExtension(dependency);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Texture), textureName), artTexture);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), textureName), artTexture);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Texture), fileName), artTexture);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Texture2D), fileName), artTexture);
            remapCount++;
            Debug.Log("[Retinar] AddRemap: " + modelPath + "\n  " + dependency + "  ->  " + targetPath +
                (copied ? "（已拷贝）" : "（已有本地副本）"));
        }

        return remapCount;
    }

    private static List<string> CollectModelExternalFbmTextures(string modelPath, string assetFolder)
    {
        var result = new List<string>();
        string[] dependencies = AssetDatabase.GetDependencies(modelPath, true);
        string assetFolderPrefix = assetFolder + "/";
        foreach (string rawDependency in dependencies)
        {
            string dependency = rawDependency.Replace("\\", "/");
            if (!dependency.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                dependency.StartsWith(assetFolderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsTextureAsset(dependency) && IsInsideEmbeddedMediaFolderPath(dependency))
            {
                result.Add(dependency);
            }
        }

        return result;
    }

    private static bool RemapAllArtMaterialsToLocalTextures(string assetFolder)
    {
        string materialFolder = assetFolder + "/Material";
        string textureFolder = assetFolder + "/Texture";
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            return false;
        }

        bool changed = false;
        int remappedMaterials = 0;
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                continue;
            }

            if (RemapMaterialTexturesToArtFolder(material, textureFolder))
            {
                EditorUtility.SetDirty(material);
                changed = true;
                remappedMaterials++;
                Debug.Log("[Retinar] Art 材质贴图收到本地: " + path);
            }
        }

        if (changed)
        {
            Debug.Log("[Retinar] RemapAllArtMaterials 完成 assetFolder=" + assetFolder +
                " 改动材质数=" + remappedMaterials);
        }

        return changed;
    }

    /// <summary>
    /// 交付区 FBX 若仍是 Everywhere，Flatten 之后会重新搜到工程里其它 .fbm。
    /// 这里把 Art 下模型统一收成 Local；有改动才 Reimport。
    /// </summary>
    private static bool TryRestrictPackagedModelMaterialSearch(string assetFolder)
    {
        string modelFolder = assetFolder + "/Model";
        if (!AssetDatabase.IsValidFolder(modelFolder))
        {
            return false;
        }

        bool changed = false;
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelFolder });
        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsModelAsset(modelPath))
            {
                continue;
            }

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab ||
                importer.materialSearch != ModelImporterMaterialSearch.Local)
            {
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
                importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                SaveAndReimportPreservingMeshVertexColors(importer);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// FBX/OBJ 的 SaveAndReimport 会从磁盘二进制重建全部 Mesh 子资产，
    /// TOol「顶点色设为全白」等改的是导入后 Mesh，会被冲掉。
    /// 打包链路凡重导交付区 Model，必须先快照顶点色再写回。
    /// </summary>
    private static void SaveAndReimportPreservingMeshVertexColors(ModelImporter importer)
    {
        if (importer == null)
        {
            return;
        }

        string modelPath = importer.assetPath.Replace("\\", "/");
        List<MeshVertexColorSnapshot> snapshot = SnapshotMeshVertexColors(modelPath);
        importer.SaveAndReimport();
        int restored = RestoreMeshVertexColors(modelPath, snapshot);
        if (restored > 0)
        {
            Debug.Log("[Retinar] SaveAndReimport 后已恢复 Mesh 顶点色: " + modelPath +
                " 数量=" + restored + "/" + snapshot.Count);
        }
    }

    private struct MeshVertexColorSnapshot
    {
        public string Name;
        public int VertexCount;
        public Color[] Colors;
    }

    private static List<MeshVertexColorSnapshot> SnapshotMeshVertexColors(string modelPath)
    {
        var snapshot = new List<MeshVertexColorSnapshot>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null)
        {
            return snapshot;
        }

        foreach (UnityEngine.Object asset in assets)
        {
            Mesh mesh = asset as Mesh;
            if (mesh == null || mesh.vertexCount <= 0)
            {
                continue;
            }

            Color[] colors = mesh.colors;
            Color[] copy = null;
            if (colors != null && colors.Length == mesh.vertexCount)
            {
                copy = new Color[colors.Length];
                System.Array.Copy(colors, copy, colors.Length);
            }

            snapshot.Add(new MeshVertexColorSnapshot
            {
                Name = mesh.name,
                VertexCount = mesh.vertexCount,
                Colors = copy
            });
        }

        return snapshot;
    }

    private static int RestoreMeshVertexColors(string modelPath, List<MeshVertexColorSnapshot> snapshot)
    {
        if (snapshot == null || snapshot.Count == 0)
        {
            return 0;
        }

        var remaining = new List<MeshVertexColorSnapshot>(snapshot);
        int restored = 0;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null)
        {
            return 0;
        }

        foreach (UnityEngine.Object asset in assets)
        {
            Mesh mesh = asset as Mesh;
            if (mesh == null || mesh.vertexCount <= 0)
            {
                continue;
            }

            int matchIndex = -1;
            for (int i = 0; i < remaining.Count; i++)
            {
                MeshVertexColorSnapshot candidate = remaining[i];
                if (candidate.Colors == null ||
                    candidate.VertexCount != mesh.vertexCount ||
                    !string.Equals(candidate.Name, mesh.name, StringComparison.Ordinal))
                {
                    continue;
                }

                matchIndex = i;
                break;
            }

            if (matchIndex < 0)
            {
                continue;
            }

            mesh.colors = remaining[matchIndex].Colors;
            EditorUtility.SetDirty(mesh);
            remaining.RemoveAt(matchIndex);
            restored++;
        }

        if (restored > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return restored;
    }

    private static string BuildDependencyDiagnosticLine(GeneratedAsset asset, string dependency)
    {
        string fullPath = AssetPathToFullPath(dependency);
        string existsText = File.Exists(fullPath) ? "文件存在" : "磁盘上找不到这个文件（很可能已被移动或删除）";
        string lastWriteText = File.Exists(fullPath)
            ? File.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd HH:mm:ss")
            : "N/A";
        string expectedFolder = IsTextureAsset(dependency) ? asset.AssetFolder + "/Texture"
            : IsMaterialAsset(dependency) ? asset.AssetFolder + "/Material"
            : IsModelAsset(dependency) ? asset.AssetFolder + "/Model"
            : "(该类型不会被自动归类，需要人工确认)";

        return asset.AssetName + ": " + dependency +
            "\n    当前状态: " + existsText + "，最后修改时间: " + lastWriteText +
            "\n    期望所在目录: " + expectedFolder +
            "\n    处理建议: 如果这个文件是模型专属资源，请把它移动/复制进上面的期望目录后重新打包；" +
            "如果它应该是公共运行时资源，请确认它在白名单目录（Assets/Retinar/Scripts|XLua|Plugins 或 Assets/RetinarRuntime）下。";
    }

    // ---------------------------------------------------------------------------
    // 打包前校验：Model 目录纯净度
    // ---------------------------------------------------------------------------

    private static bool ValidateModelFoldersAreClean(List<GeneratedAsset> assets, out string errorText)
    {
        var errors = new List<string>();
        foreach (GeneratedAsset asset in assets)
        {
            string modelFolder = asset.AssetFolder + "/Model";
            string fullModelFolder = AssetPathToFullPath(modelFolder);
            if (!Directory.Exists(fullModelFolder))
            {
                errors.Add(asset.AssetName + ": missing " + modelFolder);
                continue;
            }

            // 先自愈一次：把误留在 Model 目录里的贴图/材质等文件挪回它们该在的文件夹。
            // 这能覆盖“MoveAssetToExactPath 之前失败过，文件还留在 Model 里”的情况——
            // 现在 MoveAssetToExactPath 失败会重新走一次而不是静默放弃。
            FlattenModelCompanionFolders(asset.AssetFolder);

            foreach (string directory in Directory.GetDirectories(fullModelFolder, "*", SearchOption.AllDirectories))
            {
                errors.Add(asset.AssetName + ": unexpected Model subfolder " + FullPathToAssetPath(directory) +
                    "\n    磁盘路径: " + directory);
            }

            foreach (string filePath in Directory.GetFiles(fullModelFolder, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(filePath).Equals(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath = FullPathToAssetPath(filePath);
                if (!IsModelAsset(assetPath))
                {
                    errors.Add(asset.AssetName + ": non-model file in Model " + assetPath +
                        "\n    磁盘路径: " + filePath +
                        "\n    最后修改时间: " + File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm:ss") +
                        "\n    处理建议: 该文件已尝试自动归类失败，请手动确认它应该在 Texture/Material/Animation/Text 中的哪一个文件夹。");
                }
            }
        }

        errorText = string.Join("\n\n", errors.Distinct().OrderBy(path => path).ToArray());
        return errors.Count == 0;
    }

    // WriteModelFolderFailureReport / WriteExternalDependencyFailureReport 已删除，
    // 合并进上面的 WriteValidationFailureReport——一次打包只产出一份按资产分段的报告。
}
