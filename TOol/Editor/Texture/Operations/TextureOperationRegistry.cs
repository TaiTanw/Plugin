using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =====================================================================================
// 职责边界：
//   只负责"工程里有哪些贴图操作可用"。不执行操作、不判断该不该执行。
//
// 用反射发现的原因和 TextureCodecRegistry 一样：让"加一个操作"这件事不需要
//   回来改任何已有文件。窗口、导入回调都只跟这个注册表打交道。
// =====================================================================================

/// <summary>
/// 纹理操作反射注册表
/// </summary>
public static class TextureOperationRegistry
{
    private static List<ITextureAssetOperation> operations;

    public static IList<ITextureAssetOperation> All
    {
        get
        {
            EnsureDiscovered();
            return operations;
        }
    }
    /// <summary>
    /// 通过ID查找
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static ITextureAssetOperation FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        EnsureDiscovered();
        foreach (ITextureAssetOperation operation in operations)
        {
            if (operation.Id == id)
            {
                return operation;
            }
        }

        return null;
    }

    /// <summary>
    /// 取出配置里勾选为"导入时自动执行"的那些操作，按 Order 排好序。
    /// 配置里填了但工程里找不到的 Id 会打一条警告——通常意味着某个操作类被删掉或改名了，
    /// 这种情况必须让人看到，否则会以为导入还在处理，实际已经悄悄不做了。
    /// </summary>
    public static List<ITextureAssetOperation> GetImportAutoOperations(TextureProcessSettings settings)
    {
        var result = new List<ITextureAssetOperation>();
        if (settings == null || settings.importAutoOperationIds == null)
        {
            return result;
        }

        foreach (string id in settings.importAutoOperationIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            ITextureAssetOperation operation = FindById(id);
            if (operation == null)
            {
                Debug.LogWarning("[TextureOperationRegistry] 配置里的导入自动操作 Id 在工程里找不到对应实现: " + id +
                    "\n请到 Tools/资源处理总面板 → 贴图处理 里重新勾选，或确认对应的操作脚本是否被删除/改名。");
                continue;
            }

            if (!result.Contains(operation))
            {
                result.Add(operation);
            }
        }

        return result.OrderBy(operation => operation.Order).ToList();
    }

    /// <summary>主面板批量：读取 Settings.masterBatchOperationIds。</summary>
    public static List<ITextureAssetOperation> GetMasterBatchOperations(TextureProcessSettings settings)
    {
        var result = new List<ITextureAssetOperation>();
        if (settings == null)
        {
            return result;
        }

        settings.EnsureMasterBatchDefaults();
        if (settings.masterBatchOperationIds == null)
        {
            return result;
        }

        foreach (string id in settings.masterBatchOperationIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            ITextureAssetOperation operation = FindById(id);
            if (operation == null)
            {
                Debug.LogWarning("[TextureOperationRegistry] 主批量操作 Id 找不到实现: " + id);
                continue;
            }

            if (!result.Contains(operation))
            {
                result.Add(operation);
            }
        }

        return result.OrderBy(operation => operation.Order).ToList();
    }

    /// <summary>
    /// 确保存在（反射获得拓展的关键部分）
    /// </summary>
    private static void EnsureDiscovered()
    {
        if (operations != null)
        {
            return;
        }

        operations = new List<ITextureAssetOperation>();
        Type interfaceType = typeof(ITextureAssetOperation);
        //获得类型所在程序集
        foreach (Type type in interfaceType.Assembly.GetTypes())
        {
            //过滤程序集类型：不要抽象类/接口/不能赋值给interfaceType
            if (type.IsAbstract || type.IsInterface || !interfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning("[TextureOperationRegistry] " + type.Name + " 实现了 ITextureAssetOperation 但没有无参构造函数，已跳过。");
                continue;
            }
            //实例化并假如列表
            operations.Add((ITextureAssetOperation)Activator.CreateInstance(type));
        }
        //排序，order升序，字母升序（A到Z)
        operations = operations.OrderBy(operation => operation.Order).ThenBy(operation => operation.DisplayName).ToList();
        WarnOnDuplicateIds();
    }

    private static void WarnOnDuplicateIds()
    {
        //重名ID分组，count大于1表示有重复
        foreach (IGrouping<string, ITextureAssetOperation> group in operations.GroupBy(operation => operation.Id))
        {
            if (group.Count() > 1)
            {
                Debug.LogError("[TextureOperationRegistry] 发现重复的操作 Id \"" + group.Key + "\"，涉及: " +
                    string.Join(", ", group.Select(operation => operation.GetType().Name).ToArray()) +
                    "。Id 必须唯一，否则配置里的勾选会指向错误的操作。");
            }
        }
    }
}
