using System.Collections;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 把场景切换翻译成进出区域 / 区域停留三种触发事件。从 QuestManager 抽出来，
    /// 那边只管建一个、开一个、读档时清一次。
    ///
    /// 场景是两级的（大场景 ＋ 小场景），这里把两级拆成各自的事件上报：进大场景报一次
    /// 「小场景ID = 0」的，进小场景再报一次带小场景ID 的。所以配置只写大场景的任务
    /// 在大场景内换小场景时不会被重复触发，而配到小场景的任务每次进那个小场景都能命中。
    ///
    /// 停留协程只在「当前区域确实有还没领、且靠停留触发的任务」时才起，领完就自己收工。
    /// 大场景的停留是累计的：在同一个大场景里换小场景不重新计时。
    /// </summary>
    public class QuestZoneTracker
    {
        /// <summary>停留检查间隔，秒。</summary>
        const float CheckInterval = 0.5f;

        readonly MonoBehaviour host;
        readonly QuestAcceptScanner scanner;

        long currentMapSceneId;
        long currentSceneId;

        /// <summary>在当前大场景里累计待了多久，换小场景不清零。</summary>
        float mapStayElapsed;

        Coroutine stayRoutine;

        public long CurrentMapSceneId => currentMapSceneId;
        public long CurrentSceneId => currentSceneId;

        /// <param name="host">跑停留协程的宿主，传 <see cref="QuestManager"/> 自己。</param>
        public QuestZoneTracker(MonoBehaviour host, QuestAcceptScanner scanner)
        {
            this.host = host;
            this.scanner = scanner;
        }

        public void Start() => GameSceneManager.Instance.RegisterSceneChange(OnSceneChanged);

        public void Stop()
        {
            StopStayRoutine();
            GameSceneManager.Instance.UnregisterSceneChange(OnSceneChanged);
        }

        /// <summary>读档用：忘掉当前区域，下一次场景回调重新走一遍进入流程。</summary>
        public void Reset()
        {
            StopStayRoutine();
            currentMapSceneId = 0;
            currentSceneId = 0;
            mapStayElapsed = 0f;
        }

        void OnSceneChanged(SceneData sceneData)
        {
            // 大地图上两个 ID 都是 -1，统一规成 0：区域事件只认正数ID
            long mapSceneId = Positive(sceneData?.WordMapSceneID ?? 0);
            long sceneId = Positive(sceneData?.SceneID ?? 0);
            if (mapSceneId == currentMapSceneId && sceneId == currentSceneId) return;

            StopStayRoutine();

            bool mapChanged = mapSceneId != currentMapSceneId;

            // 退出先小后大：离开小场景才谈得上离开大场景
            if (currentSceneId > 0 && sceneId != currentSceneId) QuestEventBus.ReportExitZone(currentMapSceneId, currentSceneId);
            if (currentMapSceneId > 0 && mapChanged) QuestEventBus.ReportExitZone(currentMapSceneId, 0);

            currentMapSceneId = mapSceneId;
            currentSceneId = sceneId;
            if (mapChanged) mapStayElapsed = 0f;

            // 进入先大后小，顺序和退出相反
            if (currentMapSceneId > 0 && mapChanged) QuestEventBus.ReportEnterZone(currentMapSceneId, 0);
            if (currentSceneId > 0) QuestEventBus.ReportEnterZone(currentMapSceneId, currentSceneId);

            if (currentMapSceneId <= 0) return;
            if (scanner.HasPendingStayTrigger(currentMapSceneId, currentSceneId))
            {
                stayRoutine = host.StartCoroutine(StayLoop(currentMapSceneId, currentSceneId));
            }
        }

        static long Positive(long id) => id > 0 ? id : 0;

        /// <summary>大场景和小场景各报一路停留：整秒才报，秒数没变就跳过。</summary>
        IEnumerator StayLoop(long mapSceneId, long sceneId)
        {
            WaitForSeconds wait = new(CheckInterval);
            int mapReported = Mathf.FloorToInt(mapStayElapsed);
            float sceneElapsed = 0f;
            int sceneReported = 0;

            while (true)
            {
                yield return wait;
                mapStayElapsed += CheckInterval;
                sceneElapsed += CheckInterval;

                int mapSeconds = Mathf.FloorToInt(mapStayElapsed);
                if (mapSeconds != mapReported)
                {
                    mapReported = mapSeconds;
                    QuestEventBus.ReportZoneStay(mapSceneId, 0, mapSeconds);
                }

                int sceneSeconds = Mathf.FloorToInt(sceneElapsed);
                if (sceneId > 0 && sceneSeconds != sceneReported)
                {
                    sceneReported = sceneSeconds;
                    QuestEventBus.ReportZoneStay(mapSceneId, sceneId, sceneSeconds);
                }

                if (!scanner.HasPendingStayTrigger(mapSceneId, sceneId)) break; // 该领的都领了，收工
            }
            stayRoutine = null;
        }

        void StopStayRoutine()
        {
            if (stayRoutine == null) return;
            host.StopCoroutine(stayRoutine);
            stayRoutine = null;
        }
    }
}
