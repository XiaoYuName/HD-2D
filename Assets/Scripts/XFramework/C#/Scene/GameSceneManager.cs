using System;
using System.Collections.Generic;
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
        private string currentDefaultSceneUI;

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

            string currentScenePath = GetCurrentGameScenePath();
            if (string.IsNullOrEmpty(currentScenePath))
            {
                Debug.LogError("当前没有场景ID~");
                await UIUtility.FadeOutAsync(0.1f);
                return;
            }

            SuspendCurrentGameScene();
            await AssetsManager.Instance.ULoadSceneUniTask(currentScenePath);
            await ResumeGameSceneAsync(-1, -1);
            await UIUtility.FadeOutAsync(0.1f);
        }

        /// <summary>
        /// 场景进图
        /// </summary>
        private async UniTask MainSceneToWordMapScene(long wordMapSceneID, long sceneID)
        {
            await UIUtility.FadeInAsync(0.05f);
            await AssetsManager.Instance.ULoadSceneUniTask(AssetKeys.WordScenePath);
            SuspendCurrentGameScene();
            await ResumeGameSceneAsync(wordMapSceneID, sceneID);
            await UIUtility.FadeOutAsync(0.1f);
        }

        /// <summary>
        /// 场景切换
        /// </summary>
        private async UniTask OptionWordMapScene(long sceneID)
        {
            await UIUtility.FadeInAsync(0.05f, UICanvasLayer.UIDown, 9);

            //卸载当前场景
            string currentScenePath = GetCurrentGameScenePath();
            SuspendCurrentGameScene();
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                await AssetsManager.Instance.ULoadSceneUniTask(currentScenePath);
            }

            //加载新场景
            if (GetGameSceneData(sceneID) == null)
            {
                Debug.LogError($"未找到场景配置,SceneID={sceneID}");
                await UIUtility.FadeOutAsync(0.05f, UICanvasLayer.UIDown, 9);
                return;
            }

            await ResumeGameSceneAsync(GameSceneData.WordMapSceneID, sceneID);
            await UIUtility.FadeOutAsync(0.05f, UICanvasLayer.UIDown, 9);
        }

        private void ReleaseGameScene()
        {
            if (GameSceneData == null) return;

            string currentScenePath = GetCurrentGameScenePath();
            SuspendCurrentGameScene();

            // 在大地图时也要把大地图卸掉，否则读档加载新场景会和大地图叠在一起
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                AssetsManager.Instance.ULoadScene(currentScenePath);
            }
        }

        private async UniTask LoadGameScene()
        {
            bool isWordMapScene = !ContainsWordMapScene(GameSceneData.WordMapSceneID);
            if (isWordMapScene)
            {
                await UIUtility.FadeInAsync(0.1f);
            }
            else
            {
                await UIUtility.FadeInAsync(0.05f, UICanvasLayer.UIDown, 9);
            }

            await ResumeGameSceneAsync(GameSceneData.WordMapSceneID, GameSceneData.SceneID);

            if (isWordMapScene)
            {
                await UIUtility.FadeOutAsync(0.1f);
            }
            else
            {
                await UIUtility.FadeOutAsync(0.05f, UICanvasLayer.UIDown, 9);
            }
        }

        /// <summary>
        /// 当前所在游戏场景的资源路径,大地图返回大地图场景。没有场景时返回空。
        /// </summary>
        private string GetCurrentGameScenePath()
        {
            if (GameSceneData == null)
            {
                return null;
            }

            if (GameSceneData.SceneID == -1)
            {
                return AssetKeys.WordScenePath;
            }

            GameSceneData currentData = GetGameSceneData(GameSceneData.SceneID);
            return currentData == null ? null : GamePathTools.CombinationScenePath(currentData.ScenePath);
        }

        /// <summary>
        /// 挂起当前游戏场景的逻辑部分:释放场景控制器 + 关掉场景默认UI。
        /// 场景本体的卸载由调用方决定(有的走卸载,有的交给 LoadSceneMode.Single 隐式卸载)。
        /// </summary>
        private void SuspendCurrentGameScene()
        {
            if (CurrentSceneController != null)
            {
                CurrentSceneController.Release();
                CurrentSceneController = null;
            }

            CloseCurrentDefaultSceneUI();
        }

        /// <summary>
        /// 加载(恢复)游戏场景:Additive加载场景本体 + 取场景控制器 + 开场景默认UI + 抛场景变更事件。
        /// 不含渐变,渐变由调用方按自己的节奏控制。
        /// </summary>
        private async UniTask ResumeGameSceneAsync(long wordMapSceneID, long sceneID)
        {
            GameSceneData.SetData(wordMapSceneID, sceneID);

            if (!ContainsWordMapScene(wordMapSceneID))
            {
                await AssetsManager.Instance.LoadSceneUniTask(AssetKeys.WordScenePath, LoadSceneMode.Additive);
                onSceneChange?.Invoke(GameSceneData);
                return;
            }

            GameSceneData sceneData = GetGameSceneData(sceneID);
            if (sceneData == null)
            {
                Debug.LogError($"未找到场景配置,SceneID={sceneID}");
                return;
            }

            await AssetsManager.Instance.LoadSceneUniTask(GamePathTools.CombinationScenePath(sceneData.ScenePath),
                LoadSceneMode.Additive);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            OpenDefaultSceneUI(sceneData);
            onSceneChange?.Invoke(GameSceneData);
        }

        private void OpenDefaultSceneUI(GameSceneData sceneData)
        {
            if (sceneData == null || string.IsNullOrWhiteSpace(sceneData.SceneUI))
            {
                currentDefaultSceneUI = null;
                return;
            }

            currentDefaultSceneUI = sceneData.SceneUI;
            UISystem.Instance.OpenUI(currentDefaultSceneUI);
        }

        private void CloseCurrentDefaultSceneUI()
        {
            if (string.IsNullOrWhiteSpace(currentDefaultSceneUI))
            {
                return;
            }

            UISystem.Instance.CloseUI(currentDefaultSceneUI);
            currentDefaultSceneUI = null;
        }

        #endregion

        #region 小游戏场景切换

        private readonly struct MinGameSceneInfo
        {
            public readonly string Path;
            public readonly LoadSceneMode Mode;

            public MinGameSceneInfo(string path, LoadSceneMode mode)
            {
                Path = path;
                Mode = mode;
            }
        }

        /// <summary>
        /// 小游戏场景的资源路径和加载模式。
        /// Single:进入时卸载当前游戏场景,退出时恢复回来。Additive:当前游戏场景保持不动,退出也不恢复。
        /// </summary>
        private static readonly Dictionary<MinGameSceneType, MinGameSceneInfo> MinGameSceneInfos = new()
        {
            { MinGameSceneType.ClawMachineScene, new MinGameSceneInfo(AssetKeys.ClawMachinePath, LoadSceneMode.Additive) },
            { MinGameSceneType.ExhibitionPrepareScene, new MinGameSceneInfo(AssetKeys.ExhibitionPrepareScenePath, LoadSceneMode.Single) },
            { MinGameSceneType.ExhibitionGameScene, new MinGameSceneInfo(AssetKeys.ExhibitionGameScenePath, LoadSceneMode.Single) },
            { MinGameSceneType.GemSmartSlicerScene, new MinGameSceneInfo(AssetKeys.GameSmartSlicerPath, LoadSceneMode.Single) },
        };

        /// <summary>
        /// 当前所在的小游戏场景,null 表示不在小游戏里
        /// </summary>
        private MinGameSceneType? currentMinGameScene;

        /// <summary>
        /// Single 小游戏挂起前所在的游戏场景,退出小游戏时用它恢复。Additive 小游戏不记录。
        /// </summary>
        private SceneData suspendedSceneData;

        public bool IsInMinGameScene => currentMinGameScene.HasValue;

        /// <summary>
        /// 进入小游戏场景(自带渐变)
        /// </summary>
        public void EnterMinGameScene(MinGameSceneType minGameSceneType, Action Complete)
        {
            EnterMinGameSceneWithFade(minGameSceneType, Complete).Forget();
        }

        /// <summary>
        /// 退出小游戏场景。Single 小游戏会重新加载游戏场景,所以带渐变;Additive 小游戏保持原来的无渐变行为。
        /// </summary>
        public void QuitMinGameScene()
        {
            QuitMinGameSceneWithFade().Forget();
        }

        #region 展会特殊进入

        // 展会自己在 ExhibitionManager 里控制渐变和过场文字，所以直接走不带渐变的核心方法。

        public UniTask EnterExhibitionPrepareSceneAsync()
        {
            return EnterMinGameSceneAsync(MinGameSceneType.ExhibitionPrepareScene);
        }

        public UniTask EnterExhibitionGameSceneAsync()
        {
            return EnterMinGameSceneAsync(MinGameSceneType.ExhibitionGameScene);
        }

        public UniTask QuitExhibitionGameSceneAsync()
        {
            return QuitMinGameSceneAsync();
        }

        #endregion

        /// <summary>
        /// 进入小游戏场景(不带渐变)。
        /// Single:记录恢复点 → 卸载当前游戏场景 → 加载小游戏场景。
        /// Additive:当前游戏场景保持不动,直接叠加载小游戏场景。
        /// </summary>
        public async UniTask EnterMinGameSceneAsync(MinGameSceneType minGameSceneType)
        {
            if (!MinGameSceneInfos.TryGetValue(minGameSceneType, out MinGameSceneInfo info))
            {
                Debug.LogError($"未配置小游戏场景资源,MinGameSceneType={minGameSceneType}");
                return;
            }

            if (currentMinGameScene.HasValue)
            {
                await SwitchMinGameSceneAsync(currentMinGameScene.Value, minGameSceneType, info);
                return;
            }

            if (info.Mode == LoadSceneMode.Single)
            {
                // 先记录恢复点,再把当前游戏场景整套卸掉(场景控制器 + 默认UI + 场景本体)
                if (GameSceneData != null)
                {
                    suspendedSceneData = new SceneData(GameSceneData.WordMapSceneID, GameSceneData.SceneID);
                }

                string currentScenePath = GetCurrentGameScenePath();
                SuspendCurrentGameScene();
                if (!string.IsNullOrEmpty(currentScenePath))
                {
                    await AssetsManager.Instance.ULoadSceneUniTask(currentScenePath);
                }
            }

            currentMinGameScene = minGameSceneType;
            await AssetsManager.Instance.LoadSceneUniTask(info.Path, info.Mode);
        }

        /// <summary>
        /// 退出小游戏场景(不带渐变)。
        /// Single:先恢复挂起前的游戏场景,再卸载小游戏场景(顺序不能反,否则会去卸载当前唯一的场景)。
        /// Additive:只卸载小游戏场景,不做恢复。
        /// </summary>
        public async UniTask QuitMinGameSceneAsync()
        {
            if (!currentMinGameScene.HasValue)
            {
                return;
            }

            MinGameSceneInfo info = MinGameSceneInfos[currentMinGameScene.Value];
            currentMinGameScene = null;

            if (suspendedSceneData == null)
            {
                await AssetsManager.Instance.ULoadSceneUniTask(info.Path);
                return;
            }

            SceneData restoreData = suspendedSceneData;
            suspendedSceneData = null;
            await ResumeGameSceneAsync(restoreData.WordMapSceneID, restoreData.SceneID);
            await AssetsManager.Instance.ULoadSceneUniTask(info.Path);
        }

        /// <summary>
        /// 小游戏之间直接切换(展会准备场景 → 展会游戏场景):恢复点保持不变,只处理上一个小游戏场景。
        /// </summary>
        private async UniTask SwitchMinGameSceneAsync(MinGameSceneType previousType, MinGameSceneType nextType,
            MinGameSceneInfo nextInfo)
        {
            MinGameSceneInfo previousInfo = MinGameSceneInfos[previousType];
            currentMinGameScene = nextType;

            if (nextInfo.Mode == LoadSceneMode.Single)
            {
                // Single 加载会隐式卸载上一个场景。先加载再丢缓存,
                // 反过来先卸载的话上一个场景往往是当前唯一的场景,卸不掉。
                await AssetsManager.Instance.LoadSceneUniTask(nextInfo.Path, nextInfo.Mode);
                AssetsManager.Instance.DiscardSceneLoader(previousInfo.Path);
                return;
            }

            await AssetsManager.Instance.ULoadSceneUniTask(previousInfo.Path);
            await AssetsManager.Instance.LoadSceneUniTask(nextInfo.Path, nextInfo.Mode);
        }

        private async UniTask EnterMinGameSceneWithFade(MinGameSceneType minGameSceneType, Action Complete)
        {
            await UIUtility.FadeInAsync(0.3f);
            await EnterMinGameSceneAsync(minGameSceneType);
            Complete?.Invoke();
            await UIUtility.FadeOutAsync(0.3f);
        }

        private async UniTask QuitMinGameSceneWithFade()
        {
            bool needFade = IsSingleMinGameScene(currentMinGameScene);
            if (needFade)
            {
                await UIUtility.FadeInAsync(0.3f);
            }

            await QuitMinGameSceneAsync();

            if (needFade)
            {
                await UIUtility.FadeOutAsync(0.3f);
            }
        }

        private static bool IsSingleMinGameScene(MinGameSceneType? minGameSceneType)
        {
            return minGameSceneType.HasValue
                   && MinGameSceneInfos.TryGetValue(minGameSceneType.Value, out MinGameSceneInfo info)
                   && info.Mode == LoadSceneMode.Single;
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
