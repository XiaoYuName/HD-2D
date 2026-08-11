using UnityEngine;

namespace XFramework
{
    /// <summary>不配触发（只看领取条件）与 <c>Auto</c>：被动触发，重扫时无条件命中。</summary>
    public class PassiveQuestTrigger : IQuestTrigger
    {
        public PassiveQuestTrigger(QuestTriggerType type) => Type = type;

        public QuestTriggerType Type { get; }

        public void Init(QuestArgs config) { }

        public bool IsHit(long id, int param) => true;
    }

    /// <summary>按事件主体 ID 匹配的触发，绝大多数触发都是这种。</summary>
    public abstract class IdQuestTrigger : IQuestTrigger
    {
        public abstract QuestTriggerType Type { get; }

        protected long TargetId;

        public virtual void Init(QuestArgs config) => TargetId = config.GetLong(0, 0);

        public virtual bool IsHit(long id, int param) => id == TargetId;
    }

    /// <summary><c>EnterZone:场景ID</c></summary>
    public class EnterZoneQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZone;
    }

    /// <summary><c>ExitZone:场景ID</c></summary>
    public class ExitZoneQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.ExitZone;
    }

    /// <summary><c>ClickNpc:NPC ID</c></summary>
    public class ClickNpcQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.ClickNpc;
    }

    /// <summary><c>DialogNpc:NPC ID</c></summary>
    public class DialogNpcQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.DialogNpc;
    }

    /// <summary><c>MiniGameEnd:小游戏ID</c>，不看胜负。</summary>
    public class MiniGameEndQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.MiniGameEnd;
    }

    /// <summary><c>EnterZoneStay:场景ID:停留秒数</c>，停够了才命中。</summary>
    public class EnterZoneStayQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZoneStay;

        /// <summary>需要停留的秒数，<see cref="QuestManager"/> 起停留协程时也要读。</summary>
        public int NeedSeconds { get; private set; }

        public long SceneId => TargetId;

        public override void Init(QuestArgs config)
        {
            base.Init(config);
            NeedSeconds = config.GetInt(1, 1);
        }

        public override bool IsHit(long id, int param) => id == TargetId && param >= NeedSeconds;
    }

    /// <summary><c>MiniGameResult:小游戏ID:结果</c>（1 胜 / 2 负）</summary>
    public class MiniGameResultQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.MiniGameResult;

        int needResult;

        public override void Init(QuestArgs config)
        {
            base.Init(config);
            needResult = config.GetInt(1, 0);
        }

        public override bool IsHit(long id, int param)
            => id == TargetId && (needResult == 0 || param == needResult);
    }

    /// <summary><c>RandomChance:千分比</c>，每次重扫掷一次点。</summary>
    public class RandomChanceQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.RandomChance;

        int permille;

        public void Init(QuestArgs config) => permille = config.GetInt(0, 0);

        public bool IsHit(long id, int param) => Random.Range(0, 1000) < permille;
    }

    /// <summary><c>PlotEnd:剧情ID</c> —— 剧情完成记录尚未实现，永不命中。</summary>
    public class PlotEndQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.PlotEnd;

        public override void Init(QuestArgs config)
        {
            base.Init(config);
            Debug.LogWarning($"[Quest] {config.Owner} 用了 PlotEnd 触发，剧情完成记录尚未实现，不会触发");
        }

        public override bool IsHit(long id, int param) => false;
    }
}
