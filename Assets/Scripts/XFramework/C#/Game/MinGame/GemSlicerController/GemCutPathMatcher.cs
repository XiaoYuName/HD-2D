using UnityEngine;

/// <summary>一刀对某条目标边的判定结果。</summary>
public struct GemCutJudgement
{
    /// <summary>是否判定为“沿着这条边切的”。</summary>
    public bool pass;

    /// <summary>切得多准，0~1，用来评星。</summary>
    public float quality;

    /// <summary>目标边端点到玩家切线的最大垂距（世界单位）。</summary>
    public float offsetError;

    /// <summary>玩家笔画与目标边的夹角（度）。</summary>
    public float angleError;

    /// <summary>笔画是否在长度方向盖住了整条目标边。</summary>
    public bool covered;
}

/// <summary>
/// 判断玩家的一刀是否沿着某条目标边。
/// 只做三件事：比角度、比垂距、比覆盖长度。没有采样、没有精度问题。
/// </summary>
public static class GemCutPathMatcher
{
    /// <param name="edgeP">目标边起点（世界坐标）</param>
    /// <param name="edgeQ">目标边终点（世界坐标）</param>
    /// <param name="cutA">玩家按下点（世界坐标）</param>
    /// <param name="cutB">玩家抬手点（世界坐标）</param>
    /// <param name="maxOffset">允许的垂距（世界单位），建议取虚线线宽的一半左右</param>
    /// <param name="maxAngleDeg">允许的夹角（度）</param>
    /// <param name="coverSlack">覆盖长度的容许缺口（世界单位）</param>
    public static GemCutJudgement Judge(Vector2 edgeP, Vector2 edgeQ, Vector2 cutA, Vector2 cutB,
                                        float maxOffset, float maxAngleDeg, float coverSlack)
    {
        GemCutJudgement result = new GemCutJudgement
        {
            offsetError = float.MaxValue,
            angleError = 180f
        };

        Vector2 edgeVec = edgeQ - edgeP;
        Vector2 cutVec = cutB - cutA;

        float edgeLength = edgeVec.magnitude;
        float cutLength = cutVec.magnitude;

        if (edgeLength < 0.0001f || cutLength < 0.0001f)
        {
            return result;
        }

        Vector2 edgeDir = edgeVec / edgeLength;
        Vector2 cutDir = cutVec / cutLength;

        // 1) 角度。取绝对值，正划反划都算对
        float dot = Mathf.Clamp(Mathf.Abs(Vector2.Dot(edgeDir, cutDir)), 0f, 1f);
        result.angleError = Mathf.Acos(dot) * Mathf.Rad2Deg;

        // 2) 垂距。目标边两端点到玩家切线（当作无限长直线）的距离。
        //    玩家的笔画会超出宝石，所以比的是“共线”，不是“端点重合”
        float distanceP = PerpendicularDistance(edgeP, cutA, cutDir);
        float distanceQ = PerpendicularDistance(edgeQ, cutA, cutDir);
        result.offsetError = Mathf.Max(distanceP, distanceQ);

        // 3) 覆盖。笔画在目标边方向上的投影必须盖住整条边，
        //    否则在中间划一小截也会被判成整条边切完了
        float tA = Vector2.Dot(cutA - edgeP, edgeDir);
        float tB = Vector2.Dot(cutB - edgeP, edgeDir);
        float low = Mathf.Min(tA, tB);
        float high = Mathf.Max(tA, tB);
        result.covered = low <= coverSlack && high >= edgeLength - coverSlack;

        if (result.angleError > maxAngleDeg || result.offsetError > maxOffset || !result.covered)
        {
            return result;
        }

        result.pass = true;

        float offsetScore = 1f - result.offsetError / Mathf.Max(maxOffset, 0.0001f);
        float angleScore = 1f - result.angleError / Mathf.Max(maxAngleDeg, 0.0001f);
        result.quality = Mathf.Clamp01(offsetScore * 0.5f + angleScore * 0.5f);

        return result;
    }

    /// <summary>点到「过 lineOrigin、方向为 lineDir（须已归一化）的无限长直线」的距离。</summary>
    public static float PerpendicularDistance(Vector2 point, Vector2 lineOrigin, Vector2 lineDir)
    {
        Vector2 v = point - lineOrigin;
        return Mathf.Abs(v.x * lineDir.y - v.y * lineDir.x);
    }
}
