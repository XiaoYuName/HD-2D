using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using XFramework;

public class EatPanel : MonoBehaviour
{
    public enum EatMode { Alone, WithMachi }

    [FoldoutGroup(FgSet.Set)][SerializeField] Image eatAnim;
    [FoldoutGroup(FgSet.Set)][SerializeField] Sprite eatAloneSprite;
    [FoldoutGroup(FgSet.Set)][SerializeField] Sprite eatWithMachiSprite;
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
    [FoldoutGroup("EatEndTip")][SerializeField] Image eatEndTipAnim;
    [FoldoutGroup("EatEndTip")][SerializeField] Sprite eatEndTipAloneSprite;
    [FoldoutGroup("EatEndTip")][SerializeField] Sprite eatEndTipWithMachiSprite;
    [FoldoutGroup("EatEndTip")][SerializeField] TextMeshProUGUI eatEndTipText;

    EatMode curEatMode;

    void Awake()
    {
        closeButton.onClick.AddListener(Close);
        eatButton.onClick.AddListener(StartEat);
        curFoodItemSlotUI.OnClick += OnCurFoodItemSlotClick;

        curFoodItemSlotUI.Init(null);
    }
    void OnDestroy()
    {
        InventoryManager.Instance.UnregisterItemConsumablesTypeChangeCallBack(ItemConsumType.Food, OnFoodItemsChanged);
    }
    #region Open
    public void Open(EatMode mode)
    {
        curEatMode = mode;
        eatAnim.sprite = mode == EatMode.Alone ? eatAloneSprite : eatWithMachiSprite;

        eatPanel.SetActive(true);
        // 先反注册防止重复挂接（外层面板直接关闭时不会走本类 Close）；
        // 注册时 isTrigger=true 立即构建一次列表，之后食物增减由回调实时刷新
        InventoryManager.Instance.UnregisterItemConsumablesTypeChangeCallBack(ItemConsumType.Food, OnFoodItemsChanged);
        InventoryManager.Instance.RegisterItemConsumablesTypeChangeCallBack(ItemConsumType.Food, OnFoodItemsChanged);
    }
    void Close()
    {
        InventoryManager.Instance.UnregisterItemConsumablesTypeChangeCallBack(ItemConsumType.Food, OnFoodItemsChanged);
        SetCurFood(null);   // 关闭时清空已选食物并取消选择
        eatPanel.SetActive(false);
    }
    // 背包食物变化回调：重建列表并同步已选格（吃完清空，未吃完刷新数量）
    void OnFoodItemsChanged(List<ItemInfo> foodItems)
    {
        RefreshFoodItemUIList(foodItems);

        if(curFoodItemSlotUI.Info != null)
            SetCurFood(curFoodItemSlotUI.Info.Count > 0 ? curFoodItemSlotUI.Info : null);
    }
    void RefreshFoodItemUIList(List<ItemInfo> foodItems)
    {
        for (int i = 0; i < foodItemUIList.Count; i++)
            Destroy(foodItemUIList[i].gameObject);
        foodItemUIList.Clear();

        foreach (ItemInfo item in foodItems)
        {
            ItemSeUI itemUI = Instantiate(foodMtItemUIPrefab, foodItemUIListContainer);
            itemUI.Init(item, OnFoodItemClick);
            foodItemUIList.Add(itemUI);
        }
    }
    void OnFoodItemClick(ItemInfo info)
    {
        SetCurFood(info);
    }

    void OnCurFoodItemSlotClick(int slotIndex)
    {
        SetCurFood(null);
    }

    void SetCurFood(ItemInfo info)
    {
        curFoodItemSlotUI.Init(info);
        curFoodEffectText.text = info == null ? string.Empty : "效果功能待定";
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

        if(MiniGame1KitchenManager.St.Config.EatFoodCosumeAp > GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value)
        {
            Debug.Log("行动力不足");
            return;
        }

        GameDataManager.Instance.RemoveProperty(PropertyType.Strength, (int)MiniGame1KitchenManager.St.Config.CookStaminaCost);
        // ConsumeItem 会触发已注册的食物变化回调，列表与已选格随之刷新
        InventoryManager.Instance.ConsumeItem(curFoodItemSlotUI.Info.ID, 1);
        PlayerInputManager.Instance.OnClick += EatEnd;

        eatEndTipAnim.sprite = curEatMode == EatMode.Alone ? eatEndTipAloneSprite : eatEndTipWithMachiSprite;
        eatEndTipPanel.SetActive(true);
        eatEndTipText.text = "体力50->999";
    }

    void EatEnd()
    {
        PlayerInputManager.Instance.OnClick -= EatEnd;
        eatEndTipPanel.SetActive(false);
    }
    #endregion
}
