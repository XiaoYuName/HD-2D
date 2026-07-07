using UnityEngine;
using XFramework;

public class DollController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;
    private Collider2D polygonCollider2D;
    private SpriteRenderer spriteRenderer;

    public ItemData ItemData { get; private set; }
    public DollCatalogData dollCatalogData;

    public void SetData(DollCatalogData dollCatalogData,ItemData itemInfo)
    {
        ItemData = itemInfo;
        this.dollCatalogData = dollCatalogData;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        polygonCollider2D  = GetComponent<Collider2D>();
        if (itemInfo == null) return;

        if (!InventoryManager.Instance.HasItemUnlock(itemInfo.Id))
        {
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName));
        }
        else
        {
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(ItemData.IconPath);
        }
    }

    public void Release()
    {
        AssetsManager.Instance.FreeAsset(!InventoryManager.Instance.HasItemUnlock(ItemData.Id)
            ? GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName)
            : ItemData.IconPath);
    }
}
