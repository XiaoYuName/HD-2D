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
        ///
        /// <b>走哪条入口取决于"当前在哪"，不是"剧本填没填大场景ID"。</b>
        /// 场景加载全是 Additive、卸载得调用方显式做，而两条入口卸的东西不一样：
        ///   <c>EnterGameScene</c>  卸<b>大地图</b>    —— 给"在大地图上进小场景"用
        ///   <c>OptionGameScene</c> 卸<b>当前小场景</b> —— 给"小场景之间互换"用
        /// 在小场景里走 EnterGameScene，卸的是一个根本没加载的大地图，
        /// 旧小场景就留在 Hierarchy 里和新场景叠在一起。
        /// </summary>
        public async UniTask ChangeGameSceneAsync(long mapSceneId, long minSceneId, CancellationToken ct)
        {
            if (!GameSceneManager.IsInitialized)
            {
                Debug.LogError($"[Drama] 要切场景（{mapSceneId} / {minSceneId}）但 GameSceneManager 还没就位");
                return;
            }

            GameSceneManager manager = GameSceneManager.Instance;
            SceneData current = manager.GameSceneData;
            long currentMap = current?.WordMapSceneID ?? -1;
            long currentScene = current?.SceneID ?? -1;

            // 已经在目标场景了就别白走一趟：转场会闪一次黑幕、场景还要重载一遍
            if (currentScene == minSceneId && (mapSceneId <= 0 || currentMap == mapSceneId))
            {
                return;
            }

            if (currentScene == -1)
            {
                // 当前在大地图（或还没进任何场景）—— 这条会把大地图卸掉
                manager.EnterGameScene(mapSceneId > 0 ? mapSceneId : currentMap, minSceneId);
            }
            else
            {
                // 当前在某个小场景里。OptionGameScene 只换小场景、大场景沿用当前的，
                // 所以剧本要求换大场景时它换不了 —— 明确报出来，别悄悄用一个错的
                if (mapSceneId > 0 && mapSceneId != currentMap)
                {
                    Debug.LogWarning($"[Drama] 剧本要从小场景 {currentScene} 切到大场景 {mapSceneId}，" +
                                     $"但换小场景这条路只能留在当前大场景 {currentMap} 里，大场景没有真的切过去。" +
                                     "要跨大场景，先用一条「游戏场景」回大地图（小场景填 -1），再进目标场景");
                }

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
