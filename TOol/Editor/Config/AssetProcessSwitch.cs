using UnityEditor;

// =====================================================================================
// 职责边界：
//   这个类只负责一件事——回答"当前这台机器要不要让导入回调介入"。
//   它不做任何贴图/模型处理，只是一个被所有 AssetPostprocessor 读取的总闸。
//
// 为什么要单独建一个类，而不是继续用 SwitchManagerWindow 上的 static 字段：
//   原来的 public static bool switchValue 是一个普通静态字段，没有任何持久化。
//   Unity 在下面这些情况会重载程序集（Domain Reload），静态字段会被重置成 false：
//     - 任何一次脚本重新编译
//     - 进入 / 退出 Play Mode
//     - 手动 Reimport 部分资产触发的编译
//   结果就是：用户在窗口里明明勾上了开关，编辑器悄悄重载一次之后开关变回关闭，
//   后续导入完全不被处理，而且没有任何提示——这种"静默失效"极难自己发现。
//   改成 EditorPrefs 持久化之后，重载、重启编辑器都不会丢状态。
//
// 为什么放 EditorPrefs 而不是放进 TextureProcessSettings 资产：
//   这是"我这台机器现在的工作模式"，不是团队约定。如果跟着资产提交进版本库，
//   一个人临时关掉开关会连带影响所有人的导入行为。阈值这类团队规则才放资产里。
// =====================================================================================
public static class AssetProcessSwitch
{
    // EditorPrefs 是全编辑器共享的，key 必须带上足够的前缀，避免和别的插件撞名。
    private const string EnabledKey = "TOol.AssetProcessSwitch.Enabled";

    public const string DisplayName = "资源处理开关";

    // EditorPrefs 每次读取都要走一次进程外存储，而导入回调可能一批处理上千个资产，
    // 所以这里缓存一份。null 表示"还没从 EditorPrefs 读过"。
    private static bool? cachedEnabled;

    public static bool IsEnabled
    {
        get
        {
            if (!cachedEnabled.HasValue)
            {
                cachedEnabled = EditorPrefs.GetBool(EnabledKey, false);
            }

            return cachedEnabled.Value;
        }
        set
        {
            if (cachedEnabled.HasValue && cachedEnabled.Value == value)
            {
                return;
            }

            cachedEnabled = value;
            EditorPrefs.SetBool(EnabledKey, value);
        }
    }
}
