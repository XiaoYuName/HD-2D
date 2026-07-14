using System;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 商店帮忙小游戏右侧的一个货物箱：箱体固定不动，箱上放一件可拖拽的货物图标 + 剩余数量。
/// 拖拽事件（只在货物图标上触发）向上冒泡到本组件并转发给 <see cref="ShopHelpPanel"/> 统一处理，
/// 由面板负责跟手移动、命中货架格与飞回。剩余数量为 0 时图标隐藏且不可再拖。
/// </summary>
public class ShopHelpBoxUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [LabelText("箱体(不拖拽)")][SerializeField] Image boxImage;
    [LabelText("货物图标根(拖拽对象)")][SerializeField] RectTransform iconRoot;
    [LabelText("货物图标")][SerializeField] Image iconImage;
    [LabelText("剩余数量")][SerializeField] TMP_Text countText;

    int typeIndex = -1;
    Sequence anim;

    public int TypeIndex => typeIndex;
    public RectTransform IconRoot => iconRoot;
    public Image IconImage => iconImage;

    #region 事件（转发给面板）
    public event Action<ShopHelpBoxUI, PointerEventData> BeginDragEvt;
    public event Action<ShopHelpBoxUI, PointerEventData> DragEvt;
    public event Action<ShopHelpBoxUI, PointerEventData> EndDragEvt;
    #endregion

    /// <summary>绑定一种货物：设图标与初始数量并显示本箱。</summary>
    public void Setup(int typeIndex, string iconPath, int count)
    {
        this.typeIndex = typeIndex;
        gameObject.SetActive(true);

        if(iconImage != null && !string.IsNullOrEmpty(iconPath))
            iconImage.SetIcon(iconPath);
        if(boxImage != null)
            boxImage.raycastTarget = false;   // 只有货物图标可拖

        SetCount(count);
    }

    /// <summary>隐藏本箱（本局用不到这么多品类时）。</summary>
    public void Hide()
    {
        typeIndex = -1;
        anim.Stop();
        gameObject.SetActive(false);
    }

    /// <summary>刷新剩余数量；为 0 时图标隐藏且不可拖。</summary>
    public void SetCount(int count)
    {
        if(countText != null)
            countText.text = count.ToString();
        bool has = count > 0;
        if(iconImage != null)
            iconImage.enabled = has;
    }

    /// <summary>货物出现/退回箱子上的缩放弹跳动效（0.5s）。</summary>
    public void PlayAppear()
    {
        if(iconImage == null)
            return;
        iconImage.enabled = true;
        anim.Stop();
        iconRoot.localScale = Vector3.zero;
        anim = Sequence.Create(Tween.Scale(iconRoot, Vector3.one, ShopHelpItemCellUI.AnimDur, Ease.OutBack));
    }

    /// <summary>拖拽中每次摆放时，手上货物图标的放大缩小反馈。</summary>
    public void PulseDrag()
    {
        if(iconRoot == null)
            return;
        anim.Stop();
        anim = Sequence.Create(Tween.PunchScale(iconRoot, Vector3.one * 0.25f, 0.2f));
    }

    public void OnBeginDrag(PointerEventData eventData) => BeginDragEvt?.Invoke(this, eventData);
    public void OnDrag(PointerEventData eventData) => DragEvt?.Invoke(this, eventData);
    public void OnEndDrag(PointerEventData eventData) => EndDragEvt?.Invoke(this, eventData);
}
