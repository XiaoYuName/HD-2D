using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MiniGameCookConfig", menuName = "Scene/MiniGameCookConfig")]
public class MiniGameCookConfig : SerializedScriptableObject
{
    [LabelText("吃饭消耗行动力")][SerializeField] int eatFoodCosumeAp = 1;
    [LabelText("倒计时时间")][SerializeField] int countDownTime = 60;
    [LabelText("指示器移动速度")][SerializeField] float indicatorMoveSpeed;
    [LabelText("绿色判定区域数量")][SerializeField] int greenAreaCount = 4;
    [LabelText("绿色区域最小宽度")][SerializeField] float greenMinWidth = 80f;
    [LabelText("绿色区域最大宽度")][SerializeField] float greenMaxWidth = 280f;
    [LabelText("制作消耗体力")][SerializeField] float cookStaminaCost = 10f;
    [LabelText("右侧进度初始分数")][SerializeField] float startProgressScore;
    [LabelText("右侧进度最大分数")][SerializeField] float maxProgressScore = 100f;
    [LabelText("右侧进度目标分数")][SerializeField] float targetProgressScore = 100f;
    [LabelText("绿色区域添加分数")][SerializeField] float greenAddScore = 10f;
    [LabelText("橙色区域扣除分数")][SerializeField] float orangeSubScore = 5f;
    [LabelText("高品质剩余时间比例")][SerializeField] float goodTimeLeftRate = 0.25f;
    [LabelText("完美品质剩余时间比例")][SerializeField] float perfectTimeLeftRate = 0.5f;

    public int EatFoodCosumeAp => eatFoodCosumeAp;
    public int CountDownTime => countDownTime;
    public float IndicatorMoveSpeed => indicatorMoveSpeed;
    public int GreenAreaCount => greenAreaCount;
    public float GreenMinWidth => greenMinWidth;
    public float GreenMaxWidth => greenMaxWidth;
    public float CookStaminaCost => cookStaminaCost;
    public float StartProgressScore => startProgressScore;
    public float MaxProgressScore => maxProgressScore;
    public float TargetProgressScore => targetProgressScore;
    public float GreenAddScore => greenAddScore;
    public float OrangeSubScore => orangeSubScore;

    public CookQuality GetCookQuality(bool isSuccess, float timeLeftRate)
    {
        if(!isSuccess)
            return CookQuality.Fail;

        if(timeLeftRate >= perfectTimeLeftRate)
            return CookQuality.Perfect;

        if(timeLeftRate >= goodTimeLeftRate)
            return CookQuality.Good;

        return CookQuality.Normal;
    }

}

public enum CookQuality
{
    Fail,
    Normal,
    Good,
    Perfect,
}

public class MiniGameCookResult
{
    public bool IsSuccess { get; }
    public CookQuality Quality { get; }
    public ItemInfo RecipeItem { get; }
    public ItemInfo ResultItem { get; }
    public ItemInfo[] IngredientItems { get; }
    public bool IsNewRecipe { get; }

    public MiniGameCookResult(bool isSuccess, CookQuality quality, ItemInfo recipeItem, ItemInfo resultItem, ItemInfo[] ingredientItems, bool isNewRecipe)
    {
        IsSuccess = isSuccess;
        Quality = quality;
        RecipeItem = recipeItem;
        ResultItem = resultItem;
        IngredientItems = ingredientItems;
        IsNewRecipe = isNewRecipe;
    }
}