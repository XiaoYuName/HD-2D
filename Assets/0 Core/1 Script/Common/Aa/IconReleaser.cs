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

    /// <summary>
    /// 清空图标：归还 AA 引用并置空 sprite。
    /// 直接 image.sprite = null 会残留 key，下次 Load 同一张图会被防重复早退导致不加载。
    /// </summary>
    public void Clear(Image image)
    {
        Release();
        image.sprite = null;
    }

    async UniTaskVoid LoadAsync(Image image, string key)
    {
        Sprite sprite = await AssetsManager.Instance.LoadAssetsUniTask<Sprite>(key);
        // 加载期间被 Clear/换图/销毁：引用已在 Release 归还，丢弃结果
        if (image == null || this.key != key)
            return;
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
