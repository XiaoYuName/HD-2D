using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine.Localization.Components;
using XFramework;

public class MiniGame1UI : MonoBehaviour
{
    [FoldoutGroup(FgSet.Set)][SerializeField] GameObject cookPrePanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button cookButton, eatAloneButton, eatTogetherButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button closeButton, closePrePanelButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] EatPanel eatPanel;

    [FoldoutGroup(FgSet.Set)][SerializeField] ItemSeUI foodMtItemUIPrefab;
    [FoldoutGroup(FgSet.State)][SerializeField] List<ItemSeUI> foodMtItemUIList;
    [FoldoutGroup(FgSet.Set)][SerializeField] RectTransform foodListContainer;
    [FoldoutGroup(FgSet.Set)][SerializeField] MiniGame1KitchenManager mg;

    [FoldoutGroup(FgSet.Set)][SerializeField] ItemSlotUI[] seFootMtSlots;
    [FoldoutGroup(FgSet.Set)][SerializeField] Button cookConfirmButton;
    [FoldoutGroup(FgSet.Set)][SerializeField] WarnTip tip;
    [FoldoutGroup(FgSet.Set)][SerializeField] GameObject noFoodTip;
    [FoldoutGroup(FgSet.Set)][SerializeField] CookPanel cookPanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] NewRecipeUnlockPanel newRecipeUnlockPanel;
    [FoldoutGroup(FgSet.Set)][SerializeField] LocalizeStringEvent makeConsumeStaminaText;
    [FoldoutGroup(FgSet.Set)][SerializeField] LocalizeStringEvent eatFoodButtonTextLse;

    /// <summary>新配方解锁面板关闭后待展示的烹饪结果（等待 NewRecipeUnlockPanel.OnClose 再弹出 CookSettlePanel）</summary>
    MiniGameCookResult pendingCookResult;

    void Awake()
    {
        eatFoodButtonTextLse.SetVar(LocVarSet.MiniGame.ApConsumeCount, mg.Config.EatFoodCosumeAp);
        cookButton.onClick.AddListener(OpenCookPrePanel);

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

        closeButton.onClick.AddListener(mg.OnCloseButton);
        closePrePanelButton.onClick.AddListener(ClosePrePanel);
        eatAloneButton.onClick.AddListener(OnEatAloneButtonClick);
        eatTogetherButton.onClick.AddListener(OnEatTogetherButtonClick);
    }
    void OnEnable()
    {
        // 食材列表随背包变化实时刷新（食材分布在材料/消耗品两类，都要注册）；
        // isTrigger=false，首次构建由打开面板时的 RefreshCookPrePanel 完成
        InventoryManager.Instance.RegisterMaterialTypeChangeCallBack(ItemMaterialType.Ingredient, OnIngredientItemsChanged, false);
        InventoryManager.Instance.RegisterItemConsumablesTypeChangeCallBack(ItemConsumType.Ingredient, OnIngredientItemsChanged, false);
    }
    void OnDisable()
    {
        InventoryManager.Instance.UnregisterMaterialTypeChangeCallBack(ItemMaterialType.Ingredient, OnIngredientItemsChanged);
        InventoryManager.Instance.UnregisterItemConsumablesTypeChangeCallBack(ItemConsumType.Ingredient, OnIngredientItemsChanged);
    }
    void Start()
    {
        cookConfirmButton.onClick.AddListener(MiniGame1KitchenManager.St.OnCookConfirm);
        RefreshMakeConsumeStaminaText();
    }

    // 按配置的制作消耗体力刷新按钮文本（"制作消耗{SpConsumeCount}体力"）
    void RefreshMakeConsumeStaminaText()
    {
        makeConsumeStaminaText.SetVar(LocVarSet.MiniGame.SpConsumeCount, (int)mg.Config.CookStaminaCost);
    }
    void OnEatAloneButtonClick()
    {
        eatPanel.Open(EatPanel.EatMode.Alone);
    }
    void OnEatTogetherButtonClick()
    {
        eatPanel.Open(EatPanel.EatMode.WithMachi);
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
    // 打开备菜面板并刷新（用于新配方解锁后返回）
    void OpenCookPrePanel()
    {
        RefreshCookPrePanel();
         cookPrePanel.SetActive(true);
    }

    // 重置已选食材并按背包最新内容刷新食材列表
    void RefreshCookPrePanel()
    {
        mg.ClearSelectedFoodMtItems();
        RebuildFoodMtItemUIList();

        for(int i = 0; i < seFootMtSlots.Length; i++)
            seFootMtSlots[i].Init(null);
    }

    // 背包食材变化回调：只重建列表不动已选格（烹饪确认时消耗食材也会触发，此时选择还在用）
    void OnIngredientItemsChanged(List<ItemInfo> _)
    {
        RebuildFoodMtItemUIList();
    }

    // 按背包最新内容重建食材列表，并还原仍被选中项的高亮
    void RebuildFoodMtItemUIList()
    {
        for(int i = foodListContainer.childCount - 1; i >= 0; i--)
            Destroy(foodListContainer.GetChild(i).gameObject);
        foodMtItemUIList.Clear();

        var ingredients = InventoryManager.Instance.GetMaterialList(ItemMaterialType.Ingredient);
        ingredients.AddRange(InventoryManager.Instance.GetConsumableList(ItemConsumType.Ingredient));
        ingredients.AddRange(InventoryManager.Instance.GetMaterialList(ItemMaterialType.Fish));
        
        noFoodTip.SetActive(ingredients.Count == 0);

        ingredients.ForEach(info =>
        {
            ItemSeUI itemUI = Instantiate(foodMtItemUIPrefab, foodListContainer);
            itemUI.Init(info, OnFoodMtItemClick);
            if(IsFoodMtSelected(info))
                itemUI.SwitchState(ItemSeUI.State.Se);
            foodMtItemUIList.Add(itemUI);
        });
    }

    bool IsFoodMtSelected(ItemInfo info)
    {
        foreach(var slot in seFootMtSlots)
        {
            if(slot.Info == info)
                return true;
        }
        return false;
    }
    void ClosePrePanel()
    {
        cookPrePanel.SetActive(false);
    }
    #endregion
    #region NewRecipePanelClose
    void OnNewRecipePanelClose()
    {
        ShowCookSettlePanel(pendingCookResult);
        pendingCookResult = null;
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
            tip.Show(LocTableSet.Kitchen, LocVarSet.MiniGame1CookGame.NeedAtLeastTwoIngredients);
        }
        else if(id == MiniGame1KitchenManager.CookConfirmStaminaNotEnough)
        {
            tip.Show(LocTableSet.Kitchen, LocVarSet.MiniGame.NotEnoughStamina);
        }
    }

    void OnCookComplete(MiniGameCookResult result)
    {
        Debug.Log($"[MiniGame1] UI OnCookComplete IsSuccess={result.IsSuccess} IsNewRecipe={result.IsNewRecipe} " +
            $"recipeItem={(result.RecipeItem == null ? "null" : result.RecipeItem.ID.ToString())} " +
            $"resultItem={(result.ResultItem == null ? "null" : result.ResultItem.ID.ToString())} " +
            $"ingredientCount={(result.IngredientItems == null ? 0 : result.IngredientItems.Length)}");

        QuestEventBus.ReportMiniGameFinished(MiniGameType.Cooking, result.IsSuccess);   // 任务系统：本局结算上报

        if(result.IsSuccess && result.IsNewRecipe)
        {
            pendingCookResult = result;
            newRecipeUnlockPanel.Init(result.RecipeItem, result.ResultItem, result.IngredientItems);
            return;
        }

        ShowCookSettlePanel(result);
    }

    void ShowCookSettlePanel(MiniGameCookResult result)
    {
        CookSettlePanel.Data data = new()
        {
            Result = result,
            OnBack = OnCookSettlePanelClose,
        };
        UISystem.Instance.OpenUI<CookSettlePanel>(UIPanelIdSet.CookSettlePanel).Show(data);
    }

    // 结算面板点击返回后，重置已选食材并刷新备菜面板
    void OnCookSettlePanelClose()
    {
        OpenCookPrePanel();
    }
}
