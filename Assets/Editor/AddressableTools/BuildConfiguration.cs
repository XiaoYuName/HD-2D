using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace UniTaskFramework
{
    /// <summary>
    /// 一键Addressable 包配置
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfig",menuName = "Configs/Build/BuildConfig")]
    public class BuildConfiguration : SerializedScriptableObject
    {
        [Title("Addressable配置")]
        [FoldoutGroup("Addressable配置")]
        [FoldoutGroup("Addressable配置/分包资源"),FolderPath,LabelText("远程资源路径")]
        public string RemoteAssetPath;
        [FoldoutGroup("Addressable配置/分包资源"),AssetsOnly,LabelText("打包预设信息")]
        public  AddressableAssetGroup RemoteAssetSettings;
    }
}

