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
    [FoldoutGroup("创作"), LabelText("成功绘画判定最低评分"), MinValue(0), SerializeField]
    int successMinScore = 60;

    [FoldoutGroup("刮刮乐"), LabelText("遮罩纹理尺寸"), MinValue(64), SerializeField]
    int scratchMaskTextureSize = 256;
    [FoldoutGroup("刮刮乐"), LabelText("画笔半径"), MinValue(1f), SerializeField]
    float scratchBrushRadius = 36f;
    [FoldoutGroup("刮刮乐"), LabelText("完成刮除比例"), Range(0.1f, 1f), SerializeField]
    float scratchCompleteRatio = 0.65f;
    [FoldoutGroup("刮刮乐"), LabelText("画笔指针偏移"), SerializeField]
    Vector2 scratchPenOffset;
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光生成间距"), MinValue(1f), SerializeField]
    float sparkleSpawnDistance = 26f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光存活时长"), MinValue(0.05f), SerializeField]
    float sparkleLifetime = 0.5f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光尺寸范围"), MinMaxSlider(4f, 120f, true), SerializeField]
    Vector2 sparkleSizeRange = new(18f, 34f);
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光散布半径"), MinValue(0f), SerializeField]
    float sparkleSpreadRadius = 16f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光上飘距离"), SerializeField]
    float sparkleRiseDistance = 26f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("闪光同屏上限"), MinValue(1), SerializeField]
    int sparkleMaxCount = 24;
    [FoldoutGroup("刮刮乐闪光"), LabelText("普通稿件闪光颜色"), SerializeField]
    Color sparkleNormalColor = Color.white;
    [FoldoutGroup("刮刮乐闪光"), LabelText("特殊稿件彩光色相步进"), Range(0f, 1f), SerializeField]
    float sparkleSpecialHueStep = 0.13f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("特殊稿件彩光饱和度"), Range(0f, 1f), SerializeField]
    float sparkleSpecialSaturation = 0.7f;
    [FoldoutGroup("刮刮乐闪光"), LabelText("特殊稿件彩光循环时长"), MinValue(0.1f), SerializeField]
    float sparkleSpecialCycleDura = 1.5f;

    [FoldoutGroup("表现"), LabelText("催稿动画时长"), MinValue(0.01f), SerializeField]
    float rushAnimDura;

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
    public int SuccessMinScore => successMinScore;
    public int ScratchMaskTextureSize => scratchMaskTextureSize;
    public float ScratchBrushRadius => scratchBrushRadius;
    public float ScratchCompleteRatio => scratchCompleteRatio;
    public Vector2 ScratchPenOffset => scratchPenOffset;
    public float SparkleSpawnDistance => sparkleSpawnDistance;
    public float SparkleLifetime => sparkleLifetime;
    public Vector2 SparkleSizeRange => sparkleSizeRange;
    public float SparkleSpreadRadius => sparkleSpreadRadius;
    public float SparkleRiseDistance => sparkleRiseDistance;
    public int SparkleMaxCount => sparkleMaxCount;
    public Color SparkleNormalColor => sparkleNormalColor;
    public float SparkleSpecialHueStep => sparkleSpecialHueStep;
    public float SparkleSpecialSaturation => sparkleSpecialSaturation;
    public float SparkleSpecialCycleDura => sparkleSpecialCycleDura;
    public float RushAnimDura => rushAnimDura;

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
