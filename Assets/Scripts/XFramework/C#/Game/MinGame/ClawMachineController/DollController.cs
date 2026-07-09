using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class DollController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;
    private PolygonCollider2D polygonCollider2D;
    private SpriteRenderer spriteRenderer;

    public ItemData ItemData { get; private set; }
    public DollCatalogData dollCatalogData;
    private readonly List<Vector2> points = new();

    public void SetData(DollCatalogData dollCatalogData,ItemData itemInfo)
    {
        ItemData = itemInfo;
        this.dollCatalogData = dollCatalogData;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        polygonCollider2D  = GetComponent<PolygonCollider2D>();
        if (itemInfo == null) return;

        if (!InventoryManager.Instance.HasItemUnlock(itemInfo.ID))
        {
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName));
            Refresh();
        }
        else
        {
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(ItemData.IconName));
            Refresh();
        }
    }
    
    public void Refresh()
    {
        Sprite sprite = spriteRenderer.sprite;

        if (sprite == null)
            return;

        int shapeCount = sprite.GetPhysicsShapeCount();

        polygonCollider2D.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            points.Clear();
            sprite.GetPhysicsShape(i, points);
            polygonCollider2D.SetPath(i, points);
        }
    }

    public void Release()
    {
        AssetsManager.Instance.FreeAsset(!InventoryManager.Instance.HasItemUnlock(ItemData.ID)
            ? GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName)
            : GamePathTools.CombinationItemIconPath(ItemData.IconName));
    }
}
