using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Utils;
using DG.Tweening;
using PathologicalGames;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public partial class ExhibitionGameUI : UIBase
{
    /// <summary>
    /// 当前对局数据的副本/范例
    /// </summary>
    private List<FactoryMerchandiseItemInfo> ExhibitionItems;
    [LabelText("周边槽位")]
    public List<ExhibitionGameSlot>  ExhibitionSlots;
    [LabelText("NPC槽位")]
    public List<ExhibitionCharacterUI> ExhibitionCharacterSlots;
    [LabelText("打包模块槽位")]
    public List<PackController> PackSlots = new List<PackController>(); 
    [LabelText("周边主体色")]
    public List<Color> SlotColors;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var slot in ExhibitionSlots)
        {
            slot.Init();
            slot.SetSelected(false);
            slot.OnSelect.RemoveAllListeners();
            slot.OnSelect.AddListener(OnSelectedFactoryItemSlot);
        }

        foreach (var slot in ExhibitionCharacterSlots)
        {
            slot.Init();
        }
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate += UpdateGameTimer;
        ExhibitionItems = ExhibitionItems = ExhibitionManager.Instance.OnSelectedFactory
            .Select(item => new FactoryMerchandiseItemInfo(
                item.ID,
                item.Count,
                item.FrameItemId,
                item.PaintingItemId
            ))
            .ToList();
        CreateExhibitionEffest();
        RefreshItem();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate -= UpdateGameTimer;
        ReleaseExhibitionEffest();
    }


    private void UpdateGameTimer(float GameTime)
    {
        timeVal.text = $"{GameTime}s";
    }
    

    public void GenerateNpcExhibition()
    {
        int index = ExhibitionCharacterSlots.FindIndex(temp => temp.State == ExhibitionState.Idle);
        if (index < 0) return;
        ExhibitionGameData exhibitionGameData = new ExhibitionGameData(GetRandomExhibitionItems(Random.Range(1,4)));
        ExhibitionCharacterSlots[index].SetData(exhibitionGameData);
        RefreshItem();

    }

    /// <summary>
    /// 判断是否有空位
    /// </summary>
    /// <returns></returns>
    public bool HasIdleNpcSlot()
    {
        return ExhibitionCharacterSlots.Any(temp => temp.State == ExhibitionState.Idle);
    }

    /// <summary>
    /// 获取随机的购买的商品列表
    /// </summary>
    /// <param name="count">随机购买数量</param>
    /// <returns></returns>
    private List<FactoryMerchandiseItemInfo> GetRandomExhibitionItems(int count)
    {
        var result = new List<FactoryMerchandiseItemInfo>();

        if (ExhibitionItems == null || count <= 0)
        {
            return result;
        }

        // 清理原本就没有库存的数据
        ExhibitionItems.RemoveAll(item => item == null || item.Count <= 0);

        int takeCount = Mathf.Min(count, ExhibitionItems.Count);
        var selectedItems = RandomUtil.Take(ExhibitionItems, takeCount);

        foreach (var sourceItem in selectedItems)
        {
            // 返回一份数量为 1 的新数据，避免与源数据共享引用
            var resultItem = new FactoryMerchandiseItemInfo(
                sourceItem.ID,
                1,
                sourceItem.FrameItemId,
                sourceItem.PaintingItemId
            );

            result.Add(resultItem);

            // 源库存只减少一个
            sourceItem.Count--;

            if (sourceItem.Count <= 0)
            {
                ExhibitionItems.Remove(sourceItem);
            }
        }

        return result;
    }

    public FlyItemSlotData GetMappingFlyItemSlotData(FactoryMerchandiseItemInfo itemInfo)
    {
        foreach (var slot in ExhibitionSlots)
        {
            if (slot.ItemInfo.ID == itemInfo.ID)
            {
                return new FlyItemSlotData(slot.Color, slot.Index, slot.ItemInfo);
            }
        }

        return null;
    }


    #region OnSelectedFactoryItemSlot

    private const int FactorySlotSelectedLimit = 3;
    private List<ExhibitionGameSlot> SelectedFactoryItemSlots = new List<ExhibitionGameSlot>();

    public void OnSelectedFactoryItemSlot(ExhibitionGameSlot exhibitionGameSlot)
    {
        if (SelectedFactoryItemSlots.Contains(exhibitionGameSlot))
        {
            exhibitionGameSlot.SetSelected(false);
            SelectedFactoryItemSlots.Remove(exhibitionGameSlot);
            return;
        }

        if (SelectedFactoryItemSlots.Count >= FactorySlotSelectedLimit) return;
        
        exhibitionGameSlot.SetSelected(true);
        SelectedFactoryItemSlots.Add(exhibitionGameSlot);
    }

    #endregion

    #region 打包模块

    [Button("打包")]
    public void Pack()
    {
        if (SelectedFactoryItemSlots.Count <= 0) return;
        
        for (int i = 0; i < SelectedFactoryItemSlots.Count; i++)
        {
            var Slot = SelectedFactoryItemSlots[i];
            FlyItemSlotData slotData = new FlyItemSlotData(Slot.Color, Slot.Index, Slot.ItemInfo);
            SpawnFlyItemToTarget(Slot.Rect,slotData,PackSlots[0].FlySlots[i].transform as RectTransform);
        }
    }

    #endregion


    private void RefreshItem()
    {
        for (int i = 0; i < ExhibitionSlots.Count; i++)
        {
            if (i < ExhibitionItems.Count)
            {
                ExhibitionSlots[i].SetData(ExhibitionItems[i],i +1);
            }
            else
            {
                ExhibitionSlots[i].SetData(null,i +1);
            }
            ExhibitionSlots[i].SetColor(SlotColors[i % SlotColors.Count]);
            
        }
    }

    #region 特效对象池
    
    private SpawnPool FlyItemPool;
    private GameObject FlyItemPrefab;

    private void CreateExhibitionEffest()
    {
        GameObject newGameObject = new GameObject("Effest");
        newGameObject.transform.SetParent(transform);
        FlyItemPool = PoolManager.Pools.Create("ExhibitionGame",newGameObject);
        FlyItemPrefab = AssetsManager.Instance.LoadAssets<GameObject>(AssetKeys.FlySlotPath);
        PrefabPool audioPool = new PrefabPool(FlyItemPrefab.transform)
        {
            preloadAmount = 5,
        };
        FlyItemPool.CreatePrefabPool(audioPool);
    }
    
    private void ReleaseExhibitionEffest()
    {
        PoolManager.Pools.Destroy("Effest");
        AssetsManager.Instance.FreeAsset(AssetKeys.FlySlotPath);
        
    }

    /// <summary>
    /// 展示一个飞行周边
    /// </summary>
    /// <param name="OriginRect"></param>
    /// <param name="flyItemSlotData"></param>
    /// <param name="target"></param>
    public void SpawnFlyItemToTarget(RectTransform OriginRect,FlyItemSlotData flyItemSlotData,RectTransform target)
    {
        var flyItem = FlyItemPool.Spawn(FlyItemPrefab, target);
        var flyRect = flyItem.GetComponent<RectTransform>();
        
        flyRect.anchoredPosition = OriginRect.anchoredPosition;
        var flySlot = flyItem.GetComponent<FlySlot>();
        flySlot.Init();
        flySlot.SetData(flyItemSlotData);

        flyRect.DOAnchorPos(target.anchoredPosition, 0.15f);
    }
    
    private Vector2 ConvertToEffectLayer(
        RectTransform ui,
        RectTransform effectLayer)
    {
        Canvas sourceCanvas = ui.GetComponentInParent<Canvas>().rootCanvas;
        Canvas effectCanvas = effectLayer.GetComponentInParent<Canvas>().rootCanvas;

        Camera sourceCamera =
            sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : sourceCanvas.worldCamera;

        Camera effectCamera =
            effectCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : effectCanvas.worldCamera;

        // 获取UI视觉中心，而不是Pivot位置
        Vector3 worldCenter = ui.TransformPoint(ui.rect.center);

        Vector2 screenPoint =
            RectTransformUtility.WorldToScreenPoint(sourceCamera, worldCenter);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            effectLayer,
            screenPoint,
            effectCamera,
            out Vector2 localPoint);

        return localPoint;
    }

    #endregion
    
   
}
