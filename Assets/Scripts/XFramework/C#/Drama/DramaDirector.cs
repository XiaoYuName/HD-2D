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

        public DramaDirector()
        {
            assets = new DramaAssetProvider();
            localization = new DramaLocalization();

            context = new DramaContext
            {
                Assets = assets,
                Localization = localization,
                Audio = new DramaAudio(),
                Game = new DramaGameBridge(),
                // Dialogue / Choice / Actors 由表现层在打开剧情 UI 后赋值，见 EnsureServices
            };

            handlers = DramaDefaultHandlers.CreateDefault();

            // 包里还没带默认 Handler 的指令（ChangeBackground / PlayMusic 等）在这里补注册

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

        /// <summary>
        /// 播放前批量预载。剧情播到一半再去现加载立绘 / 语音必然卡顿。
        /// 多语言表和资源可以同时拉，互相不依赖。
        /// </summary>
        private async UniTask PreloadAsync(DramaAssetKeys keys, CancellationToken ct)
        {
            await UniTask.WhenAll(
                localization.PreloadStringTablesAsync(keys.StringTables, ct),
                localization.PreloadAssetTablesAsync(keys.VoiceTables, ct));

            List<UniTask> loads = new List<UniTask>();

            foreach (int actorId in keys.ActorIds)
            {
                loads.Add(assets.LoadActorAsync(actorId, ct));
            }

            foreach (long backgroundId in keys.BackgroundIds)
            {
                loads.Add(assets.LoadBackgroundAsync(backgroundId, ct));
            }

            foreach (string musicId in keys.MusicIds)
            {
                loads.Add(assets.LoadMusicAsync(musicId, ct));
            }

            if (loads.Count > 0)
            {
                await UniTask.WhenAll(loads);
            }
        }

        /// <summary>
        /// 一段剧本收尾：先把游离动画推到终点，再清舞台、还资源。
        /// 顺序不能反——立绘还没停就先释放，Tween 会去动一个已经销毁的 Transform。
        /// </summary>
        private void ReleaseSegment()
        {
            if (context.Actors != null)
            {
                context.Actors.CompleteAllTweens();
                context.Actors.ReleaseAll();
            }

            // 剧本可能停在「盖着黑幕」的状态（Phase=In 之后被打断），别把黑幕留在屏幕上
            context.Screen?.Clear();

            assets.ReleaseAll();
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

            if (missing.Count == 0)
            {
                return true;
            }

            Debug.LogError($"[Drama] 表现层服务还没装配：{string.Join("、", missing)}");
            return false;
        }
    }
}
