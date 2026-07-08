using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine;
using System;
// 多语言引用：把一张表的一条 Key 绑到 TMP 文本，
// 语言切换 / 占位符变化时自动写回。不依赖任何扩展方法——设引用、占位符 get-or-create、订阅退订都在类内实现。
// 订阅用命名方法 OnStringChanged（无 lambda、无返回退订委托）；Unbind 按同一方法组退订，防回调长期持有引用泄漏。
[Serializable]
public class LocRef
{
    [SerializeField] LocalizedString localized;
    [SerializeField] TMP_Text text;
    bool bound;

    LocRef(TMP_Text target)
    {
        localized = new LocalizedString();
        text = target;
    }

    // 创建并记住目标文本；先不订阅（订阅时机交给 Refresh，避开占位符未赋值时抛异常）
    public static LocRef Create(TMP_Text target) => new LocRef(target);

    public void SetReference(string table, string key) => localized.SetReference(table, key);

    // 灌占位符：取到同类型持久变量就改值，否则新建一个；刷新时机交给 Refresh
    public void SetVar(string name, int value)
    {
        if(localized.TryGetValue(name, out IVariable v) && v is IntVariable iv)
            iv.Value = value;
        else
            localized[name] = new IntVariable { Value = value };
    }

    public void SetVar(string name, float value)
    {
        if(localized.TryGetValue(name, out IVariable v) && v is FloatVariable fv)
            fv.Value = value;
        else
            localized[name] = new FloatVariable { Value = value };
    }

    public void SetVar(string name, string value)
    {
        if(localized.TryGetValue(name, out IVariable v) && v is StringVariable sv)
            sv.Value = value;
        else
            localized[name] = new StringVariable { Value = value };
    }

    public void SetVar(string name, bool value)
    {
        if(localized.TryGetValue(name, out IVariable v) && v is BoolVariable bv)
            bv.Value = value;
        else
            localized[name] = new BoolVariable { Value = value };
    }

    // 刷新到 TMP：首次订阅 StringChanged（订阅即格式化一次），已订阅则主动刷新一次。
    public void Refresh()
    {
        if(bound)
        {
            localized.RefreshString();
            return;
        }
        bound = true;
        localized.StringChanged += OnStringChanged;
    }

    public void Unbind()
    {
        if(!bound)
            return;
        bound = false;
        localized.StringChanged -= OnStringChanged;
    }

    // 清空显示：先退订，避免 StringChanged 回调把文本再写回；再置空
    public void Clear()
    {
        Unbind();
        text.text = "";
    }

    void OnStringChanged(string value) => text.text = value;
}
