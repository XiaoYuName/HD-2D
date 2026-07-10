using System.Collections.Generic;

/// <summary>音游按键类型：音符序列的按键值，供 <see cref="FactoryProcessGamePanel"/> 判定。
/// 0=无(空拍) 1=上(残次品，↑/鼠标中键丢弃) 2=左(左品，←/鼠标左键) 3=右(右品，→/鼠标右键)。</summary>
public enum FactoryNoteType
{
    None,
    Up,
    Left,
    Right,
}

/// <summary>单条流水线的按键序列。Unity 无法直接序列化 List&lt;List&lt;T&gt;&gt;，故用此包装类多套一层，
/// 供 <see cref="FactoryGameConfig"/> 的 assemblyLineNoteSequences 使用。</summary>
[System.Serializable]
public class FactoryNoteRow
{
    public List<FactoryNoteType> notes = new();
}

/// <summary>打包判定评价，同时是 <see cref="FactoryGameConfig.EvalIcons"/> 的下标：0=PERFECT 1=GOOD 2=MISS。</summary>
public enum FactoryEvaluateType
{
    Perfect,
    Good,
    Miss,
}
