using UnityEngine;
using UnityEngine.UI;
using XFramework;

public class SetDatingTargetUI : UIBase
{
    private CustomButton CloseButton;
    private CustomButton StartButton;
    private RawImage backgroundImage;
    private OptionUI optionUI;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        CloseButton = Get<CustomButton>("UIMask/Background/CloseButton");
        StartButton = Get<CustomButton>("UIMask/Background/StartButton");
        backgroundImage = Get<RawImage>("UIMask/Background/SpriteFarme/Raw");
        optionUI = Get<OptionUI>("UIMask/Background/SceneFarme/OptionUI");
    }
}
