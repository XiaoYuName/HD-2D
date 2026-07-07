using System;

// 标记 LocKeyRef.Value 字段：Inspector 里画成「文本框 + 选择按钮」，按钮弹 LocKeySelectorWindow 搜索 Key。
[AttributeUsage(AttributeTargets.Field)]
public sealed class LocKeySelectorAttribute : Attribute
{
}
