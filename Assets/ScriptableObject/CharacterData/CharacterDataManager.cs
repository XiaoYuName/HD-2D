using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "CharacterDataManager", menuName = "Configs/CharacterDataManager")]
public class CharacterDataManager : OdinScriptableManager<CharacterDataManager>
{
    [Title("角色配置列表")]
    [InfoBox("角色列表顺序会参与运行时角色配置匹配，调整顺序前请确认 PlayerData 中的角色背包顺序也同步。")]
    [HideLabel]
    [Searchable]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 8)]
    public List<CharacterData> DataList = new List<CharacterData>();

    public CharacterData GetDataByID(string ID)
    {
        return DataList.Find(temp => temp != null && temp.CharacterID == ID);
    }

    [ShowInInspector]
    [ReadOnly]
    [LabelText("角色数量")]
    [PropertyOrder(-10)]
    private int CharacterCount => DataList == null ? 0 : DataList.Count;

    [ShowInInspector]
    [ReadOnly]
    [LabelText("重复ID检查")]
    [GUIColor(nameof(GetDuplicateStatusColor))]
    [PropertyOrder(-9)]
    private string DuplicateStatus => GetDuplicateStatus();

    private string GetDuplicateStatus()
    {
        if (DataList == null || DataList.Count == 0)
        {
            return "暂无角色配置";
        }

        var duplicateIDs = DataList
            .Where(data => data != null && !string.IsNullOrEmpty(data.CharacterID))
            .GroupBy(data => data.CharacterID)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        return duplicateIDs.Count == 0
            ? "没有重复ID"
            : $"存在重复ID：{string.Join(", ", duplicateIDs)}";
    }

    private Color GetDuplicateStatusColor()
    {
        return DuplicateStatus == "没有重复ID" ? Color.green : Color.yellow;
    }
}

[Serializable]
[InlineProperty]
public class CharacterData : OdinDataItem<CharacterData>
{
    [ShowInInspector]
    [ReadOnly]
    [HideLabel]
    [PropertyOrder(-100)]
    [GUIColor(nameof(GetEditorTitleColor))]
    private string EditorTitle => GetEditorTitle();

    [BoxGroup("角色配置")]
    [FoldoutGroup("角色配置/基础信息", Expanded = true)]
    [HorizontalGroup("角色配置/基础信息/Row", Width = 0.35f)]
    [LabelText("角色ID")]
    [Required("角色ID不能为空")]
    [GUIColor(nameof(GetCharacterIDColor))]
    public string CharacterID;

    [HorizontalGroup("角色配置/基础信息/Row", Width = 0.35f)]
    [LabelText("角色名称")]
    [Required("角色名称不能为空")]
    [GUIColor(nameof(GetCharacterNameColor))]
    public string CharacterName;

    [FoldoutGroup("角色配置/基础信息")]
    [LabelText("备注")]
    [MultiLineProperty(2)]
    public string Remark;

    [FoldoutGroup("角色配置/立绘资源", Expanded = true)]
    [HorizontalGroup("角色配置/立绘资源/Split", Width = 0.5f)]
    [VerticalGroup("角色配置/立绘资源/Split/Scene")]
    [LabelText("场景立绘")]
    [PreviewField(96, ObjectFieldAlignment.Center)]
    [GUIColor(nameof(GetSceneIconColor))]
    public Sprite CharacterSceneIcon;

    [HorizontalGroup("角色配置/立绘资源/Split", Width = 0.5f)]
    [VerticalGroup("角色配置/立绘资源/Split/Dialogue")]
    [LabelText("对话立绘")]
    [PreviewField(96, ObjectFieldAlignment.Center)]
    [GUIColor(nameof(GetDialogueTextureColor))]
    public Sprite DialogueTexture;

    [FoldoutGroup("角色配置/Cubism资源", Expanded = false)]
    [LabelText("Cubism预制体")]
    [Sirenix.OdinInspector.FilePath(Extensions = "prefab", RequireExistingPath = false)]
    [ValidateInput(nameof(IsCubismPrefabPathValid), "Cubism预制体路径不存在或不是 prefab")]
    [GUIColor(nameof(GetCubismPrefabColor))]
    public string CubismPrefab;

