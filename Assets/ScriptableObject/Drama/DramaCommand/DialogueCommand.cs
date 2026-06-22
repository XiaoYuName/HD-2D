using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class DialogueCommand : DramaCommand
{
    [FoldoutGroup("说话人"),LabelText("是否系统说话人")]
    public bool isSystemName;
    [FoldoutGroup("说话人"),LabelText("说话方向"),HideIf("isSystemName")]
    public DialogueDirection DialogueDirection;
    [FoldoutGroup("说话人"),LabelText("说话人"),HideIf("isSystemName")]
    public LocalSelectedData dialogueName = new LocalSelectedData();
    [FoldoutGroup("对话内容"),LabelText("对话内容")]
    public LocalSelectedData dialogueText = new LocalSelectedData();
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
        DramaManager.Instance.AddPlayerLogCommand(this);
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
