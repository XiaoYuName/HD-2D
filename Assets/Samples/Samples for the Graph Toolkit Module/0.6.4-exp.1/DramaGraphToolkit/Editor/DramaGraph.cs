using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

[Serializable]
[Graph(AssetExtension)]
public class DramaGraph : Graph
{
    const string k_graphName = "DramaGraph";
    
    internal const string AssetExtension = "graph";
    
    /// <summary>
    /// Creates a new Visual Novel Director graph asset file in the project window.
    /// </summary>
    /// <remarks>This is also where we add the shortcut to create a new graph from the editor Asset menu.</remarks>
    [MenuItem("游戏编辑器/DramaGraph/Visual Novel Director Graph")]
    static void CreateAssetFile()
    {
        GraphDatabase.PromptInProjectBrowserToCreateNewAsset<DramaGraph>(k_graphName);
    }

    /// <summary>
    ///        <para>
    /// Called after the graph has changed.
    /// </para>
    ///      </summary>
    /// <param name="graphLogger">The GraphLogger that receives any errors or warnings related to the graph.</param>
    public override void OnGraphChanged(GraphLogger graphLogger)
    {
        base.OnGraphChanged(graphLogger);
    }
}
