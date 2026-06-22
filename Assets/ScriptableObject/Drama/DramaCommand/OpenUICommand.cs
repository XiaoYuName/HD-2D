using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[System.Serializable]
public class OpenUICommand : DramaCommand
{
    [LabelText("弹出的UI_ItemID")]
    public string PopID;

    private DramaUI dramaUI;
    
    public override void Init(DramaUI dramaUI)
    {
        this.dramaUI = dramaUI;
    }

    
    /// <summary>
    /// 进入对话
    /// </summary>
    public override void Enter()
    {
        UISystem.Instance.OpenUI(PopID);
        UISystem.Instance.CloseUI("DramaUI");
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
