using System;
using UnityEditor;

// =====================================================================================
// Flatten / Orchestration — 人工提示。普通平铺扫到相对 URI：确认后才继续。
// 点「仍要平铺」= 继续（人责）。点取消或关闭对话框 = 中止，不平铺。
// =====================================================================================

/// <summary>普通平铺扫到相对 URI 时的确认口。测试可替换 <see cref="Confirm"/>。</summary>
public static class FlattenManualPrompt
{
    public const string CancelledMessage = "用户取消，未平铺";

    /// <summary>非 null 时不弹窗，改走回调（EditMode）。返回 false 视为取消。</summary>
    public static Func<string, bool> Confirm;

    /// <summary>
    /// 无文案则直接放行。有文案则弹窗：OK 继续，取消/叉号返回 false。
    /// </summary>
    public static bool ConfirmSplitWithRelativeUris(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return true;
        }

        if (Confirm != null)
        {
            return Confirm(message);
        }

        return EditorUtility.DisplayDialog("普通平铺", message, "仍要平铺", "取消");
    }
}
