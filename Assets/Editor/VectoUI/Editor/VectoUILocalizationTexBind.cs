using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using VectoUI.Builder;

public sealed class VectoUILocalizationTexBind : IVectoUINodePostProcess
{
    private const string FontTableName = "FontAsset";
    private const string DefaultFontEntryName = "DeftualFonts";

    public void OnNodePostBuild(VectoUIPostNodeInfo nodeInfo)
    {
        if (nodeInfo?.GameObject == null)
        {
            return;
        }
        nodeInfo?.GameObject.transform.SetAsFirstSibling();
        if (nodeInfo?.GameObject == null ||
            !nodeInfo.GameObject.TryGetComponent(out TextMeshProUGUI text))
        {
            return;
        }

        LocalizeStringEvent stringEvent = GetOrAddComponent<LocalizeStringEvent>(text.gameObject);
        BindProperty(
            stringEvent.OnUpdateString,
            text,
            nameof(TextMeshProUGUI.text),
            UnityEventCallState.EditorAndRuntime);

        LocalizationFontAssetsEvent fontEvent = GetOrAddComponent<LocalizationFontAssetsEvent>(text.gameObject);
        if (fontEvent.AssetReference.IsEmpty)
        {
            fontEvent.AssetReference.SetReference(FontTableName, DefaultFontEntryName);
        }

        BindProperty(
            fontEvent.OnUpdateAsset,
            text,
            nameof(TextMeshProUGUI.font),
            UnityEventCallState.RuntimeOnly);

        EditorUtility.SetDirty(stringEvent);
        EditorUtility.SetDirty(fontEvent);
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        return gameObject.TryGetComponent(out T component)
            ? component
            : gameObject.AddComponent<T>();
    }

    private static void BindProperty<T>(
        UnityEvent<T> unityEvent,
        TextMeshProUGUI target,
        string propertyName,
        UnityEventCallState callState)
    {
        string methodName = $"set_{propertyName}";
        int listenerIndex = FindPersistentListener(unityEvent, target, methodName);

        if (listenerIndex < 0)
        {
            var setter = target.GetType().GetProperty(propertyName)?.GetSetMethod();
            if (setter == null)
            {
                Debug.LogError($"[VectoUI] 无法绑定 {target.name} 的 {propertyName} 属性。");
                return;
            }

            var action = (UnityAction<T>)Delegate.CreateDelegate(typeof(UnityAction<T>), target, setter);
            UnityEventTools.AddPersistentListener(unityEvent, action);
            listenerIndex = unityEvent.GetPersistentEventCount() - 1;
        }

        unityEvent.SetPersistentListenerState(listenerIndex, callState);
    }

    private static int FindPersistentListener(UnityEventBase unityEvent, UnityEngine.Object target, string methodName)
    {
        for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
        {
            if (unityEvent.GetPersistentTarget(i) == target &&
                unityEvent.GetPersistentMethodName(i) == methodName)
            {
                return i;
            }
        }

        return -1;
    }
}
