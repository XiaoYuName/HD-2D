using UnityEngine;
using XFramework;

public partial class RacingCarSewingMachinesUI : UIBase
{
    private CharacterBag  characterBag;
    private ClothingBag clothingBag;

    /// <summary>本次装配加载的线路资产 key。路面和小地图共用同一份，由本面板统一在关闭时释放。</summary>
    private string loadedRoutePath;

    /// <summary>缝纫评分。挂在路面节点上，不走 AutoBind，避免重新生成绑定时丢失。</summary>
    private RacingStitchScore stitchScore;

    /// <summary>当前赛道配置行，圈数/结算都要用。</summary>
    private RacingTrackData currentTrack;

    /// <summary>是否已经结算过，防止跑完后每帧重复触发。</summary>
    private bool finished;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");

        stitchScore = rasterScroll != null ? rasterScroll.GetComponent<RacingStitchScore>() : null;
        if (stitchScore != null)
        {
            // 只在整数分变化时回调，不用每帧刷字符串
            stitchScore.OnDisplayScoreChanged += RefreshScoreText;
            RefreshScoreText(stitchScore.DisplayScore);
        }
    }

    private void RefreshScoreText(int score)
    {
        if (fractionTex != null)
        {
            fractionTex.text = score.ToString();
        }
    }

    /// <summary>
    /// 全程完成度 0~1：已跑圈数 / 配置总圈数。跑满即完赛。
    /// 和小地图上的光点同源（都由里程 Travel 驱动），不会出现「进度条满了但车还没到终点」。
    /// </summary>
    public float TotalProgress01
    {
        get
        {
            if (currentTrack == null || currentTrack.LapCount <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01(LapProgress / currentTrack.LapCount);
        }
    }

    private void RefreshProgressBar()
    {
        if (process == null)
        {
            return;
        }

        // 用 SetValueWithoutNotify，避免每帧触发 onValueChanged 把回调打爆；
        // 按 Slider 自己的 min/max 插值，策划把范围改成 0~100 也照样对
        process.SetValueWithoutNotify(Mathf.Lerp(process.minValue, process.maxValue, TotalProgress01));
    }

    public void SetData(CharacterBag characterBag,ClothingBag clothingBag)
    {
        this.characterBag = characterBag;
        this.clothingBag = clothingBag;

        // Luban 的 Get(key) 就是 _dataMap[key]，查不到直接抛 KeyNotFoundException，
        // 后面的 != null 判断根本轮不到执行；要做空判断必须用 GetOrDefault
        ClothingData clothingData = LubanManager.Instance.TbClothingData.GetOrDefault(clothingBag.clothingID);
        if (clothingData == null)
        {
            Debug.LogError($"服装配置缺失: ClothingID={clothingBag.clothingID}");
            return;
        }

        if (clothingData.RacingTrackID <= 0)
        {
            Debug.LogError($"服装未配置赛车赛道: ClothingID={clothingData.ID}");
            return;
        }

        // 取 RacingTrackID 而不是 ID：前者才是这件服装选中的赛道，后者是服装自己的主键
        RacingTrackData racingData = LubanManager.Instance.TbRacingTrackData.GetOrDefault(clothingData.RacingTrackID);
        if (racingData == null)
        {
            Debug.LogError($"赛道配置缺失: ClothingID={clothingData.ID} 指向的 RacingTrackID={clothingData.RacingTrackID}");
            return;
        }

        LoadTrack(racingData);

        // 换赛道后里程从 0 重来，成绩必须一起清，否则新赛道会继承上一条的均值
        if (stitchScore != null)
        {
            stitchScore.ResetScore();
        }
    }

    /// <summary>
    /// 装配赛道。路面和小地图各自按 RoutePath 取线路资产——AssetsManager 按 key 缓存，
    /// 两边拿到的是同一个实例，不会重复加载；而它们跑的是同一个里程 Travel，所以天然同步。
    /// </summary>
    private void LoadTrack(RacingTrackData racingData)
    {
        // 换赛道时先把上一条放掉，避免反复进出面板堆着不释放
        ReleaseTrack();
        loadedRoutePath = racingData.RoutePath;
        currentTrack = racingData;
        finished = false;

        rasterScroll.SetData(racingData);
        racingTrackMinimap.SetData(racingData);

        // 里程已归零，进度条同步归零；否则换赛道那一帧会残留上一条的进度
        RefreshProgressBar();
    }

    /// <summary>
    /// 已跑圈数（含小数）。路面和小地图共用同一个里程 Travel，所以这里算出来的进度和画面严格一致。
    /// </summary>
    public float LapProgress
    {
        get
        {
            RacingTrackRoute route = rasterScroll != null ? rasterScroll.Route : null;
            if (route == null || route.TotalLength <= 0f)
            {
                return 0f;
            }

            return rasterScroll.Travel / route.TotalLength;
        }
    }

    private void Update()
    {
        if (currentTrack == null || currentTrack.LapCount <= 0)
        {
            return;
        }

        RefreshProgressBar();

        if (finished)
        {
            return;
        }

        if (LapProgress >= currentTrack.LapCount)
        {
            finished = true;
            OnTrackFinished();
        }
    }

    /// <summary>
    /// 跑完配置的全部圈数时触发一次。结算逻辑写在这里。
    /// 分数取 stitchScore.Score（0~100 的全程平均），它就是最终成绩——
    /// 逐帧增量积分和「跑完再拿轨迹跟标准线比对」在数学上是同一个积分，不需要另外算一遍。
    /// </summary>
    private void OnTrackFinished()
    {
        float score = stitchScore != null ? stitchScore.Score : 0f;

        // TODO: 接结算界面
        Debug.Log($"[赛车缝纫] 完赛！赛道ID={currentTrack.ID} 圈数={currentTrack.LapCount} " +
                  $"里程={rasterScroll.Travel:F0}m 缝纫评分={score:F1}");
    }

    private void ReleaseTrack()
    {
        if (string.IsNullOrEmpty(loadedRoutePath))
        {
            return;
        }

        AssetsManager.Instance.FreeAsset(loadedRoutePath);
        loadedRoutePath = null;
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ReleaseTrack();

        if (stitchScore != null)
        {
            stitchScore.OnDisplayScoreChanged -= RefreshScoreText;
        }
    }
}
