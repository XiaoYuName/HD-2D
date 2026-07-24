using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

[CreateAssetMenu(fileName = nameof(MachiRoomGameConfig), menuName = ConfigMenuNameSet.MiniGame + nameof(MachiRoomGameConfig))]
public class MachiRoomGameConfig : SerializedScriptableObject
{
    [FoldoutGroup("草稿"), LabelText("灵感消耗档位"), SerializeField]
    int[] draftInspirationCosts;
    [FoldoutGroup("草稿"), LabelText("三种稿件名称多语言Key"), SerializeField]
    string[] draftNameKeyList;
    [FoldoutGroup("创作"), LabelText("自然进度灵感除数"), MinValue(0.01f), SerializeField]
    float naturalProgressInspirationDivisor = 10f;
    [FoldoutGroup("创作"), LabelText("催稿进度灵感除数"), MinValue(0.01f), SerializeField]
    float rushProgressInspirationDivisor = 2f;
    [FoldoutGroup("创作"), LabelText("催稿行动力消耗"), MinValue(1), SerializeField]
    int rushActionPointCost = 1;
    [FoldoutGroup("创作"), LabelText("催稿灵感消耗"), MinValue(1), SerializeField]
    int rushInspirationCost = 10;
    [FoldoutGroup("创作"), LabelText("催稿压力增加"), MinValue(0), SerializeField]
    int rushPressureAdd = 20;
    [FoldoutGroup("创作"), LabelText("低灵感阈值"), MinValue(0), SerializeField]
    int lowInspirationThreshold = 60;
    [FoldoutGroup("创作"), LabelText("低灵感评分扣除"), MinValue(0), SerializeField]
    int lowInspirationScorePenalty = 5;
    [FoldoutGroup("创作"), LabelText("画稿完成进度"), MinValue(1f), SerializeField]
    float completeProgress = 100f;
    [FoldoutGroup("创作"), LabelText("最低获奖评分"), MinValue(0), SerializeField]
    int rewardMinScore = 60;

    [FoldoutGroup("刮刮乐"), LabelText("遮罩纹理尺寸"), MinValue(64), SerializeField]
    int scratchMaskTextureSize = 256;
    [FoldoutGroup("刮刮乐"), LabelText("画笔半径"), MinValue(1f), SerializeField]
    float scratchBrushRadius = 36f;
    [FoldoutGroup("刮刮乐"), LabelText("完成刮除比例"), Range(0.1f, 1f), SerializeField]
    float scratchCompleteRatio = 0.65f;
    [FoldoutGroup("刮刮乐"), LabelText("画笔指针偏移"), SerializeField]
    Vector2 scratchPenOffset;
    [FoldoutGroup("表现"), LabelText("催稿动画时长"), MinValue(0.01f), SerializeField]
    float rushAnimationDuration = 3f;

    public int[] DraftInspirationCosts => draftInspirationCosts;
    public string[] DraftNameKeyList => draftNameKeyList;
    public float NaturalProgressInspirationDivisor => naturalProgressInspirationDivisor;
    public float RushProgressInspirationDivisor => rushProgressInspirationDivisor;
    public int RushActionPointCost => rushActionPointCost;
    public int RushInspirationCost => rushInspirationCost;
    public int RushPressureAdd => rushPressureAdd;
    public int LowInspirationThreshold => lowInspirationThreshold;
    public int LowInspirationScorePenalty => lowInspirationScorePenalty;
    public float CompleteProgress => completeProgress;
    public int RewardMinScore => rewardMinScore;
    public int ScratchMaskTextureSize => scratchMaskTextureSize;
    public float ScratchBrushRadius => scratchBrushRadius;
    public float ScratchCompleteRatio => scratchCompleteRatio;
    public Vector2 ScratchPenOffset => scratchPenOffset;
    public float RushAnimationDuration => rushAnimationDuration;

    public bool IsDraftInspirationCost(int inspirationCost)
    {
        for (int i = 0; i < draftInspirationCosts.Length; i++)
            if (draftInspirationCosts[i] == inspirationCost)
                return true;
        return false;
    }

    public float GetDraftProbability(ManuscriptItemData data, int inspirationCost)
    {
        if (inspirationCost == draftInspirationCosts[0])
            return data.Prob30;
        if (inspirationCost == draftInspirationCosts[1])
            return data.Prob50;
        if (inspirationCost == draftInspirationCosts[2])
            return data.Prob100;

        throw new System.ArgumentOutOfRangeException(
            nameof(inspirationCost),
            inspirationCost,
            "未配置对应的稿件抽取概率档位");
    }
}
