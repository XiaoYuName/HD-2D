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

        public  event Action OnEnterGame;
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
            GameDataManager.Instance.EnterGameScene(GameDataManager.Instance.PlayerData.SceneID,GameDataManager.Instance.PlayerData.minSceneID);
        }
        
    }
}

