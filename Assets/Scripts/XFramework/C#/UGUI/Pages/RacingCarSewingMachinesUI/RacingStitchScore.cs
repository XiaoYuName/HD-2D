using System;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 缝纫评分：按赛道里程积分「针尖偏离中线的程度」，全程实时给分。
///
/// 三个设计决定，都不是随手定的：
///
/// 1. 按里程积分而不是按时间。分数是 ∫质量(e)ds / 总里程。
///    按时间累计的话开得慢就少扣分，同一条线能跑出不同分数。
///
/// 2. 容差用「白线半宽的倍数」而不是像素。容差直接从材质的 _LineWidth 反推，
///    美术把白虚线加粗加宽，判定范围自动跟着变，这边一行都不用改。
///
/// 3. 用滑动平均而不是累减。纯扣分是单调递减、中段失误就再也救不回来，
///    而且总分还取决于赛道长短。跑均值天然落在 0~100、可挽回、与赛道长度无关。
///
/// 顺带一提：这样逐帧增量算分，和「全程记录轨迹、跑完再和标准线比对」在数学上是同一个积分，
/// 结果完全等价。所以实时数字就是最终成绩，不需要为了评分而保留整条轨迹
/// ——除非结算界面要把缝歪的线画出来给玩家看。
/// </summary>
[AddComponentMenu("MiniGame/Racing Stitch Score (缝纫评分)")]
public class RacingStitchScore : MonoBehaviour
{
    [Title("数据来源")]
    [LabelText("路面驱动器"), Required]
    [SerializeField] UIRasterScroll road;

    [LabelText("针尖节点"), Required]
    [SerializeField] RectTransform needleTip;

    [LabelText("针头"), Required]
    [Tooltip("用来做中位校准。留空则不校准，中线对齐全靠场景里手摆")]
    [SerializeField] RacingCarHock hock;

    [Title("校准")]
    [LabelText("启用时自动对准中线")]
    [Tooltip("开局把针头的中位挪到路面中线上。\n" +
             "不做这一步的话，只要美术动过 Hock 或路面的位置，「完美压线」就拿不到满分——\n" +
             "基准分会莫名其妙地卡在 80 多，而且没人看得出是布局偏了")]
    [SerializeField] bool calibrateOnEnable = true;

    [Title("判定")]
    [LabelText("容差(白线半宽的倍数)"), MinValue(0f)]
    [Tooltip("偏离在这个范围内不扣分。\n" +
             "注意换算：白线半宽在针尖那一行只有 10px 出头（= 2·_LineWidth·视距/最远深度/距离·路面宽/2），" +
             "而针头全速一帧就走 9px。所以 1（严格压线）实际上是「一帧的抖动就掉分」，太苛刻；\n" +
             "4 ≈ 完美区 ±40px，大约四帧的余量，是压得住又不至于随手满分的档位")]
    [SerializeField] float tolerance = 4f;

    [LabelText("扣满阈值(白线半宽的倍数)"), MinValue(1f)]
    [Tooltip("偏到白线半宽的几倍时该段计 0 分。从容差到这里线性过渡。\n" +
             "和容差一起决定及格难度：过 80 分要求全程平均偏离 ≤ 容差 + 0.2×(本值 - 容差)，" +
             "4/22 对应平均 7.6 倍半宽 ≈ ±76px（针头行程是 ±420px）")]
    [SerializeField] float failAt = 22f;

    [LabelText("重罚大偏差")]
    [Tooltip("勾上后质量取平方，轻微跑偏几乎不扣、偏得多掉得快。要「必须压着线」的手感就开")]
    [SerializeField] bool squareError;

    [Title("即时反馈")]
    [LabelText("即时窗口(米)"), MinValue(1f)]
    [Tooltip("Instant01 的滑动窗口。用来驱动线迹变色/抖动/音效这种当下反馈，不参与最终成绩")]
    [SerializeField] float instantWindow = 12f;

    [Title("运行时"), ShowInInspector, ReadOnly, PropertyOrder(50)]
    [LabelText("当前得分")]
    public int DisplayScore { get; private set; } = 100;

    RectTransform _roadRect;
    float _lastS;
    float _sumQuality;
    float _sumDistance;
    float _instant = 1f;
    float _error = 0f;
    bool _started;
    bool _pendingCalibration;

    /// <summary>全程平均质量 0~1。这就是最终成绩，中途读到的是「到目前为止」的成绩。</summary>
    public float Score01 => _sumDistance > 1e-4f ? _sumQuality / _sumDistance : 1f;

    /// <summary>0~100 的分数。</summary>
    public float Score => Score01 * 100f;

    /// <summary>最近 <see cref="instantWindow"/> 米的质量 0~1，给当下反馈用。</summary>
    public float Instant01 => _instant;

