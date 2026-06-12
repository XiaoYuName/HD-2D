using Sirenix.OdinInspector;
using UnityEngine;


/// <summary>
/// 剧情演出的命令基类,包含了演出的抽象方法
/// </summary>
[System.Serializable]
public abstract class DramaCommand
{
    [LabelText("命令ID")]
    public string Command_ID;
    [LabelText("跳转到目标的命令ID")]
    public string ToCommandID;

    public abstract void Init(DramaUI dramaUI);
    
    /// <summary>
    /// 进入对话
    /// </summary>
    public abstract void Enter();
    
    /// <summary>
    /// 退出对话
    /// </summary>
    public abstract void Exit();

    /// <summary>
    /// 对话持续中
    /// </summary>
    public abstract void Update();
}
