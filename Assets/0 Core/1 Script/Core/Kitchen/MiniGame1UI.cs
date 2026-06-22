using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.Localization.Components;

public class MiniGame1UI : MonoBehaviour
{
    [FoldoutGroup(FgSet.Set)][SerializeField] GameObject cookPrePanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button cookButton, eatAloneButton, eatTogetherButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button closeButton, closePrePanelButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] EatPanel eatPanel;

    [FoldoutGroup(FgSet.Set)][SerializeField] ItemSeUI foodMtItemUIPrefab;
    [FoldoutGroup(FgSet.Set)][SerializeField] List<ItemSeUI> foodMtItemUIList;
    [FoldoutGroup(FgSet.Set)][SerializeField] RectTransform foodListContainer;
    [FoldoutGroup(FgSet.Set)][SerializeField] MiniGame1KitchenManager mg;

    [FoldoutGroup(FgSet.Set)][SerializeField] ItemSlotUI[] seFootMtSlots;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button cookConfirmButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] WarnTip tip;
    [FoldoutGroup(FgSet.Set)][SerializeField] MakeFoodResTip makeFoodResTip;
    [FoldoutGroup(FgSet.Set)][SerializeField] GameObject noFoodTip;
    [FoldoutGroup(FgSet.Set)][SerializeField] CookPanel cookPanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] NewRecipeUnlockPanel newRecipeUnlockPanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] LocalizeStringEvent makeConsumeStaminaText;
    [FoldoutGroup(FgSet.Set)][SerializeField] LocalizeStringEvent eatFoodButtonTextLse;
    bool isOpen;

    void Awake()
    {
        eatFoodButtonTextLse.SetVar(LocalizeVarSet.MiniGame.ApConsumeCount, mg.Config.EatFoodCosumeAp);
        cookButton.onClick.AddListener(ToggleMiniGame1Panel);
        closeButton.onClick.AddListener(ClosePanel);

        mg.OnSlotChanged += OnSlotChanged;
        mg.OnConfirm += OnConfirmShow;
        mg.OnCookComplete += OnCookComplete;
        cookPanel.OnEnd += mg.OnCookEnd;
        newRecipeUnlockPanel.OnClose += OnNewRecipePanelClose;
        cookPanel.gameObject.SetActive(false);
        newRecipeUnlockPanel.gameObject.SetActive(false);

        for(int i=0; i < seFootMtSlots.Length; i++)
        {
            seFootMtSlots[i].Init(null);
            seFootMtSlots[i].OnClick += OnFootMtSlotClick;
        }
        closePrePanelButton.onClick.AddListener(mg.Close);
        eatAloneButton.onClick.AddListener(OnEatAloneButtonClick);
    }
    void Start()
    {
        cookConfirmButton.onClick.AddListener(MiniGame1KitchenManager.St.OnCookConfirm);
        RefreshMakeConsumeStaminaText();
    }

    // 按配置的制作消耗体力刷新按钮文本（"制作消耗{SpConsumeCount}体力"）
    void RefreshMakeConsumeStaminaText()
    {
        makeConsumeStaminaText.SetVar(LocalizeVarSet.MiniGame.SpConsumeCount, (int)mg.Config.CookStaminaCost);
    }
    void OnEatAloneButtonClick()
    {
        eatPanel.Open();
    }
    #region Slot
    // 当食物槽被点击
    void OnFootMtSlotClick(int slotIndex)
    {
        foreach(var itemUI in foodMtItemUIList)
        {
            if(itemUI.Info == seFootMtSlots[slotIndex].Info)
            {
                itemUI.SwitchState(ItemSeUI.State.None);
            }
        }
        mg.CancelSeFoodMt(slotIndex);
    }
    #endregion
    #region CookPrePanel
    void ToggleMiniGame1Panel()
    {
        isOpen = !isOpen;

        if(isOpen)
            RefreshCookPrePanel();

        cookPrePanel.SetActive(isOpen);
    }

    // 打开备菜面板并刷新（用于新配方解锁后返回）
    void OpenCookPrePanel()
    {
        isOpen = true;
        RefreshCookPrePanel();
    }

    // 重置已选食材并按背包最新内容刷新食材列表
    void RefreshCookPrePanel()
    {
        mg.ClearSelectedFoodMtItems();

        for(int i = foodListContainer.childCount - 1; i >= 0; i--)
            Destroy(foodListContainer.GetChild(i).gameObject);
        foodMtItemUIList.Clear();

        var ingredients = ItemManager.St.PlayerBag.GetItemList(ItemType.Ingredient);

        noFoodTip.SetActive(ingredients.Count == 0);

        ingredients.ForEach(info =>
        {
            ItemSeUI itemUI = Instantiate(foodMtItemUIPrefab, foodListContainer);
            itemUI.Init(info, OnFoodMtItemClick);
            foodMtItemUIList.Add(itemUI);
        });

        for(int i = 0; i < seFootMtSlots.Length; i++)
            seFootMtSlots[i].Init(null);
    }
    void ClosePanel()
    {
        cookPrePanel.SetActive(false);
    }
    #endregion
    #region NewRecipePanelClose
    void OnNewRecipePanelClose()
    {
        OpenCookPrePanel();
    }
    #endregion
    #region FoodMtItemClick
    void OnFoodMtItemClick(ItemInfo info)
    {
        ItemSeUI itemUI = foodMtItemUIList.Find(ui => ui.Info == info);
        if(itemUI == null)
            return;

        if(itemUI.CurState == ItemSeUI.State.None)
        {
            // 选中成功（未重复且有空槽）才切换为已选状态
            if(mg.SeFoodMtItem(info))
                itemUI.SwitchState(ItemSeUI.State.Se);
        }
        else
        {
            mg.CancelSeFoodMt(info);
            itemUI.SwitchState(ItemSeUI.State.None);
        }
    }
    #endregion

    void OnSlotChanged(int slotIndex, ItemInfo info)
    {
        seFootMtSlots[slotIndex].Init(info);
    }

    void OnConfirmShow(int id)
    {
        if(id == MiniGame1KitchenManager.CookConfirmSuccess)
        {
            // isOpen = false;
            cookPanel.Init(mg.Config);
        }
        else if(id == MiniGame1KitchenManager.CookConfirmFoodMtNotEnough)
        {
            tip.ShowTip(LocalizeTableSet.Kitchen, LocalizeVarSet.MiniGame1CookGame.NeedAtLeastTwoIngredients);
        }
        else if(id == MiniGame1KitchenManager.CookConfirmStaminaNotEnough)
        {
            tip.ShowTip(LocalizeTableSet.Kitchen, LocalizeVarSet.MiniGame.NotEnoughStamina);
        }
    }

    void OnCookComplete(MiniGameCookResult result)
    {
        Debug.Log($"[MiniGame1] UI OnCookComplete IsSuccess={result.IsSuccess} IsNewRecipe={result.IsNewRecipe} " +
            $"recipeItem={(result.RecipeItem == null ? "null" : result.RecipeItem.Id.ToString())} " +
            $"resultItem={(result.ResultItem == null ? "null" : result.ResultItem.Id.ToString())} " +
            $"ingredientCount={(result.IngredientItems == null ? 0 : result.IngredientItems.Length)}");

        if(!result.IsSuccess)
        {
            OpenCookPrePanel();
            makeFoodResTip.ShowTip("制作失败，做出了一个拼好饭", result.ResultItem);
            return;
        }

        if(result.IsNewRecipe)
        {
            newRecipeUnlockPanel.Init(result.RecipeItem, result.ResultItem, result.IngredientItems);
        }
        else
        {
            OpenCookPrePanel();
            makeFoodResTip.ShowTip($"{result.ResultItem?.Name ?? ""} 制作成功", result.ResultItem);
        }
    }
}
