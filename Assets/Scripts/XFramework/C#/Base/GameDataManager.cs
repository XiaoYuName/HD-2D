using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFramework;

/// <summary>
/// 系统(Game)属性管理器
/// </summary>
public class GameDataManager : MonoSingleton<GameDataManager>,ISaveable
{
    [LabelText("玩家数据"),ReadOnly]
    public PlayerData PlayerData { get; private set; }
    
    public const long MianSceneID = 99999;

    public GameSceneData GameSceneData { get; private set; }

    #region ISaveable

    public string GUID => "GameDataManager";

    private void Start()
    {
        ISaveable  saveable = this;
        SaveGameManager.Instance.RegisterSaveable(saveable);
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public GameSaveData GenerateSaveData()
    {
        GameSaveData saveData = new GameSaveData();
        saveData.PlayerData = PlayerData;
        return saveData;
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="GameSave"></param>
    public void RestoreData(GameSaveData GameSave)
    {
        //读档前,卸载当前场景
        ReleaseGameScene();
        if (GameSave is { PlayerData: not null })
        {
            PlayerData = GameSave.PlayerData;
        }
        else
        {
            PlayerData = new PlayerData();
            PlayerData.UserName =  SaveGameManager.Instance.SelectUserSaveSummary.UserName;
            LanguageManager.Instance.SetGlobalVariablesSource("global","PlayerName", PlayerData.UserName);
            PlayerData.Day = 1;
            PlayerData.Week = 1;
            PlayerData.SceneID = Instance.GameSettingsData.SceneID;
            PlayerData.PropertyBag = new Dictionary<PropertyType, PropertyBag>();
            foreach (var prop in LubanManager.Instance.TbPropertyData.DataList)
            {
                PlayerData.PropertyBag.Add(prop.Property,new PropertyBag()
                {
                    PropertyType = prop.Property,
                    Value = prop.DeftualNumber,
                });
            }
        }
        //读档后进入新场景
        LoadGameScene().Forget();
    }
    

    #endregion
    
    [FoldoutGroup("Configs"),LabelText("游戏设置配置表")]
    public GameSettingsDataManager GameSettingsData;
    
    [FoldoutGroup("Configs"),LabelText("相册配置表")]
    public PhotoAlbumDataManager PhotoAlbumData;


    #region PlayerData增删改查

    public PropertyBag GetProperty(PropertyType propertyType)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            return PlayerData.PropertyBag[propertyType];
        }

        return null;
    }

    public void SetPlayerName(string userName)
    {
        PlayerData.UserName = userName;
        onPlayerDataChanger?.Invoke(PlayerData);
    }

    public void Sleep()
    {
        switch (PlayerData.EnvironmentMode )
        {
            case EnvironmentMode.Morning:
                PlayerData.EnvironmentMode = EnvironmentMode.Noon;
                break;
            case EnvironmentMode.Noon:
                PlayerData.EnvironmentMode = EnvironmentMode.Evening;
                break;
            case EnvironmentMode.Evening:
                PlayerData.EnvironmentMode = EnvironmentMode.Midnight;
                break;
            case EnvironmentMode.Midnight:
                PlayerData.EnvironmentMode = EnvironmentMode.Morning;
                ++PlayerData.Day;
                SetProperty(PropertyType.ActionPointsValue,GameSettingsData.ActionPointsValueLimit );
                SetProperty(PropertyType.Strength,GameSettingsData.StrengthLimit );
                ++PlayerData.Week;
                if(PlayerData.Week > 7)
                {
                    PlayerData.Week = 1;
                    onPlayerDataWeekChange?.Invoke(PlayerData);
                }
                onPlayerDataDayChange?.Invoke(PlayerData);
                break;
            default:
                break;
        }
        onPlayerDataChanger?.Invoke(PlayerData);
        onPlayerDataSceneChange?.Invoke(PlayerData);
    }

