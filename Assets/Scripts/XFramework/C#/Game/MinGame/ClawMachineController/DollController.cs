using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework;

public class DollController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;
    private SpriteRenderer _ulockSpriteRenderer;
    private SpriteRenderer lockSpriteRenderer;

    public ItemData ItemData { get; private set; }
    public DollCatalogData dollCatalogData;
    private readonly List<Vector2> points = new();

    public void SetData(DollCatalogData dollCatalogData,ItemData itemInfo)
    {
        ItemData = itemInfo;
        this.dollCatalogData = dollCatalogData;
        
        rigidBody2D = GetComponent<Rigidbody2D>();
        _ulockSpriteRenderer = transform.Find("ulockSprite").GetComponent<SpriteRenderer>();
        lockSpriteRenderer = transform.Find("lockSprite").GetComponent<SpriteRenderer>();
        _ulockSpriteRenderer.gameObject.layer = LayerMask.NameToLayer("Doll");
        lockSpriteRenderer.gameObject.layer = LayerMask.NameToLayer("Doll");
        
        if (itemInfo == null) return;

        if (!InventoryManager.Instance.HasItemUnlock(itemInfo.ID))
        {
            _ulockSpriteRenderer.gameObject.SetActive(true);
            lockSpriteRenderer.gameObject.SetActive(false);
        }
        else
        {
            _ulockSpriteRenderer.gameObject.SetActive(false);
            lockSpriteRenderer.gameObject.SetActive(true);
        }
    }

    private void FixedUpdate()
    {
        rigidBody2D.linearVelocity = Vector2.ClampMagnitude(rigidBody2D.linearVelocity, 4f);
        rigidBody2D.angularVelocity = Mathf.Clamp(rigidBody2D.angularVelocity, -180f, 180f);
    }
}
