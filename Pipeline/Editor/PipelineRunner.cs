using System.Collections.Generic;
using System.IO;
using UnityEngine;

// =====================================================================================
// Pipeline — 编排入口（单文件 ②→③→⑥；④⑤ 可选；结果用字符串）
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

        if (!ResolveModelPaths(options, result))
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

            List<string> written = ToolPrefabApi.BuildPrefabs(options.ModelPaths, options.MaterialId);
            prefabPaths = written ?? new List<string>();
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

        // ④ 平铺（可选）→ 成功后⑥改打 Art Prefab
        if (options.RunFlatten)
        {
            if (prefabPaths.Count == 0)
            {
                result.Fail(PipelineErrorCodes.BadArgs, "RunFlatten=true 但无 Prefab 路径");
                LogResult(result);
                return result;
            }

            List<string> artPrefabPaths;
            int n = RetinarFlattenApi.FlattenPaths(prefabPaths, quiet, out artPrefabPaths);
            result.Info("[Pipeline] ④ Flatten " + n + " / " + prefabPaths.Count);
            if (n <= 0 || artPrefabPaths == null || artPrefabPaths.Count == 0)
            {
                result.Fail(PipelineErrorCodes.FlattenFailed, "平铺未成功或未返回 Art Prefab 路径");
                LogResult(result);
                return result;
            }

            prefabPaths = artPrefabPaths;
            result.Info("[Pipeline] ④→⑥ 改用 Art Prefab × " + prefabPaths.Count);
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                result.Info("  Art: " + artPrefabPaths[i]);
            }

            // D17：⑤ 扫本次 Art 单元（含 Model/），不依赖 L1 Prefs / ②.2 写入的 Import 夹。
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
            string report = ToolPostProcessApi.RunMasterBatch(options.PostProcessFolderPaths);
            result.Info("[Pipeline] ⑤ PostProcess\n" + report);

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
                    options.ExportUnityPackage,
                    options.Quiet);
            }
            else
            {
                abOpt.ExportUnityPackage = options.ExportUnityPackage;
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
                    string reWhite = ToolPostProcessApi.RunMasterBatch(
                        options.PostProcessFolderPaths,
                        includeTexture: false,
                        includeMaterial: false,
                        includeModel: true);
                    result.Info(reWhite);

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

    /// <summary>单文件 SourcePath 或已有 ModelPaths → 工程内模型列表。</summary>
    private static bool ResolveModelPaths(PipelineOptions options, PipelineResult result)
    {
        if (!string.IsNullOrWhiteSpace(options.SourcePath))
        {
            string source = options.SourcePath.Trim();

            if (options.RunImport)
            {
                string assetPath;
                string importMsg;
                if (!ToolImportApi.ImportSingleModel(source, out assetPath, out importMsg))
                {
                    result.Fail(PipelineErrorCodes.ImportFailed, "[Pipeline] ② 导入失败: " + importMsg);
                    return false;
                }

                result.Info("[Pipeline] ② " + importMsg);
                options.ModelPaths = new List<string> { assetPath };

                if (options.SyncImportFolderToResourcePanel)
                {
                    SyncFolderToL1(assetPath, result);
                }

                return true;
            }

            // ② 关闭：仅接受已在 Assets 内的路径（ImportSingleModel 对 Assets 路径不拷贝）
            if (IsExternalDiskPath(source))
            {
                result.Fail(PipelineErrorCodes.BadArgs,
                    "[Pipeline] 工程外路径需要打开步骤②导入: " + source);
                return false;
            }

            string existing;
            string msg;
            if (!ToolImportApi.ImportSingleModel(source, out existing, out msg))
            {
                result.Fail(PipelineErrorCodes.BadArgs, "[Pipeline] 无法解析工程内模型: " + msg);
                return false;
            }

            result.Info("[Pipeline] ② 跳过导入: " + existing);
            options.ModelPaths = new List<string> { existing };
            return true;
        }

        if (options.ModelPaths != null && options.ModelPaths.Count > 0)
        {
            return true;
        }

        if (options.RunPrefab || (options.RunAb && (options.PrefabPaths == null || options.PrefabPaths.Count == 0)))
        {
            result.Fail(PipelineErrorCodes.BadArgs, "[Pipeline] 未提供 SourcePath / ModelPaths");
            return false;
        }

        return true;
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

    private static void SyncFolderToL1(string assetModelPath, PipelineResult result)
    {
        if (string.IsNullOrEmpty(assetModelPath))
        {
            return;
        }

        string folder = Path.GetDirectoryName(assetModelPath.Replace("\\", "/"));
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        folder = folder.Replace("\\", "/");
        var folders = ResourceBatchFolderStore.GetMasterFolders();
        for (int i = 0; i < folders.Count; i++)
        {
            if (string.Equals(folders[i], folder, System.StringComparison.OrdinalIgnoreCase))
            {
                result.Info("[Pipeline] 资源总面板批量路径已含: " + folder);
                return;
            }
        }

        folders.Insert(0, folder);
        ResourceBatchFolderStore.SetMasterFolders(folders);
        result.Info("[Pipeline] 已写入资源总面板批量路径: " + folder);
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
