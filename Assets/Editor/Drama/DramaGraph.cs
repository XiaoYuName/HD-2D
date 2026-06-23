using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

[Graph(nameof(AssetExtension))]
[Serializable]
public class DramaGraph : Graph
{
    public const string AssetExtension = "drama";

    [MenuItem("游戏编辑器/DramaGraph")]
    public static void CreateAssetFile()
    {
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DramaGraph>();
    }
}
