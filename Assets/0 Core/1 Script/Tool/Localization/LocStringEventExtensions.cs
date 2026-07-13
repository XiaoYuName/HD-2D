using System;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using XFramework;

public static class LocStringEventExtensions
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

    public static void ClearTextEvent(this LocalizeStringEvent e)
    {
        e.OnUpdateString.RemoveAllListeners();
    }

    public static void SetTextMeshProUGUI(this LocalizeStringEvent e, string value)
    {
        if (e.TryGetComponent<TextMeshProUGUI>(out var text))
        {
            text.text = value;
        }
    }

    // 带 null/空 Key 保护的 SetText：扩展方法可安全作用于 null 实例，省去外部判空
    public static void SetTextSafe(this LocalizeStringEvent e, string table, string key)
    {
        if(e == null || string.IsNullOrEmpty(key))
            return;
        e.SetText(table, key);
    }

    // 先灌占位符再切换引用，最后刷新一次：避免占位符未赋值时 SmartFormat 抛 FormattingException。
    // 注意顺序——SetReference 会立即按「当前占位符」格式化一次（见 LocalizedString.StringChanged 的触发条件），
    // 若此时 {占位符} 尚未赋值（如首次展示失败结算、面板上无历史变量）就会抛异常，所以必须先把变量灌好。
    public static void SetTextWithVars(this LocalizeStringEvent e, string table, string key, params (string name, object value)[] vars)
    {
        foreach((string name, object value) in vars)
            e.SetVar(name, value, false);
        e.StringReference.SetReference(table, key);
        e.RefreshString();
    }

    // 单占位符版的 SetTextWithVars：先灌变量再切引用，最后刷新一次（顺序原因同上）。
    public static void SetTextWithVar(this LocalizeStringEvent e, string table, string key, string name, object value)
    {
        e.SetVar(name, value, false);
        e.StringReference.SetReference(table, key);
        e.RefreshString();
    }

    // 把 LocalizedString 绑定到 TMP 文本（不挂 LocalizeStringEvent 组件，绑定关系留在 C# 里）：
    // 内部订阅 StringChanged，语言切换 / 占位符变化时自动写回 target.text。
    // 返回退订委托，须在 OnDisable/OnDestroy 调用一次，否则回调长期持有引用导致泄漏 / 空引用。
    // 例：unbind = betRange.BindToText(rangeText);  ...  OnDisable: unbind?.Invoke();
    public static Action BindToText(this LocalizedString source, TMP_Text target)
    {
        void Handler(string v) => target.text = v;
        source.StringChanged += Handler;
        return () => source.StringChanged -= Handler;
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

    // object 值的 SetVar（LocalizedString 直接入口，无 LocalizeStringEvent 组件时用）：按运行时类型分发到对应重载
    public static void SetVar(this LocalizedString sr, string name, object value, bool refresh = true)
    {
        switch(value)
        {
            case int i: sr.SetVar(name, i, refresh); break;
            case float f: sr.SetVar(name, f, refresh); break;
            case double d: sr.SetVar(name, (float)d, refresh); break;
            case bool b: sr.SetVar(name, b, refresh); break;
            case string s: sr.SetVar(name, s, refresh); break;
            default: sr.SetVar(name, value?.ToString() ?? string.Empty, refresh); break;
        }
    }

    // 参数保留 LocalSelectedData（而非本文件夹自维护的 LocKeyRef）：
    // CustomDropdownUI.cs（他人脚本）在用这个重载，为了不改动他人脚本而保留兼容。
    public static void SetText(this LocalizeStringEvent e, LocalSelectedData data, bool refresh = true)
    {
        e.StringReference.SetReference(data.Table, data.Value);
        if (refresh)
        {
            e.RefreshString();
        }
    }

    public static void SetText(this LocalizeStringEvent e, TbLocalzationKeyData data, bool refresh = true)
    {
        e.StringReference.SetReference(data.Table, data.Value);
        if (refresh)
        {
            e.RefreshString();
        }
    }
}
