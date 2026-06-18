using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public static class LocalizeStringEventExtensions
{
    // 设置 int 占位符并刷新，例："制作消耗{SpConsumeCount}体力" 例：makeConsumeStaminaText.SetVar("SpConsumeCount", cost);
    public static void SetVar(this LocalizeStringEvent e, string name, int value, bool refresh = true)
        => e.StringReference.SetVar(name, value, refresh);

    public static void SetVar(this LocalizeStringEvent e, string name, float value, bool refresh = true)
        => e.StringReference.SetVar(name, value, refresh);

    public static void SetVar(this LocalizeStringEvent e, string name, string value, bool refresh = true)
        => e.StringReference.SetVar(name, value, refresh);

    public static void SetVar(this LocalizeStringEvent e, string name, bool value, bool refresh = true)
        => e.StringReference.SetVar(name, value, refresh);

    // LocalizedString 入口（无 LocalizeStringEvent 组件时直接用）
    public static void SetVar(this LocalizedString sr, string name, int value, bool refresh = true)
    {
        if(sr.TryGetValue(name, out var v) && v is IntVariable iv)
            iv.Value = value;
        else
            sr[name] = new IntVariable { Value = value };

        if(refresh)
            sr.RefreshString();
    }

    public static void SetVar(this LocalizedString sr, string name, float value, bool refresh = true)
    {
        if(sr.TryGetValue(name, out var v) && v is FloatVariable fv)
            fv.Value = value;
        else
            sr[name] = new FloatVariable { Value = value };

        if(refresh)
            sr.RefreshString();
    }

    public static void SetVar(this LocalizedString sr, string name, string value, bool refresh = true)
    {
        if(sr.TryGetValue(name, out var v) && v is StringVariable sv)
            sv.Value = value;
        else
            sr[name] = new StringVariable { Value = value };

        if(refresh)
            sr.RefreshString();
    }

    public static void SetVar(this LocalizedString sr, string name, bool value, bool refresh = true)
    {
        if(sr.TryGetValue(name, out var v) && v is BoolVariable bv)
            bv.Value = value;
        else
            sr[name] = new BoolVariable { Value = value };

        if(refresh)
            sr.RefreshString();
    }

    public static void SetText(this LocalizeStringEvent e, string table, string key)
    {
        e.StringReference.SetReference(table, key);
        e.RefreshString();
    }

    // 带 null/空 Key 保护的 SetText：扩展方法可安全作用于 null 实例，省去外部判空
    public static void SetTextSafe(this LocalizeStringEvent e, string table, string key)
    {
        if(e == null || string.IsNullOrEmpty(key))
            return;
        e.SetText(table, key);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="data"></param>
    /// <param name="refresh"></param>
    public static void SetText(this LocalizeStringEvent e, LocalSelectedData data, bool refresh = true)
    {
        e.StringReference.SetReference(data.Table, data.Value);
        if (refresh)
        {
            e.RefreshString();
        }
    }

    // 设置引用并灌入占位符后再刷新一次：避免占位符未赋值时 SmartFormat 抛 FormattingException
    public static void SetTextWithVars(this LocalizeStringEvent e, string table, string key, params (string name, object value)[] vars)
    {
        e.StringReference.SetReference(table, key);
        foreach((string name, object value) in vars)
                e.SetVar(name, value, false);
        e.RefreshString();
    }

    // object 值的 SetVar：按运行时类型分发到对应重载（int/float/double/bool/string，其余 ToString）
    public static void SetVar(this LocalizeStringEvent e, string name, object value, bool refresh = true)
    {
        switch(value)
        {
            case int i: e.SetVar(name, i, refresh); break;
            case float f: e.SetVar(name, f, refresh); break;
            case double d: e.SetVar(name, (float)d, refresh); break;
            case bool b: e.SetVar(name, b, refresh); break;
            case string s: e.SetVar(name, s, refresh); break;
            default: e.SetVar(name, value?.ToString() ?? string.Empty, refresh); break;
        }
    }
    

}
