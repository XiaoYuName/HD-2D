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
            LanguageManager.Instance.Initialized().Forget();
            ResolutionManager.Instance.Initialized().Forget();
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
            await ExcelMgr.Instance.Initialized();
            await SaveGameManager.Instance.Initialized();
           
            StarGame();
        }

        private void StarGame()
        {
            _commonUI.Open();
            _commonUI.Init();
        }

        public void EnterGame(User SelectedUser)
        {
            UISystem.Instance.CloseUI("CommonUI");
            GameDataManager.Instance.SetCurrentUser(SelectedUser);
            UISystem.Instance.OpenUI<MainUI>("MainUI");
            GameDataManager.Instance.EnterGameScene(SelectedUser.SceneID,SelectedUser.minSceneID);
        }

    }
}

