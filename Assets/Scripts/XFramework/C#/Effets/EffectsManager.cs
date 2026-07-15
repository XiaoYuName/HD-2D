using Cysharp.Threading.Tasks;
using DamageNumbersPro;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class EffectsManager : MonoSingleton<EffectsManager>,IGameInitialized
    {
        #region DamageNumber

        /// <summary>
        /// 金币数
        /// </summary>
        public DamageNumberGUI coinDamageNumberGUI { get; private set; }

        /// <summary>
        /// 粉丝数
        /// </summary>
        public DamageNumberGUI fenDamageNumberGUI  { get; private set; }

        #endregion
        
        #region Initialized

        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            coinDamageNumberGUI = await AssetsManager.Instance.LoadAssetsUniTask<DamageNumberGUI>(AssetKeys.CoinNumberTexUGUIPath);
            fenDamageNumberGUI = await AssetsManager.Instance.LoadAssetsUniTask<DamageNumberGUI>(AssetKeys.FenNumberTexUGUIPath);
        }

        public async UniTask Release()
        {
            AssetsManager.Instance.FreeAsset(AssetKeys.CoinNumberTexUGUIPath);
            AssetsManager.Instance.FreeAsset(AssetKeys.FenNumberTexUGUIPath);
            await UniTask.CompletedTask;
        }

        #endregion
        
        

    }
}

