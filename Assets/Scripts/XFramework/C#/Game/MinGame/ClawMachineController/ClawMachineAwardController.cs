using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XFramework;

public class ClawMachineAwardController : MonoBehaviour
{
    private Queue<long> UnlocakItemIDs = new Queue<long>();
    private bool isShowingItemInfo = false;

    private void Start()
    {
        UnlocakItemIDs = new Queue<long>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Doll"))
        {
            Debug.Log("获取抓到的娃娃奖励!");
            var con = other.GetComponent<DollController>();
            InventoryManager.Instance.AddItem(con.dollCatalogData.ItemID,1);
            if (!InventoryManager.Instance.HasItemUnlock(con.dollCatalogData.ItemID))
            {
                UnlocakItemIDs.Enqueue(con.dollCatalogData.ItemID);
                if (!isShowingItemInfo)
                {
                    isShowingItemInfo = true;
                    ShowingItemInfo();
                }
                InventoryManager.Instance.UlockItem(con.dollCatalogData.ItemID);
            }
            AssetsManager.Instance.FreeGameObject(other.gameObject);
            GuideManager.Instance.UpdateDollGameNumber(1);
            con.Release();
        }
    }

    private void ShowingItemInfo()
    {
        isShowingItemInfo = true;
        PopUnlockDollWindowsUI popUnlockDollWindowsUI =  UISystem.Instance.OpenUI<PopUnlockDollWindowsUI>("PopUnlockDollWindowsUI");
        popUnlockDollWindowsUI.ShowItemInfo(UnlocakItemIDs.Dequeue(), () =>
        {
            if (UnlocakItemIDs.Count > 0)
            {
                ShowingItemInfo();
            }
            else
            {
                isShowingItemInfo = false;
            }
        });
    }
}
