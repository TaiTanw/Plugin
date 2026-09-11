using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =====================================================================================
// 轻量反射注册表（对齐插件 2 TextureOperationRegistry）：加处理器不必改已有文件。
// 重复 Id 打 Error，不静默吞。
// =====================================================================================

/// <summary>平铺大类处理器注册表。</summary>
public static class FlattenCategoryRegistry
{
    private static List<IFlattenCategoryProcessor> processors;

    public static IList<IFlattenCategoryProcessor> All
    {
        get
        {
            EnsureDiscovered();
            return processors;
        }
    }

    public static IFlattenCategoryProcessor FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        EnsureDiscovered();
        for (int i = 0; i < processors.Count; i++)
        {
            if (processors[i].Id == id)
            {
                return processors[i];
            }
        }

        return null;
    }

    private static void EnsureDiscovered()
    {
        if (processors != null)
        {
            return;
        }

        processors = new List<IFlattenCategoryProcessor>();
        Type interfaceType = typeof(IFlattenCategoryProcessor);
        foreach (Type type in interfaceType.Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !interfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning("[FlattenCategoryRegistry] " + type.Name +
                    " 实现了 IFlattenCategoryProcessor 但没有无参构造函数，已跳过。");
                continue;
            }

            processors.Add((IFlattenCategoryProcessor)Activator.CreateInstance(type));
        }

        processors = processors
            .OrderBy(processor => processor.Order)
            .ThenBy(processor => processor.DisplayName)
            .ToList();
        WarnOnDuplicateIds();
    }

    private static void WarnOnDuplicateIds()
    {
        foreach (IGrouping<string, IFlattenCategoryProcessor> group in processors.GroupBy(processor => processor.Id))
        {
            if (group.Count() > 1)
            {
                Debug.LogError("[FlattenCategoryRegistry] 发现重复的处理器 Id \"" + group.Key + "\"，涉及: " +
                    string.Join(", ", group.Select(processor => processor.GetType().Name).ToArray()) +
                    "。Id 必须唯一，否则 Art 单元根目录会撞名。");
            }
        }
    }
}
