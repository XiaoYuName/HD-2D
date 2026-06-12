using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class FoodRecipe
{
    [LabelText("配方道具ID")][SerializeField] long recipeItemId;
    [LabelText("结果道具ID")][SerializeField] long resultItemId;
    [LabelText("食材ID列表")][SerializeField] long[] ingredientIds;
    #region Get
    public long RecipeItemId => recipeItemId;
    public long ResultItemId => resultItemId;
    #endregion
    #region IsMatch
    // 所选食材 selectedIds 必须与本配方所需食材完全一致（multiset 相等，顺序无关）才匹配
    public bool IsMatch(long[] selectedIds)
    {
        if (ingredientIds.Length == 0)
            return false;
        if (selectedIds == null || selectedIds.Length != ingredientIds.Length)
            return false;

        bool[] used = new bool[selectedIds.Length];
        foreach (long need in ingredientIds)
        {
            bool found = false;
            for (int i = 0; i < selectedIds.Length; i++)
            {
                if (!used[i] && selectedIds[i] == need)
                {
                    used[i] = true;
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        return true;
    }
    #endregion
    #region Create
    public static FoodRecipe Create(long recipeItemId, long resultItemId, long[] ingredientIds)
    {
        return new FoodRecipe
        {
            recipeItemId = recipeItemId,
            resultItemId = resultItemId,
            ingredientIds = ingredientIds
        };
    }
    #endregion
}
