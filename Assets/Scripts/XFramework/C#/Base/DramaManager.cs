using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 主线剧情总管理器
/// </summary>
public class DramaManager : MonoSingleton<DramaManager>
{
    #region 游戏设置

    public bool isAutoDrama = false;
    

    #endregion
    
    
    
    
    #region Log系统
    private List<DialogueData> _dataList = new List<DialogueData>();

    public void AddData(DialogueData data)
    {
        _dataList.Add(data);
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
    
}
