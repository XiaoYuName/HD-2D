using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

#if UNITY_EDITOR
using UnityEditor.Localization;
#endif

[CreateAssetMenu(fileName = "GameSettingsDataManager",menuName = "Configs/GameSettingsDataManager")]
public class GameSettingsDataManager : OdinScriptableManager<GameSettingsDataManager>
{
    [BoxGroup("基本数据"),LabelText("配置列表"),TableList(CellPadding = 3),
     Searchable(FilterOptions =  SearchFilterOptions.All)]
    public List<LabelData> LabelDataList;
    
    [BoxGroup("基本数据"),LabelText("语言类型列表"),TableList(CellPadding = 3),
     Searchable(FilterOptions =  SearchFilterOptions.All)]
    public List<LanguageType> LanguageTypeList;
}

[System.Serializable]
public class LabelData
{
    [LabelText("游戏设置类型")]
    public GameSettingType  GameSettingType;
    
#if UNITY_EDITOR
    [LabelText("LabelText"),
     ValueDropdown(nameof(GetAllLocalizationTable), DropdownTitle = "多语言表", SortDropdownItems = true,NumberOfItemsBeforeEnablingSearch = 10)]
#endif
    public string LabelButtonTable;

#if UNITY_EDITOR
    [LabelText("LabelText"),
     ValueDropdown(nameof(GetLocalizationKeyDropdown), DropdownTitle = "多语言键", SortDropdownItems = true,NumberOfItemsBeforeEnablingSearch = 10)]
#endif
    public string LabelButtonName;

    [LabelText("LabelPagePath"),FilePath]
    public string LabelPagePath;
    
    
#if UNITY_EDITOR
    
    public static List<string> GetAllLocalizationTable()
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

[System.Serializable]
public class LanguageType
{
    [LabelText("本地化索引"),HorizontalGroup("Language")]
    public int LocalizationIndex;
    [LabelText("语言名称"),HorizontalGroup("Language")]
    public string LanguageName;
}


[System.Serializable]
public enum WindowType
{
    /// <summary>
    /// 全屏
    /// </summary>
    Fullscreen = 0,
    /// <summary>
    /// 窗口
    /// </summary>
    Windowed = 1,
    /// <summary>
    /// 无边框
    /// </summary>
    Borderless = 2,
}
