using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class ClothingPatternMakingUI : UIBase
{
    public ClothingAccessoriesBag CurrentBagData { get; private set; }
    public ClothingAccessoriesData CurrentData { get; private set; }

    private List<PattentSlot> PattentSlotList = new List<PattentSlot>();

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(closeButton,Close,"");
        
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        foreach (var slot in PattentSlotList)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        PattentSlotList.Clear();
    }

    public void SetData(ClothingAccessoriesBag bagData)
    {
        foreach (var slot in PattentSlotList)
        {
            slot.Release();
            AssetsManager.Instance.FreeGameObject(slot.gameObject);
        }
        PattentSlotList.Clear();
        
        CurrentBagData = bagData;
        CurrentData = LubanManager.Instance.TbClothingAccessoriesData.Get(bagData.accessoriesID);
        nameStr.SetText(CurrentData.AccessoriesName);
        descStr.SetText(CurrentData.AccessoriesDesc);
        icon.sprite = LoadAsset<Sprite>(GamePathTools.CombinationAccessoriesIconPath(CurrentData.AccessoriesIconName));
        foreach (var PcbSlotID in CurrentData.PcbSlotList)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.PattentSlotPath);
            obj.transform.SetParent(memuSlotGroup);
            obj.transform.localScale = Vector3.one;
            
            var slot = obj.transform.GetComponent<PattentSlot>();
            slot.Init();
            var PcbData = LubanManager.Instance.TbPcbSlotData.Get(PcbSlotID);
            slot.SetData(PcbData);
            PattentSlotList.Add(slot);
        }
    }

    public PcbItemSlot SpawnPcbItemSlot(PcbSlotData data, Vector2 screenPosition, Camera eventCamera)
    {
        var obj = AssetsManager.Instance.Instantiate(AssetKeys.PcbItemSlotPath);
        obj.transform.SetParent(pCB, false);
        obj.transform.localScale = Vector3.one;
        
        var slot = obj.GetComponent<PcbItemSlot>();
        slot.Init();
        slot.Rect.anchorMin = new Vector2(0.5f, 0.5f);
        slot.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        slot.Rect.pivot = new Vector2(0.5f, 0.5f);
        slot.SetData(data);
        MovePcbItemSlotToScreenPoint(slot, screenPosition, eventCamera);
        return slot;
    }

    public void  DespawnPcbItemSlot(PcbItemSlot slot)
    {
        slot.Release();
        AssetsManager.Instance.FreeGameObject(slot.gameObject);
    }

    public void MovePcbItemSlotToScreenPoint(PcbItemSlot slot, Vector2 screenPosition, Camera eventCamera)
    {
        if (slot == null || pCB == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pCB, screenPosition, eventCamera, out var localPosition))
        {
            slot.Rect.anchoredPosition = localPosition;
        }
    }
}
