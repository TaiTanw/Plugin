using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// L3 只读：展示当前大类「识别层」认哪些后缀 / 类型（改列表仍在 SO / Codec）。
// =====================================================================================

/// <summary>资源识别说明（只读 UI）。</summary>
public static class ResourceRecognitionGui
{
    private static readonly string[] TextureExtensionProbes =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".webp", ".bmp"
    };

    public static void DrawTexture()
    {
        EditorGUILayout.HelpBox(
            "【资源识别 · 只读】手动 / 总批量 / 管线⑤ 进列表的后缀由 TextureCodecRegistry（Codec 实现）决定。\n" +
            "当前可识别：" + FormatTextureExtensions() + "\n" +
            "增删格式：新增/调整 ITextureFileCodec 实现（非本页编辑）。",
            MessageType.None);
    }

    public static void DrawModel(ModelProcessSettings settings)
    {
        string list = FormatModelExtensions(settings);
        EditorGUILayout.HelpBox(
            "【资源识别 · 只读】手动 / 总批量 / 管线⑤ 进列表的后缀见 ModelProcessSettings.supportedExtensions。\n" +
            "当前可识别：" + list + "\n" +
            "修改：在下方 SO 字段或 Project 选中该资产编辑（本页不另做后缀编辑器）。",
            MessageType.None);
    }

    public static void DrawMaterial()
    {
        EditorGUILayout.HelpBox(
            "【资源识别 · 只读】收集器按 Unity 类型 t:Material（通常为 .mat 资产），不靠后缀表驱动。\n" +
            "进列表后由 Op 按 Shader 名白名单/子串决定是否烤交付 Shader。\n" +
            "无需为材质维护扩展名列表。",
            MessageType.None);
    }

    private static string FormatTextureExtensions()
    {
        var hit = new List<string>();
        for (int i = 0; i < TextureExtensionProbes.Length; i++)
        {
            string ext = TextureExtensionProbes[i];
            if (TextureCodecRegistry.IsSupported("probe" + ext) && !hit.Contains(ext))
            {
                hit.Add(ext);
            }
        }

        return hit.Count == 0 ? "（未发现 Codec）" : string.Join(", ", hit.ToArray());
    }

    private static string FormatModelExtensions(ModelProcessSettings settings)
    {
        if (settings == null || settings.supportedExtensions == null ||
            settings.supportedExtensions.Count == 0)
        {
            return "（空 — 将无人命中）";
        }

        var builder = new StringBuilder();
        for (int i = 0; i < settings.supportedExtensions.Count; i++)
        {
            string e = settings.supportedExtensions[i];
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(e.Trim().ToLowerInvariant());
        }

        return builder.Length == 0 ? "（空 — 将无人命中）" : builder.ToString();
    }
}