    /// <summary>当前偏离量，单位 = 白线半宽。1 以内表示针尖还在白线上。</summary>
    public float LateralError => _error;

    /// <summary>已计分的里程（米）。</summary>
    public float ScoredDistance => _sumDistance;

    /// <summary>整数分变化时触发，避免每帧刷字符串。</summary>
    public event Action<int> OnDisplayScoreChanged;

    void Awake()
    {
        if(road != null)
            _roadRect = road.GetComponent<RectTransform>();
    }

    void OnEnable() => ResetScore();

    void LateUpdate()
    {
        if(road == null || needleTip == null || _roadRect == null)
            return;

        // 校准推迟到这里而不是放在 OnEnable：OnEnable 时 Canvas 还没排完版，
        // 读到的 rect 可能是旧的；而且那时其它 GameObject 的 Awake 未必跑过
        if(_pendingCalibration)
        {
            _pendingCalibration = false;
            CalibrateHome();
        }

        float s = road.Travel;

        if(!_started)
        {
            _lastS = s;
            _started = true;
            return;
        }

        float ds = s - _lastS;
        _lastS = s;

        // 里程倒退或跳变（换赛道 Travel 归零、暂停后恢复）不计分，否则会凭空多出一段
        if(ds <= 0f || ds > 50f)
            return;

        if(!TrySampleError(out _error))
            return;

        float quality = 1f - Mathf.Clamp01((_error - tolerance) / Mathf.Max(failAt - tolerance, 1e-3f));
        if(squareError)
            quality *= quality;

        _sumQuality += quality * ds;
        _sumDistance += ds;
        _instant = Mathf.Lerp(_instant, quality, Mathf.Clamp01(ds / Mathf.Max(instantWindow, 1e-3f)));

        int shown = Mathf.RoundToInt(Score);
        if(shown != DisplayScore)
        {
            DisplayScore = shown;
            OnDisplayScoreChanged?.Invoke(shown);
        }
    }

    /// <summary>
    /// 把针头的中位挪到路面中线上，让「不打方向」正好等于「压着线」。
    ///
    /// 误差是在路面矩形的局部空间量的，而针头活在自己的父空间里，两者之间可能隔着缩放；
    /// 所以要经由世界空间换算过去，不能把像素值直接减到 HomeX 上。
    /// </summary>
    [Button("把针头对准中线"), PropertyOrder(99)]
    public void CalibrateHome()
    {
        if(hock == null || _roadRect == null || needleTip == null)
            return;

        Vector2 lp = _roadRect.InverseTransformPoint(needleTip.position);
        Rect r = _roadRect.rect;
        if(r.height <= 0f)
            return;

        float v = Mathf.Clamp01((lp.y - r.yMin) / r.height);
        float centerX = r.center.x + road.GetRoadCenterOffset01(v) * r.width;
        float deltaLocal = lp.x - centerX;

        // 路面局部 -> 世界 -> 针头父空间，中间的缩放差异由 Transform 自己处理
        Vector3 world = _roadRect.TransformVector(new Vector3(deltaLocal, 0f, 0f));
        var hockParent = hock.transform.parent as RectTransform;
        float deltaHock = hockParent != null
            ? hockParent.InverseTransformVector(world).x
            : world.x;

        hock.HomeX -= deltaHock;
        hock.ResetPosition();
    }

    /// <summary>清零重新计分。开新一局 / 换赛道时调。</summary>
    [Button("重置评分"), PropertyOrder(100)]
    public void ResetScore()
    {
        _pendingCalibration = calibrateOnEnable;

        _sumQuality = 0f;
        _sumDistance = 0f;
        _instant = 1f;
        _error = 0f;
        _started = false;

        if(DisplayScore != 100)
        {
            DisplayScore = 100;
            OnDisplayScoreChanged?.Invoke(100);
        }
    }

    /// <summary>取针尖当前偏离中线多少个「白线半宽」。</summary>
    bool TrySampleError(out float error)
    {
        error = 0f;

        Vector2 lp = _roadRect.InverseTransformPoint(needleTip.position);
        Rect r = _roadRect.rect;
        if(r.height <= 0f || r.width <= 0f)
            return false;

        float v = Mathf.Clamp01((lp.y - r.yMin) / r.height);
        float centerX = r.center.x + road.GetRoadCenterOffset01(v) * r.width;

        // 白线在针尖那一行的半宽。美术把线加粗，这个值自动变大，容差跟着放宽
        float halfLine = road.CenterLineWidth01(road.GetDistanceAt(v)) * r.width * 0.5f;
        if(halfLine < 1e-3f)
            return false;

        error = Mathf.Abs(lp.x - centerX) / halfLine;
        return true;
    }
}
