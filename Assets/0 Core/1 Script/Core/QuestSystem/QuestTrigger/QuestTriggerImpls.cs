using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 被动触发：重扫时无条件命中，能不能领全看接受条件。
    /// 任务没配任何触发时也按这一条走（见 <see cref="QuestData.Triggers"/>）。
    /// </summary>
    [QuestTypeInfo("被动：重扫时只看接受条件")]
    public class AutoQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.Auto;

        public bool Validate(string owner) => true;

        public bool IsHit(long id, int param) => true;
    }

    /// <summary>按事件主体 ID 匹配的触发，绝大多数触发都是这种。</summary>
    public abstract class IdQuestTrigger : IQuestTrigger
    {
        public abstract QuestTriggerType Type { get; }

        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] protected long targetId;

        public virtual bool Validate(string owner)
            => QuestConfigValidator.CheckCharacter(targetId, owner + $" 的 {Type} 触发");

        public virtual bool IsHit(long id, int param) => id == targetId;
    }

    /// <summary>
    /// 按区域匹配的触发。场景是两级的，所以配置也写两级，小场景留 0 = 只认大场景本身
    /// （从大地图进这个大场景时算一次，在里面换小场景不再算）。
    ///
    /// 匹配是严格相等的：<see cref="QuestZoneTracker"/> 会把「进出大场景」和「进出小场景」
    /// 分成两次事件上报，大场景那次的小场景ID 就是 0。
    /// </summary>
    public abstract class ZoneQuestTrigger : IQuestTrigger
    {
        public abstract QuestTriggerType Type { get; }

        [SerializeField, QuestLabel("大场景"), QuestRef(QuestRefKind.MapScene)] long mapSceneId;
        [SerializeField, QuestLabel("小场景（0=不限）"), QuestRef(QuestRefKind.Scene)] long sceneId;

        public long MapSceneId => mapSceneId;

        /// <summary>0 = 不限小场景。</summary>
        public long SceneId => sceneId;

        /// <summary>区域事件走 <see cref="IsHitZone"/>，这条只为满足接口。</summary>
        public bool IsHit(long id, int param) => false;

        public virtual bool IsHitZone(QuestZoneArgs zone)
            => zone.MapSceneId == mapSceneId && zone.SceneId == sceneId;

        /// <summary>ID 填错（写成小场景ID当大场景用、小场景不在这个大场景下）就永远不会触发，启动时当场报出来。</summary>
        public virtual bool Validate(string owner)
        {
            WordMapSceneData mapData = LubanManager.Instance.TbWordMapSceneData.GetOrDefault(mapSceneId);
            if (mapData == null)
            {
                Debug.LogError($"[Quest] {owner} 的 {Type} 触发：大场景 {mapSceneId} 不在 WordMapSceneData 里");
                return false;
            }

            if (sceneId > 0 && !mapData.SubScenes.Contains(sceneId))
            {
                Debug.LogError($"[Quest] {owner} 的 {Type} 触发：小场景 {sceneId} 不属于大场景 {mapSceneId}"
                    + $"（它的子场景是 {string.Join(",", mapData.SubScenes)}）");
                return false;
            }
            return true;
        }
    }

    [QuestTypeInfo("进入某场景时")]
    public class EnterZoneQuestTrigger : ZoneQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZone;
    }

    [QuestTypeInfo("离开某场景时")]
    public class ExitZoneQuestTrigger : ZoneQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.ExitZone;
    }

    [QuestTypeInfo("在某场景停够若干秒（小场景填 0 = 在整个大场景里累计停留）")]
    public class EnterZoneStayQuestTrigger : ZoneQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.EnterZoneStay;

        /// <summary>需要停留的秒数，<see cref="QuestZoneTracker"/> 起停留协程时也要读。</summary>
        [SerializeField, QuestLabel("停留秒数"), QuestMin(1)] int needSeconds = 1;

        public int NeedSeconds => needSeconds;

        public override bool IsHitZone(QuestZoneArgs zone) => base.IsHitZone(zone) && zone.StaySeconds >= needSeconds;
    }

    [QuestTypeInfo("点击某 NPC 时")]
    public class ClickNpcQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.ClickNpc;
    }

    [QuestTypeInfo("与某 NPC 对话时")]
    public class DialogNpcQuestTrigger : IdQuestTrigger
    {
        public override QuestTriggerType Type => QuestTriggerType.DialogNpc;
    }

    [QuestTypeInfo("某小游戏结算时，不看胜负")]
    public class MiniGameEndQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.MiniGameEnd;

        [SerializeField, QuestLabel("小游戏")] MiniGameType gameType;

        public bool Validate(string owner) => QuestConfigValidator.CheckMiniGame(gameType, owner);

        public bool IsHit(long id, int param) => id == (long)gameType;
    }

    [QuestTypeInfo("某小游戏以指定结果结算时（结果填 None = 不限胜负）")]
    public class MiniGameResultQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.MiniGameResult;

        [SerializeField, QuestLabel("小游戏")] MiniGameType gameType;
        [SerializeField, QuestLabel("结果（None=不限）")] MiniGameResult needResult;

        public bool Validate(string owner) => QuestConfigValidator.CheckMiniGame(gameType, owner);

        public bool IsHit(long id, int param)
            => id == (long)gameType && (needResult == MiniGameResult.None || param == (int)needResult);
    }

    [QuestTypeInfo("每次重扫掷一次点，按千分比命中")]
    public class RandomChanceQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.RandomChance;

        [SerializeField, QuestLabel("触发概率（千分比）"), QuestMin(1)] int permille = 1000;

        public bool Validate(string owner)
            => QuestConfigValidator.CheckPositive(permille, QuestFieldName.Permille, owner);

        public bool IsHit(long id, int param) => Random.Range(0, 1000) < permille;
    }

    /// <summary>剧情完成记录尚未实现，配了也永不命中。</summary>
    [QuestTypeInfo("某段剧情结束时 —— 剧情完成记录尚未实现，当前不会命中")]
    public class PlotEndQuestTrigger : IQuestTrigger
    {
        public QuestTriggerType Type => QuestTriggerType.PlotEnd;

        [SerializeField, QuestLabel("剧情 ID")] long plotId;

        public bool Validate(string owner)
        {
            Debug.LogWarning($"[Quest] {owner} 用了 PlotEnd 触发（剧情 {plotId}），剧情完成记录尚未实现，不会触发");
            return true;
        }

        public bool IsHit(long id, int param) => false;
    }
}
