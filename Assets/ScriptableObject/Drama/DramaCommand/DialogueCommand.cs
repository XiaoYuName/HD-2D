using Sirenix.OdinInspector;
using UnityEngine;

public class DialogueCommand : DramaCommand
{
    [LabelText("是否系统说话人")]
    public bool isSystemName;
    [LabelText("说话人"),HideIf("isSystemName")]
    public string dialogueName;
    [LabelText("对话内容"),TextArea]
    public string dialogueText;

    private DramaUI _dramaUI;

    public override void Init(DramaUI dramaUI)
    {
        this._dramaUI = dramaUI;
    }

    /// <summary>
    /// 进入对话
    /// </summary>
    public override void Enter()
    {
        _dramaUI.ShowDialogue(dialogueText);
    }

    /// <summary>
    /// 退出对话
    /// </summary>
    public override void Exit()
    {
        
    }

    /// <summary>
    /// 对话持续中
    /// </summary>
    public override void Update()
    {
        
    }
}
