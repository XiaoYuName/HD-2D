using System;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public partial class ExhibitionGameSlot : UIBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    public FlyItemSlotData FlySlotData { get; private set; }
    public RectTransform Rect { get; private set; }
    private UIEffect uiEffect;

    public UnityEvent<ExhibitionGameSlot> OnSelect;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        merchandiseRuntimeSlot.Init();
        flySlot.Init();
        Rect = transform as RectTransform;
        uiEffect = transform.GetComponent<UIEffect>();
    }

    public void SetData(FlyItemSlotData flyItemSlotData)
    {
        this.FlySlotData = flyItemSlotData;
        if (FlySlotData == null)
        {
            SetEmpty();
        }
        else
        {
            noneRect.gameObject.SetActive(false); 
            merchandiseRuntimeSlot.SetData(FlySlotData.ItemInfo);
            flySlot.SetData(FlySlotData);
        }
    }
    
    private void SetEmpty()
    {
       noneRect.gameObject.SetActive(true); 
    }

    #region IPointerEnterHandler Tweener
    private Sequence sequence;
    private bool isTweener => FlySlotData != null;
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isTweener) return;
        sequence?.Kill();
        sequence.Append(Rect.DOScale(Vector3.one * 1.05f, 0.25f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isTweener) return;
        sequence?.Kill();
        sequence.Append(Rect.DOScale(Vector3.one, 0.25f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isTweener) return;
        sequence?.Kill();
        sequence.Append(Rect.DOScale(Vector3.one * 1.05f,0.05f));
        sequence.Append(Rect.DOScale(Vector3.one,0.05f));
        OnSelect?.Invoke(this);
    }


    #endregion

    public void SetSelected(bool selected)
    {
        uiEffect.edgeMode = selected ? EdgeMode.Plain : EdgeMode.None;
    }

    public FlySlot GetFlySlot()
    {
        return flySlot;
    }


}
