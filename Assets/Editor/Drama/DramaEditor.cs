using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;

public class DramaEditor : OdinMenuEditorWindow
{
    [MenuItem("游戏编辑器/DramaEditor")]
    public static void ShowWin()
    {
        ((EditorWindow)GetWindow<DramaEditor>()).Show();
    }


    /// <summary>Builds the menu tree.</summary>
    protected override OdinMenuTree BuildMenuTree()
    {
        OdinMenuTree tree = new OdinMenuTree();
        tree.AddAllAssetsAtPath("剧情编辑器", "AddressableAssets/Remote/Configs", typeof(DramaData));
        tree.Add("剧情预览", new DramaView());
        return tree;
    }
}

[System.Serializable]
public class DramaView
{
    [LabelText("剧情")]
    public DramaData Data;

    [Button("播放")]
    public void ViewData()
    {
        var ui = UISystem.Instance.OpenUI<DramaUI>("DramaUI");
        ui.StartDrama(Data);
    }
}
