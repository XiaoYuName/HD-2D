using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class PostingUpdatesPage : UIBase
{
    private long chatMessageID;
    public List<AddPictureButton> PictureButtons = new List<AddPictureButton>();
    private List<ItemInfo> selectedItems = new List<ItemInfo>();
    
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        RandomChatData();
        Bind(randomButton,RandomChatData,"");
        foreach (var button in PictureButtons)
        {
            button.Init();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener(OpenPopSelectedPictureUI);
        }
        Bind(sendButton,SendMessage,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        sendButton.interactable = false;
        selectedItems.Clear();
        selectedPictureText.SetVar("value",$"{selectedItems.Count}/{PictureButtons.Count}");
        for (int i = 0; i < PictureButtons.Count; i++)
        {
            PictureButtons[i].Release();
            PictureButtons[i].SetData(null);
        }
        OnLineGameManager.Instance.RegisterOnMessageListUpdate(OnMessageListUpdate,true);
        OnLineGameManager.Instance.RegisterOnMessageDataUpdate(OnMessageDataUpdate,true);
       
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        OnLineGameManager.Instance.UnRegisterOnMessageListUpdate(OnMessageListUpdate);
        OnLineGameManager.Instance.UnRegisterOnMessageDataUpdate(OnMessageDataUpdate);
        foreach (var item in messageSlot)
        {
            item.Release();
            AssetsManager.Instance.FreeGameObject(item.gameObject);
        }
        messageSlot.Clear();
    }

    private void RandomChatData()
    {
        var index = Random.Range(0, LubanManager.Instance.TbChatMessageData.DataList.Count);
        var chatMessageData = LubanManager.Instance.TbChatMessageData.DataList[index];
        chatMessageID = chatMessageData.ID;
        desc.SetText(chatMessageData.Message);
    }


    private void OpenPopSelectedPictureUI()
    {
       var pictureUI =  UISystem.Instance.OpenUI<PopSelectedPictureUI>("PopSelectedPictureUI");
       pictureUI.SetStartSelected(selectedItems);
       pictureUI.RegisterOnSelected(OnSelectedPicture);
    }

    private void OnSelectedPicture(List<ItemInfo> selectedItems)
    {
        this.selectedItems = selectedItems;
        if (selectedItems == null || selectedItems.Count <= 0)
        {
            for (int i = 0; i < PictureButtons.Count; i++)
            {
                PictureButtons[i].Release();
                PictureButtons[i].SetData(null);
            }
            selectedPictureText.SetVar("value",$"{0}/{PictureButtons.Count}");
            sendButton.interactable = false;
            return;
        }
        sendButton.interactable = true;
        selectedPictureText.SetVar("value",$"{selectedItems.Count}/{PictureButtons.Count}");
        for (int i = 0; i < PictureButtons.Count; i++)
        {
            if (i <= selectedItems.Count -1)
            {
                PictureButtons[i].Release();
                PictureButtons[i].SetData(selectedItems[i]);
            }
            else
            {
                PictureButtons[i].Release();
                PictureButtons[i].SetData(null);
            }
        }
    }


    private void SendMessage()
    {
        if (GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value <= 0)
        {
            UIUtility.ShowPopWindow("StrengthCountError");
        }
        else
        {
            UIUtility.ShowPopDialogue("SendMessageContent","Count",selectedItems.Count.ToString(),confirmAction:Send);
        }
    }

    private void Send()
    {
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue,1);
        int fanNumber = 0;
       
        //UISystem.Instance.GetUI<OnLineGameUI>("OnLineGameUI").OptionPage(OnLinePageType.Fan);
        for (int i = 0; i < selectedItems.Count; i++)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(selectedItems[i].ID);
            if (itemData != null)
            {
                fanNumber += GameDataManager.Instance.PlayerData.CalculateFanGain((int)itemData.Quality);
            }
            InventoryManager.Instance.ConsumeItem(selectedItems[i].Guid, 1);
        }
        
        int originalValue = GameDataManager.Instance.GetProperty(PropertyType.FenCount).Value;
        int resultValue = originalValue + fanNumber;
        GameDataManager.Instance.AddProperty(PropertyType.FenCount,fanNumber);
        string material =
            $"{LanguageManager.Instance.GetLocalizedString("UIText", "ConsumeMaterialsText")} : {selectedItems.Count}";
        string label =
            $"{LanguageManager.Instance.GetLocalizedString(GameDataManager.Instance.GetPropertyData(PropertyType.FenCount).Name)}: {originalValue}" +
            $"=>{resultValue}";
        UIUtility.PopRewardProperty(new List<string>() {material, label });
        
        var pictureSnapshot = new List<ItemInfo>(selectedItems);
        OnLineGameManager.Instance.SendMessage(new MessageData()
        {
            MessageID = chatMessageID,
            MessagePicture = pictureSnapshot,
            SendTime = GameDataManager.Instance.PlayerData.GameDateTime,
            TimeSlot =  GameDataManager.Instance.PlayerData.TimeSlot,
        });
        
        sendButton.interactable = false;
        selectedItems.Clear();
        selectedPictureText.SetVar("value",$"{selectedItems.Count}/{PictureButtons.Count}");
        for (int i = 0; i < PictureButtons.Count; i++)
        {
            PictureButtons[i].Release();
            PictureButtons[i].SetData(null);
        }
       
    }

    #region Message
    private List<MessageSlot> messageSlot = new List<MessageSlot>();

    
    private void OnMessageListUpdate(List<MessageData> messages)
    {
        GenerateMessage(messages).Forget();
    }

    private void OnMessageDataUpdate(MessageData messageData)
    {
        GenerateMessageData(messageData).Forget();
    }

    private async UniTask GenerateMessage(List<MessageData> messages)
    {
        if(messages == null || messages.Count == 0)return;
        foreach (var VARIABLE in messages)
        {
            await GenerateMessageData(VARIABLE,0);
        }
    }

    private async UniTask GenerateMessageData(MessageData messageData,float duration = 0.3f)
    {
        var obj = AssetsManager.Instance.Instantiate(AssetKeys.MessageSlotPath);
        obj.transform.SetParent(mScrollRect.content, false);
        obj.transform.SetAsLastSibling();
        obj.transform.localScale = Vector3.zero;
        var slot = obj.GetComponent<MessageSlot>();
        slot.Init();
        slot.SetData(messageData);
        messageSlot.Add(slot);
        inputSlot.transform.SetAsLastSibling();
        await UniTask.WaitForEndOfFrame(this);
        Canvas.ForceUpdateCanvases();
        // 先计算 MessageSlot 内部文本、图片等嵌套布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(slot.transform as RectTransform);

        // 再让外层列表读取 MessageSlot 的最终高度
        LayoutRebuilder.ForceRebuildLayoutImmediate(mScrollRect.content);
            
        Canvas.ForceUpdateCanvases();
        await obj.transform.DOScale(Vector3.one, duration).AsyncWaitForCompletion();
    }

    #endregion
}
