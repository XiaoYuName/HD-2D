using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// GM 测试面板「物品目录」里的一行：展示 [ID] 名称 &lt;类型&gt; + 一个「添加」按钮。
/// 由 <see cref="TestPanel"/> 以隐藏模板运行时克隆填充（约定同 ItemBagSlot / FactoryTaskCard）。
/// 预制体里把 label / addButton 拖到对应字段即可。
/// </summary>
public class TestItemSlot : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] Button addButton;

    ItemData data;
    Action<ItemData> onAdd;

    /// <summary>用一条物品配置填充本行；点击「添加」时回调 onAdd。</summary>
    public void Set(ItemData data, Action<ItemData> onAdd)
    {
        this.data = data;
        this.onAdd = onAdd;

        if (label != null)
        {
            string name = string.IsNullOrEmpty(data.Remark) ? data.NameKey.Value : data.Remark;
            label.text = $"[{data.ID}] {name}  <{data.Quality}>";
        }

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() => this.onAdd?.Invoke(this.data));
        }
    }
}
