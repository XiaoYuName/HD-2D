using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;

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
}
