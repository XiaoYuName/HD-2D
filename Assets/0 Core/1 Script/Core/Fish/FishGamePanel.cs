using UnityEngine;
using XFramework;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using PrimeTween;

public class FishGamePanel : UIBase
{
    enum State
    {
        None,   // 未开始。未进入界面状态
        SePos,  // 等待玩家点击选择下勾位置，下勾会检验玩家的鱼饵、行动力，如果不够，则提示。
        WaitFish,   // 等待鱼移动上钩
        WaitCatchFish,  // 等待玩家点击鼠标左键确认上钩
        CatchFish,  // 抓鱼阶段，玩家要通过鼠标左键的按住和松开，调整升降控制条的升降位置。
        End,    // 结束，弹出，保持现状，等待玩家点击继续钓鱼后，回到下勾阶段
    }

    [SerializeField] FishGameManager mg;    // 钓鱼常驻管理器（等级/经验/鱼竿难度加成）
    [SerializeField] TimeSlotConfig timeSlotConfig;
    [SerializeField] FishConfig config;
    [SerializeField] FishTrashConfig trashConfig;   // 钓鱼垃圾配表
    [SerializeField] Button closeButton;
    [SerializeField] TextMeshProUGUI fishLvText, beltCountText, apText;// timeLeftText
    [SerializeField] Image timePeriodIcon;
    [SerializeField] LocalizeStringEvent timePeriodText;
    [SerializeField] FishLogCellUI fishLogCell;
    [SerializeField] Transform fishLogCellContainer;
    [SerializeField] List<FishLogCellUI> fishLogCellList;
    [SerializeField] LocalizeStringEvent tipText;
    [SerializeField] WarnTip warnTip;
    [SerializeField] CanvasGroup exclaPointCg;
    [LabelText("池塘中的3个鱼 小中大排序")][SerializeField] RectTransform[] fishRt;
    [LabelText("鱼钩物体")][SerializeField] CanvasGroup hookCg;
    [LabelText("玩家点击下勾位置区域")][SerializeField] RectTransform clickAreaRt;
    [LabelText("抓鱼的升降控制条背景区域Rt")][SerializeField] RectTransform catchCtrlBarBgRt;   // 代表限制范围

    [LabelText("抓鱼的升降控制条")][SerializeField] Image catchCtrlBar; // 玩家可控绿条
    [LabelText("抓鱼上下浮动的小鱼图标")][SerializeField] RectTransform catchTargetRt;   // QTE 中上下浮动的目标
    [LabelText("抓鱼进度条")][SerializeField] Image catchProgressBar;

    [SerializeField] List<long> curPossibleFishIds;
    [SerializeField] float targetCatchPoint;  // 目标抓鱼点数（=实际钓鱼难度）
    [SerializeField] float curCatchPoint;   // 当前抓鱼点数
    [SerializeField] State curState;
    readonly List<Tween> fishMoveTweens = new();

    // 本次咬钩抽取到的结果
    long curCatchId;
    bool curCatchIsFish;
    int curCatchQuality;
    int curCatchDifficulty;
    FishBodyType curCatchBodyType;
    bool curPerfect;                 // QTE 全程未脱离 → 完美捕获

    float baseCatchBarHeight;        // 绿条基础高度（等级加宽在此之上叠加）

    // 多语言 Key（Fish 表内 Key 带 "Fish/" 前缀，见 Fish Shared Data.asset）
    const string TipStart = "Fish/StartFishing";
    const string TipWaiting = "Fish/WaitingBite";
    const string TipBite = "Fish/FishBite";
    const string TipHold = "Fish/HoldLeftClick";
    const string TipEscaped = "Fish/FishEscaped";
    const string TipWellDone = "Fish/WellDone";
    const string TipPraiseGood = "Fish/PraiseGood";
    const string TipTryNext = "Fish/TryNextTime";

