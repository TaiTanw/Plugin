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
                result.Info("  Art: " + prefabPaths[i]);
            }
        }

        // ⑤ 依赖④：未平铺则强制跳过
        if (options.RunPostProcess && !options.RunFlatten)
        {
            result.Info("[Pipeline] ⑤ 已请求但④未开，已跳过（⑤依赖④）");
            options.RunPostProcess = false;
        }

        // ⑤ 后处理（默认关；不调用「设置自动」）
        if (options.RunPostProcess)
        {
            string report = ToolPostProcessApi.RunMasterBatch(options.PostProcessFolderPaths);
            result.Info("[Pipeline] ⑤ PostProcess\n" + report);
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
