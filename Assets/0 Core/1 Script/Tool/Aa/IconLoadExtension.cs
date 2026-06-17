using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// Image 图标异步加载扩展：数据层只持有 AA Key，由 UI 层即发即忘地异步加载，避免在属性 getter 里同步阻塞主线程。
/// </summary>
public static class IconLoadExtension
{
    /// <summary>
    /// 按 Addressable Key 异步加载 Sprite 并赋给 Image（即发即忘）。
    /// 空 Key 直接清空图标；await 期间 Image 已销毁则丢弃结果，避免空引用。
    /// </summary>
    public static void SetIcon(this Image image, string key)
    {
        if(image == null)
            return;
        if(string.IsNullOrEmpty(key))
        {
            image.sprite = null;
            return;
        }
        LoadAsync(image, key).Forget();
    }

    static async UniTaskVoid LoadAsync(Image image, string key)
    {
        Sprite sprite = await AssetsManager.Instance.LoadAssetsUniTask<Sprite>(key);
        if(image != null)
            image.sprite = sprite;
    }
}
