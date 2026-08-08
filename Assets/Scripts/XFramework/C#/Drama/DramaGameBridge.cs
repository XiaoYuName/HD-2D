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
    }
}
