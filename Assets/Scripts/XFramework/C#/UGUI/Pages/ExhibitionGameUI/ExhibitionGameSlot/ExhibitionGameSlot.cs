using System;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public partial class ExhibitionGameSlot : UIBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    public FactoryMerchandiseItemInfo ItemInfo { get; private set; }
    public Color Color { get; private set; }
    public int Index { get; private set; }

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

    public void SetData(FactoryMerchandiseItemInfo itemInfo,int Index)
    {
        this.ItemInfo = itemInfo;
        this.Index = Index;
        if (ItemInfo == null)
        {
            SetEmpty();
        }
        else
        {
            noneRect.gameObject.SetActive(false); 
            merchandiseRuntimeSlot.SetData(ItemInfo);
        }
    }

    public void SetColor(Color color)
    {
        Color = color;
        flySlot.SetData(color,Index);
    }
    
    private void SetEmpty()
    {
       noneRect.gameObject.SetActive(true); 
    }

    #region IPointerEnterHandler Tweener
    private Sequence sequence;
    private bool isTweener => ItemInfo != null;
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


}
