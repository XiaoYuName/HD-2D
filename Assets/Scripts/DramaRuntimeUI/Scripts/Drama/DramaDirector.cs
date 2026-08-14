using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Flow;
using Drama.Runtime.Handlers;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 剧情播放的总调度：装配服务、预载资源、驱动 <see cref="DramaPlayer"/>、收尾释放。
    ///
    /// <b>换剧本是个 while 循环，不是递归。</b> GotoDramaAction 只把"我要换到 N 号"
    /// 报上来，由这里退出当前这本、释放、再加载下一本——连播 50 段栈也是平的。
    /// </summary>
    public sealed class DramaDirector
    {
        // 剧本资产的位置由配置表 DramaData 给出（ID → DramaScriptsPath），
        // 不再依赖"文件名等于剧情ID"这种约定。见 ScriptKeyOf。

        private readonly DramaHandlerRegistry handlers;
        private readonly DramaPlayer player;
        private readonly DramaContext context;
        private readonly DramaAssetProvider assets;
        private readonly DramaLocalization localization;
        private readonly DramaGameBridge gameBridge;
        private readonly DramaCGStage cgStage;

        /// <summary>装配好的上下文。表现层三个服务（对话框 / 选项 / 立绘舞台）打开 UI 后往这里塞。</summary>
        public DramaContext Context => context;

        /// <summary>指令表。本工程特有的指令（转场、切背景、播音乐）在这里补注册。</summary>
        public DramaHandlerRegistry Handlers => handlers;

        /// <summary>
        /// 资源层。<see cref="DramaContext.Assets"/> 是它的接口视图，
        /// 这里给出具体类型是因为立绘那条（<c>LoadActorSkeletonAsync</c>）不在包的接口上，
        /// 舞台需要拿到具体类型才调得到。
        /// </summary>
        public DramaAssetProvider AssetProvider => assets;

        /// <summary>
        /// 业务回调层。给出具体类型是因为「UI结束」那条要在剧情<b>收尾之后</b>
        /// 才去取待打开的界面（<c>ConsumePendingEndUI</c>），那不在包的接口上。
        /// </summary>
        public DramaGameBridge GameBridge => gameBridge;

        /// <summary>
        /// CG 层。给出具体类型是因为它的三个场景引用（替身父节点 / 模型父节点 / 立绘层）
        /// 要由 <c>DramaManager</c> 在打开剧情 UI 之后塞进来，那不在包的接口上。
        /// </summary>
        public DramaCGStage CGStage => cgStage;

        // ==================================================== 存档点

        /// <summary>正在播的是哪一本。没有在播时是 0。</summary>
        public long CurrentDramaId { get; private set; }

        /// <summary>
        /// 当前停在哪条台词上。<b>这是整段剧情唯一的合法存档点</b> ——
        /// 台词等玩家点击是唯一的空闲时刻，别处存下来读档会落在一条正在跑的动画中间。
        /// 还没播到任何台词时是 -1。
        /// </summary>
        public int CurrentTalkIndex { get; private set; } = -1;

        /// <summary>本剧本已经走过的选项，读档时要原样喂回去。</summary>
        public IReadOnlyList<int> ChoicePath => context.PickedChoices;

        /// <summary>
        /// 一条台词<b>即将播出</b>时报一次（剧本ID + 这条台词）。已读记录、对话历史挂这儿。
        ///
        /// <b>读档的静默重放期间不报</b>：那些台词是上一次玩的时候读过的，
        /// 历史记录本来就在存档里，再报一遍就重复了。这正是
        /// <see cref="EDramaPlaybackMode.Restoring"/> 要和 <see cref="EDramaPlaybackMode.Skip"/>
        /// 分成两个模式的用处 —— 两者等待都归零，但一个是玩家在看戏、一个是玩家还没进场。
        ///
        /// <b>报在执行之前</b>，所以订阅方可以在这里做"这句没读过就别跳了"这类拦截：
        /// 改完 <c>Mode</c> 紧接着执行的就是这一条，语音和打字机都还没开始。
        /// </summary>
        public event Action<long, TalkAction> TalkStarting;

        /// <summary>
        /// 一<b>本</b>剧本走到头了（参数是它的剧情ID）。任务 / 条件系统的"做过某段剧情"记在这儿。
        ///
        /// <b>「走到头」= 正常播完，或者播完之后要求跳去下一本</b>（Goto 也是这本已经播完了）。
        /// 玩家中途退出、剧情被打断（Cancelled）<b>不算</b> —— 那种情况下后半段没看到，
        /// 拿它当"做过"会让任务凭空满足。
        ///
        /// 连播 N 本会报 N 次，每本一次。
        /// </summary>
        public event Action<long> DramaFinished;

        public DramaDirector()
        {
            assets = new DramaAssetProvider();
            localization = new DramaLocalization();
            gameBridge = new DramaGameBridge();

            // CG 层要在"还没有任何 CG"的时候就存在（剧本可能一条 CG 都没有），
            // 所以在这儿建好；场景相关的三个引用由 DramaManager 开播时塞进来
            cgStage = new DramaCGStage { Assets = assets };

            context = new DramaContext
            {
                Assets = assets,
                Localization = localization,
                Audio = new DramaAudio(localization),
                Game = gameBridge,
                CG = cgStage,
                // Dialogue / Choice / Actors 由表现层在打开剧情 UI 后赋值，见 EnsureServices
            };

            handlers = DramaDefaultHandlers.CreateDefault();

            // 说话人名字不在这一层装配 —— DialogueLine 交的是寻址方式，
            // 由 View 调 DramaSpeakerName 自己取（见那个类的注释）

            player = new DramaPlayer(handlers);

            // 每条指令执行前报一次，我们只挑台词记下来当存档点
            player.ActionExecuting += OnActionExecuting;
        }

        void OnActionExecuting(DramaAction action)
        {
            if (action is not TalkAction talk)
            {
                return;
            }

            CurrentTalkIndex = action.Index;

            // 存档点每一条都要更新（重放也得跟着走，否则恢复完存档点还停在 -1），
            // 但已读 / 历史只认玩家真的在看的那些，重放不算
            if (context.Mode == EDramaPlaybackMode.Restoring)
            {
                return;
            }

            TalkStarting?.Invoke(CurrentDramaId, talk);
        }

        /// <summary>
        /// 播一整段剧情，中途 Goto 会自动接着往下播，直到没有下一本。
        /// </summary>
        /// <param name="script">入口剧本。由调用方加载，本方法不会释放它。</param>
        /// <param name="ct"></param>
        /// <param name="restore">
        /// 读档恢复点，null = 从头正常播。只对<b>入口那一本</b>生效：
        /// 恢复完之后再 Goto 出去的本子都是全新开始的，没有历史要重放。
        /// </param>
        /// <param name="dramaId">
        /// 入口剧本的<b>配置表 ID</b>（调用方就是拿它查 <see cref="ScriptKeyOf"/> 把剧本加载出来的）。
        ///
        /// <b>必须传，别让它退回 <c>script.DramaId</c></b>：资产里那个 ID 是策划在剧情图
        /// 「进入」节点里填的，漏填 / 复制粘贴很容易几本剧本全是同一个值，而它<b>不参与加载</b>，
        /// 错了也照样能播 —— 于是错误一直藏着，直到存档点、已读、对话历史拿它回头查表时才爆
        /// （症状是"读档恢复不了剧情""对话记录是空的"，很难联想到是这个 ID）。
        /// </param>
        public async UniTask PlayAsync(DramaScript script, CancellationToken ct,
                                       DramaRestorePoint restore = null, long dramaId = -1)
        {
            if (script == null)
            {
                Debug.LogError("[Drama] 剧本为空");
                return;
            }

            if (!EnsureServices())
            {
                return;
            }

            // 播放模式归 DramaManager.SetPlaybackMode 管（开播前会刷一次，播放中途玩家还能改），
            // 这里不要再写 context.Mode —— 写了就等于每段开头把玩家点的 AUTO / SKIP 抹掉

            // 只释放本方法自己加载的那些剧本，入参那本归调用方
            string ownedScriptKey = null;

            try
            {
                while (script != null)
                {
                    // 播之前就把缺失的 Handler 暴露出来，别播到第 37 条才炸
                    List<Type> missing = handlers.FindMissing(script);
                    if (missing.Count > 0)
                    {
                        Debug.LogError($"[Drama] 剧本 {script.DramaId} 用到了没注册 Handler 的指令：{string.Join("、", missing.ConvertAll(t => t.Name))}");
                        break;
                    }

                    ResetPresentation();

                    // 换本子就换一套存档点上下文。选项路径必须跟着换 ——
                    // 恢复只重放当前这一本，上一本的选择留着只会错位
                    CurrentDramaId = ResolveDramaId(script, dramaId);
                    CurrentTalkIndex = -1;
                    context.ResetChoicePath(restore?.ChoicePath);

                    await PreloadAsync(DramaAssetKeys.Collect(script), ct);

                    DramaPlayResult result = await player.PlayAsync(
                        script, context, ct, restoreUntilIndex: restore?.ActionIndex ?? -1);

                    // 恢复点只用一次：Goto 出去的下一本是全新开始的
                    restore = null;

                    // 这一本走到头了才报。被取消（玩家退出剧情 / 场景销毁）不算 ——
                    // 那时候后半段根本没播，算成"做过"会让任务凭空满足
                    if (result.Kind != DramaPlayResult.EKind.Cancelled)
                    {
                        DramaFinished?.Invoke(CurrentDramaId);
                    }

                    ReleaseSegment();

                    if (result.Kind != DramaPlayResult.EKind.Goto)
                    {
                        break;
                    }

                    string nextKey = ScriptKeyOf(result.GotoDramaId);
                    if (string.IsNullOrEmpty(nextKey))
                    {
                        Debug.LogError($"[Drama] 跳转失败：剧情表里没有 {result.GotoDramaId}，或者它没填 DramaScriptsPath");
                        break;
                    }

                    DramaScript next = await AssetsManager.Instance.LoadAssetsUniTask<DramaScript>(nextKey);
                    if (next == null)
                    {
                        Debug.LogError($"[Drama] 加载剧本 {result.GotoDramaId} 失败：{nextKey}");
                        AssetsManager.Instance.FreeAsset(nextKey);
                        break;
                    }

                    FreeScript(ref ownedScriptKey);
                    ownedScriptKey = nextKey;
                    script = next;

                    // 下一本的身份同样是"拿来查表的那个 ID"，不是资产里写的
                    dramaId = result.GotoDramaId;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[Drama] 剧情被取消");
            }
            finally
            {
                // 无论正常结束、报错还是被取消，资源都要还干净
                ReleaseSegment();
                FreeScript(ref ownedScriptKey);

                // 已经不在播了，别让存档记下一个走完的剧本
                CurrentDramaId = 0;
                CurrentTalkIndex = -1;
            }
        }

        /// <summary>
        /// 定下这一本的身份。
        ///
        /// <b>以"加载它用的那个配置表 ID"为准</b>，资产里的 <c>script.DramaId</c> 只拿来对账 ——
        /// 存档点、已读、对话历史记下来的 ID 之后都要回头查 <see cref="ScriptKeyOf"/>，
        /// 记成一个查不到的值就等于这段进度全废（而且要等到读档 / 开对话记录才看得出来）。
        /// </summary>
        private static long ResolveDramaId(DramaScript script, long loadedWithId)
        {
            if (loadedWithId <= 0)
            {
                // 没人告诉我们是按哪个 ID 载进来的，只能信资产里的
                return script.DramaId;
            }

            if (script.DramaId != loadedWithId)
            {
                Debug.LogWarning(
                    $"[Drama] 剧本「{script.SourceGraph}」的「进入」节点里填的剧情ID 是 {script.DramaId}，" +
                    $"但它是按配置表 ID {loadedWithId} 加载的。已按 {loadedWithId} 记进度；" +
                    "建议把图里那个 ID 改成配置表的 ID，否则 Goto 到本剧本、以及看图排查时都会对不上");
            }

            return loadedWithId;
        }

        /// <summary>
        /// 按剧情ID 到配置表里取剧本资产的路径。开播、Goto、读档恢复走的都是这一个口子。
        ///
        /// 走配置表而不是"文件名 = 剧情ID"的约定：导出产物是按剧情图的文件名命名的，
        /// 和「进入」节点里填的剧情ID 是两个东西，靠约定对齐迟早会错开，
        /// 而且错了以后的症状是"加载不到"，很难联想到是 ID 填错了。
        /// </summary>
        /// <returns>取不到时返回 null，调用方负责报错。</returns>
        public static string ScriptKeyOf(long dramaId)
        {
            DramaData data = LubanManager.Instance.TbDramaData.GetOrDefault(dramaId);
            return string.IsNullOrEmpty(data?.DramaScriptsPath) ? null : data.DramaScriptsPath;
        }

        /// <summary>主角显示名 = 玩家自己起的昵称。</summary>
        private static string ResolveHeroName()
        {
            return GameDataManager.Instance.PlayerData?.UserName ?? string.Empty;
        }

        /// <summary>
        /// 角色显示名。<c>NpcData.Name</c> 是多语言引用，同步查表——
        /// 这张表已经由 <see cref="PreloadAsync"/> 预热过了，这里不会触发加载。
        /// </summary>
        private static string ResolveActorName(int actorId)
        {
            NpcData npc = LubanManager.Instance.TbNpcData.GetOrDefault(actorId);
            if (npc?.Name == null)
            {
                Debug.LogWarning($"[Drama] 角色 {actorId} 没有名字配置，说话人名留空");
                return string.Empty;
            }

            return LocTool.Get(npc.Name.Table, npc.Name.Value);
        }

        /// <summary>
        /// 每段剧本开播前把表现层的"持续状态"复位。
        ///
        /// <b>为什么必须在开头做而不是在 <see cref="ReleaseSegment"/> 里做</b>：
        /// UI 面板是 UISystem 复用的，第二次播剧情拿到的是同一个实例，
        /// 上次留下的对话框皮肤还在字段里；靠"上一段收尾时复位"救不了第一段。
        ///
        /// <b>只复位皮肤，不藏对话框。</b> 旧工程的 DoStart() 是连框一起藏的，
        /// 但它换本子必带全屏转场（FadeOut → 换本 → FadeIn），复位全发生在黑幕底下。
        /// 我们的 GotoDramaAction 不强制转场，这里藏框会看到一下闪。
        /// 想在换本处清屏，就在图里显式放「转场 + 对话框隐藏」——
        /// 反正显示是台词指令的副作用，隐藏才需要显式表达。
        /// </summary>
        private void ResetPresentation()
        {
            context.Dialogue?.SetFrame(ETalkFrame.Normal);
        }

        /// <summary>
        /// 播放前批量预载。剧情播到一半再去现加载立绘 / 语音必然卡顿。
        /// 多语言表和资源可以同时拉，互相不依赖。
        /// </summary>
        private async UniTask PreloadAsync(DramaAssetKeys keys, CancellationToken ct)
        {
            await UniTask.WhenAll(
                localization.PreloadStringTablesAsync(CollectStringTables(keys), ct),
                localization.PreloadAssetTablesAsync(keys.VoiceTables, ct));

            List<UniTask> loads = new List<UniTask>();

            // 立绘走本工程自己的方法：包不规定"立绘资源"是什么。
            //
            // 用 ActorAssets 而不是 ActorIds —— 前者带着"这个角色用哪种立绘"，
            // 后者只有 ID，没法决定去角色表的哪个字段取路径。
            // 同一个角色的两种立绘会各来一条，正是我们要的
            foreach (ActorAssetRef actor in keys.ActorAssets)
            {
                loads.Add(assets.PreloadActorAsync(actor, ct));
            }

            // CG 是全屏 Live2D，模型不小，不预载切 CG 时会卡一下
            foreach (long cgId in keys.CgIds)
            {
                loads.Add(assets.LoadCGPrefabAsync(cgId, ct));
            }

            foreach (long backgroundId in keys.BackgroundIds)
            {
                loads.Add(assets.LoadBackgroundAsync(backgroundId, ct));
            }

            // BGM 没有预载这一步：MusicId 是音频配置表 ID，clip 跟着配置表一起在内存里

            if (loads.Count > 0)
            {
                await UniTask.WhenAll(loads);
            }
        }

        /// <summary>
        /// 要预热的字符串表 = 剧本自己用到的 + 本段出场角色的名字表。
        ///
        /// 后者不在 <see cref="DramaAssetKeys"/> 里，因为包不认识 <c>NpcData</c>、收不到这张表。
        /// 而 <see cref="ResolveActorName"/> 是<b>同步</b>查表的（接口要求如此），
        /// 不预热就会在第一句带角色名的台词那里触发一次同步加载、掉一帧。
        /// </summary>
        private static IReadOnlyCollection<string> CollectStringTables(DramaAssetKeys keys)
        {
            HashSet<string> tables = new HashSet<string>(keys.StringTables);

            foreach (int actorId in keys.ActorIds)
            {
                NpcData npc = LubanManager.Instance.TbNpcData.GetOrDefault(actorId);
                if (npc?.Name != null && !string.IsNullOrEmpty(npc.Name.Table))
                {
                    tables.Add(npc.Name.Table);
                }
            }

            return tables;
        }

        /// <summary>
        /// 一段剧本收尾：先把游离动画推到终点，再清舞台、还资源。
        /// 顺序不能反——立绘还没停就先释放，Tween 会去动一个已经销毁的 Transform。
        /// </summary>
        private void ReleaseSegment()
        {
            // 每一步单独兜异常。本方法在 PlayAsync 的 finally 里跑，
            // 一步抛出去会有两个后果：① 顶掉正在传播的原始异常，害得真正的错因看不见；
            // ② finally 里后面的 FreeScript 跑不到，剧本资产的 AA 引用直接漏掉。
            // 退出 Play 时尤其常见 —— Unity 先销毁 GameObject，然后 ct 取消才走到这里，
            // 表现层的组件已经是 destroyed 了。
            Step(() =>
            {
                context.Actors?.CompleteAllTweens();
                context.Actors?.ReleaseAll();
            });

            Step(() =>
            {
                context.Background?.CompleteAllTweens();
                context.Background?.ReleaseAll();
            });

            // 剧本可能停在「盖着黑幕」的状态（Phase=In 之后被打断），别把黑幕留在屏幕上
            Step(() => context.Screen?.Clear());

            // 同理：剧本可能停在「CG 还盖着」的状态。Clear 里会把立绘层恢复回来 ——
            // 不恢复的话下一段剧情一个立绘都看不见，而且极难联想到是上一段 CG 没退干净
            Step(() => context.CG?.Clear());

            Step(() => assets.ReleaseAll());
        }

        /// <summary>收尾里的一步。失败只记一条日志，不让它打断后面的步骤。</summary>
        private static void Step(Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Drama] 收尾时有一步失败了，已跳过：{e.GetType().Name} {e.Message}");
            }
        }

        private static void FreeScript(ref string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            AssetsManager.Instance.FreeAsset(key);
            key = null;
        }

        /// <summary>表现层三个服务缺一个都播不了，开播前一次性报清楚，别等到空引用。</summary>
        private bool EnsureServices()
        {
            List<string> missing = new List<string>();
            if (context.Dialogue == null) missing.Add(nameof(context.Dialogue));
            if (context.Choice == null) missing.Add(nameof(context.Choice));
            if (context.Actors == null) missing.Add(nameof(context.Actors));
            if (context.Screen == null) missing.Add(nameof(context.Screen));
            if (context.Background == null) missing.Add(nameof(context.Background));

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError($"[Drama] 表现层服务还没装配：{string.Join("、", missing)}");
            return false;
        }
    }
}
