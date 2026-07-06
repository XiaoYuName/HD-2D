using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// Image 图标异步加载扩展：数据层只持有 AA Key，由 UI 层即发即忘地异步加载，避免在属性 getter 里同步阻塞主线程。
/// 加载引用由 Image 上自动挂载的 IconReleaser 托管，换图/销毁时自动 FreeAsset，调用方无需手动释放。
/// </summary>
public static class IconLoadExtension
{
    public static void SetIcon(this Image image, string key)
    {
        if (!image.TryGetComponent<IconReleaser>(out var releaser))
            releaser = image.gameObject.AddComponent<IconReleaser>();
        releaser.Load(image, key);
    }
}