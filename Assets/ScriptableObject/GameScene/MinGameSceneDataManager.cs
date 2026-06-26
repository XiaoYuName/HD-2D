using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using XFramework;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "MinGameSceneDataManager", menuName = "Configs/MinGameSceneDataManager")]
public class MinGameSceneDataManager : OdinScriptableManager<MinGameSceneDataManager>
{
    [HideLabel]
    [Searchable]
    [ListDrawerSettings(
        DraggableItems = true,
        ShowFoldout = true,
        ShowIndexLabels = true,
        NumberOfItemsPerPage = 10)]
    public List<MinSceneData> DataList = new List<MinSceneData>();
    
    public MinSceneData GetDataByID(string ID)
    {
        return DataList.FindLast(temp => temp.GetID() == ID);
    }
}

[Serializable]
[InlineProperty]
public class MinSceneData : OdinDataItem<MinSceneData>
{
    [FormerlySerializedAs("scene_id")]
    [TitleGroup("小场景配置", Alignment = TitleAlignments.Centered)]

    [FoldoutGroup("小场景配置/基础信息", Expanded = false)]
    [HorizontalGroup("小场景配置/基础信息/Row01", Width = 0.35f)]
    [LabelText("场景ID")]
    [Required("场景ID不能为空")]
    [GUIColor(nameof(GetIDColor))]
    public string SceneID;
    
    [FoldoutGroup("小场景配置/基础信息", Expanded = false)]
    [HorizontalGroup("小场景配置/基础信息/Row01", Width = 0.35f)]
    [LabelText("场景类型")]
    public SceneShowType ShowType;

    [FoldoutGroup("小场景配置/本地化", Expanded = false)]
    [LabelText("场景名称")]
    [InlineProperty]
    [HideLabel]
    public LocalSelectedData sceneName;

    [FoldoutGroup("小场景配置/资源配置", Expanded = false)]
    [HorizontalGroup("小场景配置/资源配置/Split", Width = 0.72f)]
    [VerticalGroup("小场景配置/资源配置/Split/Left")]
    [LabelText("场景资源路径")]
    [Sirenix.OdinInspector.FilePath(Extensions = "unity", RequireExistingPath = false)]
    [ValidateInput(nameof(IsScenePathValid), "场景路径不存在或不是 .unity 文件")]
    [GUIColor(nameof(GetScenePathColor))]
    public string scenePath;

    [VerticalGroup("小场景配置/资源配置/Split/Left")]
    [LabelText("场景图片路径")]
    [Sirenix.OdinInspector.FilePath(Extensions = "png,jpg,jpeg", RequireExistingPath = false)]
    [ValidateInput(nameof(IsTexturePathValid), "图片路径不存在或不是 png/jpg/jpeg 文件")]
    [GUIColor(nameof(GetTexturePathColor))]
    public string SceneTexturePath;

#if UNITY_EDITOR
    [HorizontalGroup("小场景配置/资源配置/Split", Width = 0.28f)]
    [VerticalGroup("小场景配置/资源配置/Split/Right")]
    [LabelText("图片预览")]
    [ShowInInspector]
    [ReadOnly]
    [PreviewField(100, ObjectFieldAlignment.Center)]
    private Texture2D SceneTexturePreview => LoadAsset<Texture2D>(SceneTexturePath);
#endif

    [FoldoutGroup("小场景配置/描述", Expanded = false)]
    [LabelText("场景描述")]
    [MultiLineProperty(4)]
    public string scene_description;

#if UNITY_EDITOR

    [FoldoutGroup("小场景配置/编辑器工具", Expanded = false)]
    [HorizontalGroup("小场景配置/编辑器工具/Buttons")]
    [Button("定位场景", ButtonSizes.Medium)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingSceneAsset()
    {
        PingAsset(scenePath);
    }

    [HorizontalGroup("小场景配置/编辑器工具/Buttons")]
    [Button("定位图片", ButtonSizes.Medium)]
    [GUIColor(0.45f, 0.75f, 1f)]
    private void PingTextureAsset()
    {
        PingAsset(SceneTexturePath);
    }

    [HorizontalGroup("小场景配置/编辑器工具/Buttons")]
    [Button("复制场景路径", ButtonSizes.Medium)]
    private void CopyScenePath()
    {
        EditorGUIUtility.systemCopyBuffer = scenePath;
        Debug.Log($"已复制场景路径: {scenePath}");
    }

    [HorizontalGroup("小场景配置/编辑器工具/Buttons")]
    [Button("复制图片路径", ButtonSizes.Medium)]
    private void CopyTexturePath()
    {
        EditorGUIUtility.systemCopyBuffer = SceneTexturePath;
        Debug.Log($"已复制图片路径: {SceneTexturePath}");
    }

    [FoldoutGroup("小场景配置/状态检查", Expanded = false)]
    [ShowInInspector]
    [ReadOnly]
    [LabelText("配置状态")]
    [GUIColor(nameof(GetStatusColor))]
    private string ConfigStatus
    {
        get
        {
            if (string.IsNullOrEmpty(SceneID))
            {
                return "场景ID为空";
            }

            if (!IsScenePathValid())
            {
                return "场景资源路径无效";
            }

            if (!IsTexturePathValid())
            {
                return "场景图片路径无效";
            }

            return "配置正常";
        }
    }

#endif
    
    
    public override string GetID()
    {
        return SceneID;
    }

    private Color GetIDColor()
    {
        return string.IsNullOrEmpty(SceneID) ? Color.red : Color.white;
    }

    private Color GetScenePathColor()
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return Color.red;
        }

        return IsScenePathValid() ? Color.green : Color.yellow;
    }

    private Color GetTexturePathColor()
    {
        if (string.IsNullOrEmpty(SceneTexturePath))
        {
            return Color.red;
        }

        return IsTexturePathValid() ? Color.green : Color.yellow;
    }

    private Color GetStatusColor()
    {
#if UNITY_EDITOR
        return ConfigStatus == "配置正常" ? Color.green : Color.yellow;
#else
        return Color.white;
#endif
    }

    private bool IsScenePathValid()
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return false;
        }

        if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
#else
        return File.Exists(scenePath);
#endif
    }

    private bool IsTexturePathValid()
    {
        if (string.IsNullOrEmpty(SceneTexturePath))
        {
            return false;
        }

        string extension = Path.GetExtension(SceneTexturePath).ToLowerInvariant();

        if (extension != ".png" &&
            extension != ".jpg" &&
            extension != ".jpeg")
        {
            return false;
        }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SceneTexturePath) != null;
#else
        return File.Exists(SceneTexturePath);
#endif
    }

#if UNITY_EDITOR

    private static T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<T>(path);
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

        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

#endif
}

