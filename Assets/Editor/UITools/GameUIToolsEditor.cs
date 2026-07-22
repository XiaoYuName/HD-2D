using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;
using PropertyType = XFramework.PropertyType;

public class GameUIToolsEditor : OdinEditorWindow
{
    [MenuItem("GameTools/UI Tools Editor")]
    public static void ShowWindow()
    {
        GetWindow<GameUIToolsEditor>().Show();
    }

    [TitleGroup("娃娃机")]
    
    [LabelText("娃娃机启动"),Button("打开娃娃机界面")]
    public void OpenClawMachineUI()
    {
        UISystem.Instance.OpenUI<PopClawMachineTipUI>("PopClawMachineTipUI");
    }

    [Button("测试播放")]
    public void PlayAudio()
    {
        AudioManager.Instance.PlayAudio("FactorySuccessSound");
    }

    [TitleGroup("线上玩法")]
    [Button("线上玩法")]
    public void OpenOnLineUI()
    {
        UISystem.Instance.OpenUI<OnLineGameUI>("OnLineGameUI");
    }

    [TitleGroup("线上玩法")]
    [Button("进入展会流程")]
    public void StartExhibition()
    {
        ExhibitionManager.Instance.StartPrepareExhibition();
    }

    [TitleGroup("行动")]
    [Button("扣除行动值")]
    public void RemoveActionPointsValue()
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,1);
    }

    [TitleGroup("服装制作")]
    [Button("打开服装制作流程")]
    public void GarmentMaking()
    {
        UISystem.Instance.OpenUIAsync<GarmentMakingCommonUI>("GarmentMakingCommonUI");
    }
}
