using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using XFramework;

public class PopLoadingUI : UIBase
{
    private CanvasGroup _canvasGroup;
    private Sequence _sequence;
    private Canvas _canvas;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _canvas = Get<Canvas>("");
        _canvasGroup = Get<CanvasGroup>("UIMask");
     
    }
    public void FadeIn(float duration,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        _sequence?.Kill();
        _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer),layer);
        _canvas.sortingOrder = OrderInLayer;
        _canvasGroup.blocksRaycasts = true;
        _sequence.Append(_canvasGroup.DOFade(1, duration));
    }
    public void FadeOut(float duration,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer),layer);
        _canvas.sortingOrder = OrderInLayer;
        _canvasGroup.blocksRaycasts = true;
        _sequence?.Kill();
        _sequence.Append(_canvasGroup.DOFade(0, duration)).OnComplete(() =>
        {
            _canvasGroup.blocksRaycasts = false;
        });
    }

    public async UniTask FadeInAsync(float duration,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer),layer);
        _canvas.sortingOrder = OrderInLayer;
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _canvasGroup.blocksRaycasts = true;
        _sequence.Append(_canvasGroup.DOFade(1, duration));
        await _sequence.AsyncWaitForCompletion();
    }
    
    public async UniTask FadeOutAsync(float duration,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer),layer);
        _canvas.sortingOrder = OrderInLayer;
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(0, duration));
        await _sequence.AsyncWaitForCompletion();
        _canvasGroup.blocksRaycasts = false;
    }

    public void  Fade(float duration,Action callback,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        FadeIn(duration,layer, OrderInLayer);
        callback?.Invoke();
        FadeOut(duration,layer, OrderInLayer);
    }
    
    public async UniTask FadeAsync(float duration,Action action,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        await FadeInAsync(duration,layer, OrderInLayer);
        action?.Invoke();
        await FadeOutAsync(duration,layer, OrderInLayer);
    }
    
    public async UniTask FadeAsync(float duration,Func<UniTask> action,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        await FadeInAsync(duration,layer, OrderInLayer);
        await action();
        await FadeOutAsync(duration,layer, OrderInLayer);
    }

    public async UniTask FadeAsync(float duration,List<UniTask> actions,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        await FadeInAsync(duration,layer, OrderInLayer);
        foreach (var function in actions)
        {
            await function;
        }
        await FadeOutAsync(duration,layer, OrderInLayer);
    }

}
