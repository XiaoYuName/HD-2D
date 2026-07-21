using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Localization.Components;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using PrimeTween;

namespace XFramework.Fish
{
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
        [LabelText("抓鱼上下浮动的小鱼图标")][SerializeField] RectTransform catchTargetRt;
        [LabelText("抓鱼进度条")][SerializeField] Image catchProgressBar;

        [SerializeField] List<long> curPossibleFishIds;
        [SerializeField] float targetCatchPoint;  // 目标抓鱼点数（=实际钓鱼难度）
        [SerializeField] float curCatchPoint;   // 当前抓鱼点数
        [SerializeField] State curState;
        FishPondSwimmer swimmer;

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
        float CatchTargetMoveSpeed => config.CatchTargetMoveSpeed;
        float FishProgressBarRiseSpeed => config.FishProgressBarRiseSpeed;
        float FishProgressBarFallSpeed => config.FishProgressBarFallSpeed;
        float FishMoveMinTime => config.FishMoveMinTime;
        float FishMoveMaxTime => config.FishMoveMaxTime;
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

            // 装备背包中等级最高的鱼竿（拥有多支时自动切到最好的一支）
            mg.SelectHighestLevelRod();

            PlayerInputManager.Instance.OnLeftMouseDown += OnLeftMouseDown;
            PlayerInputManager.Instance.OnLeftMouseUp += OnLeftMouseUp;

            // 首次刷新界面信息
            OnTimePerChange(GameDataManager.Instance.PlayerData);
            OnPlayerDataChange(GameDataManager.Instance.PlayerData);
            RefreshProgressText();

            // 首次打开时面板刚实例化，RectTransform 尚未经过 Canvas 布局，先强制刷新一次布局，保证各 Rect 尺寸就绪。
            Canvas.ForceUpdateCanvases();

            // 面板打开期间池塘里的鱼始终游动，状态机只负责改变咬钩目标。
            StartFishMove();

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

        void Update()
        {
            curStateBase?.Update();
            swimmer?.Update(Time.deltaTime);
        }
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

        /// <summary>
        /// 由结算面板的“继续”按钮开始下一轮，避免结算面板上的点击穿透到钓鱼输入。
        /// </summary>
        public void ContinueFishing()
        {
            if (curState == State.End)
                SwitchState(State.SePos);
        }
        #endregion

        #region 界面刷新
        void OnTimePerChange(PlayerData playerData)
        {
            timePeriodIcon.SetIcon(timeSlotConfig.GetIconPath(playerData.TimeSlot));
            timePeriodText.SetText(LocTableSet.MainUI, timeSlotConfig.GetNameKey(playerData.TimeSlot));
        }

        void OnPlayerDataChange(PlayerData playerData)
        {
            apText.text = GameDataManager.Instance.GetPropertyText(PropertyType.ActionPointsValue);
            RefreshProgressText();
        }

        void OnBaitChanged(List<ItemInfo> _)
            => beltCountText.text = InventoryManager.Instance.GetItemCount(ItemIdSet.Bait).ToString();

        void RefreshProgressText() => fishLvText.text = FishGameManager.LvPrefix + mg.Level;

        void SetTip(string key) => tipText.SetText(LocTableSet.Fish, key);
        #endregion

