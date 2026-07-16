using System.Collections.Generic;
using UnityEngine;
using XFramework;

/// <summary>
/// 托管一组 AA 资源引用，物体销毁时统一 FreeAsset。
/// 一般不手动挂载，由 UIBase.LoadAsset 自动挂到面板上。
/// Image 图标请优先用 image.SetIcon（IconLoadExtension），支持换图时提前归还引用。
/// </summary>
public class AssetReleaser : MonoBehaviour
{
    readonly List<string> keys = new();

    public void Track(string key)
    {
        if (!string.IsNullOrEmpty(key))
            keys.Add(key);
    }

    void OnDestroy()
    {
        foreach (var key in keys)
            AssetsManager.Instance.FreeAsset(key);
        keys.Clear();
    }
}
