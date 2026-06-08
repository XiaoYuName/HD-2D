using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;

public class LocalizeFontAssetEventUtility : Editor
{
    [MenuItem("CONTEXT/TMP_Text/Localize With Font Asset")]
    private static void LocalizeTMProText(MenuCommand command)
    {
        var target = command.context as TMP_Text;
        SetupLocalizationFontAssetEventComponent(target);
        SetupLocalizationStringAssetEventComponent(target);
    }
    
    private static void SetupLocalizationStringAssetEventComponent(TMP_Text target)
    {
        var comp = Undo.AddComponent<LocalizeStringEvent>(target.gameObject);
        var setmethod = target.GetType().GetProperty("text").GetSetMethod();
        var methoddelegate = System.Delegate.CreateDelegate(typeof(UnityAction<TMP_FontAsset>),target, setmethod)
            as UnityAction<string>;
        UnityEventTools.AddPersistentListener(comp.OnUpdateString,methoddelegate);
        comp.OnUpdateString.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
    }
    
    private static void SetupLocalizationFontAssetEventComponent(TMP_Text target)
    {
        var comp = Undo.AddComponent<LocalizationFontAssetsEvent>(target.gameObject);
        var setmethod = target.GetType().GetProperty("font").GetSetMethod();
        var methoddelegate = System.Delegate.CreateDelegate(typeof(UnityAction<TMP_FontAsset>),target, setmethod)
            as UnityAction<TMP_FontAsset>;
        UnityEventTools.AddPersistentListener(comp.OnUpdateAsset,methoddelegate);
        comp.OnUpdateAsset.SetPersistentListenerState(0,UnityEventCallState.EditorAndRuntime);
    }
}
