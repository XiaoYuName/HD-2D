using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "ClawMachineGuideUI", menuName = "Configs/MinGame/ClawMachineGuideUI")]
public class ClawMachineSettingData : OdinScriptableManager<ClawMachineSettingData>
{
    [Title("基本设置")]
    
    [LabelText("单次对局时间")]
    public float minGameTimer;
    [LabelText("每次随机生成娃娃数量")]
    public int DollRandomNumber;
    [LabelText("每天重置次数")]
    public int DayResetLimit = 5;

}
