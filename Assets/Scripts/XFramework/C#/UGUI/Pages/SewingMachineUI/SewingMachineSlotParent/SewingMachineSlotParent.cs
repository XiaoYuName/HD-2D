using Sirenix.OdinInspector;
using UnityEngine.UI;
using XFramework;

public partial class SewingMachineSlotParent : UIBase
{
    [LabelText("类型")]
    public ParentType ParentType;
    
    private Image image;
    public override void Init()
    {
        InitAutoBind();
        image = GetComponent<Image>();
    }
    
}

public enum ParentType
{
    [LabelText("布料1")]
    Clot_01,
    [LabelText("布料2")]
    Clot_02,
    [LabelText("布料3")]
    Clot_03,
    [LabelText("布料4")]
    Clot_04,
    [LabelText("布料5")]
    Clot_05,
    [LabelText("布料6")]
    Clot_06,
    [LabelText("布料7")]
    Clot_07,
    
}
