using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine.SceneManagement;
using XFramework;

/// <summary>
/// 系统(Game)数据管理器
/// </summary>
public class GameDataManager : MonoSingleton<GameDataManager>
{
    /// <summary>
    /// 当前存档用户数据
    /// </summary>
    [LabelText("当前存档用户数据"),ReadOnly]
    public User CurrentUser { get; private set; }
    
    [FoldoutGroup("Configs"),LabelText("大场景配置表")]
    public GameSceneDataManager GameSceneData;
    
    [FoldoutGroup("Configs"),LabelText("小场景配置表")]
    public MinGameSceneDataManager MinGameSceneData;
    
    [FoldoutGroup("Configs"),LabelText("游戏设置配置表")]
    public GameSettingsDataManager GameSettingsData;
    
    [FoldoutGroup("Configs"),LabelText("相册配置表")]
    public PhotoAlbumDataManager PhotoAlbumData;
    
    public void SetCurrentUser(User CurrentUser)
    {
        this.CurrentUser = CurrentUser;
    }


    #region User增删改查

    public void SetUserName(string userName)
    {
        CurrentUser.UserName = userName;
        onUserChanger?.Invoke(CurrentUser);
    }

    public void Sleep()
    {
        switch (CurrentUser.EnvironmentMode )
        {
            case EnvironmentMode.Morning:
                CurrentUser.EnvironmentMode = EnvironmentMode.Noon;
                break;
            case EnvironmentMode.Noon:
                CurrentUser.EnvironmentMode = EnvironmentMode.Evening;
                break;
            case EnvironmentMode.Evening:
                CurrentUser.EnvironmentMode = EnvironmentMode.Midnight;
                break;
            case EnvironmentMode.Midnight:
                CurrentUser.EnvironmentMode = EnvironmentMode.Morning;
                ++CurrentUser.Day;
                ++CurrentUser.Week;
                break;
            default:
                break;
        }
        onUserChanger?.Invoke(CurrentUser);
        onUserSceneChange?.Invoke(CurrentUser);
    }

    #endregion

    #region BindEvent
    private Action<User> onUserChanger;
    public void BindUserChange(Action<User> callback)
    {
        if (onUserChanger == null)
        {
            onUserChanger = callback;
        }
        else
        {
            onUserChanger += callback;
        }
        callback?.Invoke(CurrentUser);
    }

    public void UnBindUserChange(Action<User> callback)
    {
        onUserChanger -= callback;
    }

    private Action<User> onUserSceneChange;

    /// <summary>
    /// 绑定用户场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void BindUserSceneChange(Action<User> callback)
    {
        onUserSceneChange += callback;
        callback?.Invoke(CurrentUser);
    }
    
    /// <summary>
    /// 解绑用户场景相关字段回调
    /// </summary>
    /// <param name="callback"></param>
    public void UnBindUserSceneChange(Action<User> callback)
    {
        onUserSceneChange -= callback;
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
    
    private async UniTask EnterGameSceneAsync(string sceneID, string minSceneID)
    {
        //1.卸载当前场景
        await UIUtility.FadeInAsync(0.05f);
        if (string.IsNullOrEmpty(CurrentUser.SceneID))
        {
            await AssetsManager.Instance.ULoadSceneUniTask(MainScenePath);
        }
        else
        {
            var currentData = MinGameSceneData.GetDataByID(CurrentUser.minSceneID);
            if (CurrentSceneController != null)
            {
                CurrentSceneController.Release();
            }
            await AssetsManager.Instance.ULoadSceneUniTask(currentData.scenePath);
        }
        CurrentUser.SceneID = sceneID;
        //世界场景特殊判断
        var minSceneData = MinGameSceneData.GetDataByID(minSceneID);
        if (string.IsNullOrEmpty(sceneID))
        {
            await AssetsManager.Instance.LoadSceneUniTask(MainScenePath, LoadSceneMode.Single);
            onUserChanger?.Invoke(CurrentUser);
            await UIUtility.FadeOutAsync(0.1f);  
            return;
        }
        //加载新场景
        if (minSceneData != null)
        {
            CurrentSceneController?.Release();
            await AssetsManager.Instance.LoadSceneUniTask(minSceneData.scenePath, LoadSceneMode.Single);
            CurrentUser.SceneID = sceneID;
            CurrentUser.minSceneID = minSceneID;
            onUserChanger?.Invoke(CurrentUser);
            
            CurrentSceneController = FindAnyObjectByType<SceneController>();
            CurrentSceneController?.Initialized();
            await UIUtility.FadeOutAsync(0.1f);
        }
        
        
    }

    #endregion
    
    

}

[System.Serializable]
public class User
{
    /// <summary>
    /// 用户ID(存档的编号)
    /// </summary>
    public int UserID;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime;
    
    [LabelText("环境")]
    public EnvironmentMode EnvironmentMode;
    
    [LabelText("用户名")]
    public string UserName;

    [LabelText("游戏内天数")]
    public int Day;
    
    [LabelText("游戏内周数")]
    public int Week;

    [FoldoutGroup("属性"),LabelText("体力")]
    public int Strength;

    [FoldoutGroup("属性"),LabelText("行动值")]
    public int ActionPointsValue;
    
    [FoldoutGroup("属性"),LabelText("金币")]
    public int GoldNumber;

    [HorizontalGroup("场景信息"),LabelText("当前所处大场景ID"),ValueDropdown("GetSceneID")]
    public string SceneID;
    
    [HorizontalGroup("场景信息"),LabelText("当前所处小场景ID"),ValueDropdown("GetMinSceneItemID")]
    public string minSceneID;

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
        var data =MinGameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.scene_id))
            .Select(temp => new ValueDropdownItem(temp.scene_name,temp.scene_id)).ToList();
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
