using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DamageNumbersPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework;
using Random = UnityEngine.Random;

public partial class PrivateMessagePage : UIBase
{
    private enum ShowModel { Home,Message,Mask}
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(startReplyButton,ShowDialogue,"");
        Bind(returnButton, () => { Option(ShowModel.Home); },"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataChange(PlayerDataChange);
        Option(ShowModel.Home);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        GameDataManager.Instance.UnregisterPlayerDataChange(PlayerDataChange);
        foreach (var button in optionButtons)
        {
            AssetsManager.Instance.FreeGameObject(button.gameObject);
        }
        optionButtons.Clear();
    }

    private void PlayerDataChange(PlayerData playerData)
    {
        if (playerData.GetProperty(PropertyType.ActionPointsValue) >= 1)
        {
            startReplyButton.interactable = true;
        }
        else
        {
            startReplyButton.interactable = false;
        }
    }

    private void Option(ShowModel showModel)
    {
        messageInfo.gameObject.SetActive(showModel == ShowModel.Home);
        message.gameObject.SetActive(showModel == ShowModel.Message);
        mask.gameObject.SetActive(showModel == ShowModel.Mask);
    }
    
    private void ShowDialogue()
    {
        if (OnLineGameManager.Instance.PrivateMessageDataList.Count <= 0)
        {
            UIUtility.ShowPopDialogue("StartReplyMessageContent",confirmAction:StartReply,cancelAction:null);
        }
        else
        {
            StartReply();
        }
    }

    private List<PriavateMessageBag> PrivateMessageDataList = new List<PriavateMessageBag>();
    private List<CustomButton> optionButtons = new List<CustomButton>();
    private int messageIndex = 0;
    
    private void StartReply()
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,1);
        Option(ShowModel.Message);
        PrivateMessageDataList = new List<PriavateMessageBag>(OnLineGameManager.Instance.GetPrivateMessageDataList());
        messageIndex = 0;
        slider.minValue = 0;
        slider.maxValue = PrivateMessageDataList.Count;
        ShowingMessage(PrivateMessageDataList[messageIndex]).Forget();
    }

    private async UniTaskVoid ShowingMessage(PriavateMessageBag priavateMessageBag)
    {
        slider.value = PrivateMessageDataList.Count - messageIndex;
        processVal.text = $"{messageIndex+1} /{PrivateMessageDataList.Count}";
        tipsRow.SetVar("value",PrivateMessageDataList.Count - (messageIndex + 1));
        PriavateMessageData priavateMessageData =
            LubanManager.Instance.TbPriavateMessageData.Get(priavateMessageBag.PrivateMessageID);
        fenNameString.SetText(priavateMessageData.FenName.Table, priavateMessageData.FenName.Value);
        desc.SetText(priavateMessageData.Desc.Table, priavateMessageData.Desc.Value);
        for (int i = 0; i < priavateMessageData.Option.Count; i++)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.OptionButtonPath);
            obj.transform.SetParent(optionRow);
            obj.transform.localScale = Vector3.one;
            var btn = obj.GetComponent<CustomButton>();
            btn.SetLabel(priavateMessageData.Option[i]);
            btn.onClick.RemoveAllListeners();
            var optionIndex = i;
            btn.onClick.AddListener(()=>ClickOption(optionIndex));
            optionButtons.Add(btn);
        }
        
        await UniTask.WaitForEndOfFrame(this);
        Canvas.ForceUpdateCanvases();
        // 先计算 MessageSlot 内部文本、图片等嵌套布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(desc.transform as RectTransform);

        // 再让外层列表读取 MessageSlot 的最终高度
        LayoutRebuilder.ForceRebuildLayoutImmediate(mScrollRect.content);
            
        Canvas.ForceUpdateCanvases();
    }

    private void ClickOption(int index)
    {
        OnLineGameManager.Instance.CompletePrivateMessage(PrivateMessageDataList[messageIndex].PrivateMessageID);
        var data = OnLineGameManager.Instance.GetMessageData(PrivateMessageDataList[messageIndex].PrivateMessageID);
        if (data == null) return;
        if (index >= data.RewardID.Count)
        {
            NextPriavateMessage();
            return;
        }
        long rewardID = data.RewardID[index];
        if (rewardID <= 0)
        {
            NextPriavateMessage();
            return;
        }

        var rewardData = GameDataManager.Instance.GetRewardData(rewardID);
        if (rewardData == null)
        {
            NextPriavateMessage();
            return;
        }

        int coinNumber = 0;
        int fenNumber = 0;
        int originalCoinValue = GameDataManager.Instance.GetProperty(PropertyType.Coin).Value;
        int  originalFenNumber = GameDataManager.Instance.GetProperty(PropertyType.FenCount).Value;
        foreach (var rewardPropData in rewardData.RewardProp)
        {
            if (rewardPropData.PropType == PropertyType.Coin)
            {
                coinNumber += rewardPropData.Value;
            }

            if (rewardPropData.PropType == PropertyType.FenCount)
            {
                fenNumber += rewardPropData.Value;
            }

            GameDataManager.Instance.AddProperty(rewardPropData.PropType,rewardPropData.Value);
        }

        foreach (var rewardCharacterPropData in rewardData.RewardCharacterProp)
        {
            if (rewardCharacterPropData.CharacterPropType == CharacterPropType.Feeling)
            {
                CharacterManager.Instance.AddCharacterFeeling(rewardCharacterPropData.CharacterID,rewardCharacterPropData.Value);
            }

            if (rewardCharacterPropData.CharacterPropType == CharacterPropType.Goodwill)
            {
                CharacterManager.Instance.AddCharacterFavorability(rewardCharacterPropData.CharacterID,rewardCharacterPropData.Value);
            }
        }

        foreach (var rewardItemData in rewardData.RewardItem)
        {
            InventoryManager.Instance.AddItem(rewardItemData.ItemID, rewardItemData.Count);
        }

        string coinTip = string.Empty;
        string fenTip = string.Empty;
        coinTip = coinNumber switch
        {
            > 0 =>
                $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.Coin).Name)} : {originalCoinValue} + {coinNumber}",
            < 0 =>
                $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.Coin).Name)} : - {coinNumber}",
            _ => coinTip
        };

        fenTip = fenNumber switch
        {
            > 0 =>
                $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.FenCount).Name)} : {originalFenNumber} + {fenNumber}",
            < 0 =>
                $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.FenCount).Name)} : {originalFenNumber} - {fenNumber}",
            _ => fenTip
        };

        List<string> labels = new List<string>();
        if (!string.IsNullOrEmpty(coinTip))
        {
            labels.Add(coinTip);
        }

        if (!string.IsNullOrEmpty(fenTip))
        {
            labels.Add(fenTip);
        }

        if (labels.Count > 0)
        {
            UIUtility.PopRewardProperty(new List<string>(){coinTip, fenTip});
        }

        
        NextPriavateMessage();
        
    }

    private void NextPriavateMessage()
    {
        messageIndex++;
        foreach (var button in optionButtons)
        {
            AssetsManager.Instance.FreeGameObject(button.gameObject);
        }
        optionButtons.Clear();
        if (messageIndex >= PrivateMessageDataList.Count)
        {
            PrivateMessageDataList.Clear();
            Option(ShowModel.Mask);
            return;
        }

        ShowingMessage(PrivateMessageDataList[messageIndex]).Forget();
    }

}
