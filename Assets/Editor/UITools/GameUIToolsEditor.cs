using Drama.Runtime;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;
using PropertyType = XFramework.PropertyType;

public class GameUIToolsEditor : OdinEditorWindow
{
    // 剧本资产由配置表 DramaData（ID → DramaScriptsPath）定位，
    // 所以这里填的是剧情ID 而不是直接拖资产
    [TitleGroup("剧情"),LabelText("剧情ID")]
    public long DramaID;

    [Button("播放剧情")]
    public void PlayerDramaScript()
    {
        DramaManager.Instance.StartDramaRuntime(DramaID);
    }

    [Title("引导"),LabelText("引导ID")]
    public long TutorialID;
    
    [Button("开始引导")]
    public void StartTutorial()
    {
        TutorialManager.Instance.StartTutorial(TutorialID);
    }

    [Button("开始抓娃娃机")]
    public void StarDoll()
    {
        CharacterManager.Instance.Execute(FunctionGroup.ClawMachine,null);
    }
}
