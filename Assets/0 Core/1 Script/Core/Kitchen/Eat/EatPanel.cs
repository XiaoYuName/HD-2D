using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class EatPanel : MonoBehaviour
{
    [FoldoutGroup(FgSet.Set)][SerializeField] GameObject eatPanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button closeButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] ItemSeUI foodMtItemUIPrefab;
    [FoldoutGroup(FgSet.Set)][SerializeField] List<ItemSeUI> foodItemUIList;
    [FoldoutGroup(FgSet.Set)][SerializeField] RectTransform foodItemUIListContainer;
    [FoldoutGroup(FgSet.Set)][SerializeField] TextMeshProUGUI eatFoodTipText;
    [FoldoutGroup("EatFood")][SerializeField] ItemSlotUI curFoodItemSlotUI;
    [FoldoutGroup("EatFood")][SerializeField] TextMeshProUGUI curFoodEffectText;
    [FoldoutGroup("EatFood")][SerializeField] Button eatButton;

    [FoldoutGroup("EatEndTip")][SerializeField] GameObject eatEndTipPanel;
    [FoldoutGroup("EatEndTip")][SerializeField] TextMeshProUGUI eatEndTipText;

    void Awake()
    {
        closeButton.onClick.AddListener(Close);
        eatButton.onClick.AddListener(StartEat);
        curFoodItemSlotUI.OnClick += OnCurFoodItemSlotClick;

        curFoodItemSlotUI.Init(null);
    }
    #region Open
    public void Open()
    {
        eatPanel.SetActive(true);
        RefreshFoodItemUIList();
    }
    void Close()
    {
        eatPanel.SetActive(false);
    }
    void RefreshFoodItemUIList()
    {
        for (int i = 0; i < foodItemUIList.Count; i++)
            Destroy(foodItemUIList[i].gameObject);
        foodItemUIList.Clear();


        foreach (ItemInfo item in PlayerInfo.St.Bag.GetItemList(ItemType.Food))
        {
            ItemSeUI itemUI = Instantiate(foodMtItemUIPrefab, foodItemUIListContainer);
            itemUI.Init(item, OnFoodItemClick);
            foodItemUIList.Add(itemUI);
        }
    }
    void OnFoodItemClick(ItemInfo info)
    {
        curFoodItemSlotUI.Init(info);
        curFoodEffectText.text = "效果功能待定";
    }

    void OnCurFoodItemSlotClick(int slotIndex)
    {
        curFoodItemSlotUI.Init(null);
    }
    #endregion
    #region EatFood
    void StartEat()
    {
        if(curFoodItemSlotUI.Info == null)
        {
            Debug.Log("请选择食物");
            return;
        }
        
        if(MiniGame1KitchenManager.St.Config.EatFoodCosumeAp > PlayerInfo.St.Stats.CurAp)
        {
            Debug.Log("行动力不足");
            return;
        }
            
        PlayerInfo.St.Stats.SubSp(MiniGame1KitchenManager.St.Config.CookStaminaCost);
        ItemManager.St.PlayerBag.ConsumeItem(curFoodItemSlotUI.Info, 1);
        PlayerInputManager.Instance.OnClick += EatEnd;
        
        eatEndTipPanel.SetActive(true);
        eatEndTipText.text = "体力50->999";
    }

    void EatEnd()
    {
        PlayerInputManager.Instance.OnClick -= EatEnd;
        // 刷新UI
        RefreshFoodItemUIList();

        if(curFoodItemSlotUI.Info != null && curFoodItemSlotUI.Info.Count > 0)
            curFoodItemSlotUI.Init(curFoodItemSlotUI.Info);
        else
            curFoodItemSlotUI.Init(null);

        eatEndTipPanel.SetActive(false);
    }
    #endregion
}
