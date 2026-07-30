using System.Collections.Generic;
using UnityEngine;
using Utilities2D;

/// <summary>形状重合度评分。</summary>
public struct GemShapeScore
{
    /// <summary>目标轮廓被填满的比例，0~1。切多了这个会掉。</summary>
    public float coverage;

    /// <summary>超出目标轮廓的面积 / 目标面积。切少了这个会高。</summary>
    public float overflow;

    /// <summary>交并比，总评分看这个。</summary>
    public float iou;

    public override string ToString()
    {
        return string.Format("IoU {0:P1} / 覆盖 {1:P1} / 溢出 {2:P1}", iou, coverage, overflow);
    }
}

/// <summary>
/// 栅格采样比对剩余形状与目标轮廓的重合度。
/// 路径判定已经保证了结果正确，这里只在结算时跑一次做自检 / 调参用，不是主判定。
/// </summary>
public static class GemShapeMatcher
{
    /// <summary>
    /// 只算覆盖率：目标轮廓内还有多少面积被剩余碎块盖着。
    /// 采样范围只取目标轮廓的包围盒（不含溢出部分），比完整 Evaluate 便宜得多，
    /// 适合每切一刀都跑一次。1 - 返回值 就是「白色轮廓内被切掉的比例」。
    /// </summary>
    public static float EvaluateCoverage(Polygon2D target, List<Polygon2D> pieces, int resolution = 128)
    {
        if (target == null || target.pointsList.Count < 3)
        {
            return 0f;
        }

        Rect bounds = target.GetBounds();
        float step = Mathf.Max(bounds.width, bounds.height) / Mathf.Max(resolution, 8);
        if (step <= 0f)
        {
            return 0f;
        }

        int inTarget = 0;
        int covered = 0;

        Vector2D sample = Vector2D.Zero();

        for (float y = bounds.yMin; y <= bounds.yMax; y += step)
        {
            for (float x = bounds.xMin; x <= bounds.xMax; x += step)
            {
                sample.x = x;
                sample.y = y;

                if (!target.PointInPoly(sample))
                {
                    continue;
                }

                inTarget++;

                if (pieces == null)
                {
                    continue;
                }

                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i] != null && pieces[i].PointInPoly(sample))
                    {
                        covered++;
                        break;
                    }
                }
            }
        }

        return inTarget > 0 ? (float)covered / inTarget : 0f;
    }

    /// <param name="target">目标轮廓（世界坐标）</param>
    /// <param name="pieces">剩余的碎块（世界坐标）</param>
    /// <param name="resolution">采样分辨率，128~192 足够</param>
    public static GemShapeScore Evaluate(Polygon2D target, List<Polygon2D> pieces, int resolution = 160)
    {
        GemShapeScore score = new GemShapeScore();

        if (target == null || target.pointsList.Count < 3)
        {
            return score;
        }

        Rect bounds = target.GetBounds();
        float xMin = bounds.xMin;
        float xMax = bounds.xMax;
        float yMin = bounds.yMin;
        float yMax = bounds.yMax;

        // 并集包围盒，保证溢出的部分也被采样到
        if (pieces != null)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                Rect pieceBounds = pieces[i].GetBounds();
                xMin = Mathf.Min(xMin, pieceBounds.xMin);
                xMax = Mathf.Max(xMax, pieceBounds.xMax);
                yMin = Mathf.Min(yMin, pieceBounds.yMin);
                yMax = Mathf.Max(yMax, pieceBounds.yMax);
            }
        }

        float step = Mathf.Max(xMax - xMin, yMax - yMin) / Mathf.Max(resolution, 8);
        if (step <= 0f)
        {
            return score;
        }

        int both = 0;
        int targetOnly = 0;
        int pieceOnly = 0;

        Vector2D sample = Vector2D.Zero();

        for (float y = yMin; y <= yMax; y += step)
        {
            for (float x = xMin; x <= xMax; x += step)
            {
                sample.x = x;
                sample.y = y;

                bool inTarget = target.PointInPoly(sample);

                bool inPiece = false;
                if (pieces != null)
                {
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        if (pieces[i] != null && pieces[i].PointInPoly(sample))
                        {
                            inPiece = true;
                            break;
                        }
                    }
                }

                if (inTarget && inPiece)
                {
                    both++;
                }
                else if (inTarget)
                {
                    targetOnly++;
                }
                else if (inPiece)
                {
                    pieceOnly++;
                }
            }
        }

        float targetArea = both + targetOnly;
        float unionArea = both + targetOnly + pieceOnly;

        score.coverage = targetArea > 0f ? both / targetArea : 0f;
        score.overflow = targetArea > 0f ? pieceOnly / targetArea : 0f;
        score.iou = unionArea > 0f ? both / unionArea : 0f;

        return score;
    }
}
