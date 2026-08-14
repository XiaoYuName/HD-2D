using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 线上玩法配置数据 - 可选系统
/// </summary>
[CreateAssetMenu(fileName = "OnLineGameData", menuName = "Configs/OnLineGameData")]
public class OnLineGameData : OdinScriptableManager<OnLineGameData>
{
    [Title("线上玩法配置")]
    [LabelText("是否启用线上玩法")]
    public bool EnableOnlineGame = false;
}
