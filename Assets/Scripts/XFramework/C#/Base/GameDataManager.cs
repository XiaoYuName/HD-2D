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

public class GameDataManager : MonoSingleton<GameDataManager>,IGameInitialized
{
    /// <summary>
    /// 当前存档用户数据
    /// </summary>
    [LabelText("当前存档用户数据"),ReadOnly]
    public User CurrentUser { get; private set; }

    [LabelText("默认用户配置")] 
    public User DefaultUser;

    [FoldoutGroup("Configs"),LabelText("大场景配置表")]
    public GameSceneDataManager GameSceneData;
    
    [FoldoutGroup("Configs"),LabelText("小场景配置表")]
    public MinGameSceneDataManager MinGameSceneData;

    /// <summary>
    /// 初始化脚本函数
    /// </summary>
    /// <returns></returns>
    public async UniTask Initialized()
    {
        await LoadSaveFile();
       
    }

    private async UniTask LoadSaveFile()
    {
        //TODO: 存档加载
        CurrentUser = DefaultUser;
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// 释放脚本函数
    /// </summary>
    public async UniTask Release()
    {
        await UniTask.CompletedTask;
    }

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

    public void EnterGameScene(string sceneID)
    {
        CurrentUser.SceneID = sceneID;
        var data = GameSceneData.GetDataByID(sceneID);
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
            UISystem.Instance.OpenUI(minSceneData.page_id);
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

    [LabelText("体力")]
    public int Strength;

    [LabelText("行动值")]
    public int ActionPointsValue;

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
        
        return GameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.scene_id))
            .Select(temp => new ValueDropdownItem(temp.scene_name,temp.scene_id));
    }
    
    public IEnumerable GetMinSceneItemID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        
        return MinGameSceneDataManager.Instance.DataList.Where(temp=> temp != null && !string.IsNullOrEmpty(temp.scene_id))
            .Select(temp => new ValueDropdownItem(temp.scene_name,temp.scene_id));
    }
}
