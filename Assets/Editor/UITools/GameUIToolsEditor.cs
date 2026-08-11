using Drama.Runtime;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using XFramework;
using PropertyType = XFramework.PropertyType;

public class GameUIToolsEditor : OdinEditorWindow
{
    [TitleGroup("剧情"),LabelText("剧情")] 
    public DramaScript DramaScript;
    
    [Button("播放剧情")]
    public void PlayerDramaScript()
    {
        DramaManager.Instance.StartDramaRuntime(DramaScript);
    }
}
