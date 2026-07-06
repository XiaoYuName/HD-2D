using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using XFramework;

public class IconReleaser : MonoBehaviour
{
    string key;

    public void Load(Image image, string key)
    {
        if (this.key == key) return;          // 同一张图，避免重复计数
        Release();                        // 先还旧图
        this.key = key;
        LoadAsync(image, key).Forget();
    }

    static async UniTaskVoid LoadAsync(Image image, string key)
    {
        Sprite sprite = await AssetsManager.Instance.LoadAssetsUniTask<Sprite>(key);
        image.sprite = sprite;
    }

    void Release()
    {
        if(string.IsNullOrEmpty(key))
            return;
        AssetsManager.Instance.FreeAsset(key);
        key = null;
    }

    void OnDestroy() => Release();
}
