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
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _canvasGroup = Get<CanvasGroup>("UIMask");
     
    }
    public void FadeIn(float duration)
    {
        _sequence?.Kill();
        _canvasGroup.blocksRaycasts = true;
        _sequence.Append(_canvasGroup.DOFade(1, duration));
    }
    public void FadeOut(float duration)
    {
        _sequence?.Kill();
        _sequence.Append(_canvasGroup.DOFade(0, duration)).OnComplete(() =>
        {
            _canvasGroup.blocksRaycasts = false;
        });
    }

    public async UniTask FadeInAsync(float duration)
    {
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _canvasGroup.blocksRaycasts = true;
        _sequence.Append(_canvasGroup.DOFade(1, duration));
        await _sequence.AsyncWaitForCompletion();
    }
    
    public async UniTask FadeOutAsync(float duration)
    {
        _sequence?.Kill();
        _sequence = DOTween.Sequence();
        _sequence.Append(_canvasGroup.DOFade(0, duration));
        await _sequence.AsyncWaitForCompletion();
        _canvasGroup.blocksRaycasts = false;
    }

    public void  Fade(float duration,Action callback)
    {
        FadeIn(duration);
        callback?.Invoke();
        FadeOut(duration);
    }
    
    public async UniTask FadeAsync(float duration,Action action)
    {
        await FadeInAsync(duration);
        action?.Invoke();
        await FadeOutAsync(duration);
    }
    
    public async UniTask FadeAsync(float duration,Func<UniTask> action)
    {
        await FadeInAsync(duration);
        await action();
        await FadeOutAsync(duration);
    }

    public async UniTask FadeAsync(float duration,List<UniTask> actions)
    {
        await FadeInAsync(duration);
        foreach (var function in actions)
        {
            await function;
        }
        await FadeOutAsync(duration);
    }

}
