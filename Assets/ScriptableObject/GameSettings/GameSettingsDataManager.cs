using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 游戏设置数据管理器 - 框架通用配置
/// </summary>
[CreateAssetMenu(fileName = "GameSettingsDataManager", menuName = "Configs/GameSettingsDataManager")]
public class GameSettingsDataManager : OdinScriptableManager<GameSettingsDataManager>
{
    [Title("初始配置")]

    [LabelText("初始场景ID")]
    public long SceneID = -1;

    [LabelText("初始背包物品列表")]
    public List<ItemInfo> StarItemBagList = new List<ItemInfo>();
}
