using System.Collections;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 把场景切换翻译成进出区域 / 区域停留三种触发事件。从 QuestManager 抽出来，
    /// 那边只管建一个、开一个、读档时清一次。
    ///
    /// 停留协程只在「当前场景确实有还没领、且靠停留触发的任务」时才起，领完就自己收工。
    /// </summary>
    public class QuestZoneTracker
    {
        /// <summary>停留检查间隔，秒。</summary>
        const float CheckInterval = 0.5f;

        readonly MonoBehaviour host;
        readonly QuestAcceptScanner scanner;

        long currentSceneId;
        Coroutine stayRoutine;

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

        /// <summary>读档用：忘掉当前场景，下一次场景回调重新走一遍进入流程。</summary>
        public void Reset()
        {
            StopStayRoutine();
            currentSceneId = 0;
        }

        void OnSceneChanged(SceneData sceneData)
        {
            long newSceneId = sceneData?.SceneID ?? 0;
            if (newSceneId == currentSceneId) return;

            StopStayRoutine();
            if (currentSceneId > 0) QuestEventBus.ReportExitZone(currentSceneId);

            currentSceneId = newSceneId;
            if (currentSceneId <= 0) return;

            QuestEventBus.ReportEnterZone(currentSceneId);
            if (scanner.HasPendingStayTrigger(currentSceneId)) stayRoutine = host.StartCoroutine(StayLoop(currentSceneId));
        }

        IEnumerator StayLoop(long sceneId)
        {
            WaitForSeconds wait = new(CheckInterval);
            float elapsed = 0f;
            int reported = 0;

            while (true)
            {
                yield return wait;
                elapsed += CheckInterval;

                int seconds = Mathf.FloorToInt(elapsed);
                if (seconds == reported) continue;
                reported = seconds;

                QuestEventBus.ReportZoneStay(sceneId, seconds);
                if (!scanner.HasPendingStayTrigger(sceneId)) break; // 该领的都领了，收工
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
