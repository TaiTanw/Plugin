using System;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Pipeline — (B) CLI 外壳。唯一 -executeMethod 入口。
// 只解析 argv、填 PipelineOptions、调 Runner、Exit。禁止 Selection / Dialog / 业务细节。
// =====================================================================================

/// <summary>
/// Unity 无头入口：<c>-executeMethod PipelineCli.Run</c>。
/// 第一刀：必填 <c>-source</c>，可选 <c>-materialId</c>；步骤开关跟 <see cref="PipelineStepSettings"/>。
/// </summary>
public static class PipelineCli
{
    /// <summary>无参静态方法，供 Unity <c>-executeMethod</c> 调用。</summary>
    public static void Run()
    {
        int code = PipelineErrorCodes.Other;
        try
        {
            string source;
            string materialId;
            string parseError;
            if (!TryParseArgs(Environment.GetCommandLineArgs(), out source, out materialId, out parseError))
            {
                Debug.LogError("[PipelineCli] " + parseError);
                EditorApplication.Exit(PipelineErrorCodes.BadArgs);
                return;
            }

            PipelineOptions opt = PipelineOptions.FromSettings(PipelineStepSettings.Current, source);
            // batchmode 禁止 Dialog；Quiet ≠ Unity -quit。
            opt.Quiet = true;
            if (!string.IsNullOrWhiteSpace(materialId))
            {
                opt.MaterialId = materialId.Trim();
            }

            opt.SourceBindings = PipelineMaterialId.BuildSourceBindings(
                new[] { source },
                string.IsNullOrWhiteSpace(materialId) ? null : materialId.Trim());

            Debug.Log("[PipelineCli] source=" + source +
                      (string.IsNullOrWhiteSpace(opt.MaterialId) ? string.Empty : " materialId=" + opt.MaterialId));

            PipelineResult result = PipelineRunner.Run(opt);
            code = result != null ? result.ExitCode : PipelineErrorCodes.Other;
            if (result != null)
            {
                if (result.Ok)
                {
                    Debug.Log(result.ToString());
                }
                else
                {
                    Debug.LogError(result.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[PipelineCli] " + ex);
            code = PipelineErrorCodes.Other;
        }

        // LicenseOrEnv(70) 预留，本入口不赋值。⑤ 失败暂不映射 50（D16）。
        EditorApplication.Exit(code);
    }

    /// <summary>从 argv 取 <c>-source</c> / <c>-materialId</c>（也认 <c>-source=</c>）。</summary>
    public static bool TryParseArgs(
        string[] args,
        out string source,
        out string materialId,
        out string error)
    {
        source = null;
        materialId = null;
        error = null;

        if (args == null || args.Length == 0)
        {
            error = "缺少必填参数 -source <path>";
            return false;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string token;
            if (TryReadFlag(args, ref i, "-source", out token))
            {
                source = token;
            }
            else if (TryReadFlag(args, ref i, "-materialId", out token) ||
                     TryReadFlag(args, ref i, "-materialid", out token))
            {
                materialId = token;
            }
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "缺少必填参数 -source <path>（工程外 .glb/.fbx 或 Assets/…）";
            return false;
        }

        source = source.Trim().Trim('"').Replace("\\", "/");
        if (!string.IsNullOrEmpty(materialId))
        {
            materialId = materialId.Trim().Trim('"');
        }

        return true;
    }

    private static bool TryReadFlag(string[] args, ref int index, string flag, out string value)
    {
        value = null;
        string a = args[index];
        if (string.IsNullOrEmpty(a))
        {
            return false;
        }

        if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length || string.IsNullOrEmpty(args[index + 1]) || args[index + 1].StartsWith("-"))
            {
                return false;
            }

            index++;
            value = args[index];
            return true;
        }

        string prefix = flag + "=";
        if (a.Length > prefix.Length &&
            a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = a.Substring(prefix.Length);
            return !string.IsNullOrEmpty(value);
        }

        return false;
    }
}
