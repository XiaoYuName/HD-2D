using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XFramework;

public static class UIUtility
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
    

    /// <summary>
    /// 显示一个对话框
    /// </summary>
    /// <param name="title"></param>
    /// <param name="content"></param>
    /// <param name="cancelData"></param>
    /// <param name="actionData"></param>
    /// <param name="cancel"></param>
    /// <param name="action"></param>
    public static void PopDialogue(LocalSelectedData title,LocalSelectedData content,LocalSelectedData cancelData,LocalSelectedData actionData,
        Action cancel = null,Action action = null)
    {
        var dialogueUI = UISystem.Instance.OpenUI<PopDialogueUI>("PopDialogueUI");
        dialogueUI.ShowDialogue(title,content,cancelData,actionData,cancel,action);
    }

    /// <summary>
    /// 显示一个提示框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="content">内容</param>
    /// <param name="cancelData">取消按钮</param>
    /// <param name="cancel">按钮事件</param>
    public static void ShowPopWindow(LocalSelectedData title, LocalSelectedData content, LocalSelectedData cancelData,
        Action cancel = null)
    {
        var dialogueUI = UISystem.Instance.OpenUI<PopDialogueUI>("PopDialogueUI");
        dialogueUI.ShowPopWindow(title,content,cancelData,cancel);
    }

    /// <summary>
    /// 显示一个对话框
    /// </summary>
    /// <param name="content"></param>
    /// <param name="title"></param>
    /// <param name="cancelTex"></param>
    /// <param name="cancel"></param>
    public static void ShowPopWindow(string content,string title = "Tips",string cancelTex = "Confirm",
        Action cancel = null)
    {
        var dialogueUI = UISystem.Instance.OpenUI<PopDialogueUI>("PopDialogueUI");
        dialogueUI.ShowPopWindow(content,title,cancelTex,cancel);
    }
    
    /// <summary>
    /// 显示一个提示框
    /// </summary>
    /// <param name="title">标题多语言</param>
    /// <param name="content">内容多语言</param>
    /// <param name="runtimeName">动态文本名</param>
    /// <param name="val">动态值</param>
    /// <param name="cancelData">取消多语言</param>
    /// <param name="actionData">确定多语言</param>
    /// <param name="cancel">取消事件</param>
    /// <param name="action">确定事件</param>
    public static void PopDialogue(LocalSelectedData title,LocalSelectedData content,string runtimeName,string val,LocalSelectedData cancelData,LocalSelectedData actionData,
        Action cancel = null,Action action = null)
    {
        var dialogueUI = UISystem.Instance.OpenUI<PopDialogueUI>("PopDialogueUI");
        dialogueUI.ShowDialogue(title, content, runtimeName, val, cancelData, actionData, cancel, action);
    }
    
    /// <summary>
    /// 显示对话框
    /// </summary>
    /// <param name="content"></param>
    /// <param name="runtimeName"></param>
    /// <param name="val"></param>
    /// <param name="title"></param>
    /// <param name="confirmTex"></param>
    /// <param name="cancelTex"></param>
    /// <param name="cancelAction"></param>
    /// <param name="confirmAction"></param>
    public static void ShowPopDialogue(string content,string runtimeName = "",string val = "", string title = "Tips", string confirmTex = "Confirm",
        string cancelTex = "Cancel"
        , Action cancelAction = null, Action confirmAction = null)
    {
        var dialogueUI = UISystem.Instance.OpenUI<PopDialogueUI>("PopDialogueUI");
        dialogueUI.ShowDialogue(content, runtimeName, val, title, confirmTex, cancelTex, cancelAction, confirmAction);
    }



    /// <summary>
    /// 展示获取物品奖励弹窗
    /// </summary>
    /// <param name="reward"></param>
    public static void PopReward(List<ItemInfo> reward)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardUI>("PopRewardUI");
        if (ui != null)
        {
            ui.ShowReward(reward);
        }
    }
    
    /// <summary>
    /// 展示获取物品奖励弹窗
    /// </summary>
    /// <param name="reward"></param>
    public static void PopReward(ItemInfo reward)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardUI>("PopRewardUI");
        if (ui != null)
        {
            ui.ShowReward(reward);
        }
    }
    
    /// <summary>
    /// 展示获取物品奖励弹窗
    /// </summary>
    /// <param name="reward"></param>
    public static void PopReward(List<ShopItemBag>  reward)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardUI>("PopRewardUI");
        if (ui != null)
        {
            ui.ShowReward(reward);
        }
    }

    public static void PopReward(List<ClothBuyItemSlot> reward)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardUI>("PopRewardUI");
        if (ui != null)
        {
            ui.ShowReward(reward);
        }
    }

    /// <summary>
    /// 展示一串字符(请自行根据语言传入已经多语言过后的字符串)
    /// </summary>
    /// <param name="reward"></param>
    public static void PopRewardProperty(List<string> reward)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardPropertyUI>("PopRewardPropertyUI");
        if (ui != null)
        {
            ui.ShowingLabels(reward);
        }
    }

}
