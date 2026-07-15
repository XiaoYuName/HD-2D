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
    private List<DamageNumber> damageNumbers = new List<DamageNumber>();
    private int messageIndex = 0;
    
    private void StartReply()
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,1);
        Option(ShowModel.Message);
        PrivateMessageDataList = new List<PriavateMessageBag>(OnLineGameManager.Instance.GetPrivateMessageDataList());
        messageIndex = 0;
        ShowingMessage(PrivateMessageDataList[messageIndex]).Forget();
    }

    private async UniTaskVoid ShowingMessage(PriavateMessageBag priavateMessageBag)
    {
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

        if (coinNumber != 0)
        {
            var obj = EffectsManager.Instance.coinDamageNumberGUI.SpawnGUI(privateMessageButton, new Vector2(Random.Range(-30,30), 0));
            var NameKey = GameDataManager.Instance.GetPropertyNameKey(PropertyType.Coin);
            string fh = coinNumber > 0 ? "+" : "-";
            obj.leftText = LanguageManager.Instance.GetLocalizedString(NameKey.Table, NameKey.Value) + fh;
            obj.OnDespawn += ()=>damageNumbers.Remove(obj);
            damageNumbers.Add(obj);
        }

        if (fenNumber != 0)
        {
            var obj = EffectsManager.Instance.fenDamageNumberGUI.SpawnGUI(privateMessageButton, new Vector2(Random.Range(-30,30), 0));
            var NameKey = GameDataManager.Instance.GetPropertyNameKey(PropertyType.FenCount);
            string fh = fenNumber > 0 ? "+" : "-";
            obj.leftText = $"{LanguageManager.Instance.GetLocalizedString(NameKey.Table, NameKey.Value)}{fh}";
            obj.OnDespawn += ()=>damageNumbers.Remove(obj);
            damageNumbers.Add(obj);
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
            var numbersToDestroy = damageNumbers.ToArray();
            damageNumbers.Clear();
            foreach (var number in numbersToDestroy)
            {
                if (number != null)
                {
                    number.DestroyDNP();
                }
            }
            Option(ShowModel.Mask);
            return;
        }

        ShowingMessage(PrivateMessageDataList[messageIndex]).Forget();
    }

}
