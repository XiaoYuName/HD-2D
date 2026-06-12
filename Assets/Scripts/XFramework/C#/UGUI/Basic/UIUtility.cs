using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XFramework;

public class UIUtility : Singleton<UIUtility>
{
    public static void FadeIn(float time)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeIn(time);
    }
    
    public static void FadeOut(float time)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeOut(time);
    }
    
    public static async UniTask FadeInAsync(float time)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeInAsync(time);
    }
    public static async UniTask FadeOutAsync(float time)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeOutAsync(time);
    }
    
    
    public static async UniTask FadeAsync(float time, Func<UniTask> action)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, action);
    }
    
    public static async UniTask FadeAsync(float time, Action action)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, () => action());
    }
    
    public static async UniTask FadeAsync(float time, List<UniTask> actions)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time,actions);
    }
    

    

    
}
