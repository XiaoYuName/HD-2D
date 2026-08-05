using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class DollController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;
    private SpriteRenderer _ulockSpriteRenderer;

    public ItemData ItemData { get; private set; }
    public DollCatalogData dollCatalogData;
    private readonly List<Vector2> points = new();

    /// <summary>
    /// 是否正被爪子焊住。被抓住期间速度由 FixedJoint2D 约束决定，脚本不能再插手，
    /// 否则钳制值（4）低于爪子上升速度（riseSpeed 6）时，约束会被反复拉长再回弹，表现为抽搐。
    /// </summary>
    public bool IsGrabbed { get; set; }

    public void SetData(DollCatalogData dollCatalogData,ItemData itemInfo)
    {
        ItemData = itemInfo;
        this.dollCatalogData = dollCatalogData;
        IsGrabbed = false;

        rigidBody2D = GetComponent<Rigidbody2D>();
        _ulockSpriteRenderer = transform.Find("ulockSprite").GetComponent<SpriteRenderer>();
        _ulockSpriteRenderer.gameObject.layer = LayerMask.NameToLayer("Doll");
        
        if (itemInfo == null) return;

        if (!InventoryManager.Instance.HasItemUnlock(itemInfo.ID))
        {
            _ulockSpriteRenderer.gameObject.SetActive(true);
            
            _ulockSpriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationDollImagePath(
                dollCatalogData.UlockImageName));
            
        }
        else
        {
            _ulockSpriteRenderer.sprite = AssetsManager.Instance.LoadAssets<Sprite>(GamePathTools.CombinationItemIconPath(
                dollCatalogData.UlockImageName));
           
        }
    }

    private void FixedUpdate()
    {
        if (rigidBody2D == null) return;
        if (IsGrabbed) return;

        rigidBody2D.linearVelocity = Vector2.ClampMagnitude(rigidBody2D.linearVelocity, 4f);
        rigidBody2D.angularVelocity = Mathf.Clamp(rigidBody2D.angularVelocity, -180f, 180f);
    }
}
