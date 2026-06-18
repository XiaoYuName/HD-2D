using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public class ItemBagSlot : UIBase,IPointerClickHandler
{
    private GameObject itemSelected;
    private Image itemImg;
    private TextMeshProUGUI itemAmount;
    
    private Action<ItemBagSlot> OnClick;
    public ItemData itemData { get; private set; }
    public ItemBag  itemBag { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        itemSelected = Get("itemSelected");
        itemImg = Get<Image>("itemImg");
        itemAmount = Get<TextMeshProUGUI>("itemAmount");
    }

    public void Release()
    {
        if (itemData != null)
        {
            itemImg.sprite = null;
            AssetsManager.Instance.FreeAsset(itemData.IconPath);
        }
    }

    public void SetData(ItemBag itemBag,Action<ItemBagSlot> onClick = null)
    {
        //Assets/AddressableAssets/Remote/Texture2D/Item/IconWhiteRadish.png
        itemData = InventoryManager.Instance.GetItemData(itemBag.itemID);
        this.itemBag = itemBag;
        if (itemData != null)
        {
            try
            {
                itemImg.sprite = AssetsManager.Instance.LoadAssets<Sprite>(itemData.IconPath);
            }
            catch (Exception e)
            {
                Debug.Log($"加载Item {itemData.Id} 的Image 出现异常 :{itemData.IconPath} Message : " + e.Message);
            }
            
        }

        itemAmount.text = $"X{itemBag.itemAmount}";

        OnClick = onClick;
    }

    public void SetSelected(bool selected)
    {
        itemSelected.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke(this);
    }
}
