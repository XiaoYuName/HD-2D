using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class MessageSlot : UIBase
{
    public MessageData messageData { get; private set; }
    
    private List<RawImage> rawImageList = new List<RawImage>();


    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(MessageData Data)
    {
        if (Data == null) return;
        this.messageData = Data;
        ChatMessageData message = LubanManager.Instance.TbChatMessageData.Get(Data.MessageID);
        desc.SetText(message.Message.Table,message.Message.Value);
        commentValTex.text = $"{GameDataManager.Instance.GetProperty(PropertyType.FenCount).Value}";
        shareValTex.text = $"{GameDataManager.Instance.GetProperty(PropertyType.FenCount).Value}";
        heartValTex.text = $"{GameDataManager.Instance.GetProperty(PropertyType.FenCount).Value}";

        foreach (ItemInfo itemInfo in Data.MessagePicture)
        {
            ItemData itemData = InventoryManager.Instance.GetItemData(itemInfo.ID);
            if(itemData == null)continue;
            
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.MessageRawImagePath);
            obj.transform.SetParent(textureLayoutGroup);
            obj.transform.localScale = Vector3.one;
            
            var rawImage = obj.transform.Find("MassageRawImage").GetComponent<RawImage>();


            rawImage.texture =
                AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(itemData.IconName)).texture;
            rawImageList.Add(rawImage);
        }
    }


    public void Release()
    {
        if (messageData != null)
        {
            foreach (ItemInfo itemInfo in messageData.MessagePicture)
            {
                ItemData itemData = InventoryManager.Instance.GetItemData(itemInfo.ID);
                if(itemData == null)continue;
                AssetsManager.Instance.FreeAsset(GamePathTools.CombinationItemIconPath(itemData.IconName));
            }

            foreach (var rawImage in rawImageList)
            {
                rawImage.texture = null;
                AssetsManager.Instance.FreeGameObject(rawImage.transform.parent.gameObject);
            }
            rawImageList.Clear();
            messageData = null;
        }
    }
}
