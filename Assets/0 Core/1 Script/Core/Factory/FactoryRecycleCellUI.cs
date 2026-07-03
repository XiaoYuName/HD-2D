using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Localization.Components;

/// <summary>
/// 回收站单个周边格子：图标 + 名称 + 持有数量(x{owned}) + 已选/持有角标({selected}/{owned}) + 回收单价(¥{price}/个) + 减号按钮 + 已选高亮框。
/// 点击格子本体 +1，长按可加速连加（封顶为持有数量）；左上减号 -1。已选数量变化时回调上层重算预期收入。
/// 由 <see cref="FactoryRecyclePanel"/> 从隐藏模板实例化并绑定背包物品。
/// </summary>
public class FactoryRecycleCellUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] Image iconImage;
    [SerializeField] LocalizeStringEvent nameLse;
    [SerializeField] TMP_Text ownedText;     // 持有数量 "x{owned}"
    [SerializeField] TMP_Text selectBadge;   // 已选/持有 "{selected}/{owned}"
    [SerializeField] TMP_Text priceText;     // 回收单价 "¥{price}/个"
    [SerializeField] Button minusButton;
    [SerializeField] Image selectFrame;      // 已选(>0)高亮框

    // 长按加速：首次延迟后开始连加，间隔逐步缩短
    const float HoldStartDelay = 0.4f;
    const float HoldIntervalMax = 0.18f;
    const float HoldIntervalMin = 0.03f;
    const float HoldAccelPerStep = 0.015f;

    ItemInfo info;
    int owned;
    int selected;
    int unitPrice;
    Action onChanged;
    Coroutine holdCo;

    /// <summary>绑定的背包物品实例。</summary>
    public ItemInfo Info => info;
    /// <summary>当前已选回收数量。</summary>
    public int Selected => selected;
    /// <summary>本格回收预期收入（已选数量 × 单价）。</summary>
    public int Income => selected * unitPrice;

    void Awake()
    {
        if(minusButton != null)
            minusButton.onClick.AddListener(Decrement);
    }

    /// <summary>用背包物品填充本格；<paramref name="unitPrice"/> 为单件回收价，<paramref name="onChanged"/> 在已选数量变化时回调。</summary>
    public void Set(ItemInfo info, int unitPrice, Action onChanged)
    {
        this.info = info;
        this.onChanged = onChanged;
        owned = info.Count;
        this.unitPrice = unitPrice;
        selected = 0;

        iconImage.SetIcon(info.IconPath);
        nameLse.SetText(LocTableSet.InventoryItem, info.Name); 
        priceText.text = GetPriceText(unitPrice);
        Refresh();
    }

    void Increment()
    {
        if(selected >= owned)
            return;
        selected++;
        Refresh();
        onChanged?.Invoke();
    }

    void Decrement()
    {
        if(selected <= 0)
            return;
        selected--;
        Refresh();
        onChanged?.Invoke();
    }

    void Refresh()
    {
        ownedText.text = "x" + owned;
        selectBadge.text = selected + "/" + owned;
        if(selectFrame != null)
            selectFrame.enabled = selected > 0;
    }

    // 点击格子本体 +1，并起长按连加协程；减号是独立子按钮，其指针事件不会冒泡到此
    public void OnPointerDown(PointerEventData e)
    {
        Increment();
        if(holdCo != null)
            StopCoroutine(holdCo);
        holdCo = StartCoroutine(HoldAdd());
    }

    public void OnPointerUp(PointerEventData e) => StopHold();
    public void OnPointerExit(PointerEventData e) => StopHold();

    void StopHold()
    {
        if(holdCo == null)
            return;
        StopCoroutine(holdCo);
        holdCo = null;
    }

    IEnumerator HoldAdd()
    {
        yield return new WaitForSeconds(HoldStartDelay);
        float interval = HoldIntervalMax;
        while(selected < owned)
        {
            Increment();
            yield return new WaitForSeconds(interval);
            interval = Mathf.Max(HoldIntervalMin, interval - HoldAccelPerStep);
        }
        holdCo = null;
    }

    // 单价含 {Price} 占位符，单独构造 LocalizedString 灌值后取当前语言成品串（同 FactoryProductSelectPanel / FactorySettlePanel）
    static string GetPriceText(int price)
    {
        UnityEngine.Localization.LocalizedString ls = new ()
        {
            TableReference = LocTableSet.Factory,
            TableEntryReference = FactoryLocKeySet.UnitPriceFmt
        };
        ls.SetVar(LocVarSet.FactoryMain.Price, price, false);
        return ls.GetLocalizedString();
    }

#if UNITY_EDITOR
    /// <summary>编辑器一键生成时绑定内部引用（仅供 <see cref="FactoryRecyclePanel"/> 生成器调用）。</summary>
    public void EditorBind(Image icon, LocalizeStringEvent name, TMP_Text owned, TMP_Text badge, TMP_Text price, Button minus, Image frame)
    {
        iconImage = icon;
        nameLse = name;
        ownedText = owned;
        selectBadge = badge;
        priceText = price;
        minusButton = minus;
        selectFrame = frame;
    }
#endif
}