        #region 下勾 / 抽取 / 结算
        // 下勾：检验并扣除鱼饵（每次抛竿消耗1个鱼饵）；成功返回 true
        bool TryConsumeForCast()
        {
            if (InventoryManager.Instance.GetItemCount(ItemIdSet.Bait) == 0)
            {
                warnTip.Show(LocTableSet.Fish, LocVarSet.Fish.NotEnoughBait);
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

        Tween exclaTween;
        // 咬钩提示：感叹号弹出 + 持续闪烁（策划案 4.2.3）
        void ShowExclamation(bool show)
        {
            exclaTween.Stop();
            var t = exclaPointCg.transform;
            if (!show)
            {
                exclaPointCg.alpha = 0f;
                t.localScale = Vector3.one;
                return;
            }
            exclaPointCg.alpha = 1f;
            t.localScale = Vector3.one;
            Tween.PunchScale(t, new Vector3(0.35f, 0.35f, 0f), 0.4f, 2);
            exclaTween = Tween.Alpha(exclaPointCg, 1f, 0.35f, 0.35f, Ease.InOutSine,
                cycles: -1, cycleMode: CycleMode.Yoyo);
        }

        Canvas cachedCanvas;
        Camera UICamera
        {
            get
            {
                if (cachedCanvas == null)
                    cachedCanvas = GetComponentInParent<Canvas>();
                return cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? cachedCanvas.worldCamera : null;
            }
        }

        // 下勾阶段：鱼钩跟随鼠标，位置限制在点击区域内
        void MoveHookToMouse()
        {
            if (Mouse.current == null)
                return;
            // 开场缩放动画（isTween）期间面板整体 localScale 由 0 渐变到 1，此时 clickAreaRt 的世界矩阵被缩小/退化，
            // 屏幕→本地映射会严重失真（过度灵敏、贴边），表现为“第一局鱼钩不跟随鼠标”。等缩放到位后再跟随。
            // 后续每局不再重新 Open（只切状态），scale 已为 1，不受影响。
            if (TweenerRoot != null && TweenerRoot.localScale.x < 0.999f)
                return;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    clickAreaRt, mouse, UICamera, out Vector2 local))
                return;
            Rect r = clickAreaRt.rect;
            local.x = Mathf.Clamp(local.x, r.xMin, r.xMax);
            local.y = Mathf.Clamp(local.y, r.yMin, r.yMax);
            hookCg.transform.position = clickAreaRt.TransformPoint(local);
        }

        // 池塘游鱼由 FishPondSwimmer 模拟（有方向的随机游动 + 撞壁反弹 + 朝向翻转）
        void EnsureSwimmer()
        {
            if (swimmer != null || fishRt == null || fishRt.Length == 0)
                return;
            swimmer = new FishPondSwimmer(fishRt, fishRt[0].parent as RectTransform);
        }

        void StartFishMove()
        {
            EnsureSwimmer();
            swimmer?.SetActive(true);
        }

        void StopFishMove() => swimmer?.SetActive(false);

        void ResetFishSeek() => swimmer?.ResetSeek();

        // 上钩后让咬钩的鱼在鱼钩处摇摆抖动（溜鱼），直到本轮结束才复原
        void StartBiteFishStruggle() => swimmer?.Struggle(BiteFishIndex());

        // 让咬钩的鱼游向鱼钩并朝向鱼钩（把鱼钩世界坐标换算到鱼所在父物体的本地坐标）
        void SendBiteFishToHook(int idx)
        {
            EnsureSwimmer();
            if (swimmer == null || idx < 0 || idx >= fishRt.Length)
                return;
            if (fishRt[idx].parent is RectTransform parent)
            {
                Vector2 local = parent.InverseTransformPoint(hookCg.transform.position);
                swimmer.SeekTo(idx, local);
            }
        }

        bool HasBiteFishReachedHook(int idx)
            => swimmer != null && swimmer.HasReachedSeekTarget(idx);

        // 结算：成功则记日志、发奖励、加经验并弹胜利面板；失败弹失败面板
        void SettleResult(bool success)
        {
            if (!success)
            {
                SetTip(TipTryNext);
                OpenLosePanel();
                return;
            }

            // 完美捕获仅对鱼生效（杂物无任何变化，见策划案 4.3.4）
            bool perfectFish = curPerfect && curCatchIsFish;
            SetTip(perfectFish ? TipPraiseGood : TipWellDone);

            // 结算前的等级/等级内经验（胜利面板经验条从此涨到结算后）
            int prevLevel = mg.Level;
            int prevExp = mg.Exp;

            // 生成物品实例并入包 / 记日志（无对应 ItemData 的预留ID会被安全跳过）
            ItemInfo fishItem = null;
            float length = 0f, weight = 0f;
            bool isNewItem = false;
            if (InventoryManager.Instance.GetItemData(curCatchId) != null)
            {
                isNewItem = !InventoryManager.Instance.HasItemUnlock(curCatchId);
                (length, weight) = CalcCatchSizeWeight(perfectFish);
                fishItem = InventoryManager.Instance.NewItem(curCatchId, 1);
                AddLog(fishItem, length, weight);
                InventoryManager.Instance.AddItem(curCatchId, 1);
                if (isNewItem)
                    InventoryManager.Instance.UlockItem(curCatchId);
            }

            // 加经验（满级后为0）与扣行动力（成功钓起扣1，不消耗体力，见策划案 3.3.1）
            mg.AddExp(curCatchQuality, curCatchDifficulty, curCatchIsFish, perfectFish);
            GameDataManager.Instance.RemoveProperty(PropertyType.ActionPointsValue, 1);

            // 弹出胜利面板，展示渔获与经验增长
            var win = UISystem.Instance.OpenUI<FishGameWinPanel>(UIPanelIdSet.FishGameWinPanel);
            if (win != null)
                win.Set(fishItem, length, weight, isNewItem, prevLevel, prevExp, mg.Level, mg.Exp);
        }

