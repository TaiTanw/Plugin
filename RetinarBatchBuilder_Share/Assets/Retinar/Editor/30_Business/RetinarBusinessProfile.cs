using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// 一份业务一份 SO。总面板以后拖入全部 Profile，并选择当前业务。
// 本期不接线：创建资产不会改变平铺 / 导出行为。
// =====================================================================================

/// <summary>业务档案：启用哪些门禁、哪些输出。</summary>
[CreateAssetMenu(menuName = "Retinar/Business Profile", fileName = "RetinarBusinessProfile")]
public sealed class RetinarBusinessProfile : ScriptableObject
{
    [Tooltip("冻结业务键，例如 plane_ar / interactive_prefab。")]
    public string businessId;

    public string displayName;

    [Tooltip("启用的门禁 Id，须与 IRetinarAcceptanceGate.Id / RetinarGateIds 一致。本期导出不读此列表。")]
    public List<string> enabledGateIds = new List<string>();

    [Tooltip("启用的输出 Id，须与 IRetinarDeliverableOutput.Id / RetinarDeliverableIds 一致。本期导出不读此列表。")]
    public List<string> enabledDeliverableIds = new List<string>();
}
