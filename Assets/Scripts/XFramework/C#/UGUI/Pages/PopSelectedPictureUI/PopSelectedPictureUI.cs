using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework;

public partial class PopSelectedPictureUI : UIBase
{
    private List<ItemInfo> PlayerBags;
    private List<PictureSlot> pictureSlots = new List<PictureSlot>();
    private List<ItemInfo> selectedItems = new List<ItemInfo>();
    private Action<List<ItemInfo>> OnItemSelected;
    public const int selectedLimit = 5;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
        Bind(confirmButton,ConfirmClick,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        InventoryManager.Instance.RegisterMaterialTypeChangeCallBack(ItemMaterialType.Painting,CreatPaintingSlotUI);
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        InventoryManager.Instance.UnregisterMaterialTypeChangeCallBack(ItemMaterialType.Painting,CreatPaintingSlotUI);
        foreach (var pictureSlot in pictureSlots)
        {
            pictureSlot.Release();
            AssetsManager.Instance.FreeGameObject(pictureSlot.gameObject);
        }
        pictureSlots.Clear();
    }

    private void CreatPaintingSlotUI(List<ItemInfo> itemInfos)
    {
        PlayerBags = itemInfos;
        foreach (var pictureSlot in pictureSlots)
        {
            pictureSlot.Release();
            AssetsManager.Instance.FreeGameObject(pictureSlot.gameObject);
        }
        pictureSlots.Clear();

        foreach (var itemInfo in itemInfos)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.PictureSlotPath);
            obj.transform.SetParent(scrollView.content);
            obj.transform.localScale = Vector3.one;

            var slotUI = obj.GetComponent<PictureSlot>();
            slotUI.Init();
            slotUI.InitData(itemInfo);
            slotUI.OnClick = () =>
            {
                OnSelectedSlot(slotUI);
            };
            pictureSlots.Add(slotUI);
        }

        foreach (var pictureSlot in pictureSlots)
        {
            if (selectedItems.Contains(pictureSlot.ItemInfo))
            {
                pictureSlot.SetSelected(true);
            }
            else
            {
                pictureSlot.SetSelected(false);
            }
        }

        
    }

    private void OnSelectedSlot(PictureSlot pictureSlot)
    {
        if (selectedItems.Contains(pictureSlot.ItemInfo))
        {
            selectedItems.Remove(pictureSlot.ItemInfo);
            pictureSlot.SetSelected(false);
            return;
        }

        if (selectedItems.Count < selectedLimit)
        {
            foreach (var slot in pictureSlots)
            {
                if (slot == pictureSlot)
                {
                    slot.SetSelected(true);
                    selectedItems.Add(slot.ItemInfo);
                }
            }
        }
    }

    private void ConfirmClick()
    {
        OnItemSelected?.Invoke(new List<ItemInfo>(selectedItems));
        Close();
    }

    public void RegisterOnSelected(Action<List<ItemInfo>> callback)
    {
        OnItemSelected =  callback;
    }

    public void SetStartSelected(List<ItemInfo> itemInfos)
    {
        selectedItems = itemInfos == null
            ? new List<ItemInfo>()
            : new List<ItemInfo>(itemInfos);
        CreatPaintingSlotUI(PlayerBags);
    }

}
