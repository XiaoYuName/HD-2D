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
    [LabelText("袋子模块槽位")]
    public List<BagController> BagSlots = new List<BagController>();
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
            slot.OnClick = SendCharacter;
        }

        foreach (var slot in PackSlots)
        {
            slot.Init();
        }

        foreach (var slot in BagSlots)
        {
            slot.Init();
            slot.OnSelect.RemoveAllListeners();
            slot.OnSelect.AddListener(OnSelectedBagController);
        }
        rubbishController.Init();
        rubbishController.OnClick.RemoveAllListeners();
        rubbishController.OnClick.AddListener(OnRubbish);
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate += UpdateGameTimer;
        ExhibitionManager.Instance.ExhibitionGameCoindUpdate += UpdateGameCoin;
        ExhibitionManager.Instance.RegisterSelectedFactoryUpdate(RefreshItem);
        ExhibitionItems = ExhibitionItems = ExhibitionManager.Instance.OnSelectedFactory
            .Select(item => new FactoryMerchandiseItemInfo(
                item.ID,
                item.Count,
                item.FrameItemId,
                item.PaintingItemId
            ))
            .ToList();
        CreateExhibitionEffest();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate -= UpdateGameTimer;
        ExhibitionManager.Instance.ExhibitionGameCoindUpdate -= UpdateGameCoin;
        ExhibitionManager.Instance.UnRegisterSelectedFactoryUpdate(RefreshItem);
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
        //本地保存的只是副本-每个角色出来的时候就会吧副本内的一条数据给占用掉。如果副本内没有数据了，则不再生成角色。每次传给角色或者扔进垃圾桶的时候才会进行扣除真实数据
        //如果角色非交易离开，需要吧副本内的数据重新还给副本数据
        int index = ExhibitionCharacterSlots.FindIndex(temp => temp.State == ExhibitionState.Idle);
        if (index < 0) return;
        ExhibitionGameData exhibitionGameData = new ExhibitionGameData(GetRandomExhibitionItems(Random.Range(1,4)));
        ExhibitionCharacterSlots[index].SetData(exhibitionGameData);
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

    private ExhibitionGameSlot SelectedFactoryItemSlots = null;

    public void OnSelectedFactoryItemSlot(ExhibitionGameSlot exhibitionGameSlot)
    {
        if (SelectedPackController != null) return;
        if (SelectedFactoryItemSlots == null)
        {
            SelectedFactoryItemSlots = exhibitionGameSlot;
            SelectedFactoryItemSlots.SetSelected(true);
            return;
        }

        if (SelectedFactoryItemSlots == exhibitionGameSlot)
        {
            SelectedFactoryItemSlots.SetSelected(false);
            SelectedFactoryItemSlots = null;
        }

        if (SelectedFactoryItemSlots != exhibitionGameSlot && SelectedFactoryItemSlots != null)
        {
            SelectedFactoryItemSlots.SetSelected(false);
            exhibitionGameSlot.SetSelected(true);
            SelectedFactoryItemSlots = exhibitionGameSlot;
        }
    }

    #endregion

    #region OnSelectedBagController
    private BagController SelectedBagController = null;

    public void OnSelectedBagController(BagController bagController)
    {
        if (SelectedPackController != null) return;
        
        if (SelectedBagController == null)
        {
            SelectedBagController = bagController;
            SelectedBagController.SetSelected(true);
            return;
        }

        if (SelectedBagController == bagController)
        {
            SelectedBagController.SetSelected(false);
            SelectedBagController = null;
        }

        if (SelectedBagController != bagController && SelectedBagController != null)
        {
            SelectedBagController.SetSelected(false);
            bagController.SetSelected(true);
            SelectedBagController = bagController;
        }
    }


    #endregion

    #region OnSelectedPackController

    private PackController SelectedPackController = null;

    private void OnSelectedPackController(PackController packController)
    {
        if (SelectedPackController == null)
        {
            SelectedPackController = packController;
            SelectedPackController.SetSelected(true);
            return;
        }

        if (SelectedPackController == packController)
        {
            SelectedPackController.SetSelected(false);
            SelectedPackController = null;
        }

        if (SelectedPackController != packController && SelectedPackController != null)
        {
            SelectedPackController.SetSelected(false);
            packController.SetSelected(true);
            SelectedPackController = packController;
        }
    }

    #endregion

    #region 清除模块

    private void OnRubbish()
    {
        if (SelectedPackController == null) return;
        SelectedPackController.SetEmpty();
        SelectedPackController = null;
    }

    #endregion

    #region 打包模块

    [Button("打包")]
    public void Pack(PackController packController)
    {
        if (SelectedBagController == null && SelectedFactoryItemSlots == null)
        {
            OnSelectedPackController(packController);
            return;
        }

        if (SelectedBagController != null)
        {
            SelectedBagController.SetSelected(false);
            packController.SetBag(SelectedBagController.CurrentBagType);
            SelectedBagController = null;
        }

        if (SelectedFactoryItemSlots != null)
        {
            var targetSlot = packController.GetEmptyFlySlot();
            if (targetSlot != null)
            {
                FlyItemSlotData slotData = new FlyItemSlotData(SelectedFactoryItemSlots.Color, SelectedFactoryItemSlots.Index, SelectedFactoryItemSlots.ItemInfo);
                SpawnFlyItemToTarget(SelectedFactoryItemSlots.GetFlySlot(),targetSlot,slotData, () =>
                {
                    packController.SetFlySlotData(slotData);
                });
                SelectedFactoryItemSlots.SetSelected(false);
                SelectedFactoryItemSlots = null;
                SubItem(slotData.ItemInfo,1);
            }
        }
        
    }

    #endregion

    #region 给于角色

    private void SendCharacter(ExhibitionCharacterUI characterUI)
    {
        if (SelectedPackController == null) return;
        List<FactoryMerchandiseItemInfo> itemInfo = new List<FactoryMerchandiseItemInfo>();
        ExhibitionGameData newData = new ExhibitionGameData(SelectedPackController.GetFlyItemSlotDataList());
        characterUI.SendBuyItem(newData);
        SelectedPackController.SetEmpty();
        SelectedPackController = null;
    }

    #endregion

    #region 物品刷新
    
    private void RefreshItem(List<FactoryMerchandiseItemInfo> itemInfo)
    {
        for (int i = 0; i < ExhibitionSlots.Count; i++)
        {
            if (i < itemInfo.Count)
            {
                ExhibitionSlots[i].SetData(itemInfo[i],i +1);
            }
            else
            {
                ExhibitionSlots[i].SetData(null,i +1);
            }
            ExhibitionSlots[i].SetColor(SlotColors[i % SlotColors.Count]);
            
        }
    }

    private void SubItem( FactoryMerchandiseItemInfo itemInfo, int count)
    {
        ExhibitionManager.Instance.SubFactoryItem(itemInfo,count);
    }


    #endregion


    #region 特效对象池
    
    private GameObject FlyItemPrefab;
    private Sequence flySequence;

    private void CreateExhibitionEffest()
    {
        FlyItemPrefab = AssetsManager.Instance.LoadAssets<GameObject>(AssetKeys.FlySlotPath);
        PrefabPool flyPrefabPool = new PrefabPool(FlyItemPrefab.transform)
        {
            preloadAmount = 5,
        };
        pools.CreatePrefabPool(flyPrefabPool);
    }
    
    private void ReleaseExhibitionEffest()
    {
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
        
        var flyItem = pools.Spawn(FlyItemPrefab);
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
        
        Vector3 originPosition = origin.GetRect().position;
        Vector3 targetPosition = target.GetRect().position;

        flyRect.position = new Vector3(originPosition.x,originPosition.y,0);
        flyRect.DOMove(targetPosition, 0.35f).SetEase(Ease.InQuad).OnComplete(() => {
                if (pools.IsSpawned(flyItem))
                {
                    pools.Despawn(flyItem);
                }

                complete?.Invoke();
            });
    }
    
    #endregion
    
   
}
