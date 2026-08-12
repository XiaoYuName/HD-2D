using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaGameBridge"/> 的本工程实现：剧情要回调的业务逻辑落在这里。
    ///
    /// 本工程目前还没有任务系统，所以先记一条日志把剧本里的领任务指令暴露出来，
    /// 等任务系统落地把这里换成真正的接口调用即可。
    /// </summary>
    public sealed class DramaGameBridge : IDramaGameBridge
    {
        public UniTask ReceiveTaskAsync(long taskId, CancellationToken ct)
        {
            Debug.LogWarning($"[Drama] 剧情要求领取任务 {taskId}，但任务系统还没接，本次跳过");
            return UniTask.CompletedTask;
        }

        // ==================================================== 游戏场景

        /// <summary>
        /// 切换游戏内真实场景，等它切完再返回。
        ///
        /// <b>为什么要等：</b>后面的指令（换背景、出立绘）都是演给新场景看的，
        /// 不等的话会在旧场景上演一半。
        ///
        /// <b>怎么等：</b><c>GameSceneManager</c> 那两个入口都是 <c>Forget</c> 出去的，
        /// 调用方拿不到结束时机；但它内部的引用计数是在调用里<b>同步</b> ++ 的
        /// （见 <c>RunGameSceneTransition</c>），所以发起之后直接盯着计数归零就行，
        /// 不会出现"还没开始就以为已经结束"。
        /// </summary>
        public async UniTask ChangeGameSceneAsync(long mapSceneId, long minSceneId, CancellationToken ct)
        {
            if (!GameSceneManager.IsInitialized)
            {
                Debug.LogError($"[Drama] 要切场景（{mapSceneId} / {minSceneId}）但 GameSceneManager 还没就位");
                return;
            }

            GameSceneManager manager = GameSceneManager.Instance;

            if (mapSceneId > 0)
            {
                manager.EnterGameScene(mapSceneId, minSceneId);
            }
            else
            {
                // 大场景留空 = 留在当前大场景里只换小场景
                manager.OptionGameScene(minSceneId);
            }

            await UniTask.WaitUntil(() => !manager.IsGameSceneTransitioning, cancellationToken: ct);
        }

        // ==================================================== 结束后开界面

        string pendingEndUIPage;

        /// <summary>
        /// 记下"剧情结束后要打开哪个界面"。<b>刻意不在这儿打开</b> ——
        /// 调用发生在剧情还没收尾的时候，当场打开会被随后的
        /// "关剧情面板 + 还原进剧情前的界面"盖掉。真正打开在
        /// <see cref="DramaManager"/> 的收尾里，见 <see cref="ConsumePendingEndUI"/>。
        /// </summary>
        public void RequestOpenUIOnEnd(string uiPage)
        {
            pendingEndUIPage = uiPage;
        }

        /// <summary>取走待打开的界面名，取完就清掉（同一段剧情只开一次）。</summary>
        public string ConsumePendingEndUI()
        {
            string page = pendingEndUIPage;
            pendingEndUIPage = null;
            return page;
        }
    }
}
