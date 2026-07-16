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
        [Header("资源加载")]
        [Tooltip("编辑器下默认使用本地资源；切到 Addressables 可以预览真实 Addressables 加载链路。打包后始终使用 Addressables。")]
        [SerializeField] 
        private AssetsLoadMode assetsLoadMode = AssetsLoadMode.LocalAssetDatabase;

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
            AssetsManager.Instance.SetLoadMode(assetsLoadMode);
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
            await EffectsManager.Instance.Initialized();
            await SaveGameManager.Instance.Initialized();
            await InventoryManager.Instance.Initialized();
            await GuideManager.Instance.Initialized();
            
            Application.targetFrameRate = -1;
            StarGame();
        }

        public async UniTask Release()
        {
            await GuideManager.Instance.Release();
            await InventoryManager.Instance.Release();
            await SaveGameManager.Instance.Release();
            await EffectsManager.Instance.Release();
            await UISystem.Instance.Release();
            await AudioManager.Instance.Release();
            await PlayerInputManager.Instance.Release();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Release().Forget();
        }

        private void StarGame()
        {
            _commonUI.uiPageData = LubanManager.Instance.TbUIPageData.Get("CommonUI");
            _commonUI.Open();
            _commonUI.Init();
        }

        public void EnterGame(UserSaveSummary selectedUserSaveSummary)
        {
            UISystem.Instance.CloseUI("CommonUI");
            OnEnterGame?.Invoke();
            UISystem.Instance.OpenUI<MainUI>("MainUI");
        }

        private void OnOnExitGame()
        {
            OnExitGame?.Invoke();
        }

        #region 展会事件
        
        /// <summary>
        /// 展会开启事件
        /// </summary>
        public event Action OnStartExhibition;
        
        /// <summary>
        /// 展会关闭事件
        /// </summary>
        public event Action OnStopExhibition;


        public void StartExhibition()
        {
            OnStartExhibition?.Invoke();
            StartExhibitionAsync().Forget();
        }

        private async UniTask StartExhibitionAsync()
        {
            await UIUtility.FadeInAsync(0.3f);
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","StartExhibitionFade_00"));
            //TODO: 关闭其他所有UI,强制进入展会场景
            UISystem.Instance.CloseUI("MainUI");//暂时只是关闭了MainUI，后面需要遍历所有UI进行Close操作
            await GameSceneManager.Instance.EnterExhibitionMachineSceneAsync();
            await UIUtility.FadeLabel(LanguageManager.Instance.GetLocalizedString("Exhibition","StartExhibitionFade_01"));
            await UIUtility.FadeOutAsync(0.3f);
        }

        #endregion
    }
}

