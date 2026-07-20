using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public partial class RubbishController : UIBase,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
{
    private UIEffect uiEffect;

    public UnityEvent OnClick;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        uiEffect = GetComponent<UIEffect>();
    }
    
    private Sequence tweenSequence;

    public void OnPointerEnter(PointerEventData eventData)
    {
        tweenSequence?.Kill();
        tweenSequence = DOTween.Sequence();
        tweenSequence.Append(transform.DOScale(Vector3.one * 1.03f, 0.15f));
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tweenSequence?.Kill();
        tweenSequence = DOTween.Sequence();
        tweenSequence.Append(transform.DOScale(Vector3.one, 0.15f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
