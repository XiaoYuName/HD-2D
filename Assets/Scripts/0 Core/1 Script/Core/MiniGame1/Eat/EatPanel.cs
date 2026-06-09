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


        foreach (ItemInfo item in ItemManager.St.PlayerBag.GetItemList(ItemType.Food))
        {
            ItemSeUI itemUI = Instantiate(foodMtItemUIPrefab, foodItemUIListContainer);
            itemUI.Init(item, OnFoodItemClick);
            foodItemUIList.Add(itemUI);
        }
    }
    void OnFoodItemClick(ItemInfo info)
    {
        curFoodItemSlotUI.Init(info);
        curFoodEffectText.text = "效果功能待完成";
    }

    void OnCurFoodItemSlotClick(ItemInfo info)
    {
        
    }
    #endregion
    #region EatFood
    void StartEat()
    {
        ItemManager.St.PlayerBag.ConsumeItem(curFoodItemSlotUI.Info, 1);

        eatEndTipPanel.SetActive(true);
        eatEndTipText.text = "体力50->999";
        
        PlayerInputManager.St.OnClick += EatEnd;
    }

    void EatEnd()
    {
        PlayerInputManager.St.OnClick -= EatEnd;
        eatEndTipPanel.SetActive(false);
    }
    #endregion
}
