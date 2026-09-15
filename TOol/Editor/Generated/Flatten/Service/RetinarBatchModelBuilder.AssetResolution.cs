using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ④ 引用整理与 E 抽取。步骤 12 已删除旧 SafeZone 专用的源资产搜集链。
// E 是 Extract 唯一产品入口；Finish 不再 Extract。普通 B 的贴图来源受步骤 11 约束，
// 无合法副本只警告并保留引用；B′ 相对树沿用原有处理。这里不是交付质量闸。
public static partial class RetinarBatchModelBuilder
{
    // ---------------------------------------------------------------------------
    // 资产类型判断
    // ---------------------------------------------------------------------------

    private static bool IsModelAsset(string assetPath)
    {
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        // .glb/.gltf：UnityGLTF ScriptedImporter 主资产，外来 Prefab 常嵌套引用它（无 FBX/OBJ）。
        return extension == ".fbx" || extension == ".obj" ||
               extension == ".glb" || extension == ".gltf";
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
            // 原来这里只 LogWarning 然后静默放弃，文件留在错误位置，要等出包前的
            // 目录清洁度校验才报错，那时已看不出是这里移动失败。那道校验现已删除
            // （D24-7），所以这条 LogError 是唯一的信号，更不能降级成 Warning。
            Debug.LogError("[Retinar] 移动资产失败，文件将保留在原位置，交付副本目录会不干净：\n" +
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
    // 公共运行时依赖白名单
    //
    // 2026-09-03（backlog D24-7）：出包前的三道校验（ValidateModelFoldersAreClean /
    // ValidatePrefabSpatialPlacement / ValidateExternalDependencies）连同它们的调度
    // （PartitionAssetsThatPassValidation / CollectValidationFailures）与报告
    // （WriteValidationFailureReport）已整体删除——管线 ①→⑥ 从不经过它们。
    // 白名单本身留下：④ 的依赖分类与 ⑥ RetinarAbApi 仍在读。
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
    public static List<string> ListExternalFbmTextureDependencies(string assetFolder, string prefabPath)
    {
        if (string.IsNullOrEmpty(assetFolder) || string.IsNullOrEmpty(prefabPath))
        {
            return new List<string>();
        }

        return CollectExternalFbmTextureDependencies(assetFolder, prefabPath);
    }

    private static List<string> CollectExternalFbmTextureDependencies(string assetFolder, string prefabPath)
    {
        var asset = new GeneratedAsset(
            Path.GetFileName(assetFolder),
            assetFolder,
            prefabPath);
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
    private static bool TryHealExternalDependencies(
        GeneratedAsset asset,
        FlattenOperationPolicy operationPolicy,
        out List<string> healedPaths,
        FlattenTextureIdentity identity = null)
    {
        healedPaths = new List<string>();
        string materialFolder = FlattenLayout.MaterialFolder(asset.AssetFolder);
        string textureFolder = FlattenLayout.TextureFolder(asset.AssetFolder);
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
                        if (string.IsNullOrEmpty(copiedMaterialPath) ||
                            copiedMaterialPath.Equals(materialPath, StringComparison.OrdinalIgnoreCase) ||
                            !copiedMaterialPath.StartsWith(asset.AssetFolder + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.LogError(
                                "[Retinar] 自愈复制外部材质失败，拒绝直接修改源材质: " +
                                materialPath + " -> " + targetMaterialPath);
                            continue;
                        }

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

                    if (RemapMaterialTexturesToArtFolder(workingMaterial, textureFolder, identity))
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

        if (CopyRemainingExternalDependencies(asset, operationPolicy, healedPaths, identity))
        {
            changed = true;
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

            if (RemapMaterialTexturesToArtFolder(material, textureFolder, identity))
            {
                EditorUtility.SetDirty(material);
                changed = true;
                healedPaths.Add("贴图重映射: " + path);
            }
        }

        // 切断 Model FBX 对导入区 .fbm 的依赖曾在自愈里再 Extract 一次。
        // 步骤 10：Extract 只归 E（FlattenApplyImportAndExtract）。自愈只补拷 + remap。
        Debug.Log("[Retinar] " + asset.AssetName + "：自愈不 Extract（所有者是 E）");

        if (RemapAllArtMaterialsToLocalTextures(asset.AssetFolder, identity))
        {
            changed = true;
            healedPaths.Add("Art/Material 贴图全部收到本模型 " + FlattenLayout.TextureFolder(asset.AssetFolder));
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
    /// GetDependencies 全量补拷：动画 PPtr、兄弟 Art 包、源导入区等 Renderer 扫不到的引用。
    /// 目标已存在则只加入 remap 表（含 .anim），不覆盖本包副本。
    /// </summary>
    private static bool CopyRemainingExternalDependencies(
        GeneratedAsset asset,
        FlattenOperationPolicy operationPolicy,
        List<string> healedPaths,
        FlattenTextureIdentity identity = null)
    {
        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] dependencies = AssetDatabase.GetDependencies(asset.PrefabPath, true);
        string folderPrefix = asset.AssetFolder + "/";

        foreach (string rawDependency in dependencies)
        {
            string path = rawDependency.Replace("\\", "/");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.Equals(asset.PrefabPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase) ||
                IsApprovedRuntimeDependency(path))
            {
                continue;
            }

            // 步骤 11：收尾不再发现/补拷贴图。身份来自 Begin+B / E，不能用重导后的依赖扩充。
            if (identity != null && FlattenTextureIdentity.IsTextureFile(path))
            {
                if (identity.ResolveExact(path) == null)
                    identity.Warn("收尾不补拷未知来源贴图", asset.PrefabPath, path);
                continue;
            }
            string targetFolder = FlattenCopyRunner.ResolveRelativeFolder(path, operationPolicy);
            if (string.IsNullOrEmpty(targetFolder))
            {
                continue;
            }

            string destFolder = asset.AssetFolder + "/" + targetFolder;
            FlattenLayout.EnsureFolder(destFolder);
            string requestedTargetPath = destFolder + "/" + Path.GetFileName(path);
            if (IsTextureAsset(path))
            {
                SyncNewerSourceTextureToWorkingCopy(path, requestedTargetPath);
            }

            string copiedPath = CopyAssetToExactPath(path, requestedTargetPath);
            if (!copiedPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                copied[path] = copiedPath;
                healedPaths.Add(path + "  ->  " + copiedPath);
            }
        }

        if (copied.Count == 0)
        {
            return false;
        }

        RemapCopiedAssetReferences(copied, asset.AssetFolder, identity);
        return true;
    }

    /// <summary>
    /// 把交付区 FBX 的内嵌贴图强制抽到 Assets/Art/&lt;模型&gt;/image/Texture，
    /// 并把 ModelImporter 上仍指向外部（尤其是导入区 .fbm）的贴图 remap 到本地副本。
    ///
    /// 根因：Copy/Flatten 之后 Art/Texture 里已有副本，但 FBX 再导入时 Unity 会按
    /// 贴图名复用工程里先存在的 Assets/AAA/.../fbx.fbm，GetDependencies 于是一直挂外部路径。
    /// materialSearch=Local 只影响材质搜索，管不到这层“同名贴图复用”。
    /// </summary>
    private static bool ExtractAndBindPackagedModelTextures(
        string assetFolder,
        FlattenOperationPolicy operationPolicy,
        FlattenTextureIdentity identity = null)
    {
        string modelFolder = FlattenLayout.ModelFolder(assetFolder);
        string textureFolder = FlattenLayout.TextureFolder(assetFolder);
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

            // 已无外部 .fbm 时不必 Extract（会盖贴图、冲顶点色）。
            // 但仍可能挂着兄弟 Art 包或源导入区的同名贴图（GetDependencies 会带上），要 AddRemap 到本包副本。
            if (externalBefore.Count == 0)
            {
                bool settingsDirty =
                    importer.materialLocation != ModelImporterMaterialLocation.InPrefab ||
                    importer.materialSearch != ModelImporterMaterialSearch.Local ||
                    importer.materialName != ModelImporterMaterialName.BasedOnMaterialName;
                int remapCount = RemapModelImporterTexturesToArtFolder(importer, assetFolder, textureFolder, identity);
                if (settingsDirty || remapCount > 0)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.materialSearch = ModelImporterMaterialSearch.Local;
                    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    Debug.Log("[Retinar] ExtractAndBind 跳过 Extract（无外部 .fbm），校正材质搜索并 AddRemap 外部贴图 " +
                        remapCount + " 条后重导: " + modelPath);
                    SaveAndReimportPreservingMeshVertexColors(importer);
                    changed = true;
                }
                else
                {
                    Debug.Log("[Retinar] ExtractAndBind 跳过: 无外部 .fbm / 外部贴图，材质搜索已是 Local — " + modelPath);
                }

                continue;
            }

            // ExtractTextures 会把 FBX 内嵌原始大图写进 Texture/，覆盖两遍流程里已压缩的同名文件。
            // 先快照再抽取，抽取后把“被放大”的文件恢复成压缩版；缺失的仍用抽取结果补齐。
            Dictionary<string, byte[]> preservedTextures = SnapshotTextureFolderFiles(textureFolder);
            var extractBefore = identity?.SnapshotExtractFolder(textureFolder);
            bool extracted = false;

            try
            {
                extracted = importer.ExtractTextures(textureFolder);
                ExtractTexturesInvokeCount++;
                changed = true;
                Debug.Log("[Retinar] ExtractTextures 已调用: " + modelPath + " -> " + textureFolder +
                          "（本趟第 " + ExtractTexturesInvokeCount + " 次）");
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
            // 必须在旧压缩图恢复后核对；不能把已被恢复的旧像素冒认成本轮 Extract 产物。
            if (extracted && identity != null)
                identity.RegisterExtracted(modelPath, textureFolder, extractBefore);

            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;

            // 先按当前依赖表 remap 一次，再导入；导入后若仍挂外部 .fbm，再 remap + 导入一次。
            int remapPass1 = RemapModelImporterTexturesToArtFolder(importer, assetFolder, textureFolder, identity);
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
                int remapPass2 = RemapModelImporterTexturesToArtFolder(importer, assetFolder, textureFolder, identity);
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

        FlattenModelCompanionFolders(assetFolder, operationPolicy);
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
        string textureFolder,
        FlattenTextureIdentity identity = null)
    {
        if (identity != null) return RemapKnownModelTextures(importer, identity);
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
            string targetPath = FlattenLayout.ResolveExistingOrDefaultTexturePath(assetFolder, fileName);
            bool copied = false;
            if (AssetDatabase.LoadMainAssetAtPath(targetPath) == null)
            {
                FlattenLayout.EnsureFolder(Path.GetDirectoryName(targetPath).Replace("\\", "/"));
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

    private static bool RemapAllArtMaterialsToLocalTextures(string assetFolder, FlattenTextureIdentity identity = null)
    {
        string materialFolder = FlattenLayout.MaterialFolder(assetFolder);
        string textureFolder = FlattenLayout.TextureFolder(assetFolder);
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

            if (RemapMaterialTexturesToArtFolder(material, textureFolder, identity))
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

}
