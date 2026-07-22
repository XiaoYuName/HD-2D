using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 系统(Player)管理器，负责PlayerData相关数据逻辑
/// </summary>
public class GameDataManager : MonoSingleton<GameDataManager>, ISaveable
{
    [LabelText("玩家数据"), ShowInInspector]
    public PlayerData PlayerData { get; private set; }
    #region Get
    public TimeSlot CurTimeSlot => PlayerData.TimeSlot;
    #endregion
    #region ISaveable

    public string GUID => "GameDataManager";
    private void Start()
    {
        ((ISaveable)this).RegisterSaveable();
    }

    /// <summary>
    /// 存储数据
    /// </summary>
    /// <returns>GameSavaData 保存了所有要存储的数据</returns>
    public void SaveData(GameSaveData data)
    {
        data.PlayerData = new PlayerData
        {
            UserName = PlayerData.UserName,
            TimeSlot = PlayerData.TimeSlot,
            Day = PlayerData.Day,
            Week = PlayerData.Week,
            PropertyBag = PlayerData.PropertyBag.ToDictionary(
                kvp => kvp.Key,
                kvp => new PropertyBag { PropertyType = kvp.Value.PropertyType, Value = kvp.Value.Value })
        };
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
            
            long timestamp = new DateTimeOffset(
                2026, 1, 1,
                0, 0, 0,
                TimeSpan.FromHours(8)
            ).ToUnixTimeSeconds();
            PlayerData.GameDateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
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

    #region Configs

    [FoldoutGroup("Configs"),LabelText("游戏设置配置表")]
    public GameSettingsDataManager GameSettingsData;
    
    [FoldoutGroup("Configs"),LabelText("相册配置表")]
    public PhotoAlbumDataManager PhotoAlbumData;
    
    [FoldoutGroup("Configs"),LabelText("线上玩法配置表")]
    public OnLineGameData onLineGameData;


    #endregion

    #region 奖励表

    public RewardData GetRewardData(long rewardID)
    {
        RewardData reward = null;
        try
        {
            reward = LubanManager.Instance.TbRewardData.Get(rewardID);
        }
        catch (Exception e)
        {
            Debug.LogError($"没有找到对应的奖励表数据 {rewardID} Message: {e.Message}");
            return null;
        }

        return reward;
    }

    #endregion
    
    #region PlayerData增删改查

    public PropertyBag GetProperty(PropertyType propertyType)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            return PlayerData.PropertyBag[propertyType];
        }