    #region Get
    float FishCatchCtrlBarMoveSpeed => config.FishCatchCtrlBarMoveSpeed;
    float FishProgressBarSpeed => config.FishProgressBarSpeed;
    float FishMoveMinTime => config.FishMoveMinTime;
    float FishMoveMaxTime => config.FishMoveMaxTime;
    TweenSettings FishMoveTs => config.FishMoveTs;
    #endregion
    FishStateBase curStateBase;
    Dictionary<State, FishStateBase> stateDict;

    #region Lifecycle
    public override void Init()
    {
        closeButton.onClick.AddListener(Close);
        baseCatchBarHeight = catchCtrlBar.rectTransform.sizeDelta.y;
        stateDict = new ()
        {
            { State.None, new NoneState(this) },
            { State.SePos, new SePosState(this) },
            { State.WaitFish, new WaitFishState(this) },
            { State.WaitCatchFish, new WaitCatchFishState(this) },
            { State.CatchFish, new CatchFishState(this) },
            { State.End, new EndState(this) },
        };
    }

    public override void Open()
    {
        base.Open();
        GameDataManager.Instance.RegisterPlayerDataDayChange(OnTimePerChange);
        GameDataManager.Instance.RegisterPlayerDataChange(OnPlayerDataChange);
        // 鱼饵数量走事件刷新（注册即触发一次；抛竿/购买后自动更新）
        InventoryManager.Instance.RegisterItemIDChangeCallBack(ItemIdSet.Bait, OnBaitChanged);
        mg.OnProgressChanged += RefreshProgressText;

        PlayerInputManager.Instance.OnLeftMouseDown += OnLeftMouseDown;
        PlayerInputManager.Instance.OnLeftMouseUp += OnLeftMouseUp;

        // 首次刷新界面信息
        OnTimePerChange(GameDataManager.Instance.PlayerData);
        OnPlayerDataChange(GameDataManager.Instance.PlayerData);
        RefreshProgressText();

        // 进入下勾阶段，等待玩家点击抛竿
        SwitchState(State.SePos);
    }

    public override void Close()
    {
        base.Close();

        SwitchState(State.None);
        StopFishMove();

        timePeriodIcon.ClearIcon();
        GameDataManager.Instance.UnregisterPlayerDataDayChange(OnTimePerChange);
        GameDataManager.Instance.UnregisterPlayerDataChange(OnPlayerDataChange);
        InventoryManager.Instance.UnregisterItemIDChangeCallBack(ItemIdSet.Bait, OnBaitChanged);
        mg.OnProgressChanged -= RefreshProgressText;

        PlayerInputManager.Instance.OnLeftMouseDown -= OnLeftMouseDown;
        PlayerInputManager.Instance.OnLeftMouseUp -= OnLeftMouseUp;

        foreach (var item in fishLogCellList)
            Destroy(item.gameObject);
        fishLogCellList.Clear();
    }

    void Update() => curStateBase?.Update();
    #endregion

    #region 状态切换 / 输入分发
    void SwitchState(State targetState)
    {
        curStateBase?.Exit();
        curState = targetState;
        curStateBase = stateDict[targetState];
        curStateBase.Enter();
    }

    void OnLeftMouseDown() => curStateBase?.OnLeftDown();
    void OnLeftMouseUp() => curStateBase?.OnLeftUp();
    #endregion

    #region 界面刷新
    void OnTimePerChange(PlayerData playerData)
    {
        timePeriodIcon.SetIcon(timeSlotConfig.GetIconPath(playerData.TimeSlot));
        timePeriodText.SetText(LocTableSet.MainUI, timeSlotConfig.GetNameKey(playerData.TimeSlot));
    }

