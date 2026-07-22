using System;
using System.Collections;
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
    /// 当前可分配给新 NPC 的周边快照。
    /// 实际数量由“真实库存 + 包内库存 - NPC 待满足需求”动态计算。
    /// </summary>
    [ShowInInspector,LabelText("可分配周边"),ReadOnly]
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
        ExhibitionManager.Instance.ExhibitionGameCoinUpdate += UpdateGameCoin;
        ExhibitionManager.Instance.RegisterGameProductsUpdate(RefreshItem);
        ExhibitionManager.Instance.SuperTotalUpdate += SuperTotal;
        RefreshAvailableExhibitionItems();
        
        superSlider.minValue = 0;
        superSlider.maxValue = ExhibitionManager.Instance.ExhibitionInfoData.SuperCount;
        superSlider.value = 0;
        
        CreateExhibitionEffest();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate -= UpdateGameTimer;
        ExhibitionManager.Instance.ExhibitionGameCoinUpdate -= UpdateGameCoin;
        ExhibitionManager.Instance.SuperTotalUpdate -= SuperTotal;
        ExhibitionManager.Instance.UnRegisterGameProductsUpdate(RefreshItem);
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
        // 每次生成前都根据真实库存、包内库存和正在等待的 NPC 重新计算，
        // 避免 Pack、丢垃圾和角色离开后手动同步两份库存。
        RefreshAvailableExhibitionItems();

        if (ExhibitionItems.Count <= 0)
        {
            Debug.Log("当前没有可分配商品,暂不刷新NPC");
            return;
        }

        int index = ExhibitionCharacterSlots.FindIndex(temp => temp.State == ExhibitionState.Idle);
        if (index < 0) return;
        ExhibitionGameData exhibitionGameData = new ExhibitionGameData(GetRandomExhibitionItems(Random.Range(1,4)));
        ExhibitionCharacterSlots[index].SetData(exhibitionGameData);
    }

    /// <summary>
    /// 刷新当前可分配库存快照。
    /// Pack 时真实库存减少、包内库存增加，总可分配量不变；
    /// 丢垃圾时包内库存消失，可分配量会自然减少；
    /// NPC 离开后不再参与预占计算，可分配量会自然恢复。
    /// </summary>
    public void RefreshAvailableExhibitionItems()
    {
        ExhibitionItems = BuildAvailableExhibitionItems();
    }

    private List<FactoryMerchandiseItemInfo> BuildAvailableExhibitionItems()
    {
        var itemCounts = new Dictionary<long, int>();
        var itemTemplates = new Dictionary<long, FactoryMerchandiseItemInfo>();

        void AddItem(FactoryMerchandiseItemInfo itemInfo, int count)
        {
            if (itemInfo == null || count == 0) return;

            itemTemplates[itemInfo.ID] = itemInfo;
            if (itemCounts.ContainsKey(itemInfo.ID))
            {
                itemCounts[itemInfo.ID] += count;
            }
            else
            {
                itemCounts.Add(itemInfo.ID, count);
            }
        }

        // 1. 尚未打包的真实库存。
        if (ExhibitionManager.Instance.GameProducts != null)
        {
            foreach (var itemInfo in ExhibitionManager.Instance.GameProducts)
            {
                AddItem(itemInfo, itemInfo.Count);
            }
        }

        // 2. 已从真实库存扣除、但仍存在于包裹中的商品。
        // 每个 FlySlot 表示一个实际商品，因此这里按 1 计数，不使用 ItemInfo.Count。
        foreach (var packController in PackSlots)
        {
            foreach (var itemInfo in packController.GetFlyItemSlotDataList())
            {
                AddItem(itemInfo, 1);
            }
        }

        // 3. 等待中的 NPC 尚未满足的需求属于预占库存。
        foreach (var characterSlot in ExhibitionCharacterSlots)
        {
            if (!characterSlot.HasPendingReservation) continue;

            foreach (var itemInfo in characterSlot.NeedGameData.FactoryInfo)
            {
                AddItem(itemInfo, -itemInfo.Count);
            }
        }

        var result = new List<FactoryMerchandiseItemInfo>();
        foreach (var itemCount in itemCounts)
        {
            // 负数表示真实商品已经不足以覆盖现有 NPC，不能再分配给新 NPC。
            if (itemCount.Value <= 0) continue;

            var template = itemTemplates[itemCount.Key];
            result.Add(new FactoryMerchandiseItemInfo(
                template.ID,
                itemCount.Value,
                template.FrameItemId,
                template.PaintingItemId
            ));
        }

        return result;
    }

    /// <summary>
    /// 判断是否有空位
    /// </summary>
    /// <returns></returns>
    public bool HasIdleNpcSlot()
    {
        return ExhibitionCharacterSlots.Any(temp => temp.State == ExhibitionState.Idle);
    }

    public bool HasAllIdleNpcSlot()
    {
        return ExhibitionCharacterSlots.All(temp => temp.State == ExhibitionState.Idle);
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
        RefreshAvailableExhibitionItems();
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
                var sourceItem = SelectedFactoryItemSlots.FlySlotData.ItemInfo;
                var packedItem = new FactoryMerchandiseItemInfo(
                    sourceItem.ID,
                    1,
                    sourceItem.FrameItemId,
                    sourceItem.PaintingItemId
                );
                FlyItemSlotData slotData = new FlyItemSlotData(SelectedFactoryItemSlots.FlySlotData.Color, SelectedFactoryItemSlots.FlySlotData.Index,
                    packedItem);
                SpawnFlyItemToTarget(SelectedFactoryItemSlots.GetFlySlot(),targetSlot,slotData, () =>
                {
                    packController.SetFlySlotData(slotData);
                    RefreshAvailableExhibitionItems();
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
        if (SelectedPackController == null && isSuperTimer && !isAutoPack)
        {
            isAutoPack = true;
            StartCoroutine(AutoPack(characterUI));
            return;
        }

        if (SelectedPackController == null) return;
        if (SelectedPackController.currentBagType == BagType.None) return;
        ExhibitionGameData newData = new ExhibitionGameData(SelectedPackController.GetFlyItemSlotDataList());
        characterUI.SendBuyItem(newData);
        SelectedPackController.SetEmpty();
        SelectedPackController = null;
        RefreshAvailableExhibitionItems();
    }

    #endregion

    #region 物品刷新

    private Dictionary<long, FlyItemSlotData> GameFlyMapping = new(); 
    
    private void RefreshItem(List<FactoryMerchandiseItemInfo> itemInfo)
    {
        for (int i = 0; i < ExhibitionSlots.Count; i++)
        {
            if (i < itemInfo.Count)
            {
                if (GameFlyMapping.ContainsKey(itemInfo[i].ID))
                {
                    GameFlyMapping[itemInfo[i].ID].ItemInfo = itemInfo[i];
                    ExhibitionSlots[i].SetData(GameFlyMapping[itemInfo[i].ID]);
                }
                else
                {
                    GameFlyMapping.Add(itemInfo[i].ID, new FlyItemSlotData(SlotColors[i % SlotColors.Count]
                    ,i +1 ,itemInfo[i]));
                    ExhibitionSlots[i].SetData(GameFlyMapping[itemInfo[i].ID]);
                }
            }
            else
            {
                ExhibitionSlots[i].SetData(null);
            }
            
        }

        float totalCount = 0;
        foreach (var itemBag in itemInfo)
        {
            totalCount += itemBag.Count;
        }
        
        
        stockVal.SetVar("value",totalCount);
    }

    private void SubItem( FactoryMerchandiseItemInfo itemInfo, int count)
    {
        ExhibitionManager.Instance.SubGameProductItem(itemInfo,count);
    }

    private void SuperTotal(int total)
    {
        if (isSuperTimer) return;
        superSlider.DOKill();
        superSlider.DOValue(total, 0.1f);
    }

    public FlyItemSlotData GetMappingFlyItemSlotData(long itemID)
    {
        if (GameFlyMapping.ContainsKey(itemID))
        {
            return GameFlyMapping[itemID];
        }

        return null;
    }

    #endregion

    #region 超级时间

    private bool isSuperTimer;
    private float superTime;
    
    public void StarSuperTime()
    {
        if (!isSuperTimer)
        {
            superFarme.gameObject.SetActive(true);
            superTime = ExhibitionManager.Instance.ExhibitionInfoData.SuperTimer;
            superSlider.minValue = 0;
            superSlider.maxValue = superTime;
            superSlider.value = superTime;
            isSuperTimer = true;
            fill.Play(true);
        }
    }

    private void Update()
    {
        if (isSuperTimer)
        {
            superTime -= Time.deltaTime;
            superSlider.value = superTime;
            if (superTime <= 0)
            {
                isSuperTimer = false;
                superTime = 0;
                superSlider.minValue = 0;
                superSlider.maxValue = ExhibitionManager.Instance.ExhibitionInfoData.SuperCount;
                superSlider.value = ExhibitionManager.Instance.SuperTotal;
                superFarme.gameObject.SetActive(false);
                ExhibitionManager.Instance.StopSuperTimer();
                fill.Stop();
            }
        }
    }

    private bool isAutoPack;
    private IEnumerator AutoPack(ExhibitionCharacterUI characterUI)
    {
        isAutoPack = true;
        if (PackSlots[0].currentBagType == BagType.None)
        {
            PackSlots[0].SetBag(RandomUtil.NextBool() ? BagType.PaperBag : BagType.PlasticBag);
        }
        
        
        foreach (var itemInfo in characterUI.NeedGameData.FactoryInfo)
        {
            var exhibitionSlot = ExhibitionSlots.FirstOrDefault(temp =>
                temp.FlySlotData?.ItemInfo != null && temp.FlySlotData.ItemInfo.ID == itemInfo.ID);
            if (exhibitionSlot == null) continue;

            var targetSlot = PackSlots[0].GetEmptyFlySlot();
            if (targetSlot == null) break;

            var flySlotData = new FlyItemSlotData(
                exhibitionSlot.FlySlotData.Color,
                exhibitionSlot.FlySlotData.Index,
                itemInfo
            );
            SpawnFlyItemToTarget(exhibitionSlot.GetFlySlot(),targetSlot,flySlotData, () =>
            {
                PackSlots[0].SetFlySlotData(flySlotData);
                RefreshAvailableExhibitionItems();
            });
            SubItem(flySlotData.ItemInfo,1);

            // 等待当前物品进入包裹后再处理下一件，确保包内槽位计数和
            // 可分配库存快照在自动打包过程中保持一致。
            yield return new WaitForSeconds(0.4f);
        }

        if (characterUI.NeedGameData.isPhotograph)
        {
            characterUI.Photograph();
        }

        ExhibitionGameData newData = new ExhibitionGameData(new List<FactoryMerchandiseItemInfo>(characterUI.NeedGameData.FactoryInfo));
        characterUI.SendBuyItem(newData);

        // 包裹已经交给 NPC，不应继续作为可供其他 NPC 使用的包内库存。
        PackSlots[0].SetEmpty();
        RefreshAvailableExhibitionItems();
        yield return new WaitForSeconds(1f);
        isAutoPack = false;
    }

    #endregion


    #region 特效对象池
    
    private GameObject FlyItemPrefab;
    private Sequence flySequence;
    private GameObject CoinFlyItemPrefab;
    private GameObject FenFlyItemPrefab;

    private void CreateExhibitionEffest()
    {
        FlyItemPrefab = AssetsManager.Instance.LoadAssets<GameObject>(AssetKeys.FlySlotPath);
        CoinFlyItemPrefab = AssetsManager.Instance.LoadAssets<GameObject>(AssetKeys.CoinFlyItemPath);
        FenFlyItemPrefab = AssetsManager.Instance.LoadAssets<GameObject>(AssetKeys.FenFlyItemPath);
        PrefabPool flyPrefabPool = new PrefabPool(FlyItemPrefab.transform)
        {
            preloadAmount = 5,
        };
        PrefabPool coinPrefabPool = new PrefabPool(CoinFlyItemPrefab.transform)
        {
            preloadAmount = 5,
        };
        PrefabPool fenPrefabPool = new PrefabPool(FenFlyItemPrefab.transform)
        {
            preloadAmount = 5,
        };
        pools.CreatePrefabPool(flyPrefabPool);
        pools.CreatePrefabPool(coinPrefabPool);
        pools.CreatePrefabPool(fenPrefabPool);
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
