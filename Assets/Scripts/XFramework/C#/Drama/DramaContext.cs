using System.Collections.Generic;
using Drama.Runtime.Flow;
using Drama.Runtime.Services;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaContext"/> 的本工程实现。
    ///
    /// 就是个服务容器——Handler 碰外部世界全部经过它。
    /// <b>不要往这里塞逻辑</b>，它存在的意义就是让 Drama 包不认识本工程任何类型。
    /// 装配在 <see cref="DramaDirector"/> 的构造函数里。
    /// </summary>
    public sealed class DramaContext : IDramaContext
    {
        /// <summary>播放模式。Director 每段开播前按当前设置（自动播放 / 跳过）刷一次。</summary>
        public EDramaPlaybackMode Mode { get; set; }

        public IDialogueView       Dialogue     { get; set; }
        public IChoiceView         Choice       { get; set; }
        public IActorStage         Actors       { get; set; }
        public IDramaScreen        Screen       { get; set; }
        public IDramaBackground    Background   { get; set; }
        public IDramaLocalization  Localization { get; set; }
        public IDramaAssetProvider Assets       { get; set; }
        public IDramaAudio         Audio        { get; set; }
        public IDramaGameBridge    Game         { get; set; }

        // ==================================================== 选项路径（存档用）

        /// <summary>本剧本里玩家已经做过的选择，按执行顺序。存档存它，读档喂回去。</summary>
        readonly List<int> pickedChoices = new List<int>();

        /// <summary>读档恢复时待消费的记录。</summary>
        readonly Queue<int> restoredChoices = new Queue<int>();

        public IReadOnlyList<int> PickedChoices => pickedChoices;

        /// <summary>
        /// 换一本剧本时重置。
        ///
        /// <b>路径是按剧本算的，不是按整条剧情链算的</b> —— 恢复只重放当前这一本，
        /// 跳转之前那些本子里的选择跟这次重放无关，留着只会错位。
        /// </summary>
        public void ResetChoicePath(IReadOnlyList<int> restoreFrom = null)
        {
            pickedChoices.Clear();
            restoredChoices.Clear();

            if (restoreFrom == null) return;

            for (int i = 0; i < restoreFrom.Count; i++)
            {
                restoredChoices.Enqueue(restoreFrom[i]);
            }
        }

        public bool TryTakeRestoredChoice(out int optionIndex)
        {
            if (restoredChoices.Count == 0)
            {
                optionIndex = -1;
                return false;
            }

            optionIndex = restoredChoices.Dequeue();

            // 取走的同时也要记下来：恢复完玩家可能马上又存一次档，
            // 不补这一句的话前面这些选择就从新存档里消失了
            pickedChoices.Add(optionIndex);
            return true;
        }

        public void ReportChoicePicked(int optionIndex)
        {
            pickedChoices.Add(optionIndex);
        }
    }
}