    void OnPlayerDataChange(PlayerData playerData)
        => apText.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);

    void OnBaitChanged(List<ItemInfo> _)
        => beltCountText.text = InventoryManager.Instance.GetItemCount(ItemIdSet.Bait).ToString();

    void RefreshProgressText() => fishLvText.text = mg.Level.ToString();

    void SetTip(string key) => tipText.SetText(LocTableSet.Fish, key);
    #endregion

    #region 下勾 / 抽取 / 结算
    // 下勾：检验并扣除鱼饵（每次抛竿消耗1个鱼饵）；成功返回 true
    bool TryConsumeForCast()
    {
        if (InventoryManager.Instance.GetItemCount(ItemIdSet.Bait) == 0)
        {
            warnTip.Show(LocTableSet.Fish, LocVarSet.FishGame.NotEnoughBait);
            return false;
        }
        if (GameDataManager.Instance.GetProperty(PropertyType.ActionPointsValue).Value == 0)
        {
            warnTip.Show(LocTableSet.GameEnterPanel, LocVarSet.MiniGame.NotEnoughAp);
            return false;
        }
        InventoryManager.Instance.ConsumeItem(ItemIdSet.Bait, 1);
        return true;
    }

    // 根据当前时段，从鱼种池 + 垃圾池 按基础出现权重抽取本次咬钩物
    // 注：解锁等级、鱼竿限制、宝箱(15%)暂未接入，可在此扩展
    bool RollCatch()
    {
        TimeSlot slot = GameDataManager.Instance.PlayerData.TimeSlot;

        var pool = new List<(long id, int weight, int quality, int difficulty, bool isFish, FishBodyType body)>();
        curPossibleFishIds.Clear();

        foreach (var f in config.DataDict.Values)
        {
            if (!f.CanCatchAt(slot))
                continue;
            pool.Add((f.Id, f.AppearWeight, f.Quality, f.Difficulty, true, f.BodyType));
            curPossibleFishIds.Add(f.Id);
        }
        foreach (var t in trashConfig.DataDict.Values)
            pool.Add((t.Id, t.AppearWeight, 0, t.Difficulty, false, FishBodyType.SmallSlim));

        if (pool.Count == 0)
            return false;

        int total = 0;
        foreach (var e in pool)
            total += Mathf.Max(0, e.weight);
        int roll = Random.Range(0, Mathf.Max(1, total));
        var picked = pool[pool.Count - 1];
        int acc = 0;
        foreach (var e in pool)
        {
            acc += Mathf.Max(0, e.weight);
            if (roll < acc) { picked = e; break; }
        }

        curCatchId = picked.id;
        curCatchIsFish = picked.isFish;
        curCatchQuality = picked.quality;
        curCatchDifficulty = picked.difficulty;
        curCatchBodyType = picked.body;
        return true;
    }

    // 体型 → 池塘中对应的鱼（fishRt 按 小/中/大 排序，传奇视作大）
    int BiteFishIndex() => curCatchBodyType switch
    {
        FishBodyType.SmallSlim => 0,
        FishBodyType.Medium => 1,
        FishBodyType.LargeThick => 2,
        FishBodyType.Legendary => 2,
        _ => 0,
    };

    void ShowHook(bool show)
    {
        hookCg.alpha = show ? 1f : 0f;
        hookCg.blocksRaycasts = show;
    }

    // 池塘 3 条鱼上下摆动"游动"（各自独立的无限 Yoyo 补间）
    void StartFishMove()
    {
        StopFishMove();
        foreach (var rt in fishRt)
        {
            float y = rt.localPosition.y;
            fishMoveTweens.Add(Tween.LocalPositionY(rt, y + 20f, Random.Range(0.6f, 1.2f),
                cycles: -1, cycleMode: CycleMode.Yoyo));
        }
    }

    void StopFishMove()
    {
        foreach (var t in fishMoveTweens)
            t.Stop();
        fishMoveTweens.Clear();
    }

    // 结算：成功则记日志、发奖励、加经验；失败给提示
    void SettleResult(bool success)
    {
        if (!success)
        {
            SetTip(TipTryNext);
            return;
        }

        // 完美捕获仅对鱼生效（杂物无任何变化，见策划案 4.3.4）
        bool perfectFish = curPerfect && curCatchIsFish;
        SetTip(perfectFish ? TipPraiseGood : TipWellDone);

        // 生成物品实例并入包 / 记日志（无对应 ItemData 的预留ID会被安全跳过）
        if (InventoryManager.Instance.GetItemData(curCatchId) != null)
        {
            AddLog(InventoryManager.Instance.NewItem(curCatchId, 1));
            InventoryManager.Instance.AddItem(curCatchId, 1);
        }

        // 加经验（满级后为0）与扣行动力（成功钓起扣1，不消耗体力，见策划案 3.3.1）
        mg.AddExp(curCatchQuality, curCatchDifficulty, curCatchIsFish, perfectFish);
        GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue, 1);
    }
    #endregion

    // 增添渔获日志
    void AddLog(ItemInfo fishItem)
    {
        FishLogCellUI fishLogCellUI = Instantiate(fishLogCell, fishLogCellContainer);
        fishLogCellUI.Set(fishItem, 0f, 0f);
        fishLogCellList.Add(fishLogCellUI);
    }

    #region 状态机
    abstract class FishStateBase : StateMachBase<FishGamePanel>
    {
        protected FishStateBase(FishGamePanel owner) { this.owner = owner; }
        public virtual void OnLeftDown() { }
        public virtual void OnLeftUp() { }
    }

    // 未进入 / 已关闭
    class NoneState : FishStateBase
    {
        public NoneState(FishGamePanel o) : base(o) { }
    }

    // 下勾阶段：点击抛竿，校验并扣除鱼饵/行动力
    class SePosState : FishStateBase
    {
        public SePosState(FishGamePanel o) : base(o) { }

        public override void Enter()
        {
            owner.ShowHook(false);
            owner.StopFishMove();
            owner.catchTargetRt.gameObject.SetActive(false);
            owner.catchCtrlBar.gameObject.SetActive(false);
            owner.SetTip(TipStart);
        }

        public override void OnLeftDown()
        {
            if (!owner.TryConsumeForCast())
                return;
            if (!owner.RollCatch())
                return;
            owner.SwitchState(State.WaitFish);
        }
    }

    // 等待咬钩：随机 5-15 秒（受等级咬钩减免）后咬钩
    class WaitFishState : FishStateBase
    {
        float timer;
        float biteTime;

        public WaitFishState(FishGamePanel o) : base(o) { }

        public override void Enter()
        {
            owner.ShowHook(true);
            owner.SetTip(TipWaiting);
            owner.StartFishMove();
            timer = 0f;
            biteTime = Random.Range(owner.FishMoveMinTime, owner.FishMoveMaxTime)
                       * (1f - owner.mg.BiteTimeReduceFactor);
        }

        public override void Update()
        {
            timer += Time.deltaTime;
            if (timer >= biteTime)
                owner.SwitchState(State.WaitCatchFish);
        }

        public override void Exit() => owner.StopFishMove();
    }

    // 上钩响应窗口：窗口内点击左键则进入遛鱼，超时鱼跑了
    class WaitCatchFishState : FishStateBase
    {
        float timer;

        public WaitCatchFishState(FishGamePanel o) : base(o) { }

        public override void Enter()
        {
            owner.SetTip(TipBite);
            timer = 0f;

            // 咬钩的鱼游向鱼钩（曲线/时长走 FishMoveTs 配置）
            int idx = owner.BiteFishIndex();
            Tween.LocalPosition(owner.fishRt[idx], new TweenSettings<Vector3>(owner.hookCg.transform.localPosition, owner.FishMoveTs));
        }

        public override void Update()
        {
            timer += Time.deltaTime;
            if (timer >= owner.config.ResponseWindow)
            {
                // 超时：鱼饵已消耗、无奖励，自动收杆
                owner.SetTip(TipEscaped);
                owner.SwitchState(State.End);
            }
        }

        public override void OnLeftDown() => owner.SwitchState(State.CatchFish);
    }

    // QTE 遛鱼：按住左键绿条上升，松开回落；绿条覆盖小鱼则进度上升，否则下降
    class CatchFishState : FishStateBase
    {
        bool holding;
        float targetPhase;   // 小鱼浮动相位

        public CatchFishState(FishGamePanel o) : base(o) { }

        public override void Enter()
        {
            owner.SetTip(TipHold);
            holding = false;
            targetPhase = 0f;
            owner.curPerfect = true;

            // 目标点 = 实际钓鱼难度（鱼难度 - 鱼竿难度降低），而非固定值
            owner.targetCatchPoint = owner.mg.CalcFishDifficulty(owner.curCatchDifficulty);
            owner.curCatchPoint = Mathf.Min(owner.config.CatchPointStart, owner.targetCatchPoint);

            // 绿条按等级加宽（策划案 5.6：容错率随等级提升）
            var barRt = owner.catchCtrlBar.rectTransform;
            barRt.sizeDelta = new Vector2(barRt.sizeDelta.x, owner.baseCatchBarHeight + owner.mg.GreenBarWidthBonus);

            owner.catchCtrlBar.gameObject.SetActive(true);
            owner.catchTargetRt.gameObject.SetActive(true);
            SetBarY(0f);
            RefreshProgress();
        }

        public override void OnLeftDown() => holding = true;
        public override void OnLeftUp() => holding = false;

        public override void Update()
        {
            float trackH = owner.catchCtrlBarBgRt.rect.height;
            float barH = owner.catchCtrlBar.rectTransform.rect.height;
            float targetH = owner.catchTargetRt.rect.height;

            // 绿条升降（按住上升，松开回落），限制在背景区域内
            float dir = holding ? 1f : -1f;
            float barY = Mathf.Clamp(
                owner.catchCtrlBar.rectTransform.anchoredPosition.y + dir * owner.FishCatchCtrlBarMoveSpeed * Time.deltaTime,
                0f, Mathf.Max(0f, trackH - barH));
            SetBarY(barY);

            // 小鱼上下自主浮动（PingPong，无需随机种子）
            targetPhase += Time.deltaTime * Mathf.Max(20f, owner.targetCatchPoint);
            float targetY = Mathf.PingPong(targetPhase, Mathf.Max(0f, trackH - targetH));
            owner.catchTargetRt.anchoredPosition = new Vector2(owner.catchTargetRt.anchoredPosition.x, targetY);

            // 覆盖判定：小鱼中心是否落在绿条纵向区间内
            float targetCenter = targetY + targetH * 0.5f;
            bool covered = targetCenter >= barY && targetCenter <= barY + barH;

            // 覆盖 → 进度 +；未覆盖 → 进度 - 且取消完美
            if (covered)
            {
                owner.curCatchPoint += owner.FishProgressBarSpeed * Time.deltaTime;
            }
            else
            {
                owner.curCatchPoint -= owner.FishProgressBarSpeed * Time.deltaTime;
                owner.curPerfect = false;
            }
            owner.curCatchPoint = Mathf.Clamp(owner.curCatchPoint, 0f, owner.targetCatchPoint);
            RefreshProgress();

            if (owner.curCatchPoint >= owner.targetCatchPoint)
            {
                owner.SettleResult(true);
                owner.SwitchState(State.End);
            }
            else if (owner.curCatchPoint <= 0f)
            {
                owner.SettleResult(false);
                owner.SwitchState(State.End);
            }
        }

        public override void Exit()
        {
            owner.catchTargetRt.gameObject.SetActive(false);
            owner.catchCtrlBar.gameObject.SetActive(false);
        }

        void SetBarY(float y)
        {
            var rt = owner.catchCtrlBar.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        void RefreshProgress()
            => owner.catchProgressBar.fillAmount = owner.curCatchPoint / owner.targetCatchPoint;
    }

    // 结算：保持现状，等待玩家点击继续钓鱼后回到下勾阶段
    class EndState : FishStateBase
    {
        public EndState(FishGamePanel o) : base(o) { }

        public override void Enter() => owner.ShowHook(false);

        // 点击任意处继续钓鱼（下一竿会重新校验并扣除鱼饵/行动力）
        public override void OnLeftDown() => owner.SwitchState(State.SePos);
    }
    #endregion
}