    [FoldoutGroup("角色配置/出现规则", Expanded = true)]
    [LabelText("规则列表")]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 6)]
    public List<ShowingData> ShowingDataList = new List<ShowingData>();

    [FoldoutGroup("角色配置/出现规则")]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("规则数量")]
    [GUIColor(nameof(GetShowingRuleCountColor))]
    private int ShowingRuleCount => ShowingDataList == null ? 0 : ShowingDataList.Count;

    [FoldoutGroup("角色配置/规则预览", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("出现规则预览")]
    [MultiLineProperty(6)]
    private string ShowingRulePreview => GetShowingRulePreview();

    [FoldoutGroup("角色配置/状态检查", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("配置状态")]
    [GUIColor(nameof(GetConfigStatusColor))]
    private string ConfigStatus => GetConfigStatus();

#if UNITY_EDITOR
    [FoldoutGroup("角色配置/编辑器工具", Expanded = false)]
    [HorizontalGroup("角色配置/编辑器工具/Buttons")]
    [Button("定位场景立绘", ButtonSizes.Medium)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingSceneIcon()
    {
        PingObject(CharacterSceneIcon);
    }

    [HorizontalGroup("角色配置/编辑器工具/Buttons")]
    [Button("定位对话立绘", ButtonSizes.Medium)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingDialogueTexture()
    {
        PingObject(DialogueTexture);
    }

    [HorizontalGroup("角色配置/编辑器工具/Buttons")]
    [Button("定位Cubism预制体", ButtonSizes.Medium)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingCubismPrefab()
    {
        PingAsset(CubismPrefab);
    }

    [HorizontalGroup("角色配置/编辑器工具/Buttons")]
    [Button("复制角色ID", ButtonSizes.Medium)]
    private void CopyCharacterID()
    {
        EditorGUIUtility.systemCopyBuffer = CharacterID;
        Debug.Log($"已复制角色ID: {CharacterID}");
    }
#endif

    public override string GetID()
    {
        return CharacterID;
    }

    private string GetEditorTitle()
    {
        string id = string.IsNullOrEmpty(CharacterID) ? "未填写ID" : CharacterID;
        string name = string.IsNullOrEmpty(CharacterName) ? "未填写名称" : CharacterName;
        int ruleCount = ShowingDataList == null ? 0 : ShowingDataList.Count;

        return $"角色：{name}    ID：{id}    出现规则：{ruleCount} 条";
    }

    private string GetShowingRulePreview()
    {
        if (ShowingDataList == null || ShowingDataList.Count == 0)
        {
            return "暂无出现规则";
        }

        List<string> lines = new List<string>();

        for (int i = 0; i < ShowingDataList.Count; i++)
        {
            ShowingData showingData = ShowingDataList[i];

            if (showingData == null)
            {
                lines.Add($"{i + 1}. 空规则");
                continue;
            }

            lines.Add($"{i + 1}. {showingData.GetEditorSummary()}");
        }

        return string.Join("\n", lines);
    }

    private string GetConfigStatus()
    {
        if (string.IsNullOrEmpty(CharacterID))
        {
            return "角色ID为空";
        }

        if (string.IsNullOrEmpty(CharacterName))
        {
            return "角色名称为空";
        }

        if (CharacterSceneIcon == null)
        {
            return "场景立绘为空";
        }

        if (DialogueTexture == null)
        {
            return "对话立绘为空";
        }

        if (!IsCubismPrefabPathValid())
        {
            return "Cubism预制体路径无效";
        }

        if (ShowingDataList == null || ShowingDataList.Count == 0)
        {
            return "出现规则为空";
        }

        if (ShowingDataList.Any(data => data == null || !data.IsConfigValid()))
        {
            return "存在未配置完整的出现规则";
        }

        return "配置正常";
    }

    private bool IsCubismPrefabPathValid()
    {
        if (string.IsNullOrEmpty(CubismPrefab))
        {
            return false;
        }

        if (!CubismPrefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(CubismPrefab) != null;
#else
        return true;
#endif
    }

    private Color GetEditorTitleColor()
    {
        return GetConfigStatus() == "配置正常"
            ? new Color(0.45f, 0.85f, 1f)
            : Color.yellow;
    }

    private Color GetCharacterIDColor()
    {
        return string.IsNullOrEmpty(CharacterID) ? Color.red : Color.white;
    }

    private Color GetCharacterNameColor()
    {
        return string.IsNullOrEmpty(CharacterName) ? Color.red : Color.white;
    }

    private Color GetSceneIconColor()
    {
        return CharacterSceneIcon == null ? Color.yellow : Color.white;
    }

    private Color GetDialogueTextureColor()
    {
        return DialogueTexture == null ? Color.yellow : Color.white;
    }

    private Color GetCubismPrefabColor()
    {
        return IsCubismPrefabPathValid() ? Color.green : Color.yellow;
    }

    private Color GetShowingRuleCountColor()
    {
        return ShowingRuleCount <= 0 ? Color.yellow : Color.green;
    }

    private Color GetConfigStatusColor()
    {
        return GetConfigStatus() == "配置正常" ? Color.green : Color.yellow;
    }

#if UNITY_EDITOR
    private static void PingObject(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("资源为空，无法定位。");
            return;
        }

        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

    private static void PingAsset(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("路径为空，无法定位资源。");
            return;
        }

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

        if (asset == null)
        {
            Debug.LogWarning($"资源不存在: {path}");
            return;
        }

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
#endif
}


[Serializable]
[InlineProperty]
public class ShowingData
{
    [ShowInInspector]
    [ReadOnly]
    [HideLabel]
    [PropertyOrder(-50)]
    [GUIColor(nameof(GetEditorTitleColor))]
    private string EditorTitle => GetEditorSummary();

    [BoxGroup("出现规则")]
    [FoldoutGroup("出现规则/时间条件", Expanded = true)]
    [HorizontalGroup("出现规则/时间条件/Row", Width = 0.4f)]
    [LabelText("出现周几")]
    [EnumToggleButtons]
    [GUIColor(nameof(GetShowWeekColor))]
    public ShowingWeek ShowWeek;

    [HorizontalGroup("出现规则/时间条件/Row", Width = 0.4f)]
    [LabelText("出现时间")]
    [EnumToggleButtons]
    [GUIColor(nameof(GetShowTimeColor))]
    public ShowingTime ShowTime;

    [HorizontalGroup("出现规则/时间条件/Row", Width = 0.2f)]
    [LabelText("出现模式")]
    [EnumToggleButtons]
    public ShowingModel ShowingModel;

    [FoldoutGroup("出现规则/固定场景", Expanded = true)]
    [LabelText("场景配置")]
    [HideIf(nameof(IsCustomMode))]
    public SceneData FixedSceneData = new SceneData();

    [FoldoutGroup("出现规则/自定义场景", Expanded = true)]
    [LabelText("出现场景")]
    [ShowIf(nameof(IsCustomMode))]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 5)]
    public List<CustomSceneData> CustomSceneList = new List<CustomSceneData>();

    [FoldoutGroup("出现规则/剧情", Expanded = true)]
    [LabelText("对话ID")]
    [GUIColor(nameof(GetDialogueIDColor))]
    public long DialogueIds;
    


    [FoldoutGroup("出现规则/状态检查", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("规则状态")]
    [GUIColor(nameof(GetStatusColor))]
    private string ConfigStatus => GetConfigStatus();
    
    //[HorizontalGroup("功能/功能列表/Row", Width = 0.2f)]
    [LabelText("出现模式")]
    [EnumToggleButtons]
    public FunctionType FunctionGroup;

    public string GetEditorSummary()
    {
        string mode = ShowingModel == ShowingModel.Custom ? "自定义" : "固定";
        string scene = ShowingModel == ShowingModel.Custom
            ? $"自定义条件 {GetCustomSceneCount()} 组"
            : GetFixedSceneSummary();

        return $"{ShowWeek} / {ShowTime} / {mode} / {scene} / 对话ID：{DialogueIds}";
    }

    public bool IsConfigValid()
    {
        if (ShowWeek == 0 || ShowTime == 0)
        {
            return false;
        }

        if (DialogueIds <= 0)
        {
            return false;
        }

        if (ShowingModel == ShowingModel.Custom)
        {
            return CustomSceneList != null &&
                   CustomSceneList.Count > 0 &&
                   CustomSceneList.All(data => data != null && data.IsConfigValid());
        }

        return FixedSceneData != null && FixedSceneData.IsConfigValid();
    }

    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }
        
        return MinGameSceneDataManager.Instance.DataList.Where(t => t != null && !string.IsNullOrEmpty(t.SceneID))
            .Select(t => new ValueDropdownItem(GetSceneDropdownLabel(t), t.SceneID));
    }

    private bool IsCustomMode()
    {
        return ShowingModel == ShowingModel.Custom;
    }

    private int GetCustomSceneCount()
    {
        return CustomSceneList == null ? 0 : CustomSceneList.Count;
    }

    private string GetFixedSceneSummary()
    {
        if (FixedSceneData == null || string.IsNullOrEmpty(FixedSceneData.SceneID))
        {
            return "未配置固定场景";
        }

        return $"固定场景：{FixedSceneData.GetSceneLabel()}";
    }

    private string GetConfigStatus()
    {
        if (ShowWeek == 0)
        {
            return "未选择出现周几";
        }

        if (ShowTime == 0)
        {
            return "未选择出现时间";
        }

        if (DialogueIds <= 0)
        {
            return "对话ID无效";
        }

        if (ShowingModel == ShowingModel.Custom)
        {
            if (CustomSceneList == null || CustomSceneList.Count == 0)
            {
                return "自定义场景为空";
            }

            if (CustomSceneList.Any(data => data == null || !data.IsConfigValid()))
            {
                return "存在未配置完整的自定义场景";
            }

            return "配置正常";
        }

        if (FixedSceneData == null || !FixedSceneData.IsConfigValid())
        {
            return "固定场景未配置完整";
        }

        return "配置正常";
    }

    private Color GetEditorTitleColor()
    {
        return GetConfigStatus() == "配置正常"
            ? new Color(0.45f, 0.85f, 1f)
            : Color.yellow;
    }

    private Color GetShowWeekColor()
    {
        return ShowWeek == 0 ? Color.yellow : Color.white;
    }

    private Color GetShowTimeColor()
    {
        return ShowTime == 0 ? Color.yellow : Color.white;
    }

    private Color GetDialogueIDColor()
    {
        return DialogueIds <= 0 ? Color.yellow : Color.white;
    }

    private Color GetStatusColor()
    {
        return GetConfigStatus() == "配置正常" ? Color.green : Color.yellow;
    }

    private static string GetSceneDropdownLabel(MinSceneData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        string description = string.IsNullOrEmpty(data.scene_description)
            ? "未填写描述"
            : data.scene_description;

        return $"{description} / {data.SceneID}";
    }
}

[Serializable]
[InlineProperty]
public class CustomSceneData
{
    [ShowInInspector]
    [ReadOnly]
    [HideLabel]
    [PropertyOrder(-20)]
    [GUIColor(nameof(GetEditorTitleColor))]
    private string EditorTitle => GetEditorSummary();

    [BoxGroup("自定义条件")]
    [HorizontalGroup("自定义条件/Condition", Width = 0.35f)]
    [LabelText("属性")]
    [EnumToggleButtons]
    public CharacterPropertyType characterPropertyType;

    [HorizontalGroup("自定义条件/Condition", Width = 0.35f)]
    [LabelText("范围")]
    [MinMaxSlider(0, 100)]
    [GUIColor(nameof(GetRadiusColor))]
    public Vector2 Radius;

    [BoxGroup("自定义条件")]
    [LabelText("场景配置")]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 5)]
    public List<SceneData> SceneList = new List<SceneData>();

    [BoxGroup("自定义条件")]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("条件状态")]
    [GUIColor(nameof(GetStatusColor))]
    private string ConfigStatus => GetConfigStatus();

    public bool IsConfigValid()
    {
        return Radius.x <= Radius.y &&
               SceneList != null &&
               SceneList.Count > 0 &&
               SceneList.All(data => data != null && data.IsConfigValid());
    }

    private string GetEditorSummary()
    {
        int sceneCount = SceneList == null ? 0 : SceneList.Count;
        return $"{characterPropertyType}：{Radius.x:0.#} - {Radius.y:0.#}    候选场景：{sceneCount} 个";
    }

    private string GetConfigStatus()
    {
        if (Radius.x > Radius.y)
        {
            return "范围最小值不能大于最大值";
        }

        if (SceneList == null || SceneList.Count == 0)
        {
            return "候选场景为空";
        }

        if (SceneList.Any(data => data == null || !data.IsConfigValid()))
        {
            return "存在未配置完整的候选场景";
        }

        return "配置正常";
    }

    private Color GetEditorTitleColor()
    {
        return GetConfigStatus() == "配置正常"
            ? new Color(0.45f, 0.85f, 1f)
            : Color.yellow;
    }

    private Color GetRadiusColor()
    {
        return Radius.x > Radius.y ? Color.yellow : Color.white;
    }

    private Color GetStatusColor()
    {
        return GetConfigStatus() == "配置正常" ? Color.green : Color.yellow;
    }
}

