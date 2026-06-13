using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor.Localization;
using UnityEngine;


/// <summary>
/// 剧情演出的命令基类,包含了演出的抽象方法
/// </summary>
[System.Serializable]
public abstract class DramaCommand
{
    [LabelText("命令ID")]
    public int CommandIndex;
    [LabelText("跳转到目标的命令ID")]
    public string ToIndex;
    
#if UNITY_EDITOR
    [LabelText("本地化表集合"), ValueDropdown(nameof(GetLocal))]
    public string LocalSet;

    [LabelText("本地化Key")]
    //[LocalizationKeySelector(nameof(LocalSet))]
    public string LocalEntity;
#endif

    public abstract void Init(DramaUI dramaUI);
    
    /// <summary>
    /// 进入对话
    /// </summary>
    public abstract void Enter();
    
    /// <summary>
    /// 退出对话
    /// </summary>
    public abstract void Exit();

    /// <summary>
    /// 对话持续中
    /// </summary>
    public abstract void Update();

#if UNITY_EDITOR
    public IEnumerable GetLocal()
    {
        return LocalizationEditorSettings
            .GetStringTableCollections()
            .Where(c => c != null)
            .Select(c => c.TableCollectionName)
            .Distinct()
            .ToList();
    }
    
    public static List<string> GetAllLocalizationKeys()
    {
        return LocalizationEditorSettings.GetStringTableCollections()
            .Where(c => c != null && c.SharedData != null)
            .SelectMany(c => c.SharedData.Entries)
            .Where(e => e != null && !string.IsNullOrEmpty(e.Key))
            .Select(e => e.Key)
            .Distinct()
            .ToList();
    }
    
    public static IEnumerable<ValueDropdownItem<string>> GetLocalizationKeyDropdown()
    {
        return GetAllLocalizationKeys()
            .Select(k => new ValueDropdownItem<string>(k, k));
    }
#endif
   
}
