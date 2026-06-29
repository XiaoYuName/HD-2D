using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;

public class DramaEditor : OdinMenuEditorWindow
{
    [MenuItem("游戏编辑器/配置表编辑器")]
    public static void ShowWin()
    {
        ((EditorWindow)GetWindow<DramaEditor>()).Show();
    }


    /// <summary>Builds the menu tree.</summary>
    protected override OdinMenuTree BuildMenuTree()
    {
        OdinMenuTree tree = new OdinMenuTree();
        tree.AddAllAssetsAtPath("大场景编辑器", "Assets/AddressableAssets/Remote/Configs/GameScene",typeof(ScriptableObject),true);
        return tree;
    }
}

