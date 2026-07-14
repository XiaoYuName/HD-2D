using System.Collections.Generic;
using UnityEngine;
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
        if (GameDataManager.Instance.GetProperty(PropertyType.Strength).Value <= 0)
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
        GameDataManager.Instance.RemoveProperty(PropertyType.Strength,1);
        int fanNumber = 0;
        UISystem.Instance.GetUI<OnLineGameUI>("OnLineGameUI").OptionPage(OnLinePageType.Fan);
        for (int i = 0; i < selectedItems.Count; i++)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(selectedItems[i].ID);
            if (itemData != null)
            {
                fanNumber += GameDataManager.Instance.PlayerData.CalculateFanGain((int)itemData.Quality);
            }
            InventoryManager.Instance.ConsumeItem(selectedItems[i].Guid, 1);
        }
        GameDataManager.Instance.AddProperty(PropertyType.FenCount,fanNumber);
        
        var pictureSnapshot = new List<ItemInfo>(selectedItems);
        OnLineGameManager.Instance.SendMessage(new MessageData()
        {
            MessageID = chatMessageID,
            MessagePicture = pictureSnapshot
        });
       
    }
}
