using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Utils;
using DG.Tweening;
using PathologicalGames;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;
using Random = UnityEngine.Random;

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
        ExhibitionManager.Instance.ExhibitionGameCoindUpdate += UpdateGameCoin;
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
        ExhibitionManager.Instance.ExhibitionGameCoindUpdate -= UpdateGameCoin;
        ReleaseExhibitionEffest();
    }


    private void UpdateGameTimer(float GameTime)
    {
        timeVal.text = $"{GameTime}s";
    }

    private void UpdateGameCoin(int CoinNumber)
    {
        goldVal.text  = $"{CoinNumber}";
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
            SpawnFlyItemToTarget(Slot.GetFlySlot(),PackSlots[0].FlySlots[i],slotData, () =>
            {
                PackSlots[0].SetFlySlotData(slotData);
            });
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
    private Sequence flySequence;

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
    /// <param name="flyItemSlotData">周边数据</param>
    /// <param name="origin">起点位置</param>
    /// <param name="target">终点位置</param>
    /// <param name="complete">完成后回调函数</param>
    public void SpawnFlyItemToTarget(FlySlot origin,FlySlot target,FlyItemSlotData flyItemSlotData,Action complete)
    {
        
        var flyItem = FlyItemPool.Spawn(FlyItemPrefab);
        if (flyItem == null) return;

        var flyRect = flyItem.GetComponent<RectTransform>();

        flyRect.DOKill();
        flyRect.localScale = Vector3.one;
        flyRect.localRotation = Quaternion.identity;
        flyRect.SetAsLastSibling();

        var flySlot = flyItem.GetComponent<FlySlot>();
        flySlot.Init();
        flySlot.SetData(flyItemSlotData);

        Canvas.ForceUpdateCanvases();
        
        Vector3 originPosition = GetRectWorldCenter(origin.GetRect());
        Vector3 targetPosition = GetRectWorldCenter(target.GetRect());

        // position/DOMove 使用世界坐标，不受双方父节点和锚点差异影响。
        flyRect.anchoredPosition = originPosition;
        flyRect.DOMove(targetPosition, 1f).SetEase(Ease.InQuad)
            .OnComplete(() => {
                if (FlyItemPool.IsSpawned(flyItem))
                {
                    FlyItemPool.Despawn(flyItem);
                }
            });
    }
    
    /// <summary>
    /// 获取 RectTransform 可视矩形中心的世界坐标。
    /// Transform.position 表示 Pivot 的世界坐标，Pivot 不在中心时会产生偏移，
    /// 因此这里使用 rect.center 转换得到真实的可视中心。
    /// </summary>
    private Vector3 GetRectWorldCenter(RectTransform rectTransform)
    {
        return rectTransform != null
            ? rectTransform.TransformPoint(rectTransform.rect.center)
            : Vector3.zero;
    }

    #endregion
    
   
}
