using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XFramework;

public class UIUtility : Singleton<UIUtility>
{
    public static void FadeIn(float time,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeIn(time,layer,OrderInLayer);
    }
    
    public static void FadeOut(float time,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeOut(time,layer, OrderInLayer);
    }
    
    public static async UniTask FadeInAsync(float time,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeInAsync(time,layer, OrderInLayer);
    }
    public static async UniTask FadeOutAsync(float time,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeOutAsync(time,layer, OrderInLayer);
    }
    
    
    public static async UniTask FadeAsync(float time, Func<UniTask> action,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, action,layer, OrderInLayer);
    }
    
    public static async UniTask FadeAsync(float time, Action action,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, () => action(),layer, OrderInLayer);
    }
    
    public static async UniTask FadeAsync(float time, List<UniTask> actions,UICanvasLayer layer = UICanvasLayer.UITop,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, actions, layer, OrderInLayer);
    }
    

    

    
}
