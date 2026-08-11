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
        /// <summary>导出的剧本资产命名约定：drama_{DramaId}.asset。</summary>
        private const string ScriptKeyFormat = "Assets/AddressableAssets/Remote/Configs/Drama/drama_{0}.asset";

        private readonly DramaHandlerRegistry handlers;
        private readonly DramaPlayer player;
        private readonly DramaContext context;
        private readonly DramaAssetProvider assets;
        private readonly DramaLocalization localization;

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

        public DramaDirector()
        {
            assets = new DramaAssetProvider();
            localization = new DramaLocalization();

            context = new DramaContext
            {
                Assets = assets,
                Localization = localization,
                Audio = new DramaAudio(localization),
                Game = new DramaGameBridge(),
                // Dialogue / Choice / Actors 由表现层在打开剧情 UI 后赋值，见 EnsureServices
            };

            handlers = DramaDefaultHandlers.CreateDefault();

            // 说话人名字不在这一层装配 —— DialogueLine 交的是寻址方式，
            // 由 View 调 DramaSpeakerName 自己取（见那个类的注释）

            player = new DramaPlayer(handlers);
        }

        /// <summary>
        /// 播一整段剧情，中途 Goto 会自动接着往下播，直到没有下一本。
        /// </summary>
        /// <param name="script">入口剧本。由调用方加载，本方法不会释放它。</param>
        /// <param name="ct"></param>
        public async UniTask PlayAsync(DramaScript script, CancellationToken ct)
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

            context.Mode = DramaManager.Instance.isAutoDrama
                ? EDramaPlaybackMode.Auto
                : EDramaPlaybackMode.Normal;

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

                    await PreloadAsync(DramaAssetKeys.Collect(script), ct);

                    DramaPlayResult result = await player.PlayAsync(script, context, ct);

                    ReleaseSegment();

                    if (result.Kind != DramaPlayResult.EKind.Goto)
                    {
                        break;
                    }

                    string nextKey = string.Format(ScriptKeyFormat, result.GotoDramaId);
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
            }
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

            // 立绘走本工程自己的方法：包不规定"立绘资源"是什么，
            // 我们这边是 Spine 的 SkeletonDataAsset（NpcData.IllustPath）
            foreach (int actorId in keys.ActorIds)
            {
                loads.Add(assets.LoadActorSkeletonAsync(actorId, ct));
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
