using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assets.Scripts.Utils;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 展会总管理器
    /// </summary>
    public class ExhibitionManager : MonoSingleton<ExhibitionManager>
    {
        #region Event

        /// <summary>
        /// 展会开启事件
        /// </summary>
        public event Action OnStartExhibition;
        
        /// <summary>
        /// 展会关闭事件
        /// </summary>
        public event Action OnStopExhibition;

        #endregion

        #region 展会上架周边数据

        public List<FactoryMerchandiseItemInfo> OnSelectedFactory { get; private set; } = new();

        private Action<List<FactoryMerchandiseItemInfo>> OnSelectedFactoryUpdate;

        public void RegisterSelectedFactoryUpdate(Action<List<FactoryMerchandiseItemInfo>> callback)
        {
            OnSelectedFactoryUpdate += callback;
            callback?.Invoke(OnSelectedFactory);
        }

        public void UnRegisterSelectedFactoryUpdate(Action<List<FactoryMerchandiseItemInfo>> callback)
        {
            OnSelectedFactoryUpdate -= callback;
        }

        public void SetSelectedFactory(List<FactoryMerchandiseItemInfo> items)
        {
            OnSelectedFactory = new List<FactoryMerchandiseItemInfo>(items);
            OnSelectedFactoryUpdate?.Invoke(OnSelectedFactory);
        }

        public void SubFactoryItem(FactoryMerchandiseItemInfo FactoryMerchandiseItemInfo)
        {
            if (OnSelectedFactory.Any(temp => temp == FactoryMerchandiseItemInfo))
            {
                int index = OnSelectedFactory.FindIndex(temp => temp == FactoryMerchandiseItemInfo);
                OnSelectedFactory.RemoveAt(index);
                OnSelectedFactoryUpdate?.Invoke(OnSelectedFactory);
            }
        }

     

        public void AutoAddFactoryList()
        {
            OnSelectedFactory.Clear();
            InventoryManager.Instance.GetRuntimeList().ForEach(item => {
                    if (OnSelectedFactory.Count >= 10) return;
                    if (item is FactoryMerchandiseItemInfo factoryItemInfo)
                    {
                        OnSelectedFactory.Add(factoryItemInfo);
                    }
            });
            OnSelectedFactoryUpdate?.Invoke(OnSelectedFactory);
        }

        #endregion

        #region 展会准备
        /// <summary>
        /// 当前报名的展会数据
        /// </summary>
        public ExhibitionInfoData ExhibitionInfoData { get; private set; }
        
        /// <summary>
        /// 开始展会
        /// </summary>
        public void StartPrepareExhibition()
        {
            ExhibitionInfoData = OnLineGameManager.Instance.GetRecentExhibitionInfo();
            OnStartExhibition?.Invoke();
            StartPrepareExhibitionAsync().Forget();
        }
        
        private async UniTask StartPrepareExhibitionAsync()
        {
            await UIUtility.FadeInAsync(0.3f);
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","StartExhibitionFade_00"));
            //TODO: 关闭其他所有UI,强制进入展会场景
            if (InventoryManager.Instance.GetFactoryMerchandiseList().Count <= 0)
            {
                await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","QuitExhibitionFade_02"));
                await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","QuitExhibitionFade_01"));  
                await UIUtility.FadeOutAsync(0.3f);
                return;
            }
            

            UISystem.Instance.CloseUI("MainUI");//暂时只是关闭了MainUI，后面需要遍历所有UI进行Close操作
            await GameSceneManager.Instance.EnterExhibitionPrepareSceneAsync();
            UISystem.Instance.OpenUI<BoothGameStartUI>("BoothGameStartUI");
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","StartExhibitionFade_01"));
            await UIUtility.FadeOutAsync(0.3f);
        }
        
        #endregion
        
        #region 进入展会
        public void EnterExhibition()
        {
            EnterExhibitionGameScene().Forget();
        }

        public async UniTask EnterExhibitionGameScene()
        {
            await UIUtility.FadeInAsync(0.3f);
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","StartExhibitionFade_02")); 
            await GameSceneManager.Instance.EnterExhibitionGameSceneAsync();
            _tokenSource = new CancellationTokenSource();
            GameProducts = OnSelectedFactory.Select(item => new FactoryMerchandiseItemInfo(
                    item.ID,
                    item.Count,
                    item.FrameItemId,
                    item.PaintingItemId
                ))
                .ToList();
            SoldItems = new List<FactoryMerchandiseItemInfo>();
            exhibitionGameUI= UISystem.Instance.OpenUI<ExhibitionGameUI>("ExhibitionGameUI");
            CustomTotal = 0;
            SuperTotal = 0;
            isSuperTimer = false;
            
            await UIUtility.FadeOutAsync(0.3f);
            CountdownGameTime().Forget();
        }
        
        #endregion

        #region 退出展会

        public void QuitExhibition()
        {
            QuitExhibitionScene().Forget();
        }

        private async UniTask QuitExhibitionScene()
        {
            await UIUtility.FadeInAsync(0.3f);
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","QuitExhibitionFade_01")); 
            await GameSceneManager.Instance.QuitExhibitionGameSceneAsync();
            GameProducts.Clear();
            SoldItems.Clear();
            OnStopExhibition?.Invoke();
            OnSelectedFactory.Clear();
            OnSelectedFactoryUpdate?.Invoke(OnSelectedFactory);
            UISystem.Instance.CloseUI("ExhibitionGameUI");
            UISystem.Instance.CloseUI("");
            UISystem.Instance.OpenUI("MainUI");
            await UIUtility.FadeOutAsync(0.3f);
        }

        #endregion

        #region 游戏数据

        private CancellationTokenSource _tokenSource;

        #region 上架周边

        /// <summary>
        /// 上架的商品数据
        /// </summary>
        public List<FactoryMerchandiseItemInfo> GameProducts { get; private set; } =new();

        /// <summary>
        /// 已销售的物品列表
        /// </summary>
        public List<FactoryMerchandiseItemInfo> SoldItems { get; private set; } = new();

        private Action<List<FactoryMerchandiseItemInfo>> GameProductsUpdate;
        
        public void RegisterGameProductsUpdate(Action<List<FactoryMerchandiseItemInfo>> callback)
        {
            GameProductsUpdate += callback;
            callback?.Invoke(GameProducts);
        }

        public void UnRegisterGameProductsUpdate(Action<List<FactoryMerchandiseItemInfo>> callback)
        {
            GameProductsUpdate -= callback;
        }
        
        public void SubGameProductItem(FactoryMerchandiseItemInfo FactoryMerchandiseItemInfo, int count)
        {
            if (GameProducts.Any(temp => temp.ID == FactoryMerchandiseItemInfo.ID))
            {
                int index = GameProducts.FindIndex(temp => temp.ID == FactoryMerchandiseItemInfo
                    .ID);
                GameProducts[index].Count -= count;
                if (GameProducts[index].Count <= 0)
                {
                    GameProducts.RemoveAt(index);
                }
                GameProductsUpdate?.Invoke(GameProducts);
            }
        }

        public void AddSoldItems(FactoryMerchandiseItemInfo FactoryMerchandiseItemInfo, int count)
        {
            if (SoldItems.Any(temp => temp.ID == FactoryMerchandiseItemInfo.ID))
            {
                int index = SoldItems.FindIndex(temp => temp.ID == FactoryMerchandiseItemInfo
                    .ID);
                SoldItems[index].Count += count;
                if (SoldItems[index].Count <= 0)
                {
                    SoldItems.RemoveAt(index);
                }
            }
            else
            {
                SoldItems.Add(FactoryMerchandiseItemInfo);
            }
        }

        #endregion
        
        public ExhibitionGameUI exhibitionGameUI { get; private set; }

        /// <summary>
        /// 当前游戏剩余时间
        /// </summary>
        public float ExhibitionGameTimer { get; private set; }
        /// <summary>
        /// 刷新间隔
        /// </summary>
        private float updateInterval;

        /// <summary>
        /// 获取金币数
        /// </summary>
        public int CoinNumber { get; private set; }

        /// <summary>
        /// 接待总数
        /// </summary>
        public int CustomTotal { get; private set; }

        /// <summary>
        /// 当前轮次的接待总数
        /// </summary>
        public int SuperTotal { get; private set; }

        /// <summary>
        /// 当前是否是超级时间
        /// </summary>
        public bool isSuperTimer { get; private set; }

        /// <summary>
        /// 游戏时间倒计时
        /// </summary>
        public event Action<float> ExhibitionGameTimerUpdate;
        /// <summary>
        /// 游戏金币数
        /// </summary>
        public event Action<int> ExhibitionGameCoinUpdate;
        /// <summary>
        /// 接待顾客总数
        /// </summary>
        public event Action<int> SuperTotalUpdate;

        public async UniTask CountdownGameTime()
        {
            ExhibitionGameTimer = ExhibitionInfoData.GameTime;
            ExhibitionGameTimerUpdate?.Invoke(ExhibitionGameTimer);
            CoinNumber = 0;
            ExhibitionGameCoinUpdate?.Invoke(CoinNumber);
            updateInterval = 1.5F; //首个客人时间短点
            
            while (!_tokenSource.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1),cancellationToken: _tokenSource.Token);
                ExhibitionGameTimer--;
                ExhibitionGameTimerUpdate?.Invoke(ExhibitionGameTimer);
                if (exhibitionGameUI.HasIdleNpcSlot())
                {
                    updateInterval--;
                }
                if (updateInterval <= 0)
                {
                    updateInterval = UnityEngine.Random.Range(ExhibitionInfoData.UpdateInterval.X,
                        ExhibitionInfoData.UpdateInterval.Y);
                    exhibitionGameUI.GenerateNpcExhibition();
                }
                if (ExhibitionGameTimer <= 0f)
                {
                    ExhibitionGameTimer = 0;
                    break;
                }

               
            }
        }
        
        /// <summary>
        /// 获取对应UI映射的数据
        /// </summary>
        /// <param name="factoryInfo"></param>
        /// <returns></returns>
        public FlyItemSlotData GetMappingFlyItemSlotData(FactoryMerchandiseItemInfo factoryInfo)
        {
            return exhibitionGameUI.GetMappingFlyItemSlotData(factoryInfo.ID);
        }

        /// <summary>
        /// 将选中物品打包到打包区域
        /// </summary>
        /// <param name="packController"></param>
        public void Pack(PackController packController)
        {
            exhibitionGameUI.Pack(packController);
        }

        public void AddCoin(int coinNumber)
        {
            this.CoinNumber += coinNumber;
            ExhibitionGameCoinUpdate?.Invoke(CoinNumber);
        }
        
        public void AddCustomerTotal(int customerTotal)
        {
            CustomTotal += customerTotal;
           
            if (!isSuperTimer)
            {
                SuperTotal += customerTotal;
                if (SuperTotal > ExhibitionInfoData.SuperCount)
                {
                    isSuperTimer = true;
                    StartSuperTimer();
                    SuperTotal = 0;
                }
            }
            SuperTotalUpdate?.Invoke(SuperTotal);
        }

        public void CheckGameEnd()
        {
            if (exhibitionGameUI.HasAllIdleNpcSlot() && GameProducts.Count <= 0  || ExhibitionGameTimer <= 0f)
            {
                Debug.LogError("游戏已经彻底结束!");
                if (_tokenSource != null)
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                    _tokenSource = null;
                }

                var ui = UISystem.Instance.OpenUI<PopExhibitionSettlementUI>("PopExhibitionSettlementUI");
                ui.SetData(CoinNumber,CustomTotal,ExhibitionInfoData.GoodwillValue,SoldItems);
                ExhibitionGameTimer = 0;
            }
        }
        
        

        #endregion

        #region 超级时间

        private void StartSuperTimer()
        {
            exhibitionGameUI.StarSuperTime();
        }

        public void StopSuperTimer()
        {
            isSuperTimer = false;
            SuperTotal = 0;
            SuperTotalUpdate?.Invoke(SuperTotal);
        }

        #endregion
        
    }

    [System.Serializable]
    public class ExhibitionGameData
    {
        //1.所需周边货物
        public List<FactoryMerchandiseItemInfo>  FactoryInfo { get; private set; }
        //2.是否有拍照
        public bool isPhotograph { get; private set; } = RandomUtil.NextBool();

        public ExhibitionGameData(List<FactoryMerchandiseItemInfo> factoryInfo)
        {
            FactoryInfo = factoryInfo;
        }
    }
    
    [System.Serializable]
    public class FlyItemSlotData
    {
        public Color Color { get; private set; }
        public int Index  { get; private set; }
        public FactoryMerchandiseItemInfo ItemInfo { get; set; }

        public FlyItemSlotData(Color color,int index,FactoryMerchandiseItemInfo itemInfo)
        {
            Color = color;
            Index = index;
            ItemInfo = itemInfo;
        }
    }
}

