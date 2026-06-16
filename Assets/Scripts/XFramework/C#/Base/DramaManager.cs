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
    private List<DialogueCommand> PlayerLogCommands = new List<DialogueCommand>();
    
    public void AddPlayerLogCommand(DialogueCommand command)
    {
        PlayerLogCommands.Add(command);
    }

    [Button("显示游戏对话日志")]
    public void ShowingPlayerLog()
    {
       var dramaLogUI =  UISystem.Instance.OpenUI<DramaLogUI>("DramaLogUI");
       if (dramaLogUI != null)
       {
           dramaLogUI.SetDates(PlayerLogCommands);
       }
    }

    #endregion
    
}
