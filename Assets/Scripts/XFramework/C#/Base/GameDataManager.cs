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


    #endregion

    #region 场景切换

    private const string MainScenePath = "Assets/AddressableAssets/Remote/Scenes/WordMap.unity";

    public void EnterGameScene(string sceneID)
    {
        
        
        //卸载当前场景
        if (!string.IsNullOrEmpty(CurrentUser.SceneID))
        {
            var currentData = MinGameSceneData.GetDataByID(CurrentUser.minSceneID);
            AssetsManager.Instance.ULoadScene(currentData.scenePath);
        }
        
        CurrentUser.SceneID = sceneID;
        var data = GameSceneData.GetDataByID(sceneID);

        if (string.IsNullOrEmpty(sceneID))
        {
            CurrentUser.SceneID = sceneID;
            AssetsManager.Instance.LoadScene(MainScenePath,LoadSceneMode.Single);
            onUserChanger?.Invoke(CurrentUser);
            return;
        }
        if (data != null)
        {
            EnterGameScene(sceneID,data.min_sceneList[0]);
        }
    }

    public void EnterGameScene(string sceneID, string minSceneID)
    {
        CurrentUser.SceneID = sceneID;
        CurrentUser.minSceneID = minSceneID;

        var minSceneData = MinGameSceneDataManager.Instance.GetDataByID(minSceneID);
        if (minSceneData != null)
        {
            AssetsManager.Instance.LoadScene(minSceneData.scenePath, LoadSceneMode.Single);
        }
        onUserChanger?.Invoke(CurrentUser);
        
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
    [LabelText("白天")]
    Day = 0,
    [LabelText("夜晚")]
    Night = 1,
}
