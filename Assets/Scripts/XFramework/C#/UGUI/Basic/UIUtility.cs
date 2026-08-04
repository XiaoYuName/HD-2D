using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using XFramework;

public static class UIUtility
{
    public static void FadeIn(float time,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeIn(time,layer,OrderInLayer);
    }
    
    public static void FadeOut(float time,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        loadingUI.FadeOut(time,layer, OrderInLayer);
    }
    
    public static async UniTask FadeInAsync(float time,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeInAsync(time,layer, OrderInLayer);
    }
    public static async UniTask FadeOutAsync(float time,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeOutAsync(time,layer, OrderInLayer);
    }
    
    
    public static async UniTask FadeAsync(float time, Func<UniTask> action,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, action,layer, OrderInLayer);
    }
    
    public static async UniTask FadeAsync(float time, Action action,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
    {
        var loadingUI = UISystem.Instance.OpenUI<PopLoadingUI>("PopLoadingUI");
        await loadingUI.FadeAsync(time, () => action(),layer, OrderInLayer);
    }
    
    public static async UniTask FadeAsync(float time, List<UniTask> actions,FadeLayer layer = FadeLayer.All,int OrderInLayer = 60)
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
    /// 服装上身结算：解锁刚刚做好的这个配件，然后弹结算窗。
    /// 一件服装的四个配件全解锁时，CharacterManager.UlockAccessories 会顺带解锁这件服装。
    /// </summary>
    /// <param name="characterBag">角色背包</param>
    /// <param name="clothingBag">本次制作的服装</param>
    /// <param name="accessoriesBag">本次做好的配件</param>
    /// <param name="closeSelf">关闭服装上身面板</param>
    public static void PopClothingAccessoriesComplete(CharacterBag characterBag, ClothingBag clothingBag,
        ClothingAccessoriesBag accessoriesBag, Action closeSelf)
    {
        if (characterBag == null || clothingBag == null || accessoriesBag == null)
        {
            Debug.LogError("服装上身结算缺少 CharacterBag / ClothingBag / 配件数据，配件没有解锁");
            PopCompleteWindow(closeSelf);
            return;
        }

        long characterID = characterBag.CharacterID;
        long clothingID = clothingBag.clothingID;

        CharacterManager.Instance.UlockAccessories(characterID, clothingID, accessoriesBag);
        SaveGameManager.Instance.Save();

        PopCompleteWindow(() =>
        {
            closeSelf?.Invoke();
            RunAfterMinGameClosed(() =>
            {
                var garmentMakingUI = UISystem.Instance.GetUI<GarmentMakingUI>("GarmentMakingUI");
                if (garmentMakingUI == null) return;

                // 整件服装解锁后会从待制作列表里消失，这时再回配件列表已经没有意义，直接退回服装选择
                if (CharacterManager.Instance.IsClothingUnlocked(characterID, clothingID))
                {
                    garmentMakingUI.OptionClothing();
                    return;
                }

                // 还有配件没做完：留在配件列表上刷新，方便接着做下一个
                garmentMakingUI.RefreshClothingFittingData(characterID, clothingID);
            });
        });
    }

    /// <summary>
    /// 服装小游戏通用结算。小游戏已经不参与服装解锁（解锁改由「服装打板 → 服装上身」推进），
    /// 所以这里只弹结算窗并退回服装界面，不再记录进度、也不再串下一个小游戏。
    /// </summary>
    /// <param name="closeSelf">关闭当前小游戏面板</param>
    public static void PopClothingMinGameComplete(Action closeSelf)
    {
        PopCompleteWindow(() =>
        {
            closeSelf?.Invoke();
            RunAfterMinGameClosed(() =>
            {
                UISystem.Instance.GetUI<GarmentMakingUI>("GarmentMakingUI")?.OptionClothing();
            });
        });
    }

    /// <summary>
    /// 小游戏关掉之后再执行。
    ///
    /// 独占场景的小游戏（宝石切割）关闭时会走一次异步退场：渐变 → 重新加载原场景 →
    /// RestoreUI 按快照把玩家原来开着的界面重新打开。退场还在进行时开下一个界面，
    /// 会被这次恢复用 SetAsLastSibling 压到 GarmentMakingUI 下面，看起来就像
    /// "配置了这个小游戏但它没执行"，而进度也因此没被记录，下次制作又会从它开始。
    /// UI 型小游戏没有转场，这里会直接同步执行，行为和以前一致。
    /// </summary>
    private static void RunAfterMinGameClosed(Action action)
    {
        if (GameSceneManager.IsInitialized)
        {
            GameSceneManager.Instance.RunAfterMinGameTransition(action);
            return;
        }

        action?.Invoke();
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
