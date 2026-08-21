using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 平铺分类面板：勾选大类 + 编辑后缀；输出路径只读。
// =====================================================================================

/// <summary>平铺资源大类配置窗口。</summary>
public sealed class FlattenWindow : EditorWindow
{
    private Vector2 scroll;
    private FlattenCategorySettings settings;

    public static void Open()
    {
        GetWindow<FlattenWindow>("平铺分类").Show();
    }

    private void OnEnable()
    {
        settings = FlattenCategorySettings.Load();
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            settings = FlattenCategorySettings.Load();
        }

        EditorGUILayout.HelpBox(
            "大类勾选 = 是否参与筛选。输出文件夹由各处理器类内 const 决定，面板只读。\n" +
            "未勾选的大类，其后缀文件会落到 Unknown/（提示不阻断）。\n" +
            "Art 一包一夹；单元根目录 = 处理器 Id。",
            MessageType.Info);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("后处理（后续会做成调度层）", EditorStyles.boldLabel);
        bool addCollider = EditorGUILayout.ToggleLeft(
            "添加根 BoxCollider",
            FlattenPostProcessSettings.AddBoxCollider);
        if (addCollider != FlattenPostProcessSettings.AddBoxCollider)
        {
            FlattenPostProcessSettings.AddBoxCollider = addCollider;
        }

        EditorGUILayout.HelpBox(
            "关掉后平铺不加碰撞体，规范化导出也不再要求有碰撞体。\n" +
            "Prefab 入口：套空父、不缩放、不 Bake 子节点。FBX 入口 SafeZone 暂保持。\n" +
            "门禁与输出清单已在 30_Business 留 SO 接口，总面板未接线。",
            MessageType.Info);
        EditorGUILayout.EndVertical();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        IList<IFlattenCategoryProcessor> processors = FlattenCategoryRegistry.All;
        for (int i = 0; i < processors.Count; i++)
        {
            DrawProcessor(processors[i]);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawProcessor(IFlattenCategoryProcessor processor)
    {
        EditorGUILayout.BeginVertical("box");
        bool locked = processor.Id == UnknownFlattenProcessor.ProcessorId;
        EditorGUI.BeginDisabledGroup(locked);
        bool enabled = EditorGUILayout.ToggleLeft(
            processor.DisplayName + "  [" + processor.Id + "]",
            settings.IsEnabled(processor.Id));
        if (!locked && enabled != settings.IsEnabled(processor.Id))
        {
            settings.SetEnabled(processor.Id, enabled);
        }

        EditorGUI.EndDisabledGroup();

        if (processor.DefaultSuffixes != null && processor.DefaultSuffixes.Length > 0)
        {
            EditorGUI.BeginDisabledGroup(!settings.IsEnabled(processor.Id));
            string current = settings.GetSuffixesText(processor.Id, processor.DefaultSuffixes);
            string edited = EditorGUILayout.DelayedTextField("后缀", current);
            if (edited != current)
            {
                settings.SetSuffixesText(processor.Id, edited);
            }

            EditorGUI.EndDisabledGroup();
        }
        else
        {
            EditorGUILayout.LabelField("后缀", "（兜底，无后缀表）");
        }

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField("输出", string.Join(" ； ", processor.OutputFolderHints));
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }
}
