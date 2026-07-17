using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<FactoryMerchandiseItemInfo> OnSelectedFactory = new List<FactoryMerchandiseItemInfo>();

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

        public void SubFactoryItem(FactoryMerchandiseItemInfo FactoryMerchandiseItemInfo, int count)
        {
            if (OnSelectedFactory.Any(temp => temp == FactoryMerchandiseItemInfo))
            {
                int index = OnSelectedFactory.FindIndex(temp => temp == FactoryMerchandiseItemInfo);
                OnSelectedFactory[index].Count -= count;
                if (OnSelectedFactory[index].Count <= 0)
                {
                    OnSelectedFactory.RemoveAt(index);
                }
                OnSelectedFactoryUpdate?.Invoke(OnSelectedFactory);
            }
        }

        #endregion
        
        /// <summary>
        /// 当前报名的展会数据
        /// </summary>
        public ExhibitionInfoData ExhibitionInfoData { get; private set; }
        
        /// <summary>
        /// 开始展会
        /// </summary>
        public void StartExhibition()
        {
            ExhibitionInfoData = OnLineGameManager.Instance.GetRecentExhibitionInfo();
            OnStartExhibition?.Invoke();
        }
    }
}

