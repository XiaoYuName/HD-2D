using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework
{
    public class GameSceneManager : MonoSingleton<GameSceneManager>,ISaveable
    {
        #region 常量
        public const long MianSceneID = 99999;
        

        #endregion
        
        #region ISaveable


        public SceneData GameSceneData { get; private set; }

        public string GUID => "GameSceneManager";

        public void Start()
        {
            ISaveable saveable = this;
            SaveGameManager.Instance.RegisterSaveable(saveable);
            
        }

        /// <summary>
        /// 存储数据
        /// </summary>
        /// <returns>GameSavaData 保存了所有要存储的数据</returns>
        public GameSaveData GenerateSaveData()
        {
            GameSaveData data = new GameSaveData();
            if (GameSceneData != null)
            {
                data.SceneData = new SceneData(GameSceneData.SceneID,GameSceneData.WordMapSceneID);
            }
            return data;
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="GameSave"></param>
        public void RestoreData(GameSaveData GameSave)
        {
            ReleaseGameScene();
            if (GameSave is { SceneData: not null })
            {
                GameSceneData = new SceneData(GameSave.SceneData.SceneID, GameSave.SceneData.WordMapSceneID);
            }
            else
            {
                GameSceneData = new SceneData(GameDataManager.Instance.GameSettingsData.SceneID, GameDataManager.Instance.GameSettingsData.SceneID);
            }
            LoadGameScene().Forget();
        }

        #endregion
        
        #region 场景切换

        /// <summary>
    /// 当前场景控制器
    /// </summary>
        public SceneController CurrentSceneController { get; private set; }



        public void EnterGameScene(long sceneID)
        {
            EnterGameSceneAsync(sceneID).Forget();
        }

        /// <summary>
        /// 从小场景退回到大地图
        /// </summary>
        private async UniTask QuitSceneToMainScene(long sceneID)
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
        await AssetsManager.Instance.ULoadSceneUniTask(CombinationScenePath(currentData.ScenePath));
        GameSceneData.SceneID = MianSceneID;
        var newData = GetGameSceneData(sceneID);
        if (newData == null)
        {
            Debug.LogError("新场景ID找不到~");
            return;
        }
        await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(newData.ScenePath), LoadSceneMode.Additive);
        onSceneChange?.Invoke(GameSceneData);
        
        
        
        
        await UIUtility.FadeOutAsync(0.1f); 
    }

        /// <summary>
        /// 从大地图场景进入到小场景
        /// </summary>
        private async UniTask MainSceneToWordMapScene(long sceneID)
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
        if (SceneData.ID != MianSceneID)
        {
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(SceneData.ScenePath), LoadSceneMode.Additive);
            GameSceneData.SceneID = sceneID;
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
        }
        await UIUtility.FadeOutAsync(0.1f);
    }

        /// <summary>
        /// 小场景之间切换
        /// </summary>
        private async UniTask OptionWordMapScene(long sceneID)
    {
        await UIUtility.FadeInAsync(0.05f,UICanvasLayer.UIDown,9);
        
        //卸载当前场景
        var currentData = GetGameSceneData(GameSceneData.SceneID);
        
        if (CurrentSceneController != null)
        {
            CurrentSceneController.Release();
        }
        await AssetsManager.Instance.ULoadSceneUniTask(CombinationScenePath(currentData.ScenePath));
        
        //
        var SceneData = GetGameSceneData(sceneID);
        //加载新场景
        if (SceneData != null)
        {
            CurrentSceneController?.Release();
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(SceneData.ScenePath),LoadSceneMode.Additive);
            GameSceneData.SceneID = sceneID;
            onSceneChange?.Invoke(GameSceneData);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.05f,UICanvasLayer.UIDown,9);
        }
    }

        private async UniTask EnterGameSceneAsync(long sceneID)
        {
            //1.从场景退回到大地图场景
            if (GameSceneData.SceneID != MianSceneID && sceneID == MianSceneID)
        {
            await QuitSceneToMainScene(sceneID);
            return;
        }
        
            //2.从大地图进入到小场景
            if (GameSceneData.SceneID == MianSceneID && sceneID != MianSceneID)
        {
            await MainSceneToWordMapScene(sceneID);
            return;
        }
        
            await OptionWordMapScene(sceneID);
        }

        private void ReleaseGameScene()
    {
         //卸载当前场景
         if (GameSceneData == null) return;
         var currentData = Instance.GetGameSceneData(GameSceneData.SceneID);
         if (currentData != null)
         {
             if (CurrentSceneController != null)
             {
                 CurrentSceneController.Release();
             }
             AssetsManager.Instance.ULoadScene(CombinationScenePath(currentData.ScenePath));
         }
    }

        private async UniTask LoadGameScene()
    {
        var currentData = LubanManager.Instance.TbGameSceneData.Get(GameSceneData.SceneID);
        if (currentData == null)
        {
            Debug.LogError("没有定义的场景ID~");
            return;
        }
        if (currentData.ID == MianSceneID)
        {
            await UIUtility.FadeInAsync(0.1f);
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(currentData.ScenePath),LoadSceneMode.Additive);
            onSceneChange?.Invoke(GameSceneData);
            await UIUtility.FadeOutAsync(0.1f);  
        }
        else
        {
            await UIUtility.FadeInAsync(0.05f,UICanvasLayer.UIDown,9);
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(currentData.ScenePath), LoadSceneMode.Additive);
            onSceneChange?.Invoke(GameSceneData);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.05f,UICanvasLayer.UIDown,9);
        }
    }

        public string CombinationScenePath(string scenePath)
    {
        return $"{AssetsPaths.GameScenePath}{scenePath}.unity";
    }

        public string CombinationSceneImagePath(string scenePath)
        {
            return $"{AssetsPaths.GameSceneTexturePath}{scenePath}.jpg";
        }

        #endregion

        #region Event

    private Action<SceneData> onSceneChange;

    /// <summary>
    /// 绑定场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void BindSceneChange(Action<SceneData> callback)
    {
        onSceneChange += callback;
        callback?.Invoke(GameSceneData);
    }
    
    /// <summary>
    /// 解绑场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnBindSceneChange(Action<SceneData> callback)
    {
        onSceneChange -= callback;
    }

    #endregion
    
        #region GetData
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
        [HorizontalGroup("SceneData"),LabelText("大场景ID")]
        public long WordMapSceneID;
        [HorizontalGroup("SceneData"),LabelText("小场景ID")]
        public long SceneID;

        public SceneData(long WordMapSceneID,long sceneID)
        {
            this.WordMapSceneID = WordMapSceneID;
            this.SceneID = sceneID;
        }
    }

}