    public void AddProperty(PropertyType propertyType, int value)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            int newValue = PlayerData.PropertyBag[propertyType].Value + value;
            var data = LubanManager.Instance.TbPropertyData.Get(propertyType);
            if (newValue <= 0)
            {
                newValue = 0;
            }
            if (newValue > data.NumberLimit)
            {
                newValue = data.NumberLimit;
            }
            PlayerData.PropertyBag[propertyType].Value = newValue;
        }
    }

    public void SetProperty(PropertyType propertyType, int value)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            int newValue = value;
            var data = LubanManager.Instance.TbPropertyData.Get(propertyType);
            if (newValue <= 0)
            {
                newValue = 0;
            }
            if (newValue > data.NumberLimit)
            {
                newValue = data.NumberLimit;
            }
            PlayerData.PropertyBag[propertyType].Value = newValue;
        }
    }

    public void RemoveProperty(PropertyType propertyType, int value)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            int newValue = PlayerData.PropertyBag[propertyType].Value - value;
            var data = LubanManager.Instance.TbPropertyData.Get(propertyType);
            if (newValue <= 0)
            {
                newValue = 0;
            }
            if (newValue > data.NumberLimit)
            {
                newValue = data.NumberLimit;
            }
            PlayerData.PropertyBag[propertyType].Value = newValue;
        }
    }

    #endregion

    #region BindEvent
    private Action<PlayerData> onPlayerDataChanger;
    public void BindPlayerDataChange(Action<PlayerData> callback)
    {
        if (onPlayerDataChanger == null)
        {
            onPlayerDataChanger = callback;
        }
        else
        {
            onPlayerDataChanger += callback;
        }
        callback?.Invoke(PlayerData);
    }

    public void UnBindPlayerDataChange(Action<PlayerData> callback)
    {
        onPlayerDataChanger -= callback;
    }

    private Action<PlayerData> onPlayerDataSceneChange;

    /// <summary>
    /// 绑定用户场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void BindPlayerDataSceneChange(Action<PlayerData> callback)
    {
        onPlayerDataSceneChange += callback;
        callback?.Invoke(PlayerData);
    }
    
    /// <summary>
    /// 解绑用户场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnBindPlayerDataSceneChange(Action<PlayerData> callback)
    {
        onPlayerDataSceneChange -= callback;
    }

    private Action<PlayerData> onPlayerDataDayChange;

    /// <summary>
    /// 注册用户日期变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void BindPlayerDataDayChange(Action<PlayerData> callback)
    {
        onPlayerDataDayChange += callback;
        callback?.Invoke(PlayerData);
    }

    /// <summary>
    /// 反注册用户日期变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnBindPlayerDataDayChange(Action<PlayerData> callback)
    {
        onPlayerDataDayChange -= callback;
    }

    private Action<PlayerData> onPlayerDataWeekChange;

    public void BindPlayerDataWeekChange(Action<PlayerData> callback)
    {
        onPlayerDataWeekChange += callback;
        onPlayerDataChanger?.Invoke(PlayerData);
    }

    public void UnBindPlayerDataWeekChange(Action<PlayerData> callback)
    {
        onPlayerDataWeekChange -= callback;
    }


    #endregion

    #region 场景切换

    /// <summary>
    /// 当前场景控制器
    /// </summary>
    public SceneController CurrentSceneController { get; private set; }

    public GameSceneData GetGameSceneData(long sceneID)
    {
        return LubanManager.Instance.TbGameSceneData.Get(sceneID);
    }

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
    
        var currentData = GetGameSceneData(PlayerData.SceneID);
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
        PlayerData.SceneID = MianSceneID;
        
        var newData = GetGameSceneData(sceneID);
        if (newData == null)
        {
            Debug.LogError("新场景ID找不到~");
            return;
        }

        await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(newData.ScenePath), LoadSceneMode.Additive);
        onPlayerDataChanger?.Invoke(PlayerData);
        
        
        
        
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
            CurrentSceneController?.Release();
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(SceneData.ScenePath), LoadSceneMode.Additive);
            PlayerData.SceneID = sceneID;
            onPlayerDataChanger?.Invoke(PlayerData);
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
        var currentData = GetGameSceneData(PlayerData.SceneID);
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
            PlayerData.SceneID = sceneID;
            onPlayerDataChanger?.Invoke(PlayerData);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.05f,UICanvasLayer.UIDown,9);
        }
    }

    private async UniTask EnterGameSceneAsync(long sceneID)
    {
        //1.从场景退回到大地图场景
        if (PlayerData.SceneID != MianSceneID && sceneID == MianSceneID)
        {
            await QuitSceneToMainScene(sceneID);
            return;
        }
        
        //2.从大地图进入到小场景
        if (PlayerData.SceneID == MianSceneID && sceneID != MianSceneID)
        {
            await MainSceneToWordMapScene(sceneID);
            return;
        }
        
        await OptionWordMapScene(sceneID);
    }

    private void ReleaseGameScene()
    {
         //卸载当前场景
         if (PlayerData == null) return;
         var currentData = Instance.GetGameSceneData(PlayerData.SceneID);
         if (currentData != null)
         {
             if (CurrentSceneController != null)
             {
                 CurrentSceneController.Release();
             }
             AssetsManager.Instance.ULoadScene(CombinationScenePath(GameSceneData.ScenePath));
         }
    }

    private async UniTask LoadGameScene()
    {
        GameSceneData = LubanManager.Instance.TbGameSceneData.Get(PlayerData.SceneID);
        if (GameSceneData == null)
        {
            Debug.LogError("没有定义的场景ID~");
            return;
        }
        if (PlayerData.SceneID == MianSceneID)
        {
            await UIUtility.FadeInAsync(0.1f);
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(GameSceneData.ScenePath),LoadSceneMode.Additive);
            onPlayerDataChanger?.Invoke(PlayerData);
            await UIUtility.FadeOutAsync(0.1f);  
        }
        else
        {
            await UIUtility.FadeInAsync(0.05f,UICanvasLayer.UIDown,9);
            await AssetsManager.Instance.LoadSceneUniTask(CombinationScenePath(GameSceneData.ScenePath), LoadSceneMode.Additive);
            onPlayerDataChanger?.Invoke(PlayerData);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.05f,UICanvasLayer.UIDown,9);
        }
    }

    private string CombinationScenePath(string scenePath)
    {
        return $"{AssetsPaths.GameScenePath}{scenePath}.unity";
    }

    public string CombinationSceneImagePath(string scenePath)
    {
        return $"{AssetsPaths.GameSceneTexturePath}{scenePath}.jpg";
    }

    #endregion
    
    
    

}

