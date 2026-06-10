using UnityEngine;
using System;

public class MiniGame1Manager : MonoBehaviour
{
    public const int CookConfirmSuccess = 0;
    public const int CookConfirmFoodMtNotEnough = 1;
    public const int CookConfirmStaminaNotEnough = 2;

    private static MiniGame1Manager st;
    public static MiniGame1Manager St => st != null ? st : st = FindAnyObjectByType<MiniGame1Manager>();
    [SerializeField] MiniGameCookConfig config;
    [SerializeReference] ItemInfo[] footMtItemSlots;

    public MiniGameCookConfig Config => config;
    public event Action<int, ItemInfo> OnSlotChanged;
    public event Action<int> OnConfirm;
    public event Action<MiniGameCookResult> OnCookComplete;

    void Awake()
    {
        st = this;
    }
    void OnDestroy()
    {
        if(st == this)
            st = null;
    }

    public bool SeFoodMtItem(ItemInfo info)
    {
        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] == info)
                return false;
        }

        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] == null)
            {
                footMtItemSlots[i] = info;
                OnSlotChanged?.Invoke(i, info);
                return true;
            }
        }

        return false;
    }
    public void CancelSeFoodMt(int slotIndex)
    {
        footMtItemSlots[slotIndex] = null;
        OnSlotChanged?.Invoke(slotIndex, null);
    }

    public bool CancelSeFoodMt(ItemInfo info)
    {
        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] == info)
            {
                footMtItemSlots[i] = null;
                OnSlotChanged?.Invoke(i, null);
                return true;
            }
        }

        return true;
    }

    public void OnCookConfirm()
    {
        OnConfirm.Invoke(CheckCook());
    }

    public void OnCookEnd(bool isSuccess, CookQuality quality)
    {
        Debug.Log($"[MiniGame1] OnCookEnd isSuccess={isSuccess} quality={quality}");
        MiniGameCookResult result = CompleteCook(isSuccess, quality);
        OnCookComplete?.Invoke(result);
    }

    MiniGameCookResult CompleteCook(bool isSuccess, CookQuality quality)
    {
        ItemInfo[] ingredients = GetSelectedIngredients();
        long[] ingredientIds = new long[ingredients.Length];
        ItemInfo[] ingredientItems = new ItemInfo[ingredients.Length];
        for(int i = 0; i < ingredients.Length; i++)
        {
            ingredientIds[i] = ingredients[i].Id;
            ingredientItems[i] = ItemInfo.Create(ingredients[i].Id, 1);
        }
        Debug.Log($"[MiniGame1] CompleteCook ingredientIds=[{string.Join(",", ingredientIds)}]");

        FoodRecipe recipe = isSuccess ? ItemManager.St.Config.GetRecipe(ingredientIds) : null;
        long resultItemId = recipe?.ResultItemId ?? 0;
        long recipeItemId = recipe?.RecipeItemId ?? 0;
        bool isNewRecipe = false;
        ItemInfo recipeItem = null;
        ItemInfo resultItem = null;

        // 未匹配到配方时（烹饪失败，或食材未完全匹配任何配方）生成默认食物“拼好饭”
        if(recipe == null && resultItemId <= 0)
        {
            resultItemId = ItemManager.St.Config.FoodMakeDefaultId;
        }

        if(resultItemId > 0)
        {
            // 默认食物（拼好饭）无配方道具，不计入新配方解锁
            isNewRecipe = recipeItemId > 0 && !ItemManager.St.IsRecipeUnlocked(recipeItemId);
            Debug.Log($"[MiniGame1] 匹配配方 recipeItemId={recipeItemId} resultItemId={resultItemId} isNewRecipe={isNewRecipe}");

            if(isNewRecipe)
            {
                ItemManager.St.UnlockRecipe(recipeItemId);
                ItemManager.St.AddItem(recipeItemId, 1);
                recipeItem = ItemInfo.Create(recipeItemId, 1);
                if(ItemManager.St.GetItemData(recipeItemId) == null)
                    Debug.LogError($"[MiniGame1] 配方道具数据缺失 recipeItemId={recipeItemId}，NewRecipeUnlockPanel 将无法显示");
            }

            ItemManager.St.AddItem(resultItemId, 1);
            resultItem = ItemInfo.Create(resultItemId, 1);
            if(ItemManager.St.GetItemData(resultItemId) == null)
                Debug.LogError($"[MiniGame1] 结果道具数据缺失 resultItemId={resultItemId}");
        }

        ClearSelectedFoodMtItems();
        return new MiniGameCookResult(isSuccess, quality, recipeItem, resultItem, ingredientItems, isNewRecipe);
    }

    int CheckCook()
    {
        ItemInfo[] ingredients = GetSelectedIngredients();
        if(ingredients.Length < 2)
            return CookConfirmFoodMtNotEnough;

        for(int i = 0; i < ingredients.Length; i++)
        {
            if(!ItemManager.St.PlayerBag.CanConsumeFoodMtItem(ingredients[i], 1))
                return CookConfirmFoodMtNotEnough;
        }

        PlayerStats playerStats = FindAnyObjectByType<PlayerStats>();
        if(!playerStats.CanConsumeStamina(config.CookStaminaCost))
            return CookConfirmStaminaNotEnough;

        playerStats.SubStamina(config.CookStaminaCost);
        for(int i = 0; i < ingredients.Length; i++)
            ItemManager.St.PlayerBag.ConsumeItem(ingredients[i], 1);

        return CookConfirmSuccess;
    }
    ItemInfo[] GetSelectedIngredients()
    {
        int count = 0;
        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] != null)
                count++;
        }

        ItemInfo[] ingredients = new ItemInfo[count];
        int index = 0;
        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] != null)
                ingredients[index++] = footMtItemSlots[i];
        }

        return ingredients;
    }
    public void ClearSelectedFoodMtItems()
    {
        for(int i = 0; i < footMtItemSlots.Length; i++)
        {
            if(footMtItemSlots[i] != null)
            {
                footMtItemSlots[i] = null;
                OnSlotChanged?.Invoke(i, null);
            }
        }
    }
}
