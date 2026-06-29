using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 主线剧情总管理器
/// </summary>
public class DramaManager : MonoSingleton<DramaManager>,ISaveable
{
    #region 游戏设置

    public bool isAutoDrama = false;
    

    #endregion
    
    #region ISaveable
    
    public string GUID => "DramaManager";

    public void Start()
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
        data.DialogueDataList = _dataList;
    }

    /// <summary>
    /// 读取数据
    /// </summary>
    /// <param name="data"></param>
    public void LoadData(GameSaveData data)
    {
        if (data is { DialogueDataList: not null })
        {
            _dataList = data.DialogueDataList;
        }
        else
        {
            _dataList = new List<DialogueData>();
        }
    }
    

    #endregion
    
    #region Log系统
    private List<DialogueData> _dataList = new();

    public void AddData(DialogueData data)
    {
        _dataList.Add(data);
    }

    /// <summary>
    /// 判断对话是否对话过
    /// </summary>
    /// <param name="dialogueID"></param>
    /// <returns></returns>
    public bool HasDialogue(long dialogueID)
    {
        return _dataList.Any(temp => temp.Id == dialogueID);
    }

    /// <summary>
    /// 显示对话日志UI
    /// </summary>
    public void ShowDramaLogUI()
    {
         var logUI = UISystem.Instance.OpenUI<DramaLogUI>("DramaLogUI");
         if (logUI != null)
         {
             logUI.SetDates(_dataList);
         }
    }

    #endregion

    #region 获取数据

    public NpcData GetNpcData(long npcID)
    {
        return LubanManager.Instance.TbNpcData[npcID];
    }

    #endregion
    
}
