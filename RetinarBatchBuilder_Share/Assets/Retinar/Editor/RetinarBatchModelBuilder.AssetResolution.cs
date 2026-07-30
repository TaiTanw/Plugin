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

            string[] dependencies = AssetDatabase.GetDependencies(asset.PrefabPath, true);
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

    /// <summary>
    /// 尝试把预制体里指向"资产文件夹之外"的贴图/材质依赖，复制并重定向进该模型自己的
    /// Art 目录。只处理贴图和材质这两种最常见、也最容易因为"文件被挪动"而失联的类型；
    /// 其它类型（脚本、动画控制器、网格等）不做自动处理，交给后面的报错去暴露真正的结构性问题。
    /// </summary>
    private static bool TryHealExternalDependencies(GeneratedAsset asset, out List<string> healedPaths)
    {
        healedPaths = new List<string>();
        string materialFolder = asset.AssetFolder + "/Material";
        string textureFolder = asset.AssetFolder + "/Texture";

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
                    if (string.IsNullOrEmpty(materialPath) ||
                        materialPath.StartsWith(asset.AssetFolder + "/", StringComparison.OrdinalIgnoreCase) ||
                        !IsMaterialAsset(materialPath))
                    {
                        continue;
                    }

                    string targetMaterialPath = materialFolder + "/" + Path.GetFileName(materialPath);
                    string copiedMaterialPath = CopyAssetToExactPath(materialPath, targetMaterialPath);
                    Material copiedMaterial = AssetDatabase.LoadAssetAtPath<Material>(copiedMaterialPath);
                    if (copiedMaterial == null)
                    {
                        continue;
                    }

                    RemapMaterialTexturesToArtFolder(copiedMaterial, textureFolder);
                    EditorUtility.SetDirty(copiedMaterial);

                    materials[i] = copiedMaterial;
                    rendererChanged = true;
                    healedPaths.Add(materialPath + "  ->  " + copiedMaterialPath);
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

        return changed;
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
