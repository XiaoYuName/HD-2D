using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class DialogueCommand : DramaCommand
{
    [LabelText("是否系统说话人")]
    public bool isSystemName;
    [LabelText("说话方向"),HideIf("isSystemName")]
    public DialogueDirection DialogueDirection;
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
        if (isSystemName)
        {
            _dramaUI.ShowDialogue(dialogueText);
        }
        else
        {
            _dramaUI.ShowDialogue(dialogueName,DialogueDirection,dialogueText);
        }
    }

    /// <summary>
    /// 退出对话
    /// </summary>
    public override void Exit()
    {
        _dramaUI.SkipDialogue();
    }

    /// <summary>
    /// 对话持续中
    /// </summary>
    public override void Update()
    {
        
    }
}
