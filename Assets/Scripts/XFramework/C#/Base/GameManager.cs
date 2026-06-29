using System;
using Cysharp.Threading.Tasks;
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

        /// <summary>
        /// 游戏开始事件
        /// </summary>
        public  event Action OnEnterGame;
        /// <summary>
        /// 游戏结束事件
        /// </summary>
        public event Action OnExitGame;
        
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
            await PlayerInputManager.Instance.Initialized();
            await AudioManager.Instance.Initialized();
            await UISystem.Instance.Initialized();
            await SaveGameManager.Instance.Initialized();
            await InventoryManager.Instance.Initialized();
            
            Application.targetFrameRate = -1;
            StarGame();
        }

        private void StarGame()
        {
            _commonUI.Open();
            _commonUI.Init();
        }

        public void EnterGame(UserSaveSummary selectedUserSaveSummary)
        {
            UISystem.Instance.CloseUI("CommonUI");
            OnEnterGame?.Invoke();
            UISystem.Instance.OpenUI<MainUI>("MainUI");
        }

        protected virtual void OnOnExitGame()
        {
            OnExitGame?.Invoke();
        }
    }
}

