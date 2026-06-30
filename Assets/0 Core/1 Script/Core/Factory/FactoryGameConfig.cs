using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 工厂加工（传送带下压）小游戏配置：单局时长、传送带节奏、良品率、下压判定区间、积分与奖励、消耗。
/// 通过菜单 MiniGame/FactoryGameConfig 创建资产，挂到 <see cref="FactoryProcessGameManager"/> 上。
/// 备注：传送带速度、良品率、单件奖励等理论上应由「流水线生产力 + 装备模具属性」推导（见策划案 2.2 / 3.2），
/// 设备与模具系统尚未实现，本配置先以固定值驱动，后续接入养成数值后由生产力覆盖。
/// </summary>
[CreateAssetMenu(fileName = "FactoryGameConfig", menuName = "MiniGame/FactoryGameConfig")]
public class FactoryGameConfig : ScriptableObject
{
    [Title("时长 / 节奏")]
    [LabelText("单局时长(秒)"), MinValue(1f)][SerializeField] float duration = 30f;
    [LabelText("传送带速度(归一化/秒)"), MinValue(0.01f)][SerializeField] float beltSpeed = 0.3f;
    [LabelText("出货间隔(秒)"), MinValue(0.1f)][SerializeField] float spawnInterval = 0.9f;

    [Title("产品品质")]
    [LabelText("合格品概率(良品率)"), Range(0f, 1f)][SerializeField] float qualifiedRate = 0.7f;

    [Title("基础养成数值（设备升级在此基础上叠加，见升级设备系统）")]
    [LabelText("基本生产量"), MinValue(0)][SerializeField] int baseProductionVolume = 50;
    [LabelText("基础良品率(%)"), Range(0, 100)][SerializeField] int baseYieldRate = 50;

    [Title("下压判定区(归一化X，0=入口 1=出口)")]
    [LabelText("下压区中心"), Range(0f, 1f)][SerializeField] float pressCenter = 0.62f;
    [LabelText("下压区半宽(OK 区)"), Range(0.01f, 0.5f)][SerializeField] float pressHalfWidth = 0.08f;
    [LabelText("完美区半宽(GOOD 区)"), Range(0.005f, 0.5f)][SerializeField] float goodHalfWidth = 0.035f;

    [Title("积分 / 奖励")]
    [LabelText("OK 得分"), MinValue(0)][SerializeField] int okScore = 60;
    [LabelText("GOOD 得分"), MinValue(0)][SerializeField] int goodScore = 100;
    [LabelText("每件成功奖励金币"), MinValue(0)][SerializeField] int rewardPerSuccess = 50;

    [Title("消耗")]
    [LabelText("开局 / 再来一局消耗体力"), MinValue(0)][SerializeField] int startSpCost = 30;

    [Title("评价图标（下压判定飘字改用图标，碾压机旁弹出）")]
    [LabelText("评价图标列表 [0]=合格品Good [1]=次品Bad")][SerializeField] List<Sprite> evalIcons = new ();

    [Title("打包盒预制（产品压制后变为打包盒，正品 / 次品为两种物品）")]
    [LabelText("正品打包盒预制")][SerializeField] GameObject qualifiedBoxPrefab;
    [LabelText("次品打包盒预制")][SerializeField] GameObject defectiveBoxPrefab;

    #region Get
    public float Duration => Mathf.Max(1f, duration);
    public float BeltSpeed => Mathf.Max(0.01f, beltSpeed);
    public float SpawnInterval => Mathf.Max(0.1f, spawnInterval);
    public float QualifiedRate => Mathf.Clamp01(qualifiedRate);
    /// <summary>基本生产量（设备「生产量」加成在此基础上叠加）。</summary>
    public int BaseProductionVolume => Mathf.Max(0, baseProductionVolume);
    /// <summary>基础良品率（百分比，设备「良品率」加成在此基础上叠加）。</summary>
    public int BaseYieldRate => Mathf.Clamp(baseYieldRate, 0, 100);
    public float PressCenter => Mathf.Clamp01(pressCenter);
    public float PressHalfWidth => Mathf.Max(0.01f, pressHalfWidth);
    public float GoodHalfWidth => Mathf.Min(PressHalfWidth, Mathf.Max(0.005f, goodHalfWidth));
    public int OkScore => Mathf.Max(0, okScore);
    public int GoodScore => Mathf.Max(0, goodScore);
    public int RewardPerSuccess => Mathf.Max(0, rewardPerSuccess);
    public int StartSpCost => Mathf.Max(0, startSpCost);

    /// <summary>合格品（Good）评价图标，未配置返回 null。</summary>
    public Sprite QualifiedEvalIcon => evalIcons.Count > 0 ? evalIcons[0] : null;
    /// <summary>次品（Bad）评价图标，未配置返回 null。</summary>
    public Sprite DefectiveEvalIcon => evalIcons.Count > 1 ? evalIcons[1] : null;

    /// <summary>正品打包盒预制（合格品压制后变为此盒）。</summary>
    public GameObject QualifiedBoxPrefab => qualifiedBoxPrefab;
    /// <summary>次品打包盒预制（次品压制后变为此盒）。</summary>
    public GameObject DefectiveBoxPrefab => defectiveBoxPrefab;
    #endregion

    /// <summary>按良品率随机一件产品是否合格。</summary>
    public bool RollQualified() => UnityEngine.Random.value < QualifiedRate;
}
