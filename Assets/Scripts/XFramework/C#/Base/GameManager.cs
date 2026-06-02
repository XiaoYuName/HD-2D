using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 游戏总管理器
    /// </summary>
    public class GameManager : MonoOdinSingleton<GameManager>
    {
        public CommonUI _commonUI;
        
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialized().Forget();
        }
        
        public async UniTask Initialized()
        {
            await Addressables.InitializeAsync();
            await AudioManager.Instance.Initialized();
            await UISystem.Instance.Initialized();
            StarGame();
        }

        private void StarGame()
        {
            _commonUI.Init();
            AudioManager.Instance.PlayAudio("HomeBGM");
        }
        
    }
}

