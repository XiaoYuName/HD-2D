using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// Image 图标异步加载扩展：数据层只持有 AA Key，由 UI 层即发即忘地异步加载，避免在属性 getter 里同步阻塞主线程。
/// </summary>
public static class IconLoadExtension
{
    public static void SetIcon(this Image image, string key)
    {
        LoadAsync(image, key).Forget();
    }

    static async UniTaskVoid LoadAsync(Image image, string key)
    {
        Sprite sprite = await AssetsManager.Instance.LoadAssetsUniTask<Sprite>(key);
        image.sprite = sprite;
    }
}
