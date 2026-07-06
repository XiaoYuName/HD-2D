using System;
using UnityEngine;
using XFramework;

public class ClawMachineAwardController : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Doll"))
        {
            Debug.Log("获取抓到的娃娃奖励!");
            var con = other.GetComponent<DollController>();
            con.Release();
            InventoryManager.Instance.AddItem(con.dollCatalogData.ItemID,1);
            AssetsManager.Instance.FreeGameObject(other.gameObject);
            GuideManager.Instance.UpdateDollGameNumber(1);
        }
    }
}
