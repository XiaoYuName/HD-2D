using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine.SceneManagement;
using XFramework;

/// <summary>
/// 系统(Game)属性管理器
/// </summary>
public class GameDataManager : MonoSingleton<GameDataManager>,ISaveable
{
    [LabelText("玩家数据"),ReadOnly]
    public PlayerData PlayerData { get; private set; }

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
            PlayerData.minSceneID = Instance.GameSettingsData.minSceneID;
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
    }
    

    #endregion


    [FoldoutGroup("Configs"),LabelText("大场景配置表")]
    public GameSceneDataManager GameSceneData;
    
    [FoldoutGroup("Configs"),LabelText("小场景配置表")]
    public MinGameSceneDataManager MinGameSceneData;
    
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

    private const string MainScenePath = "Assets/AddressableAssets/Remote/Scenes/WordMap.unity";

    /// <summary>
    /// 当前场景控制器
    /// </summary>
    public SceneController CurrentSceneController { get; private set; }

    public void EnterGameScene(string sceneID, string minSceneID)
    {
        EnterGameSceneAsync(sceneID,minSceneID).Forget();
    }

    /// <summary>
    /// 首次进入大地图场景
    /// </summary>
    private async UniTask EnterWordMapScene()
    {
        await UIUtility.FadeInAsync(0.1f);
        await AssetsManager.Instance.LoadSceneUniTask(MainScenePath, LoadSceneMode.Single);
        onPlayerDataChanger?.Invoke(PlayerData);
        await UIUtility.FadeOutAsync(0.1f);  
    }

    /// <summary>
    /// 从小场景退回到大地图
    /// </summary>
    private async UniTask QuitSceneToMainScene()
    {
        await UIUtility.FadeInAsync(0.1f);
        var currentData = MinGameSceneData.GetDataByID(PlayerData.minSceneID);
        if (CurrentSceneController != null)
        {
            CurrentSceneController.Release();
        }
        await AssetsManager.Instance.ULoadSceneUniTask(currentData.scenePath);
        PlayerData.SceneID = string.Empty;
        await AssetsManager.Instance.LoadSceneUniTask(MainScenePath, LoadSceneMode.Single);
        onPlayerDataChanger?.Invoke(PlayerData);
        await UIUtility.FadeOutAsync(0.1f); 
    }

    /// <summary>
    /// 从大地图场景进入到小场景
    /// </summary>
    private async UniTask MainSceneToWordMapScene(string sceneID,string minSceneID)
    {
        await UIUtility.FadeInAsync(0.05f);
        await AssetsManager.Instance.ULoadSceneUniTask(MainScenePath);
        if (CurrentSceneController != null)
        {
            CurrentSceneController.Release();
        }
        //世界场景特殊判断
        var minSceneData = MinGameSceneData.GetDataByID(minSceneID);
        //加载新场景
        if (minSceneData != null)
        {
            CurrentSceneController?.Release();
            await AssetsManager.Instance.LoadSceneUniTask(minSceneData.scenePath, LoadSceneMode.Single);
            PlayerData.SceneID = sceneID;
            PlayerData.minSceneID = minSceneID;
            onPlayerDataChanger?.Invoke(PlayerData);
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
        }
        await UIUtility.FadeOutAsync(0.1f);
    }

    /// <summary>
    /// 小场景之间切换
    /// </summary>
    private async UniTask OptionWordMapScene(string sceneID,string minSceneID)
    {
        await UIUtility.FadeInAsync(0.05f,UICanvasLayer.UIDown,9);
        
        //卸载当前场景
        var currentData = MinGameSceneData.GetDataByID(PlayerData.minSceneID);
        if (CurrentSceneController != null)
        {
            CurrentSceneController.Release();
        }
        await AssetsManager.Instance.ULoadSceneUniTask(currentData.scenePath);
        
        //
        var minSceneData = MinGameSceneData.GetDataByID(minSceneID);
        //加载新场景
        if (minSceneData != null)
        {
            CurrentSceneController?.Release();
            await AssetsManager.Instance.LoadSceneUniTask(minSceneData.scenePath, LoadSceneMode.Single);
            PlayerData.SceneID = sceneID;
            PlayerData.minSceneID = minSceneID;
            onPlayerDataChanger?.Invoke(PlayerData);
            
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.05f,UICanvasLayer.UIDown,9);
        }
    }

    private async UniTask EnterGameSceneAsync(string sceneID, string minSceneID)
    {
        //0.从外面进入大地图场景
        if (string.IsNullOrEmpty(PlayerData.SceneID) && string.IsNullOrEmpty(sceneID))
        {
            await EnterWordMapScene();
            return;
        }

        //1.从场景退回到大地图场景
        if (!string.IsNullOrEmpty(PlayerData.SceneID) && string.IsNullOrEmpty(sceneID))
        {
            await QuitSceneToMainScene();
            return;
        }

        //2.从大地图进入到小场景
        if (string.IsNullOrEmpty(PlayerData.SceneID) && !string.IsNullOrEmpty(sceneID))
        {

            await MainSceneToWordMapScene(sceneID, minSceneID);
            return;
        }
        
        await OptionWordMapScene(sceneID,minSceneID);
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

    [HorizontalGroup("场景信息"),LabelText("当前所处大场景ID"),ValueDropdown("GetSceneID")]
    public string SceneID;
    
    [HorizontalGroup("场景信息"),LabelText("当前所处小场景ID"),ValueDropdown("GetMinSceneItemID")]
    public string minSceneID;

    public int GetProperty(PropertyType propertyType)
    {
        if (PropertyBag.ContainsKey(propertyType))
        {
            return PropertyBag[propertyType].Value;
        }

        return 0;
    }
    
    
    public IEnumerable GetSceneID()
    {
        if (GameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }


        var data = GameSceneDataManager.Instance.DataList
            .Select(temp => new ValueDropdownItem(temp.scene_name, temp.scene_id)).ToList();
        data.Add(new ValueDropdownItem("世界场景",""));

        return data;
    }
    
    public IEnumerable GetMinSceneItemID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        var data =MinGameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.SceneID))
            .Select(temp => new ValueDropdownItem(temp.scene_description,temp.SceneID)).ToList();
        data.Add(new ValueDropdownItem("世界场景",""));
        return data;
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
