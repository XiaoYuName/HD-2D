using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
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
            UISystem.Instance.OpenUIAsync<ExhibitionGameUI>("ExhibitionGameUI");
            await UIUtility.FadeOutAsync(0.3f);
            CountdownGameTime().Forget();
        }

        

        #endregion

        #region 游戏数据

        private CancellationTokenSource _tokenSource;

        private float ExhibitionGameTimer;
        
        /// <summary>
        /// 游戏时间倒计时
        /// </summary>
        public event Action<float> ExhibitionGameTimerUpdate;

        public async UniTask CountdownGameTime()
        {
            ExhibitionGameTimer = ExhibitionInfoData.GameTime;
            ExhibitionGameTimerUpdate?.Invoke(ExhibitionGameTimer);
            await UniTask.Delay(TimeSpan.FromSeconds(1),cancellationToken: _tokenSource.Token);
            while (!_tokenSource.IsCancellationRequested)
            {
                ExhibitionGameTimer--;
                ExhibitionGameTimerUpdate?.Invoke(ExhibitionGameTimer);
                if (ExhibitionGameTimer <= 0f)
                {
                    ExhibitionGameTimer = 0;
                    break;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(1),cancellationToken: _tokenSource.Token);
            }
        }

        #endregion
    }
}