/// <summary>
/// 保存存档界面的摘要信息
/// </summary>
[System.Serializable]
public class UserSaveSummary
{
    /// <summary>
    /// 用户ID(存档的编号)
    /// </summary>
    public int UserID;
    
    [LabelText("用户名")]
    public string UserName;
    
    [LabelText("创建时间")]
    public DateTime CreateTime;
    
    [LabelText("摘要金币")]
    public int PreviewGoldNumber;
    
    [LabelText("摘要天数")]
    public int PreviewDay;
    
    [LabelText("摘要星期数")]
    public int PreviewWeek;
    

}

[System.Serializable]
public class PlayerData
{
    [LabelText("用户名")]
    public string UserName;
    [LabelText("环境")]
    public EnvironmentMode EnvironmentMode;
    [LabelText("游戏内天数")]
    public int Day;
    [LabelText("游戏内周数")]
    public int Week;
    
    [ShowInInspector,ReadOnly,LabelText("属性背包")]
    public Dictionary<PropertyType, PropertyBag> PropertyBag;

    [HorizontalGroup("场景信息"),LabelText("当前所处场景ID")]
    public long SceneID;

    public int GetProperty(PropertyType propertyType)
    {
        if (PropertyBag.ContainsKey(propertyType))
        {
            return PropertyBag[propertyType].Value;
        }

        return 0;
    }
    
}


public enum EnvironmentMode
{
    [LabelText("早上")]
    Morning = 0,
    [LabelText("中午")]
    Noon = 1,
    [LabelText("傍晚")]
    Evening = 2,
    [LabelText("半夜")]
    Midnight = 3
}

[System.Serializable]
public class PropertyBag
{
    [LabelText("属性类型")]
    public PropertyType PropertyType;
    [LabelText("属性值")]
    public int Value;
}
