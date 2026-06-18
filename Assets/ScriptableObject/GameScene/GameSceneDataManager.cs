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

[CreateAssetMenu(fileName = "GameSceneDataManager", menuName = "Configs/GameSceneDataManager")]
public class GameSceneDataManager : OdinScriptableManager<GameSceneDataManager>
{
    [Title("大场景配置列表")]
    [HideLabel]
    [Searchable]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 10)]
    public List<GameSceneData> DataList = new List<GameSceneData>();

    public GameSceneData GetDataByID(string ID)
    {
        return DataList.FindLast(temp => temp != null && temp.GetID() == ID);
    }
}

[Serializable]
[InlineProperty]
public class GameSceneData : OdinDataItem<GameSceneData>
{
    // =========================================================
    // 编辑器标题
    // =========================================================

    [ShowInInspector]
    [ReadOnly]
    [HideLabel]
    [PropertyOrder(-100)]
    [GUIColor(nameof(GetEditorTitleColor))]
    private string EditorTitle => GetEditorTitle();

    // =========================================================
    // 基础信息
    // =========================================================
    [BoxGroup("大场景配置")]
    [FoldoutGroup("大场景配置/基础信息", Expanded = true)]
    [HorizontalGroup("大场景配置/基础信息/Row", Width = 0.5f)]
    [LabelText("场景ID")]
    [Required("场景ID不能为空")]
    [GUIColor(nameof(GetSceneIDColor))]
    public string scene_id;

    [HorizontalGroup("大场景配置/基础信息/Row", Width = 0.5f)]
    [LabelText("场景名称")]
    [Required("场景名称不能为空")]
    [GUIColor(nameof(GetSceneNameColor))]
    public string scene_name;

    // =========================================================
    // 地图显示
    // =========================================================

    [FoldoutGroup("大场景配置/地图显示", Expanded = true)]
    [HorizontalGroup("大场景配置/地图显示/Split", Width = 0.35f)]
    [VerticalGroup("大场景配置/地图显示/Split/Icon")]
    [LabelText("地图Icon")]
    [PreviewField(90, ObjectFieldAlignment.Center)]
    [GUIColor(nameof(GetIconColor))]
    public Sprite word_icon;

