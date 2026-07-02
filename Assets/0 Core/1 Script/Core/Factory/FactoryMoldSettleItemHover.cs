using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在 <see cref="FactoryMoldSettlePanel"/> 结算列表里每张产品卡上，只负责鼠标进入/离开的事件转发：
/// 进入时把本卡的 Hover 大图 Key + 名称 Key 报给面板，离开时通知面板隐藏。卡片内容与展示逻辑都在面板里，
/// 本脚本不持有面板引用，靠 <see cref="Setup"/> 注入回调，便于与 <see cref="FactorySelectCellUI"/> 解耦复用。
/// </summary>
public class FactoryMoldSettleItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    string cardSpriteKey;
    string nameKey;
    Action<string, string> onHoverEnter;
    Action onHoverExit;

    public void Setup(string cardSpriteKey, string nameKey, Action<string, string> onHoverEnter, Action onHoverExit)
    {
        this.cardSpriteKey = cardSpriteKey;
        this.nameKey = nameKey;
        this.onHoverEnter = onHoverEnter;
        this.onHoverExit = onHoverExit;
    }

    public void OnPointerEnter(PointerEventData eventData) => onHoverEnter?.Invoke(cardSpriteKey, nameKey);
    public void OnPointerExit(PointerEventData eventData) => onHoverExit?.Invoke();

    void OnDisable() => onHoverExit?.Invoke();
}
