using UnityEngine;
using XFramework;

public class DollController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;
    private Collider2D polygonCollider2D;
    private SpriteRenderer spriteRenderer;

    public GuideBag Data { get; private set; }
    public DollCatalogData dollCatalogData;

    public void SetData(DollCatalogData dollCatalogData,GuideBag guideBag)
    {
        Data = guideBag;
        this.dollCatalogData = dollCatalogData;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        polygonCollider2D  = GetComponent<Collider2D>();
        
        if (guideBag.StateType == StateType.Unlock)
        {
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName));
        }
        else
        {
            //ItemData itemData = InventoryManager.Instance.GetItemData(dollCatalogData.ID);
            spriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.ImageName));
        }
    }

    public void Release()
    {
        if (Data.StateType == StateType.Unlock)
        {
            AssetsManager.Instance.FreeAsset(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.UlockImageName));
        }
        else
        {
            //ItemData itemData = InventoryManager.Instance.GetItemData(dollCatalogData.ID);
            AssetsManager.Instance.FreeAsset(GuideManager.Instance.CombinationDollImagePath(dollCatalogData.ImageName));
        }
    }
}
