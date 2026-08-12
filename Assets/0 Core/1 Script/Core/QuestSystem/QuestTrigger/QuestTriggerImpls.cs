using UnityEngine;

namespace XFramework
{
    /// <summary>不配触发（只看领取条件）与 <c>Auto</c>：被动触发，重扫时无条件命中。</summary>
    public class PassiveQuestTrigger : IQuestTrigger
    {
        public PassiveQuestTrigger(QuestTriggerType type) => Type = type;

        public QuestTriggerType Type { get; }

        public void Init(QuestArgs config) { }
        public void Init(QuestTriggerSpec config, QuestArgs context) { }

        public bool IsHit(long id, int param) => true;
    }

    /// <summary>按事件主体 ID 匹配的触发，绝大多数触发都是这种。</summary>
    public abstract class IdQuestTrigger : IQuestTrigger
    {
        public abstract QuestTriggerType Type { get; }

        protected long TargetId;

        public virtual void Init(QuestArgs config) => TargetId = config.GetLong(0, 0);
        public virtual void Init(QuestTriggerSpec config, QuestArgs context) => TargetId = config.npcId;

        public virtual bool IsHit(long id, int param) => id == TargetId;
    }

    /// <summary>
    /// 按区域匹配的触发。场景是两级的，所以配置也写两级：<c>大场景ID:小场景ID</c>，
    /// 小场景留空/写 0 = 只认大场景本身（从大地图进这个大场景时算一次，在里面换小场景不再算）。
    ///
    /// 匹配是严格相等的：<see cref="QuestZoneTracker"/> 会把「进出大场景」和「进出小场景」
    /// 分成两次事件上报，大场景那次的小场景ID 就是 0。
    /// </summary>
    public abstract class ZoneQuestTrigger : IQuestTrigger
    {
        public abstract QuestTriggerType Type { get; }

        public long MapSceneId { get; private set; }

        /// <summary>0 = 不限小场景。</summary>
        public long SceneId { get; private set; }

        public virtual void Init(QuestArgs config)
        {
            MapSceneId = config.GetLong(0, 0);
            SceneId = config.GetLong(1, 0);
            Validate(config);
        }

        public virtual void Init(QuestTriggerSpec config, QuestArgs context)
        {
            MapSceneId = config.mapSceneId;
            SceneId = config.sceneId;
            Validate(context);
        }

        /// <summary>区域事件走 <see cref="IsHitZone"/>，这条只为满足接口。</summary>
        public bool IsHit(long id, int param) => false;

        public virtual bool IsHitZone(QuestZoneArgs zone)
            => zone.MapSceneId == MapSceneId && zone.SceneId == SceneId;

        /// <summary>ID 填错（写成小场景ID当大场景用、小场景不在这个大场景下）就永远不会触发，启动时当场报出来。</summary>
        void Validate(QuestArgs config)
        {
            WordMapSceneData mapData = LubanManager.Instance.TbWordMapSceneData.GetOrDefault(MapSceneId);
            if (mapData == null)
            {
                Debug.LogError($"[Quest] {config.Owner} 的 \"{config.Raw}\" 大场景 {MapSceneId} 不在 WordMapSceneData 里，正确写法: {Type}:大场景ID:小场景ID");
                return;
            }

            if (SceneId > 0 && !mapData.SubScenes.Contains(SceneId))
            {
                Debug.LogError($"[Quest] {config.Owner} 的 \"{config.Raw}\" 小场景 {SceneId} 不属于大场景 {MapSceneId}（它的子场景是 {string.Join(",", mapData.SubScenes)}）");
            }
        }
    }

    /// <summary><c>EnterZone:大场景ID:小场景ID</c>，小场景可省略。</summary>
    public class EnterZoneQuestTrigger : ZoneQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZone;
    }

    /// <summary><c>ExitZone:大场景ID:小场景ID</c>，小场景可省略。</summary>
    public class ExitZoneQuestTrigger : ZoneQuestTrigger
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

    /// <summary><c>MiniGameEnd:小游戏类型</c>，不看胜负。</summary>
    public class MiniGameEndQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.MiniGameEnd;

        public override void Init(QuestArgs config) => TargetId = (long)config.GetEnum(0, MiniGameType.None);
        public override void Init(QuestTriggerSpec config, QuestArgs context) => TargetId = (long)config.gameType;
    }

    /// <summary>
    /// <c>EnterZoneStay:大场景ID:小场景ID:停留秒数</c>，停够了才命中。
    /// 小场景写 0 = 在整个大场景里累计停留（中途换小场景不重新计时）。
    /// </summary>
    public class EnterZoneStayQuestTrigger : ZoneQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZoneStay;

        /// <summary>需要停留的秒数，<see cref="QuestZoneTracker"/> 起停留协程时也要读。</summary>
        public int NeedSeconds { get; private set; }

        public override void Init(QuestArgs config)
        {
            base.Init(config);
            NeedSeconds = config.GetInt(2, 1);
        }

        public override void Init(QuestTriggerSpec config, QuestArgs context)
        {
            base.Init(config, context);
            NeedSeconds = config.staySeconds;
        }

        public override bool IsHitZone(QuestZoneArgs zone) => base.IsHitZone(zone) && zone.StaySeconds >= NeedSeconds;
    }

    /// <summary><c>MiniGameResult:小游戏类型:结果</c>（<c>Win</c> / <c>Lose</c>）</summary>
    public class MiniGameResultQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.MiniGameResult;

        int needResult;

        public override void Init(QuestArgs config)
        {
            TargetId = (long)config.GetEnum(0, MiniGameType.None);
            needResult = (int)config.GetEnum(1, MiniGameResult.None);
        }

        public override void Init(QuestTriggerSpec config, QuestArgs context)
        {
            TargetId = (long)config.gameType;
            needResult = (int)config.gameResult;
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
        public void Init(QuestTriggerSpec config, QuestArgs context) => permille = config.permille;

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

        public override void Init(QuestTriggerSpec config, QuestArgs context)
        {
            TargetId = config.plotId;
            Debug.LogWarning($"[Quest] {context.Owner} 用了 PlotEnd 触发，剧情完成记录尚未实现，不会触发");
        }

        public override bool IsHit(long id, int param) => false;
    }
}