        // 失败/逃跑：弹出失败面板
        void OpenLosePanel() => UISystem.Instance.OpenUI(UIPanelIdSet.FishGameLosePanel);

        // 尺寸/重量计算（策划案 5.1.2 / 5.1.3）：
        // 最终长度 = 基础随机长度 + 等级加成(每级+0.6,满级额外+3) + 完美(+2) + 运气(-1.5~+1.5)
        // 重量(kg) = 长度(cm)³ × 鱼种固定换算系数 ÷ 1000（杂物无长度/重量，返回0）
        (float length, float weight) CalcCatchSizeWeight(bool perfect)
        {
            if (!curCatchIsFish || !config.Contains(curCatchId))
                return (0f, 0f);

            FishItemData d = config.Get(curCatchId);
            float baseLen = Random.Range(d.LengthMin, d.LengthMax);
            float levelBonus = (mg.Level - 1) * 0.6f + (mg.IsMaxLevel ? 3f : 0f);
            float perfectBonus = perfect ? 2f : 0f;
            float luck = Random.Range(-1.5f, 1.5f);

            float length = Mathf.Max(0f, baseLen + levelBonus + perfectBonus + luck);
            float weight = length * length * length * d.WeightFactor / 1000f;
            return (length, weight);
        }
        #endregion

        // 增添渔获日志
        void AddLog(ItemInfo fishItem, float length, float weight)
        {
            FishLogCellUI fishLogCellUI = Instantiate(fishLogCell, fishLogCellContainer);
            fishLogCellUI.Set(fishItem, length, weight);
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
                // 鱼钩显示并跟随鼠标，跟随期间不挡点击
                owner.ShowHook(true);
                owner.hookCg.blocksRaycasts = false;
                owner.ShowExclamation(false);
                owner.catchTargetRt.gameObject.SetActive(false);
                owner.catchCtrlBar.gameObject.SetActive(false);
                owner.SetTip(TipStart);

                // 进入即把鱼钩对准当前鼠标，避免第一帧停在预制默认位置（配合 Open 里的布局刷新，第一局也能立即跟随）
                owner.MoveHookToMouse();
            }

            public override void Update() => owner.MoveHookToMouse();

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
            int biteFishIndex;
            bool biteFishSeeking;
            Tween castTween;

            public WaitFishState(FishGamePanel o) : base(o) { }

            public override void Enter()
            {
                owner.ShowHook(true);

                // 下勾反馈：鱼钩从选定位置上方快速落下
                var hookTf = owner.hookCg.transform;
                Vector3 castPos = hookTf.localPosition;
                castTween.Stop();
                hookTf.localPosition = castPos + new Vector3(0f, 60f, 0f);
                castTween = Tween.LocalPosition(hookTf, castPos, 0.3f, Ease.OutBack);

                owner.SetTip(TipWaiting);
                owner.ResetFishSeek();
                timer = 0f;
                biteTime = Random.Range(owner.FishMoveMinTime, owner.FishMoveMaxTime)
                        * (1f - owner.mg.BiteTimeReduceFactor);
                biteFishIndex = owner.BiteFishIndex();
                biteFishSeeking = false;
            }

            public override void Update()
            {
                if (!biteFishSeeking)
                {
                    timer += Time.deltaTime;
                    if (timer < biteTime)
                        return;

                    // WaitFish 包含咬钩鱼游到鱼钩的全过程。
                    biteFishSeeking = true;
                    owner.SendBiteFishToHook(biteFishIndex);
                    return;
                }

                if (owner.HasBiteFishReachedHook(biteFishIndex))
                    owner.SwitchState(State.WaitCatchFish);
            }

