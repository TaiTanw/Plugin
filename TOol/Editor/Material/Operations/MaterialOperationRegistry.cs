using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>材质 Op 反射注册表。</summary>
public static class MaterialOperationRegistry
{
    private static List<IMaterialAssetOperation> operations;

    public static IList<IMaterialAssetOperation> All
    {
        get
        {
            EnsureDiscovered();
            return operations;
        }
    }

    public static IMaterialAssetOperation FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        EnsureDiscovered();
        for (int i = 0; i < operations.Count; i++)
        {
            if (operations[i].Id == id)
            {
                return operations[i];
            }
        }

        return null;
    }

    public static List<IMaterialAssetOperation> GetMasterBatchOperations(MaterialProcessSettings settings)
    {
        var result = new List<IMaterialAssetOperation>();
        if (settings == null)
        {
            return result;
        }

        settings.EnsureMasterBatchDefaults();
        if (settings.masterBatchOperationIds == null)
        {
            return result;
        }

        for (int i = 0; i < settings.masterBatchOperationIds.Count; i++)
        {
            string id = settings.masterBatchOperationIds[i];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            IMaterialAssetOperation operation = FindById(id);
            if (operation == null)
            {
                Debug.LogWarning("[MaterialOperationRegistry] 主批量操作 Id 找不到实现: " + id);
                continue;
            }

            if (!result.Contains(operation))
            {
                result.Add(operation);
            }
        }

        return result.OrderBy(op => op.Order).ToList();
    }

    private static void EnsureDiscovered()
    {
        if (operations != null)
        {
            return;
        }

        operations = new List<IMaterialAssetOperation>();
        Type interfaceType = typeof(IMaterialAssetOperation);
        Type[] types = interfaceType.Assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type.IsAbstract || type.IsInterface || !interfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning("[MaterialOperationRegistry] " + type.Name + " 无无参构造，已跳过。");
                continue;
            }

            operations.Add((IMaterialAssetOperation)Activator.CreateInstance(type));
        }

        operations = operations.OrderBy(op => op.Order).ThenBy(op => op.DisplayName).ToList();
    }
}
