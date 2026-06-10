using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor.Localization;
#endif

using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = "PhotoAlbumDataManager", menuName = "Configs/PhotoAlbumDataManager")]
public class PhotoAlbumDataManager : OdinScriptableManager<PhotoAlbumDataManager>
{
    [BoxGroup("基本数据"),LabelText("配置列表"),TableList(CellPadding = 3),
     Searchable(FilterOptions =  SearchFilterOptions.All)]
    public List<PhotoLabelData> LabelDataList;
    
    [BoxGroup("CG数据"),LabelText("配置列表"),TableList(CellPadding = 3),
     Searchable(FilterOptions =  SearchFilterOptions.All)]
    public List<ActionCGData> ActionCGDataList;
}

[System.Serializable]
public class PhotoLabelData
{
    [LabelText("游戏设置类型")]
    public PhotoLabelType  PhotoLabelType;
    
#if UNITY_EDITOR
    [LabelText("LabelText"),
     ValueDropdown(nameof(GetAllLocalizationTable), DropdownTitle = "多语言", SortDropdownItems = true,NumberOfItemsBeforeEnablingSearch = 10)]
#endif
    public string LabelButtonTable;

#if UNITY_EDITOR
    [LabelText("LabelText"),
     ValueDropdown(nameof(GetLocalizationKeyDropdown), DropdownTitle = "多语言", SortDropdownItems = true,NumberOfItemsBeforeEnablingSearch = 10)]
#endif
    [Searchable]
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
public class ActionCGData
{
    [FilePath,LabelText("CG图片")]
    public string minSpritePath;
    [FilePath,LabelText("CG大图")]
    public string maxSpritePath;

    //TODO:后续扩展需求
}
