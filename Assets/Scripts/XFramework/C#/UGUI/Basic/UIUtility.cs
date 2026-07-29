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

    public static async UniTask FadeLabel(string label)
    {
        var loadingUI = UISystem.Instance.GetUI<PopLoadingUI>("PopLoadingUI");
        if (loadingUI == null) return;
        await loadingUI.ShowLabel(label);
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
    public static void PopRewardProperty(List<string> reward,Action onClose = null)
    {
        var ui = UISystem.Instance.OpenUI<PopRewardPropertyUI>("PopRewardPropertyUI");
        if (ui != null)
        {
            ui.ShowingLabels(reward,onClose);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="OnComplete">点"返回界面"的回调</param>
    /// <param name="OnNext">不为空时显示"继续"按钮，点它走这个回调</param>
    public static void PopCompleteWindow(Action OnComplete = null, Action OnNext = null)
    {
        //TODO: 判断已经生成的PCB是否有红色状态，如果有算作失败，全绿色状态才算成功

        var ui = UISystem.Instance.OpenUI<PopCompleteWindow>("PopCompleteWindow");
        if (ui != null)
        {
            ui.ShowCompleteWindow(OnComplete, OnNext);
        }
    }

    /// <summary>
    /// 服装小游戏通用结算：记录本次通关进度，然后弹结算窗。
    /// 还有没玩的小游戏时给出"继续"按钮直接进下一个；全部通关时由
    /// CharacterManager.CompleteMinGame 真正解锁这件服装。
    /// </summary>
    /// <param name="characterBag">角色背包</param>
    /// <param name="clothingBag">本次制作的服装</param>
    /// <param name="finished">刚刚通关的小游戏</param>
    /// <param name="closeSelf">关闭当前小游戏面板</param>
    public static void PopClothingMinGameComplete(CharacterBag characterBag, ClothingBag clothingBag,
        ClothingMinGameType finished, Action closeSelf)
    {
        if (characterBag == null || clothingBag == null)
        {
            Debug.LogError($"服装小游戏 {finished} 结算缺少 CharacterBag / ClothingBag，进度没有记录");
            PopCompleteWindow(closeSelf);
            return;
        }

        long characterID = characterBag.CharacterID;
        long clothingID = clothingBag.clothingID;

        CharacterManager.Instance.CompleteMinGame(characterID, clothingID, finished);

        // UI 手上的 ClothingBag 可能是拷贝，进度一律以 Manager 里的为准
        ClothingBag latestBag = CharacterManager.Instance.GetCharacterBag(characterID)
            ?.ClothingBags.Find(temp => temp.clothingID == clothingID) ?? clothingBag;

        // "返回界面"：关掉小游戏并切回服装面板，和改动前的行为一致
        Action backToClothing = () =>
        {
            closeSelf?.Invoke();
            var garmentMakingUI = UISystem.Instance.GetUI<GarmentMakingUI>("GarmentMakingUI");
            if (garmentMakingUI != null)
            {
                garmentMakingUI.OptionClothing();
            }
        };

        ClothingMinGameType next = CharacterManager.Instance.GetNextMinGame(latestBag);
        if (next == ClothingMinGameType.None)
        {
            PopCompleteWindow(backToClothing);
            return;
        }

        PopCompleteWindow(backToClothing, () =>
        {
            closeSelf?.Invoke();
            CharacterManager.Instance.StartNextMinGame(characterID, latestBag);
        });
    }

    /// <summary>
    /// 显示失败弹窗
    /// </summary>
    /// <param name="isReset">是否允许再次挑战</param>
    /// <param name="OnFail">再次挑战回调</param>
    /// <param name="OnClose">关闭回调</param>
    public static void PopFailWindow(bool isReset = true,Action OnFail = null, Action OnClose = null)
    {
        var fail = UISystem.Instance.OpenUI<PopFailWindows>("PopFailWindows");
        if (fail != null)
        {
            fail.InitializeUI(isReset, OnFail, OnClose);
        }
    }

}
