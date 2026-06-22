using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[System.Serializable]
public class OptionsCommand : DramaCommand
{
    [LabelText("选项列表")]
    public List<DramaOptionsData> Options = new();
    [LabelText("隐藏对话内容!")]
    public bool HideDrama;
    
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
        dramaUI.SetDialogueActive(!HideDrama);
        dramaUI.ShowOptions(Options,SelectedOptions);
       
    }

    private void SelectedOptions(DramaOptionsData selected)
    {
        dramaUI.CloseOptions();
        dramaUI.ToDrama(selected.ToDramaIndex);
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

[System.Serializable]
public class DramaOptionsData
{
    [LabelText("选项数据")]
    public LocalSelectedData LocalSelectedData = new LocalSelectedData();
    [LabelText("跳转到")]
    public int ToDramaIndex;
    
}
