using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// 动画 m_PPtrCurves（材质换槽等）不会走 Prefab remap。
// 必须以 SerializedObject 改原曲线（YAML classID 23 = MeshRenderer）。
// 禁止用错误的绑定类型调用 SetObjectReferenceCurve：会另写 classID 2 重复曲线，
// 运行时仍走 23，源 GUID 不变。
// =====================================================================================

/// <summary>平铺时把 AnimationClip 对象曲线改到本包材质。</summary>
public static class FlattenAnimationClipRemapper
{
    public static void CopyAndRemapPrefabClips(string prefabPath, string assetFolder, string assetName)
    {
        if (string.IsNullOrEmpty(prefabPath) || string.IsNullOrEmpty(assetFolder))
        {
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform searchRoot = instance.transform;
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                searchRoot = animator.transform;
            }

            int remapped = 0;
            int removedDupes = 0;
            var leftover = new List<string>();
            var clipPaths = new List<string>();
            CollectClipPaths(animator, assetFolder, clipPaths);
            CollectClipPathsInFolder(FlattenLayout.AnimationFolder(assetFolder), clipPaths);

            for (int i = 0; i < clipPaths.Count; i++)
            {
                AnimationClip[] clips = LoadClipsAt(clipPaths[i]);
                for (int c = 0; c < clips.Length; c++)
                {
                    removedDupes += RemoveDuplicatePPtrCurves(clips[c]);
                    remapped += RemapClipInPlace(clips[c], searchRoot, assetFolder, leftover);
                }
            }

            if (removedDupes > 0)
            {
                Debug.Log("[Retinar] " + assetName + "：已删除 " + removedDupes +
                    " 条重复的动画对象曲线（错误绑定类型）");
            }

            if (remapped > 0)
            {
                Debug.Log("[Retinar] " + assetName + "：已重绑 " + remapped +
                    " 个动画对象关键帧到本包副本");
            }

            if (leftover.Count > 0)
            {
                Debug.LogError("[Retinar] " + assetName +
                    "：动画曲线仍有 Missing（播放会把材质打成紫色）：\n" +
                    string.Join("\n", leftover.ToArray()));
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }

    private static void CollectClipPaths(Animator animator, string assetFolder, List<string> clipPaths)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
        {
            return;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AddClipPath(clips[i], assetFolder, clipPaths);
        }
    }

    private static void CollectClipPathsInFolder(string animationFolder, List<string> clipPaths)
    {
        if (string.IsNullOrEmpty(animationFolder) || !AssetDatabase.IsValidFolder(animationFolder))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { animationFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace("\\", "/");
            if (!clipPaths.Contains(path) &&
                Path.GetExtension(path).ToLowerInvariant() == ".anim")
            {
                clipPaths.Add(path);
            }
        }
    }

    private static void AddClipPath(AnimationClip clip, string assetFolder, List<string> clipPaths)
    {
        if (clip == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(clip).Replace("\\", "/");
        if (string.IsNullOrEmpty(path) ||
            !path.StartsWith(assetFolder + "/", System.StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).ToLowerInvariant() != ".anim")
        {
            return;
        }

        if (!clipPaths.Contains(path))
        {
            clipPaths.Add(path);
        }
    }

    private static AnimationClip[] LoadClipsAt(string clipPath)
    {
        var result = new List<AnimationClip>();
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(clip);
            }
        }