            // 到达鱼钩后仍保持池塘动画；进入 QTE 时才统一停止。
            public override void Exit() => castTween.Stop();
        }

        // 上钩响应窗口：窗口内点击左键则进入遛鱼，超时鱼跑了
        class WaitCatchFishState : FishStateBase
        {
            float timer;

            public WaitCatchFishState(FishGamePanel o) : base(o) { }

            public override void Enter()
            {
                // 上钩：让咬钩的鱼停在鱼钩处摇摆抖动（溜鱼），持续到 CatchFish 结束
                owner.StartBiteFishStruggle();
                owner.SetTip(TipBite);
                timer = 0f;

                // 咬钩提示：头顶感叹号弹出闪烁
                owner.ShowExclamation(true);
            }

            public override void Exit() => owner.ShowExclamation(false);

            public override void Update()
            {
                timer += Time.deltaTime;
                if (timer >= owner.config.ResponseWindow)
                {
                    // 超时：鱼饵已消耗、无奖励，自动收杆并弹失败面板
                    owner.SetTip(TipEscaped);
                    owner.SwitchState(State.End);
                    owner.OpenLosePanel();
                }
            }

            public override void OnLeftDown() => owner.SwitchState(State.CatchFish);
        }

        // QTE 遛鱼：按住左键绿条上升，松开回落；绿条覆盖小鱼则进度上升，否则下降
        class CatchFishState : FishStateBase
        {
            bool holding;
            float barBottomY;

            public CatchFishState(FishGamePanel o) : base(o) { }

            public override void Enter()
            {
                owner.SetTip(TipHold);
                owner.curPerfect = true;

                // 捕获进度固定 0~100：初始 10，满 100 判定成功（策划案 4.3.2/4.3.3）。
                // 注：难度不再直接作为目标点（旧写法在强竿下 CalcFishDifficulty→1，Min(10,1)=1 会瞬间满进度），
                // 难度仅影响经验/奖励结算，见 FishGameManager。
                owner.targetCatchPoint = 100f;
                owner.curCatchPoint = Mathf.Clamp(owner.config.CatchPointStart, 1f, owner.targetCatchPoint);

                // 进入时若鼠标已按住（玩家正是按下左键触发上钩），沿用其按压状态，避免第一帧绿条误回落
                holding = PlayerInputManager.Instance.IsMouseLeftDown;

                // 绿条按等级加宽（策划案 5.6：容错率随等级提升）
                var barRt = owner.catchCtrlBar.rectTransform;
                barRt.sizeDelta = new Vector2(barRt.sizeDelta.x, owner.baseCatchBarHeight + owner.mg.GreenBarWidthBonus);

                owner.catchCtrlBar.gameObject.SetActive(true);
                owner.catchTargetRt.gameObject.SetActive(true);

                float trackH = owner.catchCtrlBarBgRt.rect.height;
                float barH = barRt.rect.height;

                // 绿条初始对准小鱼（避免一进场就未覆盖而掉进度），玩家需靠升降维持覆盖
                float fishCenter = TargetCenterY();
                barBottomY = Mathf.Clamp(fishCenter - barH * 0.5f, 0f, Mathf.Max(0f, trackH - barH));
                SetBarY(barBottomY);
                RefreshProgress();
            }

            public override void OnLeftDown() => holding = true;
            public override void OnLeftUp() => holding = false;

            public override void Update()
            {
                float trackH = owner.catchCtrlBarBgRt.rect.height;
                float barH = owner.catchCtrlBar.rectTransform.rect.height;

                MoveTarget();

                // 绿条升降（按住上升，松开回落），限制在背景区域内
                float dir = holding ? 1f : -1f;
                barBottomY = Mathf.Clamp(
                    barBottomY + dir * owner.FishCatchCtrlBarMoveSpeed * Time.deltaTime,
                    0f, Mathf.Max(0f, trackH - barH));
                SetBarY(barBottomY);

                // 覆盖判定：移动中的小鱼中心是否落在绿条纵向区间内
                float targetCenter = TargetCenterY();
                bool covered = targetCenter >= barBottomY && targetCenter <= barBottomY + barH;

                // 覆盖 → 进度 +；未覆盖 → 进度 - 且取消完美
                if (covered)
                {
                    owner.curCatchPoint += owner.FishProgressBarRiseSpeed * Time.deltaTime;
                }
                else
                {
                    owner.curCatchPoint -= owner.FishProgressBarFallSpeed * Time.deltaTime;
                    owner.curPerfect = false;
                }
                owner.curCatchPoint = Mathf.Clamp(owner.curCatchPoint, 0f, owner.targetCatchPoint);
                RefreshProgress();

                if (owner.curCatchPoint >= owner.targetCatchPoint)
                {
                    owner.SwitchState(State.End);
                    owner.SettleResult(true);
                }
                else if (owner.curCatchPoint <= 0f)
                {
                    owner.SwitchState(State.End);
                    owner.SettleResult(false);
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
                var bg = owner.catchCtrlBarBgRt;

                // y 表示绿条底边到轨道底边的距离；换算为当前 anchor/pivot 下的 anchoredPosition。
                // GreenBar 默认使用居中锚点和居中 Pivot，因此不能直接把 y 写入 anchoredPosition.y。
                float anchorY = Mathf.Lerp(bg.rect.yMin, bg.rect.yMax, rt.anchorMin.y);
                float anchoredY = bg.rect.yMin + y + rt.rect.height * rt.pivot.y - anchorY;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, anchoredY);
            }

            float targetMoveDirection = 1f;
            readonly Vector3[] targetWorldCorners = new Vector3[4];

            void MoveTarget()
            {
                var bg = owner.catchCtrlBarBgRt;
                var target = owner.catchTargetRt;
                Vector3 worldCenter = target.TransformPoint(target.rect.center);
                Vector3 localCenter = bg.InverseTransformPoint(worldCenter);

                target.GetWorldCorners(targetWorldCorners);
                float halfHeight = 0f;
                for (int i = 0; i < targetWorldCorners.Length; i++)
                    halfHeight = Mathf.Max(halfHeight,
                        Mathf.Abs(bg.InverseTransformPoint(targetWorldCorners[i]).y - localCenter.y));

                float minY = bg.rect.yMin + halfHeight;
                float maxY = bg.rect.yMax - halfHeight;
                if (minY > maxY)
                    minY = maxY = bg.rect.center.y;

                float nextY = localCenter.y
                        + targetMoveDirection * owner.CatchTargetMoveSpeed * Time.deltaTime;
                if (nextY >= maxY)
                {
                    nextY = maxY;
                    targetMoveDirection = -1f;
                }
                else if (nextY <= minY)
                {
                    nextY = minY;
                    targetMoveDirection = 1f;
                }

                Vector3 nextWorldCenter = bg.TransformPoint(
                    new Vector3(localCenter.x, nextY, localCenter.z));
                target.position += nextWorldCenter - worldCenter;
            }

            // 小鱼中心相对轨道底边的纵坐标（与绿条 barY 同一坐标系）。
            // 经世界坐标换算，不受小鱼锚点/pivot 及其在层级中的位置影响。
            float TargetCenterY()
            {
                var bg = owner.catchCtrlBarBgRt;
                var target = owner.catchTargetRt;
                Vector3 world = target.TransformPoint(target.rect.center);
                float localY = bg.InverseTransformPoint(world).y;
                return localY - bg.rect.yMin;
            }

            void RefreshProgress()
                => owner.catchProgressBar.fillAmount = owner.curCatchPoint / owner.targetCatchPoint;
        }

        // 结算：保持现状，等待玩家点击继续钓鱼后回到下勾阶段
        class EndState : FishStateBase
        {
            public EndState(FishGamePanel o) : base(o) { }

            public override void Enter()
            {
                owner.ShowHook(false);
                // 溜鱼结束：清除咬钩鱼的挣扎状态，复原旋转并恢复自由游动
                owner.ResetFishSeek();
            }

            // 下一轮只由结算面板的“继续”按钮显式触发，防止按钮点击穿透到本面板。
        }
        #endregion
    }
}
