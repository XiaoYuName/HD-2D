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
/// 系统(Player)管理器，负责PlayerData相关数据逻辑
/// </summary>
public class GameDataManager : MonoSingleton<GameDataManager>, ISaveable
{
    [LabelText("玩家数据"),ReadOnly]
    public PlayerData PlayerData { get; private set; }
    

    

    #region ISaveable

    public string GUID => "GameDataManager";

    private void Start()
    {
        ISaveable saveable = this;
        SaveGameManager.Instance.RegisterSaveable(saveable);
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.PlayerData = PlayerData;
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="data"></param>
    public void LoadData(GameSaveData data)
    {
        // 场景卸载已由 GameSceneManager.LoadData 负责，这里只处理 PlayerData
        if (data is { PlayerData: not null })
        {
            PlayerData = data.PlayerData;
        }
        else
        {
            PlayerData = new();
            PlayerData.UserName =  SaveGameManager.Instance.CurUserSaveSummary.UserName;
            PlayerData.Day = 1;
            PlayerData.Week = 1;
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
        LanguageManager.Instance.SetGlobalVariablesSource("global","PlayerName", PlayerData.UserName);
        LanguageManager.Instance.SetGlobalVariablesSource("global","HeartCoins", PlayerData.GetProperty(PropertyType.HeartCoins).ToString());
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

            if (data.NumberLimit > 0)
            {
                if (newValue > data.NumberLimit)
                {
                    newValue = data.NumberLimit;
                }
            }

           
            PlayerData.PropertyBag[propertyType].Value = newValue;
            onPlayerDataChanger?.Invoke(PlayerData);
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
            if (data.NumberLimit > 0)
            {
                if (newValue > data.NumberLimit)
                {
                    newValue = data.NumberLimit;
                }
            }
            PlayerData.PropertyBag[propertyType].Value = newValue;
            onPlayerDataChanger?.Invoke(PlayerData);
        }

        if (propertyType == PropertyType.HeartCoins)
        {
            LanguageManager.Instance.SetGlobalVariablesSource("global","HeartCoins", PlayerData.GetProperty(PropertyType.HeartCoins).ToString());
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
            if (data.NumberLimit > 0)
            {
                if (newValue > data.NumberLimit)
                {
                    newValue = data.NumberLimit;
                }
            }
            PlayerData.PropertyBag[propertyType].Value = newValue;
            onPlayerDataChanger?.Invoke(PlayerData);
        }
        if (propertyType == PropertyType.HeartCoins)
        {
            LanguageManager.Instance.SetGlobalVariablesSource("global","HeartCoins", PlayerData.GetProperty(PropertyType.HeartCoins).ToString());
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

    /// <summary>
    /// 浅拷贝一份摘要（字段均为值类型/字符串，浅拷贝即可）。
    /// 用于「另存到其它槽位」时，避免篡改原槽位摘要的引用。
    /// </summary>
    public UserSaveSummary Clone()
    {
        return new UserSaveSummary
        {
            UserID = UserID,
            UserName = UserName,
            CreateTime = CreateTime,
            PreviewGoldNumber = PreviewGoldNumber,
            PreviewDay = PreviewDay,
            PreviewWeek = PreviewWeek,
        };
    }
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

    public int GetProperty(PropertyType propertyType)
    {
        if (PropertyBag.ContainsKey(propertyType))
        {
            return PropertyBag[propertyType].Value;
        }

        return 0;
    }
    
    public ShowRuleWeekType GetWeekType()
    {
        switch (Week)
        {
            case 1 :
                return ShowRuleWeekType.Monday;
            case 2 :
                return ShowRuleWeekType.Tuesday;
            case 3 :
                return ShowRuleWeekType.Wednesday;
            case 4 :
                return ShowRuleWeekType.Thursday;
            case 5 :
                return ShowRuleWeekType.Friday;
            case 6 :
                return ShowRuleWeekType.Saturday;
            case 7 :
                return ShowRuleWeekType.Sunday;
            default:
                return ShowRuleWeekType.Monday;
        }
    }

    public ShowRuleTimeType GetTimeType()
    {
        switch (EnvironmentMode)
        {
            case EnvironmentMode.Morning:
               return ShowRuleTimeType.Morning;
            case EnvironmentMode.Noon:
                return ShowRuleTimeType.Noon;
            case EnvironmentMode.Evening:
                return ShowRuleTimeType.Evening;
            case EnvironmentMode.Midnight:
                return ShowRuleTimeType.Midnight;
            default:
                return ShowRuleTimeType.All;
        }
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
