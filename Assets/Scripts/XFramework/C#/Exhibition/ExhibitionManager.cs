using System;
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