    [HorizontalGroup("大场景配置/地图显示/Split", Width = 0.65f)]
    [VerticalGroup("大场景配置/地图显示/Split/Position")]
    [LabelText("地图位置")]
    public Vector2 WordPosition;

#if UNITY_EDITOR
    [VerticalGroup("大场景配置/地图显示/Split/Position")]
    [Button("定位地图Icon", ButtonSizes.Small)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingMapIcon()
    {
        PingObject(word_icon);
    }
#endif

    // =========================================================
    // 小场景配置
    // =========================================================

    [FoldoutGroup("大场景配置/小场景配置", Expanded = true)]
    [LabelText("小场景列表")]
    [ValueDropdown(nameof(GetMinSceneItemID))]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 8)]
    public List<string> min_sceneList = new List<string>();

    [FoldoutGroup("大场景配置/小场景配置", Expanded = true)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("小场景数量")]
    [GUIColor(nameof(GetMinSceneCountColor))]
    private int MinSceneCount => min_sceneList == null ? 0 : min_sceneList.Count;

    [FoldoutGroup("大场景配置/小场景预览", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("小场景预览")]
    [MultiLineProperty(6)]
    private string MinScenePreview => GetMinScenePreview();

    // =========================================================
    // 状态检查
    // =========================================================

    [FoldoutGroup("大场景配置/状态检查", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("配置状态")]
    [GUIColor(nameof(GetStatusColor))]
    private string ConfigStatus => GetConfigStatus();

#if UNITY_EDITOR

    // =========================================================
    // 编辑器工具
    // =========================================================

    [FoldoutGroup("大场景配置/编辑器工具", Expanded = false)]
    [HorizontalGroup("大场景配置/编辑器工具/Buttons")]
    [Button("复制场景ID", ButtonSizes.Medium)]
    private void CopySceneID()
    {
        EditorGUIUtility.systemCopyBuffer = scene_id;
        Debug.Log($"已复制场景ID: {scene_id}");
    }

    [HorizontalGroup("大场景配置/编辑器工具/Buttons")]
    [Button("复制场景名称", ButtonSizes.Medium)]
    private void CopySceneName()
    {
        EditorGUIUtility.systemCopyBuffer = scene_name;
        Debug.Log($"已复制场景名称: {scene_name}");
    }

    [HorizontalGroup("大场景配置/编辑器工具/Buttons")]
    [Button("清理空小场景", ButtonSizes.Medium)]
    [GUIColor(1f, 0.75f, 0.35f)]
    private void ClearEmptyMinScenes()
    {
        if (min_sceneList == null)
        {
            min_sceneList = new List<string>();
            return;
        }

        int beforeCount = min_sceneList.Count;
        min_sceneList = min_sceneList
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        int afterCount = min_sceneList.Count;

        Debug.Log($"清理完成，原数量: {beforeCount}，当前数量: {afterCount}");
    }

#endif

    // =========================================================
    // 基础方法
    // =========================================================

    public override string GetID()
    {
        return scene_id;
    }

    public IEnumerable GetMinSceneItemID()
    {
        if (MinGameSceneDataManager.Instance == null)
        {
            return new List<string>();
        }

        return MinGameSceneDataManager.Instance.DataList
            .Where(temp => temp != null && !string.IsNullOrEmpty(temp.scene_id))
            .Select(temp =>
            {
                string label = string.IsNullOrEmpty(temp.scene_description)
                    ? temp.scene_id
                    : $"{temp.scene_description} / {temp.scene_id}";

                return new ValueDropdownItem(label, temp.scene_id);
            });
    }

    // =========================================================
    // 显示文本
    // =========================================================

    private string GetEditorTitle()
    {
        string id = string.IsNullOrEmpty(scene_id) ? "未填写ID" : scene_id;
        string name = string.IsNullOrEmpty(scene_name) ? "未填写名称" : scene_name;
        int count = min_sceneList == null ? 0 : min_sceneList.Count;

        return $"大场景：{name}    ID：{id}    小场景：{count} 个";
    }

    private string GetMinScenePreview()
    {
        if (min_sceneList == null || min_sceneList.Count == 0)
        {
            return "暂无小场景";
        }

        if (MinGameSceneDataManager.Instance == null)
        {
            return string.Join("\n", min_sceneList);
        }

        List<string> lines = new List<string>();

        for (int i = 0; i < min_sceneList.Count; i++)
        {
            string minSceneID = min_sceneList[i];

            if (string.IsNullOrEmpty(minSceneID))
            {
                lines.Add($"{i + 1}. 空ID");
                continue;
            }

            var minSceneData = MinGameSceneDataManager.Instance.DataList
                .FirstOrDefault(temp => temp != null && temp.scene_id == minSceneID);

            if (minSceneData == null)
            {
                lines.Add($"{i + 1}. {minSceneID}  [未找到]");
                continue;
            }

            string desc = string.IsNullOrEmpty(minSceneData.scene_description)
                ? "未填写描述"
                : minSceneData.scene_description;

            lines.Add($"{i + 1}. {desc} / {minSceneID}");
        }

        return string.Join("\n", lines);
    }

    private string GetConfigStatus()
    {
        if (string.IsNullOrEmpty(scene_id))
        {
            return "场景ID为空";
        }

        if (string.IsNullOrEmpty(scene_name))
        {
            return "场景名称为空";
        }

        if (word_icon == null)
        {
            return "地图Icon为空";
        }

        if (min_sceneList == null || min_sceneList.Count == 0)
        {
            return "小场景列表为空";
        }

        if (HasInvalidMinSceneID())
        {
            return "存在无效的小场景ID";
        }

        return "配置正常";
    }

    private bool HasInvalidMinSceneID()
    {
        if (min_sceneList == null || min_sceneList.Count == 0)
        {
            return false;
        }

        if (MinGameSceneDataManager.Instance == null)
        {
            return false;
        }

        foreach (string minSceneID in min_sceneList)
        {
            if (string.IsNullOrEmpty(minSceneID))
            {
                return true;
            }

            bool exists = MinGameSceneDataManager.Instance.DataList
                .Any(temp => temp != null && temp.scene_id == minSceneID);

            if (!exists)
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // 颜色
    // =========================================================

    private Color GetEditorTitleColor()
    {
        return GetConfigStatus() == "配置正常"
            ? new Color(0.45f, 0.85f, 1f)
            : Color.yellow;
    }

    private Color GetSceneIDColor()
    {
        return string.IsNullOrEmpty(scene_id) ? Color.red : Color.white;
    }

    private Color GetSceneNameColor()
    {
        return string.IsNullOrEmpty(scene_name) ? Color.red : Color.white;
    }

    private Color GetIconColor()
    {
        return word_icon == null ? Color.yellow : Color.white;
    }

    private Color GetMinSceneCountColor()
    {
        return MinSceneCount <= 0 ? Color.yellow : Color.green;
    }

    private Color GetStatusColor()
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

#endif
}