        return result.ToArray();
    }

    private static int RemoveDuplicatePPtrCurves(AnimationClip clip)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty curves = serializedClip.FindProperty("m_PPtrCurves");
        if (curves == null || !curves.isArray || curves.arraySize < 2)
        {
            return 0;
        }

        int removed = 0;
        for (int i = curves.arraySize - 1; i >= 1; i--)
        {
            SerializedProperty later = curves.GetArrayElementAtIndex(i);
            string path = later.FindPropertyRelative("path").stringValue;
            string attribute = later.FindPropertyRelative("attribute").stringValue;
            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                SerializedProperty earlier = curves.GetArrayElementAtIndex(j);
                if (earlier.FindPropertyRelative("path").stringValue == path &&
                    earlier.FindPropertyRelative("attribute").stringValue == attribute)
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
            {
                curves.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        if (removed > 0)
        {
            serializedClip.ApplyModifiedProperties();
            EditorUtility.SetDirty(clip);
        }

        return removed;
    }

    private static int RemapClipInPlace(
        AnimationClip clip,
        Transform searchRoot,
        string assetFolder,
        List<string> leftover)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty curves = serializedClip.FindProperty("m_PPtrCurves");
        if (curves == null || !curves.isArray || curves.arraySize == 0)
        {
            return 0;
        }

        int remapped = 0;
        bool serializedChanged = false;
        var runtimeWrites = new List<RuntimeWrite>();

        for (int i = 0; i < curves.arraySize; i++)
        {
            SerializedProperty curveProp = curves.GetArrayElementAtIndex(i);
            string path = curveProp.FindPropertyRelative("path").stringValue;
            string attribute = curveProp.FindPropertyRelative("attribute").stringValue;
            SerializedProperty keyframes = curveProp.FindPropertyRelative("curve");
            if (keyframes == null || !keyframes.isArray)
            {
                continue;
            }

            System.Type bindingType = ResolveBindingType(attribute, path, searchRoot);
            var keys = new ObjectReferenceKeyframe[keyframes.arraySize];
            bool curveChanged = false;
            bool hasMissing = false;

            for (int k = 0; k < keyframes.arraySize; k++)
            {
                SerializedProperty keyProp = keyframes.GetArrayElementAtIndex(k);
                keys[k].time = keyProp.FindPropertyRelative("time").floatValue;
                Object current = keyProp.FindPropertyRelative("value").objectReferenceValue;
                Object replacement = ResolveCurveValue(current, path, attribute, bindingType, searchRoot, assetFolder);
                if (replacement != null)
                {
                    keys[k].value = replacement;
                    if (replacement != current)
                    {
                        keyProp.FindPropertyRelative("value").objectReferenceValue = replacement;
                        curveChanged = true;
                        serializedChanged = true;
                        remapped++;
                    }
                }
                else
                {
                    keys[k].value = current;
                    if (current == null)
                    {
                        hasMissing = true;
                    }
                }
            }

            if (curveChanged)
            {
                runtimeWrites.Add(new RuntimeWrite
                {
                    Path = path,
                    Attribute = attribute,
                    BindingType = bindingType,
                    Keys = keys
                });
            }

            if (hasMissing)
            {
                leftover.Add("  clip=" + clip.name + " path=" + path + " prop=" + attribute);
            }
        }

        if (serializedChanged)
        {
            serializedClip.ApplyModifiedProperties();
            EditorUtility.SetDirty(clip);
        }

        for (int i = 0; i < runtimeWrites.Count; i++)
        {
            RuntimeWrite write = runtimeWrites[i];
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                write.Path, write.BindingType, write.Attribute);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, write.Keys);
            EditorUtility.SetDirty(clip);
        }

        return remapped;
    }

    private static System.Type ResolveBindingType(string attribute, string path, Transform searchRoot)
    {
        if (!string.IsNullOrEmpty(attribute) &&
            attribute.StartsWith("m_Materials.Array.data[", System.StringComparison.Ordinal))
        {
            Transform target = FindByAnimPath(searchRoot, path);
            if (target != null)
            {
                if (target.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    return typeof(SkinnedMeshRenderer);
                }

                if (target.GetComponent<SpriteRenderer>() != null)
                {
                    return typeof(SpriteRenderer);
                }
            }

            return typeof(MeshRenderer);
        }

        return typeof(MeshRenderer);
    }

    private static Object ResolveCurveValue(
        Object current,
        string path,
        string attribute,
        System.Type bindingType,
        Transform searchRoot,
        string assetFolder)
    {
        if (current != null)
        {
            Object copied = CopyIntoPackIfExternal(current, assetFolder);
            if (copied != null)
            {
                return copied;
            }
        }

        return FindFallbackOnPrefab(path, attribute, bindingType, searchRoot);
    }

    private static Object CopyIntoPackIfExternal(Object source, string assetFolder)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source).Replace("\\", "/");
        if (string.IsNullOrEmpty(sourcePath))
        {
            return source;
        }

        if (sourcePath.StartsWith(assetFolder + "/", System.StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        string relative = FlattenCopyRunner.ResolveRelativeFolder(sourcePath);
        if (string.IsNullOrEmpty(relative))
        {
            return source;
        }

        string destFolder = assetFolder + "/" + relative;
        FlattenLayout.EnsureFolder(destFolder);
        string destPath = destFolder + "/" + Path.GetFileName(sourcePath);
        if (AssetDatabase.LoadMainAssetAtPath(destPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                Debug.LogWarning("[Retinar] 动画曲线引用拷贝失败: " + sourcePath + " -> " + destPath);
                return source;
            }
        }

        Object match = LoadMatchingCopy(source, destPath);
        return match != null ? match : source;
    }

    private static Object LoadMatchingCopy(Object source, string destPath)
    {
        Object typed = AssetDatabase.LoadAssetAtPath(destPath, source.GetType());
        if (typed != null)
        {
            return typed;
        }

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(destPath);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetType() == source.GetType() && all[i].name == source.name)
            {
                return all[i];
            }
        }

        return AssetDatabase.LoadMainAssetAtPath(destPath);
    }

    private static Object FindFallbackOnPrefab(
        string path,
        string attribute,
        System.Type bindingType,
        Transform searchRoot)
    {
        Transform target = FindByAnimPath(searchRoot, path);
        if (target == null)
        {
            return null;
        }

        Renderer renderer = null;
        if (bindingType != null && typeof(Renderer).IsAssignableFrom(bindingType))
        {
            renderer = target.GetComponent(bindingType) as Renderer;
        }

        if (renderer == null)
        {
            renderer = target.GetComponent<Renderer>();
        }

        int slot = ParseMaterialArrayIndex(attribute);
        if (renderer != null && slot >= 0)
        {
            Material[] materials = renderer.sharedMaterials;
            if (slot < materials.Length)
            {
                return materials[slot];
            }
        }

        return null;
    }

    private static Transform FindByAnimPath(Transform root, string path)
    {
        if (root == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(path))
        {
            return root;
        }

        Transform current = root;
        string[] parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            Transform child = FindDirectChild(current, parts[i]);
            if (child == null)
            {
                return null;
            }

            current = child;
        }

        return current;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static int ParseMaterialArrayIndex(string propertyName)
    {
        const string prefix = "m_Materials.Array.data[";
        if (string.IsNullOrEmpty(propertyName) ||
            !propertyName.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            return -1;
        }

        int close = propertyName.IndexOf(']', prefix.Length);
        if (close <= prefix.Length)
        {
            return -1;
        }

        int index;
        if (int.TryParse(propertyName.Substring(prefix.Length, close - prefix.Length), out index))
        {
            return index;
        }

        return -1;
    }

    private struct RuntimeWrite
    {
        public string Path;
        public string Attribute;
        public System.Type BindingType;
        public ObjectReferenceKeyframe[] Keys;
    }
}