[Serializable]
[InlineProperty]
public class SceneData
{
    [HorizontalGroup("Row", Width = 0.32f)]
    [LabelText("场景ID")]
    [ValueDropdown(nameof(GetMinSceneID))]
    [GUIColor(nameof(GetSceneIDColor))]
    public string SceneID;

    [HorizontalGroup("Row", Width = 0.32f)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("场景描述")]
    private string SceneDescription => GetSceneDescription();

    [HorizontalGroup("Row", Width = 0.36f)]
    [LabelText("出现位置")]
    public Vector3 Position;

    public bool IsConfigValid()
    {
        return !string.IsNullOrEmpty(SceneID) && DoesSceneExist(SceneID);
    }

    public string GetSceneLabel()
    {
        string description = GetSceneDescription();

        if (string.IsNullOrEmpty(description))
        {
            return SceneID;
        }

        return $"{description} / {SceneID}";
    }

    public IEnumerable GetMinSceneID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }

        return MinGameSceneDataManager.Instance.DataList
            .Where(t => t != null && !string.IsNullOrEmpty(t.SceneID))
            .Select(t => new ValueDropdownItem(GetSceneDropdownLabel(t), t.SceneID));
    }

    private string GetSceneDescription()
    {
        if (string.IsNullOrEmpty(SceneID) || MinGameSceneDataManager.Instance == null)
        {
            return string.Empty;
        }

        MinSceneData minSceneData = MinGameSceneDataManager.Instance.DataList
            .FirstOrDefault(t => t != null && t.SceneID == SceneID);

        if (minSceneData == null)
        {
            return "未找到场景";
        }

        return string.IsNullOrEmpty(minSceneData.scene_description)
            ? "未填写描述"
            : minSceneData.scene_description;
    }

    private bool DoesSceneExist(string sceneID)
    {
        if (string.IsNullOrEmpty(sceneID))
        {
            return false;
        }

        if (MinGameSceneDataManager.Instance == null)
        {
            return true;
        }

        return MinGameSceneDataManager.Instance.DataList
            .Any(t => t != null && t.SceneID == sceneID);
    }

    private Color GetSceneIDColor()
    {
        return IsConfigValid() ? Color.white : Color.yellow;
    }

    private static string GetSceneDropdownLabel(MinSceneData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        string description = string.IsNullOrEmpty(data.scene_description)
            ? "未填写描述"
            : data.scene_description;

        return $"{description} / {data.SceneID}";
    }
}

