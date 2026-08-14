using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 新手引导总管理器。
    ///
    /// 职责边界：配置解析 / 触发判定 / 目标解析 / 步骤推进 / 完成记录都在这里，
    /// <see cref="TutorialUI"/> 只负责把一个 <see cref="TutorialStepContext"/> 画出来。
    /// 结果是<b>业务界面为引导写零行代码</b> —— 要高亮哪个按钮，是引导按配置里的
    /// PageID + 节点路径自己找的；只有动态生成的节点才需要挂一个 <see cref="TutorialAnchor"/>。
    ///
    /// 配置在 <see cref="TutorialDatabase"/> 资产里（原来是两张 Luban 表，字段互斥太多，
    /// 表格摊平了没法看，改成 SO + Odin 条件显示）。
    ///
    /// 别和 <see cref="GuideManager"/> 搞混：那个名字虽然叫 Guide，管的是娃娃机图鉴。
    /// </summary>
    public class TutorialManager : MonoSingleton<TutorialManager>, IGameInitialized, ISaveable
    {
        #region ISaveable

        public string GUID => "TutorialManager";

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        public void SaveData(GameSaveData data)
        {
            data.FinishedTutorialIds = new List<long>(finishedTutorials);

            // 跨存档那份走自己的文件，但落盘时机蹭存档这一下正好（同 DramaReadMarks 的做法）
            GlobalMarks.SaveIfDirty();
        }

        public void LoadData(GameSaveData data)
        {
            // 换存档槽必须整个换掉：留着上一档的完成记录，新档就再也不会教了
            finishedTutorials.Clear();

            if (data?.FinishedTutorialIds != null)
            {
                foreach (long id in data.FinishedTutorialIds)
                {
                    finishedTutorials.Add(id);
                }
            }

            // 读档时正在播的引导直接掐掉：它指着的界面已经是上一档的了
            StopTutorial("读取存档");
        }

        #endregion

        #region IGameInitialized

        public async UniTask Initialized()
        {
            database = await AssetsManager.Instance.LoadAssetsUniTask<TutorialDatabase>(AssetKeys.TutorialDatabasePath);

            if (database == null)
            {
                Debug.LogError($"引导配置资产加载失败,新手引导不会生效: {AssetKeys.TutorialDatabasePath}");
            }

            BuildCache();

            // 场景切换事件用来做 EnterScene 触发。注意 RegisterSceneChange 会立刻回调一次当前场景，
            // 那次的 SceneID 可能还是 -1（没进过场景），OnSceneChange 里会挡掉。
            if (GameSceneManager.IsInitialized)
            {
                GameSceneManager.Instance.RegisterSceneChange(OnSceneChange);
            }

            // ClickHole 用：只看点在哪，不吃点击，所以听全局点击而不是让遮罩去收
            if (PlayerInputManager.IsInitialized)
            {
                PlayerInputManager.Instance.OnClick += OnGlobalClick;
            }
        }

        public async UniTask Release()
        {
            if (GameSceneManager.IsInitialized)
            {
                GameSceneManager.Instance.UnregisterSceneChange(OnSceneChange);
            }

            if (PlayerInputManager.IsInitialized)
            {
                PlayerInputManager.Instance.OnClick -= OnGlobalClick;
            }

            StopTutorial("游戏释放");
            GlobalMarks.SaveIfDirty();
            TutorialAnchorRegistry.Clear();

            if (database != null)
            {
                AssetsManager.Instance.FreeAsset(AssetKeys.TutorialDatabasePath);
                database = null;
            }

            await UniTask.CompletedTask;
        }

        #endregion

        #region 配置缓存

        private TutorialDatabase database;

        /// <summary>触发索引：Key = 触发方式 + 触发参数，Value 已按 Priority 从大到小排好。</summary>
        private readonly Dictionary<string, List<TutorialConfig>> tutorialsByTrigger =
            new Dictionary<string, List<TutorialConfig>>();

        private void BuildCache()
        {
            tutorialsByTrigger.Clear();

            if (database?.Tutorials == null)
            {
                return;
            }

            foreach (TutorialConfig tutorial in database.Tutorials)
            {
                if (tutorial.Steps == null || tutorial.Steps.Count == 0)
                {
                    Debug.LogError($"引导 {tutorial.ID}({tutorial.Description}) 一步都没配,起不来");
                    continue;
                }

                // 手动触发的不进索引：它只能由代码点名 StartTutorial
                if (tutorial.TriggerType == TutorialTriggerType.Manual)
                {
                    continue;
                }

                string key = tutorial.GetTriggerKey();
                if (!tutorialsByTrigger.TryGetValue(key, out List<TutorialConfig> list))
                {
                    list = new List<TutorialConfig>();
                    tutorialsByTrigger[key] = list;
                }

                list.Add(tutorial);
            }

            foreach (List<TutorialConfig> list in tutorialsByTrigger.Values)
            {
                list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        #endregion

        #region 完成记录

        /// <summary>本存档已完成的引导（<see cref="TutorialRepeatType.Once"/>）。</summary>
        private readonly HashSet<long> finishedTutorials = new HashSet<long>();

        private TutorialGlobalMarks globalMarks;

        /// <summary>跨存档的完成记录（<see cref="TutorialRepeatType.OnceGlobal"/>）。</summary>
        public TutorialGlobalMarks GlobalMarks => globalMarks ??= new TutorialGlobalMarks();

        /// <summary>这段引导已经播过了没有。任务 / 解锁那边想问"教过了吗"用它。</summary>
        public bool HasFinished(long tutorialID)
        {
            return finishedTutorials.Contains(tutorialID) || GlobalMarks.IsFinished(tutorialID);
        }

        #endregion

        #region 运行时状态

        [ShowInInspector, ReadOnly, LabelText("正在播的引导")]
        private TutorialConfig runningTutorial;

        [ShowInInspector, ReadOnly, LabelText("当前步骤下标")]
        private int runningStepIndex = -1;

        /// <summary>当前步骤解析出来的目标节点，None 类型的步骤是 null。</summary>
        private RectTransform currentTarget;

        /// <summary><see cref="TutorialFinishType.Delay"/> 的剩余秒数。</summary>
        private float delayLeft;

        public bool IsRunning => runningTutorial != null;

        /// <summary>当前步骤配置，没在播时是 null。</summary>
        public TutorialStepConfig CurrentStep =>
            runningTutorial?.Steps != null && runningStepIndex >= 0 && runningStepIndex < runningTutorial.Steps.Count
                ? runningTutorial.Steps[runningStepIndex]
                : null;

        #endregion

        #region 对外入口

        /// <summary>
        /// 直接播一段引导。<see cref="TutorialTriggerType.Manual"/> 的引导只能这样起。
        /// </summary>
        /// <param name="tutorialID">引导ID</param>
        /// <param name="force">true 时跳过"播过了没有"的检查，调试用</param>
        public void StartTutorial(long tutorialID, bool force = false)
        {
            TutorialConfig tutorial = database?.Get(tutorialID);
            if (tutorial == null)
            {
                Debug.LogError($"引导配置里没有这条引导: {tutorialID}");
                return;
            }

            TryStart(tutorial, force);
        }

        /// <summary>
        /// 中止当前引导：<b>不</b>记完成，下次满足条件还会再播。
        /// 切场景、进小游戏、读档这些"引导指着的界面已经不在了"的时机调它。
        /// </summary>
        public void StopTutorial(string reason = null)
        {
            if (!IsRunning)
            {
                return;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                Debug.Log($"引导 {runningTutorial.ID} 中止({reason}),停在第 {runningStepIndex + 1} 步");
            }

            ClearRunning();
        }

        /// <summary>
        /// 按钮点击上报。<see cref="UIBase.Bind"/> 里统一调，所以全项目的按钮都自动生效，
        /// 业务代码不用为引导多写一行。<see cref="TutorialFinishType.ClickTarget"/> 靠它推进。
        /// </summary>
        public void NotifyClick(GameObject clicked)
        {
            if (!IsRunning || clicked == null)
            {
                return;
            }

            TutorialStepConfig step = CurrentStep;
            if (step == null || step.FinishType != TutorialFinishType.ClickTarget || currentTarget == null)
            {
                return;
            }

            // 配置指的可能是按钮本身，也可能是包着按钮的一整块（比如整个按钮框），两种都算点中
            Transform clickedTransform = clicked.transform;
            if (clickedTransform == currentTarget || clickedTransform.IsChildOf(currentTarget))
            {
                AdvanceStep();
            }
        }

        /// <summary>
        /// 全局点击。<see cref="TutorialFinishType.ClickHole"/> 靠它推进：
        /// 只判断点击位置在不在洞里，<b>不消费这一下点击</b> —— 场景里的小人照样被点到，
        /// 引导同时往下走。目标不是 UI 按钮（点击报不上来）的步骤就用这种。
        /// </summary>
        private void OnGlobalClick()
        {
            if (!IsRunning || CurrentStep?.FinishType != TutorialFinishType.ClickHole)
            {
                return;
            }

            TutorialUI view = UISystem.Instance.GetLoadedUI<TutorialUI>(UIKeys.TutorialUI);
            if (view == null)
            {
                return;
            }

            Vector2 screenPoint = UnityEngine.InputSystem.Pointer.current != null
                ? UnityEngine.InputSystem.Pointer.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            if (view.IsPointInsideHole(screenPoint))
            {
                AdvanceStep();
            }
        }

        /// <summary>遮罩被点了一下。<see cref="TutorialUI"/> 调，用于 AnyClick 过场。</summary>
        public void NotifyMaskClick()
        {
            if (!IsRunning)
            {
                return;
            }

            if (CurrentStep?.FinishType == TutorialFinishType.AnyClick)
            {
                AdvanceStep();
            }
        }

        /// <summary>
        /// 有界面打开了。<see cref="UIBase.Open"/> 里统一调 —— 打开界面的入口有好几个
        /// （同步/异步/协程重载），但最后都会走到 Open，所以挂在这一处最稳。
        /// </summary>
        public void NotifyUIOpened(string pageID)
        {
            if (string.IsNullOrEmpty(pageID))
            {
                return;
            }

            if (IsRunning)
            {
                TutorialStepConfig step = CurrentStep;
                if (step != null && step.FinishType == TutorialFinishType.UIOpen && step.FinishPageID == pageID)
                {
                    AdvanceStep();
                    return;
                }
            }

            TryTrigger(TutorialConfig.TriggerKeyOf(TutorialTriggerType.OpenUI, pageID));
        }

        /// <summary>一段剧情播完了。<see cref="DramaManager"/> 转过来的。</summary>
        public void NotifyDramaFinished(long dramaID)
        {
            if (dramaID <= 0)
            {
                return;
            }

            TryTrigger(TutorialConfig.TriggerKeyOf(TutorialTriggerType.DramaFinish, dramaID.ToString()));
        }

        /// <summary>
        /// 业务自定义事件：既能当触发条件（<see cref="TutorialTriggerType.Event"/>），
        /// 也能当某一步的完成条件（<see cref="TutorialFinishType.Event"/>）。
        /// "做完了某件事才继续教"就用它，比在业务里到处判断引导状态干净。
        /// </summary>
        public void TriggerEvent(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey))
            {
                return;
            }

            if (IsRunning)
            {
                TutorialStepConfig step = CurrentStep;
                if (step != null && step.FinishType == TutorialFinishType.Event && step.FinishEventKey == eventKey)
                {
                    AdvanceStep();
                    return;
                }
            }

            TryTrigger(TutorialConfig.TriggerKeyOf(TutorialTriggerType.Event, eventKey));
        }

        #endregion

        #region 触发

        private void OnSceneChange(SceneData sceneData)
        {
            // 还没进过任何小场景（-1）时不触发：那是初始化时 RegisterSceneChange 立刻回调的那一下
            if (sceneData == null || sceneData.SceneID <= 0)
            {
                return;
            }

            TryTrigger(TutorialConfig.TriggerKeyOf(TutorialTriggerType.EnterScene, sceneData.SceneID.ToString()));
        }

        /// <summary>
        /// 某个触发点到了，挑一条能播的引导播。同一触发点上配了多条时按 Priority 从大到小挑第一条能播的。
        /// </summary>
        private void TryTrigger(string triggerKey)
        {
            if (IsRunning)
            {
                return;
            }

            if (!tutorialsByTrigger.TryGetValue(triggerKey, out List<TutorialConfig> list))
            {
                return;
            }

            foreach (TutorialConfig tutorial in list)
            {
                if (TryStart(tutorial, false))
                {
                    return;
                }
            }
        }

        private bool TryStart(TutorialConfig tutorial, bool force)
        {
            if (IsRunning)
            {
                return false;
            }

            if (!force && !CanStart(tutorial))
            {
                return false;
            }

            if (tutorial.Steps == null || tutorial.Steps.Count == 0)
            {
                Debug.LogError($"引导 {tutorial.ID} 没有步骤,起不来");
                return false;
            }

            runningTutorial = tutorial;
            runningStepIndex = -1;

            return AdvanceStep();
        }

        private bool CanStart(TutorialConfig tutorial)
        {
            switch (tutorial.RepeatType)
            {
                case TutorialRepeatType.Once:
                    if (finishedTutorials.Contains(tutorial.ID))
                    {
                        return false;
                    }

                    break;

                case TutorialRepeatType.OnceGlobal:
                    if (GlobalMarks.IsFinished(tutorial.ID))
                    {
                        return false;
                    }

                    break;
            }

            if (tutorial.UnlockConditionID > 0)
            {
                // TODO: 解锁条件判定目前只在 CharacterManager 里有一份私有实现，
                // 提成公共工具之后接到这里。在那之前配了条件的引导会直接播，不会被条件挡住。
                Debug.LogError($"引导 {tutorial.ID} 配了解锁条件 {tutorial.UnlockConditionID},但条件判定还没接入,本次按满足处理");
            }

            return true;
        }

        #endregion

        #region 步骤推进

        /// <summary>
        /// 走到下一步；已经是最后一步就算整段完成。
        /// </summary>
        /// <returns>还在播返回 true（正常播完返回 false）</returns>
        private bool AdvanceStep()
        {
            int next = runningStepIndex + 1;

            if (runningTutorial?.Steps == null || next >= runningTutorial.Steps.Count)
            {
                CompleteTutorial();
                return false;
            }

            runningStepIndex = next;
            return PlayCurrentStep();
        }

        private bool PlayCurrentStep()
        {
            TutorialStepConfig step = CurrentStep;
            if (step == null)
            {
                CompleteTutorial();
                return false;
            }

            if (!TryResolveTarget(step, out RectTransform target))
            {
                // 用户定的策略：目标找不到就整段中止,并且报错 —— 大概率是配置写错了或者界面结构改了,
                // 跳过这一步继续往下走只会把玩家教到一个更莫名其妙的地方
                Debug.LogError($"引导 {runningTutorial.ID} 第 {runningStepIndex + 1} 步找不到目标节点," +
                               $"TargetType={step.TargetType} PageID={step.TargetPageID} Path={step.TargetPath} AnchorKey={step.AnchorKey}");
                ClearRunning();
                return false;
            }

            currentTarget = target;
            delayLeft = step.FinishType == TutorialFinishType.Delay ? step.DelaySeconds : 0f;

            TutorialStepContext context = new TutorialStepContext
            {
                Data = step,
                Target = target,
            };

            TutorialUI view = UISystem.Instance.OpenUI<TutorialUI>(UIKeys.TutorialUI);
            if (view == null)
            {
                Debug.LogError($"打开 {UIKeys.TutorialUI} 失败,引导 {runningTutorial.ID} 中止");
                ClearRunning();
                return false;
            }

            view.ShowStep(context);
            return true;
        }

        private void CompleteTutorial()
        {
            if (runningTutorial == null)
            {
                return;
            }

            switch (runningTutorial.RepeatType)
            {
                case TutorialRepeatType.Once:
                    finishedTutorials.Add(runningTutorial.ID);
                    break;

                case TutorialRepeatType.OnceGlobal:
                    GlobalMarks.MarkFinished(runningTutorial.ID);
                    break;
            }

            ClearRunning();
        }

        /// <summary>
        /// 收摊：清运行时状态、关掉引导界面。
        /// 完成和中止都走这里，区别只在于要不要记完成。
        /// </summary>
        private void ClearRunning()
        {
            runningTutorial = null;
            runningStepIndex = -1;
            currentTarget = null;
            delayLeft = 0f;

            TutorialUI view = UISystem.Instance.GetLoadedUI<TutorialUI>(UIKeys.TutorialUI);
            if (view != null)
            {
                view.Clear();
                UISystem.Instance.CloseUI(UIKeys.TutorialUI);
            }
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            TutorialStepConfig step = CurrentStep;

            // 目标在播到一半时消失（界面被关了、格子被回收了）：同样整段中止，
            // 留一个指着空气的洞比直接收掉更让人困惑。屏幕固定位置的洞没有节点可失效，不参与这个判断
            if (step != null && step.NeedsTargetNode &&
                (currentTarget == null || !currentTarget.gameObject.activeInHierarchy))
            {
                Debug.LogError($"引导 {runningTutorial.ID} 第 {runningStepIndex + 1} 步的目标节点中途失效,整段中止");
                ClearRunning();
                return;
            }

            if (step != null && step.FinishType == TutorialFinishType.Delay)
            {
                delayLeft -= Time.unscaledDeltaTime;
                if (delayLeft <= 0f)
                {
                    AdvanceStep();
                }
            }
        }

        #endregion

        #region 目标解析

        private bool TryResolveTarget(TutorialStepConfig step, out RectTransform target)
        {
            target = null;

            switch (step.TargetType)
            {
                case TutorialTargetType.None:
                case TutorialTargetType.ScreenRect:
                    // 这两种都没有要跟的节点：一个不挖洞，一个洞的位置写死在配置里
                    return true;

                case TutorialTargetType.Anchor:
                    return TutorialAnchorRegistry.TryGet(step.AnchorKey, out target);

                case TutorialTargetType.UIPath:
                    // 用 GetLoadedUI 而不是 GetUI：后者在界面没加载过时会顺手实例化一个出来，
                    // 引导只是想看看界面在不在，不该有这种副作用
                    UIBase ui = UISystem.Instance.GetLoadedUI<UIBase>(step.TargetPageID);
                    if (ui == null || !ui.isOpen)
                    {
                        Debug.LogError($"引导要指的界面没打开: {step.TargetPageID}");
                        return false;
                    }

                    Transform node = string.IsNullOrEmpty(step.TargetPath)
                        ? ui.transform
                        : ui.transform.Find(step.TargetPath);

                    if (node == null)
                    {
                        Debug.LogError($"界面 {step.TargetPageID} 下找不到节点: {step.TargetPath}");
                        return false;
                    }

                    target = node as RectTransform;
                    return target != null;

                default:
                    return false;
            }
        }

        #endregion
    }
}
