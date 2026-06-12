using System;
using Febucci.TextAnimatorForUnity;
using UnityEngine;
using XFramework;

public class DramaUI : UIBase
{
    /// <summary>
    /// 打字机对象
    /// </summary>
    private TypewriterComponent typewriter;
    /// <summary>
    /// 当前播放的剧情
    /// </summary>
    private DramaData dramaData;
    /// <summary>
    /// 当前执行的剧情命令
    /// </summary>
    public DramaCommand CurrentCommand { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        
    }

    public void StartDrama(DramaData dramaData)
    {
        this.dramaData = dramaData;
        CurrentCommand = this.dramaData.Commands[0];
        CurrentCommand.Init(this);
        CurrentCommand.Enter();
    }

    public void ShowDialogue(string dialogue)
    {
        typewriter.ShowText(dialogue);
    }

    private void Update()
    {
        CurrentCommand?.Update();
    }
}