        return null;
    }

    public PropertyData GetPropertyData(PropertyType propertyType)
    {
        return LubanManager.Instance.TbPropertyData.Get(propertyType);
    }

    /// <summary>属性的「当前值/上限」展示文本，如 "5/5"。</summary>
    public string GetPropertyText(PropertyType propertyType)
    {
        return $"{GetProperty(propertyType).Value}/{GetPropertyData(propertyType).NumberLimit}";
    }

    public TbLocalzationKeyData GetPropertyNameKey(PropertyType propertyType)
    {
        return GetPropertyData(propertyType)?.Name;
    }

    public void SetPlayerName(string userName)
    {
        PlayerData.UserName = userName;
        onPlayerDataChanger?.Invoke(PlayerData);
    }

    public void Sleep()
    {
        switch (PlayerData.TimeSlot )
        {
            case TimeSlot.Morning:
                PlayerData.TimeSlot = TimeSlot.Noon;
                SetProperty(PropertyType.ActionPointsValue,GetPropertyData(PropertyType.ActionPointsValue).DeftualNumber);
                onPlayerDataTimeSlotChangeDay?.Invoke(PlayerData);
                break;
            case TimeSlot.Noon:
                PlayerData.TimeSlot = TimeSlot.Evening;
                SetProperty(PropertyType.ActionPointsValue,GetPropertyData(PropertyType.ActionPointsValue).DeftualNumber);
                onPlayerDataTimeSlotChangeDay?.Invoke(PlayerData);
                break;
            case TimeSlot.Evening:
                PlayerData.TimeSlot = TimeSlot.Midnight;
                SetProperty(PropertyType.ActionPointsValue,GetPropertyData(PropertyType.ActionPointsValue).DeftualNumber);
                onPlayerDataTimeSlotChangeDay?.Invoke(PlayerData);
                break;
            case TimeSlot.Midnight:
                PlayerData.TimeSlot = TimeSlot.Morning;
                ++PlayerData.Day;
                ++PlayerData.Week;
                if(PlayerData.Week > 7)
                {
                    PlayerData.Week = 1;
                    onPlayerDataWeekChange?.Invoke(PlayerData);
                }
                SetProperty(PropertyType.ActionPointsValue,GetPropertyData(PropertyType.ActionPointsValue).DeftualNumber);
                SetProperty(PropertyType.Strength,GetPropertyData(PropertyType.Strength).DeftualNumber);
                PlayerData.GameDateTime += new TimeSpan(1, 0, 0, 0, 0);
                onPlayerDataTimeSlotChangeDay?.Invoke(PlayerData);
                onPlayerDataDayChange?.Invoke(PlayerData);
                break;
        }
        
        if (PlayerData.Week == 7 && PlayerData.TimeSlot == TimeSlot.Morning)
        {
            ExhibitionManager.Instance.StartPrepareExhibition();
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

        if (propertyType == PropertyType.ActionPointsValue)
        {
            if (GetProperty(PropertyType.ActionPointsValue).Value <= 0)
            {
                Sleep();
            }
        }
    }

    public bool HasProperty(PropertyType propertyType, int value)
    {
        if (PlayerData.PropertyBag.ContainsKey(propertyType))
        {
            if (PlayerData.PropertyBag[propertyType].Value >= value)
                return true;
        }

        return false;
    }
    #region 添加上限属性
    // public void AddPropertyLimit(PropertyType pt, int value)
    // {
    //     if(pt == PropertyType.Strength)
    //     {
    //         PlayerData.PropertyBag[pt].Value += value;
    //         PlayerData.PropertyBag[pt].Value
    //     }
  
    //     var data = LubanManager.Instance.TbPropertyData.Get(propertyType);
    //     if (newValue <= 0)
    //     {
    //         newValue = 0;
    //     }
    // }
    #endregion
    
    
    #endregion

    #region BindEvent
    private Action<PlayerData> onPlayerDataChanger;
    public void RegisterPlayerDataChange(Action<PlayerData> callback)
    {
        onPlayerDataChanger += callback;
        callback?.Invoke(PlayerData);
    }

    public void UnregisterPlayerDataChange(Action<PlayerData> callback)
    {
        onPlayerDataChanger -= callback;
    }
    
    private Action<PlayerData> onPlayerDataDayChange;

    /// <summary>
    /// 注册用户日期变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void RegisterPlayerDataDayChange(Action<PlayerData> callback)
    {
        onPlayerDataDayChange += callback;
        callback?.Invoke(PlayerData);
    }

    /// <summary>
    /// 反注册用户日期变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnregisterPlayerDataDayChange(Action<PlayerData> callback)
    {
        onPlayerDataDayChange -= callback;
    }

    private Action<PlayerData> onPlayerDataWeekChange;

    public void RegisterPlayerDataWeekChange(Action<PlayerData> callback)
    {
        onPlayerDataWeekChange += callback;
        callback?.Invoke(PlayerData);
    }

    public void UnregisterPlayerDataWeekChange(Action<PlayerData> callback)
    {
        onPlayerDataWeekChange -= callback;
    }
    
    private Action<PlayerData> onPlayerDataTimeSlotChangeDay;

    /// <summary>
    /// 注册时间段变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void RegisterPlayerDataTimeSlotChange(Action<PlayerData> callback)
    {
        onPlayerDataTimeSlotChangeDay += callback;
        callback?.Invoke(PlayerData);
    }

    /// <summary>
    /// 反注册时间段变化回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnregisterPlayerDataTimeSlotChange(Action<PlayerData> callback)
    {
        onPlayerDataTimeSlotChangeDay -= callback;
        callback?.Invoke(PlayerData);
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
    public TimeSlot TimeSlot;
    [LabelText("游戏内天数")]
    public int Day;
    [LabelText("游戏内周数")]
    public int Week;
    [LabelText("游戏内当前时间")]
    public DateTime GameDateTime;
    
    [ShowInInspector, LabelText("属性背包")]
    public Dictionary<PropertyType, PropertyBag> PropertyBag;

    public int GetProperty(PropertyType propertyType)
    {
        if (PropertyBag.ContainsKey(propertyType))
        {
            return PropertyBag[propertyType].Value;
        }

        return 0;
    }
    
    /// <summary>
    /// 计算粉丝增长系数
    /// </summary>
    /// <param name="baseFanGain"></param>
    /// <returns></returns>
    public int CalculateFanGain(int baseFanGain)
    {
        return Mathf.RoundToInt(baseFanGain * GetFanGrowthRate() / 100f);
    }
    
    private int GetFanGrowthRate()
    {
        int value = GetProperty(PropertyType.FanGrowthRate);
        return value <= 0 ? 100 : value;
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
        switch (TimeSlot)
        {
            case TimeSlot.Morning:
               return ShowRuleTimeType.Morning;
            case TimeSlot.Noon:
                return ShowRuleTimeType.Noon;
            case TimeSlot.Evening:
                return ShowRuleTimeType.Evening;
            case TimeSlot.Midnight:
                return ShowRuleTimeType.Midnight;
            default:
                return ShowRuleTimeType.All;
        }
    }

    public LocalSelectedData GetTimeSlotText(TimeSlot mode)
    {
        return new LocalSelectedData()
        {
            Table = "EnumsText",
            Value = mode.ToString(),
        };
    }

}


public enum TimeSlot
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
