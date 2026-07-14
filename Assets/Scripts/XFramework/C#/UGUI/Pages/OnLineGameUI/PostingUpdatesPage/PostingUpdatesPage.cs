using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class PostingUpdatesPage : UIBase
{
    private long chatMessageID;
    public List<AddPictureButton> PictureButtons = new List<AddPictureButton>();
    
    
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
        //base.Open();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        //base.Close();
        
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
       pictureUI.RegisterOnSelected(OnSelectedPicture);
    }

    private void OnSelectedPicture(List<ItemInfo> selectedItems)
    {
        selectedPictureText.SetVar("value",$"{selectedItems.Count}/{PictureButtons.Count}");
        for (int i = 0; i < PictureButtons.Count; i++)
        {
            if (i <= selectedItems.Count -1)
            {
                PictureButtons[i].Release();
                PictureButtons[i].SetData(selectedItems[i]);
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
            //UIUtility.ShowPopWindow();
        }
    }
}
