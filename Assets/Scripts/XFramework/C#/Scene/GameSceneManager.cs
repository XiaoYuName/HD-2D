using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework
{
    public class GameSceneManager : MonoSingleton<GameSceneManager>, ISaveable
    {
        #region ISaveable


        public SceneData GameSceneData { get; private set; }

        public string GUID => "GameSceneManager";

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        /// <summary>
        /// 存储数据
        /// </summary>
        /// <returns>GameSavaData 保存了所有要存储的数据</returns>
        public void SaveData(GameSaveData data)
        {
            if (GameSceneData != null)
            {
                data.SceneData = new SceneData(GameSceneData.SceneID, GameSceneData.WordMapSceneID);
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="data"></param>
        public void LoadData(GameSaveData data)
        {
            ReleaseGameScene();
            if (data is { SceneData: not null })
            {
                GameSceneData = new SceneData(data.SceneData.SceneID, data.SceneData.WordMapSceneID);
            }
            else
            {
                GameSceneData = new SceneData(GameDataManager.Instance.GameSettingsData.SceneID,
                    GameDataManager.Instance.GameSettingsData.SceneID);
            }

            LoadGameScene().Forget();
        }

        #endregion

        #region 场景切换

        /// <summary>
        /// 当前场景控制器
        /// </summary>
        public SceneController CurrentSceneController { get; private set; }

        public void EnterGameScene(long mapSceneID, long sceneID)
        {
            MainSceneToWordMapScene(mapSceneID, sceneID).Forget();
        }

        public void OptionGameScene(long sceneID)
        {
            OptionWordMapScene(sceneID).Forget();
        }

        public void QuitGameScene()
        {
            QuitSceneToMainScene().Forget();
        }

        /// <summary>
        /// 场景退出
        /// </summary>
        private async UniTask QuitSceneToMainScene()
        {
            await UIUtility.FadeInAsync(0.1f);

            var currentData = GetGameSceneData(GameSceneData.SceneID);
            if (currentData == null)
            {
                Debug.LogError("当前没有场景ID~");
                return;
            }

            if (CurrentSceneController != null)
            {
                CurrentSceneController.Release();
            }

            await AssetsManager.Instance.ULoadSceneUniTask(GamePathTools.CombinationScenePath(currentData.ScenePath));
            GameSceneData.SetData(-1, -1);
            await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.WordScenePath, LoadSceneMode.Additive);
            onSceneChange?.Invoke(GameSceneData);
            await UIUtility.FadeOutAsync(0.1f);
        }

        /// <summary>
        /// 场景进图
        /// </summary>
        private async UniTask MainSceneToWordMapScene(long wordMapSceneID, long sceneID)
        {
            await UIUtility.FadeInAsync(0.05f);
            await AssetsManager.Instance.ULoadSceneUniTask(AssetKeys.WordScenePath);
            if (CurrentSceneController != null)
            {
                CurrentSceneController.Release();
            }

            //世界场景特殊判断
            var SceneData = GetGameSceneData(sceneID);
            //加载新场景
            await AssetsManager.Instance.LoadSceneUniTask(GamePathTools.CombinationScenePath(SceneData.ScenePath),
                LoadSceneMode.Additive);
            GameSceneData.SetData(wordMapSceneID, SceneData.ID);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            onSceneChange?.Invoke(GameSceneData);
            await UIUtility.FadeOutAsync(0.1f);
        }

        /// <summary>
        /// 场景切换
        /// </summary>
        private async UniTask OptionWordMapScene(long sceneID)
        {
            await UIUtility.FadeInAsync(0.05f, UICanvasLayer.UIDown, 9);

            //卸载当前场景
            var currentData = GetGameSceneData(GameSceneData.SceneID);
            if (CurrentSceneController != null)
            {
                CurrentSceneController.Release();
            }

            await AssetsManager.Instance.ULoadSceneUniTask(GamePathTools.CombinationScenePath(currentData.ScenePath));

            //加载新场景
            var SceneData = GetGameSceneData(sceneID);
            if (SceneData != null)
            {
                CurrentSceneController?.Release();
                await AssetsManager.Instance.LoadSceneUniTask(GamePathTools.CombinationScenePath(SceneData.ScenePath),
                    LoadSceneMode.Additive);
                GameSceneData.SetData(GameSceneData.WordMapSceneID, SceneData.ID);
                onSceneChange?.Invoke(GameSceneData);
                CurrentSceneController = FindAnyObjectByType<SceneController>();
                CurrentSceneController?.Initialized();
                await UIUtility.FadeOutAsync(0.05f, UICanvasLayer.UIDown, 9);
            }
        }

        private void ReleaseGameScene()
        {
            if (GameSceneData == null) return;
            if (GameSceneData.SceneID == -1) return;
            var currentData = Instance.GetGameSceneData(GameSceneData.SceneID);
            if (currentData != null)
            {
                if (CurrentSceneController != null)
                {
                    CurrentSceneController.Release();
                }

                AssetsManager.Instance.ULoadScene(GamePathTools.CombinationScenePath(currentData.ScenePath));
            }
        }

        private async UniTask LoadGameScene()
        {
            if (!ContainsWordMapScene(GameSceneData.WordMapSceneID))
            {
                await UIUtility.FadeInAsync(0.1f);
                await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.WordScenePath, LoadSceneMode.Additive);
                onSceneChange?.Invoke(GameSceneData);
                await UIUtility.FadeOutAsync(0.1f);
            }
            else
            {
                var currentData = LubanManager.Instance.TbGameSceneData.Get(GameSceneData.SceneID);
                await UIUtility.FadeInAsync(0.05f, UICanvasLayer.UIDown, 9);
                await AssetsManager.Instance.LoadSceneUniTask(GamePathTools.CombinationScenePath(currentData.ScenePath),
                    LoadSceneMode.Additive);
                onSceneChange?.Invoke(GameSceneData);
                CurrentSceneController = FindAnyObjectByType<SceneController>();
                CurrentSceneController?.Initialized();
                await UIUtility.FadeOutAsync(0.05f, UICanvasLayer.UIDown, 9);
            }
        }

        #endregion

        #region 小游戏场景切换

        private MinGameSceneType minGameSceneType;

        public void EnterMinGameScene(MinGameSceneType minGameSceneType, Action Complete)
        {
            //ReleaseGameScene();
            ProcessMinGameScene(minGameSceneType,Complete).Forget();
        }

        #region 展会特殊进入

        public async UniTask EnterExhibitionPrepareSceneAsync()
        {
            ReleaseGameScene();
            this.minGameSceneType = MinGameSceneType.ExhibitionPrepareScene;
            await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.ExhibitionPrepareScenePath, LoadSceneMode.Single);
        }

        public async UniTask EnterExhibitionGameSceneAsync()
        {
            QuitMinGameScene();
            this.minGameSceneType = MinGameSceneType.ExhibitionGameScene;
            await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.ExhibitionGameScenePath, LoadSceneMode.Single);
        }

        public async UniTask QuitExhibitionGameSceneAsync()
        {
            await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.WordScenePath, LoadSceneMode.Additive);
            await AssetsManager.Instance.ULoadSceneUniTask(AssetKeys.ExhibitionGameScenePath);
        }

        #endregion
        
        private async UniTask ProcessMinGameScene(MinGameSceneType minGameSceneType,Action Complete)
        {
            await UIUtility.FadeInAsync(0.3f);
            this.minGameSceneType = minGameSceneType;
            switch (minGameSceneType)
            {
                case MinGameSceneType.ClawMachineScene:
                    await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.ClawMachinePath, LoadSceneMode.Additive);
                    break;
                case MinGameSceneType.ExhibitionPrepareScene:
                    ReleaseGameScene();
                    await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.ExhibitionPrepareScenePath, LoadSceneMode.Single);
                    break;
                case MinGameSceneType.ExhibitionGameScene:
                    ReleaseGameScene();
                    await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.ExhibitionGameScenePath, LoadSceneMode.Single);
                    break;
            }
            Complete?.Invoke();
            await UIUtility.FadeOutAsync(0.3f);
        }

        public void QuitMinGameScene()
        {
            switch (this.minGameSceneType)
            {
                case MinGameSceneType.ClawMachineScene:
                    AssetsManager.Instance.ULoadScene(AssetKeys.ClawMachinePath);
                    break;
                case  MinGameSceneType.ExhibitionPrepareScene:
                    AssetsManager.Instance.ULoadScene(AssetKeys.ExhibitionPrepareScenePath);
                    break;
                case  MinGameSceneType.ExhibitionGameScene:
                    AssetsManager.Instance.ULoadScene(AssetKeys.ExhibitionGameScenePath);
                    break;
            }
        }

        #endregion

        #region Event

        private Action<SceneData> onSceneChange;

        /// <summary>
        /// 绑定场景相关字段回调
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterSceneChange(Action<SceneData> callback)
        {
            onSceneChange += callback;
            callback?.Invoke(GameSceneData);
        }

        /// <summary>
        /// 解绑场景相关字段回调
        /// </summary>
        /// <param name="callback"></param>
        public void UnregisterSceneChange(Action<SceneData> callback)
        {
            onSceneChange -= callback;
        }

        #endregion

        #region GetData

        public bool ContainsWordMapScene(long sceneID)
        {
            return LubanManager.Instance.TbWordMapSceneData.DataMap.ContainsKey(sceneID);
        }

        public GameSceneData GetGameSceneData(long sceneID)
        {
            return LubanManager.Instance.TbGameSceneData.Get(sceneID);
        }

        public WordMapSceneData GetWordMapSceneData(long sceneID)
        {
            return LubanManager.Instance.TbWordMapSceneData.Get(sceneID);
        }


        #endregion
    }

    [System.Serializable]
    public class SceneData
    {
        [HorizontalGroup("SceneData"), LabelText("大场景ID")]
        public long WordMapSceneID { get; private set; }

        [HorizontalGroup("SceneData"), LabelText("小场景ID")]
        public long SceneID { get; private set; }

        public SceneData(long WordMapSceneID, long sceneID)
        {
            this.WordMapSceneID = WordMapSceneID;
            this.SceneID = sceneID;
        }

        public void SetData(long mapSceneID, long sceneID)
        {
            this.WordMapSceneID = mapSceneID;
            this.SceneID = sceneID;
        }
    }


}
