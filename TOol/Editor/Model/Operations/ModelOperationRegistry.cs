using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ModelOperationRegistry
{
    private static List<IModelAssetOperation> operations;

    public static IList<IModelAssetOperation> All
    {
        get
        {
            EnsureDiscovered();
            return operations;
        }
    }

    public static IModelAssetOperation FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        EnsureDiscovered();
        foreach (IModelAssetOperation operation in operations)
        {
            if (operation.Id == id)
            {
                return operation;
            }
        }

        return null;
    }

    public static List<IModelAssetOperation> GetImportAutoOperations(ModelProcessSettings settings)
    {
        var result = new List<IModelAssetOperation>();
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

            IModelAssetOperation operation = FindById(id);
            if (operation == null)
            {
                Debug.LogWarning("[ModelOperationRegistry] 配置里的导入自动操作 Id 找不到实现: " + id);
                continue;
            }

            if (!result.Contains(operation))
            {
                result.Add(operation);
            }
        }

        return result.OrderBy(operation => operation.Order).ToList();
    }

    private static void EnsureDiscovered()
    {
        if (operations != null)
        {
            return;
        }

        operations = new List<IModelAssetOperation>();
        Type interfaceType = typeof(IModelAssetOperation);
        foreach (Type type in interfaceType.Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !interfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                Debug.LogWarning("[ModelOperationRegistry] " + type.Name + " 没有无参构造函数，已跳过。");
                continue;
            }

            operations.Add((IModelAssetOperation)Activator.CreateInstance(type));
        }

        operations = operations.OrderBy(operation => operation.Order).ThenBy(operation => operation.DisplayName).ToList();
    }
}
