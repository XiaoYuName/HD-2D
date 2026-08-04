using UnityEngine;
using XFramework;

public partial class RacingCarSewingMachinesUI : UIBase
{
    /// <summary>及格线。通关判定和评分图标的镜像共用它，避免两处各写一个数字后对不上。</summary>
    private const float PassScore = 80f;

    private ClothingBag clothingBag;

    /// <summary>本次装配加载的线路资产 key。路面和小地图共用同一份，由本面板统一在关闭时释放。</summary>
    private string loadedRoutePath;

    /// <summary>缝纫评分。挂在路面节点上，不走 AutoBind，避免重新生成绑定时丢失。</summary>
    private RacingStitchScore stitchScore;

    /// <summary>当前赛道配置行，圈数/结算都要用。</summary>
    private RacingTrackData currentTrack;

    /// <summary>是否已经结算过，防止跑完后每帧重复触发。</summary>
    private bool finished;

    /// <summary>缝纫线迹。重试时要清掉，否则上一局缝歪的线还留在路面上。</summary>
    private RacingStitchTrail stitchTrail;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");

        stitchScore = rasterScroll != null ? rasterScroll.GetComponent<RacingStitchScore>() : null;
        stitchTrail = rasterScroll != null ? rasterScroll.GetComponentInChildren<RacingStitchTrail>(true) : null;

        if (stitchScore != null)
        {
            // 只在整数分变化时回调，不用每帧刷字符串
            stitchScore.OnDisplayScoreChanged += RefreshScoreDisplay;
            RefreshScoreDisplay(stitchScore.DisplayScore);
        }
    }

    private void RefreshScoreDisplay(int score)
    {
        if (fractionTex != null)
        {
            fractionTex.text = score.ToString();
        }

        RefreshScoreIcon(score);
    }

    /// <summary>
    /// 及格时把评分图标上下镜像（箭头朝上），不及格恢复原样（箭头朝下）。
    /// Flip 是位标志枚举，「关闭镜像」就是 0，不能用 Flip.None（没有这个值）。
    /// </summary>
    private void RefreshScoreIcon(int score)
    {
        if (pingFenIcon == null)
        {
            return;
        }

        pingFenIcon.flip = score >= PassScore
            ? Coffee.UIEffects.Flip.Vertical
            : 0;
    }

    /// <summary>
    /// 全程完成度 0~1：已跑圈数 / 配置总圈数。跑满即完赛。
    /// 和小地图上的光点同源（都由里程 Travel 驱动），不会出现「进度条满了但车还没到终点」。
    /// </summary>
    public float TotalProgress01
    {
        get
        {
            int laps = EffectiveLapCount;
            return laps <= 0 ? 0f : Mathf.Clamp01(LapProgress / laps);
        }
    }

    /// <summary>
    /// 本局要跑的圈数。开放赛道（起点终点不相连）跑到终点就结束，配多少圈都按 1 圈算
    /// ——里程超过终点后线路是夹住的，再跑也只是原地不动。
    /// </summary>
    private int EffectiveLapCount
    {
        get
        {
            if (currentTrack == null || currentTrack.LapCount <= 0)
            {
                return 0;
            }

            RacingTrackRoute route = rasterScroll != null ? rasterScroll.Route : null;
            return route != null && !route.IsClosed ? 1 : currentTrack.LapCount;
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

    /// <summary>
    /// 赛道取自服装配置的 RacingTrackID，所以要传服装；小游戏已经不参与解锁，不需要角色数据。
    /// </summary>
    public void SetData(ClothingBag clothingBag)
    {
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
        int laps = EffectiveLapCount;
        if (laps <= 0)
        {
            return;
        }

        RefreshProgressBar();

        if (finished)
        {
            return;
        }

        if (LapProgress >= laps)
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

        // 先停住：弹窗期间路面还在滚、针还在扎的话，背后的分数会继续变，
        // 玩家看到的结算分和面板上的数字就对不上了
        StopRun();

        if (score < PassScore)
        {
            // isReset=true 才会给「再次挑战」按钮；OnFail 是重试，OnClose 是放弃退出
            UIUtility.PopFailWindow(true, RestartRun, Close);
        }
        else
        {
            UIUtility.PopClothingMinGameComplete(Close);
        }
    }

    /// <summary>结算时冻住这一局：车不再前进，针不再扎。</summary>
    private void StopRun()
    {
        if (rasterScroll != null)
        {
            rasterScroll.DriveSpeed = 0f;
        }

        if (hock != null)
        {
            hock.Steer = 0f;
            hock.StopStitching();
        }
    }

    /// <summary>
    /// 失败后重跑本条赛道。不走 LoadTrack：那会先 FreeAsset 再重新加载线路，
    /// 而重试用的是同一条赛道，白白放掉再取一次没有意义。
    /// </summary>
    private void RestartRun()
    {
        if (currentTrack == null)
        {
            return;
        }

        finished = false;

        // SetData 内部会把 Travel 归零并重新套用配置（含车速），相当于重开一局
        rasterScroll.SetData(currentTrack);
        racingTrackMinimap.SetData(currentTrack);

        if (hock != null)
        {
            hock.ResetPosition();
            hock.StartStitching();
        }

        // 线迹和成绩都要清，否则上一局缝歪的线还留在路面上、均值也会被继承
        if (stitchTrail != null)
        {
            stitchTrail.Clear();
        }

        if (stitchScore != null)
        {
            stitchScore.ResetScore();
            RefreshScoreDisplay(stitchScore.DisplayScore);
        }

        RefreshProgressBar();
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
            stitchScore.OnDisplayScoreChanged -= RefreshScoreDisplay;
        }
    }
}
