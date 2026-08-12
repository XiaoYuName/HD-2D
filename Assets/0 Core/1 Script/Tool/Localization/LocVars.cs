using System.Collections.Generic;

// 取一条多语言文本时要塞进去的占位符集合。
// 独立成一个类型，业务侧（配表数据、UI）就不用认识插件的 LocalizedString —— 换插件时只有 LocKeyRef 要改。
public class LocVars
{
    readonly Dictionary<string, object> values = new();

    public IReadOnlyDictionary<string, object> Values => values;

    // 链式写法，一行塞完几个占位符：vars.Set("ItemName", name).Set("Value", 3)
    public LocVars Set(string name, object value)
    {
        values[name] = value;
        return this;
    }
}
