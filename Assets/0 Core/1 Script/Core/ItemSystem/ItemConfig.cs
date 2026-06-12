using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemConfig", menuName = "Config/ItemConfig")]
public class ItemConfig : SerializedScriptableObject
{
    [FoldoutGroup("ItemConfig")][SerializeField] Dictionary<long, ItemData> itemDataDict;
    [FoldoutGroup("FoodConfig")][LabelText("食物配方")][SerializeField] FoodRecipe[] foodRecipes;
    [FoldoutGroup("FoodConfig")][LabelText("食物制作无配方默认合成Id")] [SerializeField] long foodMakeDefaultId;
    #region Get
    public Dictionary<long, ItemData> ItemDataDict => itemDataDict;
    public ItemData GetItemData(long id) => itemDataDict.TryGetValue(id, out var d) ? d : null;
    public long FoodMakeDefaultId => foodMakeDefaultId;

    public FoodRecipe GetRecipe(IReadOnlyList<long> ingredientIds)
    {
        if (foodRecipes == null || ingredientIds == null) return null;

        long[] selected = new long[ingredientIds.Count];
        for (int i = 0; i < selected.Length; i++)
            selected[i] = ingredientIds[i];

        // 所选食材必须与某配方所需食材完全一致（顺序无关）才匹配；若有多个匹配，返回检索到的第一个
        foreach (var recipe in foodRecipes)
        {
            if (recipe.IsMatch(selected))
                return recipe;
        }
        return null;
    }
    #endregion

#if UNITY_EDITOR
    [FoldoutGroup("Query")][LabelText("查询ID")][SerializeField] long queryId;
    [FoldoutGroup("Query")][ShowInInspector][LabelText("查询结果")][ReadOnly] ItemData queryResult;

    [FoldoutGroup("Query")]
    [Button("通过ID查询ItemData")]
    public ItemData QueryItemDataById()
    {
        queryResult = GetItemData(queryId);
        if (queryResult == null)
            Debug.LogWarning($"[ItemConfig] 未找到 Id={queryId} 对应的 ItemData");
        else
            Debug.Log($"[ItemConfig] Id={queryResult.Id} 名称={queryResult.Name} 图标={(string.IsNullOrEmpty(queryResult.IconPath) ? "无" : queryResult.IconPath)}");
        return queryResult;
    }

    [Button]
    public void ImportFromExcel() => ItemConfigImporter.Import(this);

    public void SetItemData(Dictionary<long, ItemData> data) => itemDataDict = data;
    public void SetFoodRecipes(FoodRecipe[] recipes) => foodRecipes = recipes;
#endif
}