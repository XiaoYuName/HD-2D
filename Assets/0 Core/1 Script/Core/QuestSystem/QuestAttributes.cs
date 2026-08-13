using System;

namespace XFramework
{
    /// <summary>字段在任务编辑器里显示的名字。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class QuestLabelAttribute : Attribute
    {
        public QuestLabelAttribute(string text) => Text = text;

        public string Text { get; }
    }

    /// <summary>
    /// 字段不进任务编辑器的详情面板（Odin Inspector 里照常显示）。
    /// 给 Id 这类由字典 Key 回填的字段用 —— 表格那一列已经是 Key，详情里再画一遍是重复。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class QuestHiddenAttribute : Attribute { }

    /// <summary>这个 ID 字段引用的是什么，编辑器据此给出可搜索的下拉。</summary>
    public enum QuestRefKind
    {
        Item,
        Npc,
        Dialogue,
        MapScene,
        Scene,
        Quest,
        Obj,
        Cond,
    }

    /// <summary>标在 long 或 List&lt;long&gt; 字段上：编辑器把它画成「ID | 备注」选择器，存的仍是数值。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class QuestRefAttribute : Attribute
    {
        public QuestRefAttribute(QuestRefKind kind) => Kind = kind;

        public QuestRefKind Kind { get; }
    }

    /// <summary>目标/触发/奖励实现类的一句中文说明，编辑器的类型下拉里跟在类名后面。</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QuestTypeInfoAttribute : Attribute
    {
        public QuestTypeInfoAttribute(string text) => Text = text;

        public string Text { get; }
    }

    /// <summary>数值字段的下限，编辑器输入时钳住。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class QuestMinAttribute : Attribute
    {
        public QuestMinAttribute(int value) => Value = value;

        public int Value { get; }
    }
}
