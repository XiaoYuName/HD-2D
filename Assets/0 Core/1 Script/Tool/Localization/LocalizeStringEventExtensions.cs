using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

/// <summary>
/// LocalizeStringEvent 的占位符变量扩展：一行代码设置并刷新 Smart String 中的占位符。
/// 例：makeConsumeStaminaText.SetVar("SpConsumeCount", cost);
/// </summary>
public static class LocalizeStringEventExtensions
{
    // 设置 int 占位符并刷新，例："制作消耗{SpConsumeCount}体力"
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
    
    public static void SetLocalizedString(this LocalizeStringEvent sr,string table,string key)
    {
        sr.SetTable(table);
        sr.SetEntry(key);
    }
}
