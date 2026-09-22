using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 同一 FlattenOperationSettings 在人工/管线面板中的统一绘制与所有权提示。
// =====================================================================================

public static class FlattenOperationSettingsGui
{
    public static void Draw(
        FlattenOperationSettings settings,
        FlattenSettingsScope panelScope,
        bool drawCategories)
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("没有平铺操作 SO。", MessageType.Error);
            return;
        }

        FlattenSettingsScope assetScope = FlattenOperationSettings.GetScope(settings);
        bool editable = assetScope == panelScope;

        if (editable)
        {
            EditorGUILayout.HelpBox(
                panelScope == FlattenSettingsScope.Pipeline
                    ? "管线 SO：本面板可编辑；运行时先冻结快照。"
                    : "人工 SO：本面板可编辑；只影响人工操作。",
                MessageType.Info);
        }
        else
        {
            string reason = assetScope == FlattenSettingsScope.Unclassified
                ? "SO 不在约定的 Manual 或 Pipeline 配置目录，所有面板均只读。"
                : "这是" + ScopeName(assetScope) + " SO，在" + ScopeName(panelScope) + "面板中只读。";
            EditorGUILayout.HelpBox(reason + " 仍可按其快照执行。", MessageType.Warning);
        }

        if (panelScope == FlattenSettingsScope.Pipeline)
        {
            EditorGUILayout.HelpBox(
                "Missing Script：自动线固定剥后再写入 Art 副本（Incoming 不改）。" +
                "人工在「平铺设置（人工）」勾选；不勾则④失败并列出缺脚本。",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!editable))
        {
            EditorGUI.BeginChangeCheck();
            bool clear = EditorGUILayout.ToggleLeft(
                "执行前清空本次 Art 单元", settings.ClearDestinationArtFolder);
            bool collider = EditorGUILayout.ToggleLeft(
                "最终 Prefab 添加根 BoxCollider", settings.AddBoxCollider);
            bool strip = settings.StripMissingScripts;
            if (panelScope == FlattenSettingsScope.Manual)
            {
                strip = EditorGUILayout.ToggleLeft(
                    "剥 Missing Script 再存 Art 副本（不勾则④失败并列出）",
                    settings.StripMissingScripts);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "修改平铺操作配置");
                settings.ClearDestinationArtFolder = clear;
                settings.AddBoxCollider = collider;
                if (panelScope == FlattenSettingsScope.Manual)
                {
                    settings.StripMissingScripts = strip;
                }

                EditorUtility.SetDirty(settings);
            }

            if (drawCategories)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("分类规则", EditorStyles.boldLabel);
                IList<IFlattenCategoryProcessor> processors = FlattenCategoryRegistry.All;
                for (int i = 0; i < processors.Count; i++)
                {
                    DrawProcessor(settings, processors[i], editable);
                }
            }
        }
    }

    private static void DrawProcessor(
        FlattenOperationSettings settings,
        IFlattenCategoryProcessor processor,
        bool editable)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            bool locked = processor.Id == UnknownFlattenProcessor.ProcessorId;
            bool enabled = settings.IsCategoryEnabled(processor.Id, true);
            string suffixes = settings.GetCategorySuffixes(processor.Id, processor.DefaultSuffixes);

            using (new EditorGUI.DisabledScope(locked || !editable))
            {
                bool nextEnabled = EditorGUILayout.ToggleLeft(
                    processor.DisplayName + "  [" + processor.Id + "]", enabled);
                string nextSuffixes = suffixes;
                if (processor.DefaultSuffixes != null && processor.DefaultSuffixes.Length > 0)
                {
                    nextSuffixes = EditorGUILayout.DelayedTextField("后缀", suffixes);
                }
                else
                {
                    EditorGUILayout.LabelField("后缀", "（兜底，无后缀表）");
                }

                if (!locked && editable &&
                    (nextEnabled != enabled || nextSuffixes != suffixes))
                {
                    Undo.RecordObject(settings, "修改平铺分类规则");
                    settings.SetCategory(processor.Id, nextEnabled, nextSuffixes);
                    EditorUtility.SetDirty(settings);
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("输出", string.Join(" ； ", processor.OutputFolderHints));
            }
        }
    }

    private static string ScopeName(FlattenSettingsScope scope)
    {
        switch (scope)
        {
            case FlattenSettingsScope.Manual:
                return "人工";
            case FlattenSettingsScope.Pipeline:
                return "管线";
            default:
                return "未归类";
        }
    }
}
