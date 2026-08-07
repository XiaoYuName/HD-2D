using System;

namespace XFramework
{
    /// <summary>
    /// 任务系统的事件源。每种事件一个带真实签名的委托，订阅者只会收到自己关心的那一种，
    /// 不需要在回调里判类型、也不需要一个万能载荷结构体。
    ///
    /// 业务系统在关键点调 <c>ReportXxx</c> 一行即可，任务系统不反向侵入其它系统。
    /// 场景进出/停留由 <see cref="QuestManager"/> 自己监听 GameSceneManager 上报，业务侧不用管。
    /// </summary>
    public static class QuestEventBus
    {
        /// <summary>进入场景。参数：场景ID</summary>
        public static event Action<long> EnterZone;
        /// <summary>离开场景。参数：场景ID</summary>
        public static event Action<long> ExitZone;
        /// <summary>在场景中停留。参数：场景ID、已停留秒数</summary>
        public static event Action<long, int> ZoneStay;
        /// <summary>点击 NPC。参数：NPC ID</summary>
        public static event Action<long> NpcClicked;
        /// <summary>与 NPC 对话。参数：NPC ID</summary>
        public static event Action<long> NpcTalked;
        /// <summary>一段对话播完。参数：对话ID</summary>
        public static event Action<long> DialogueFinished;
        /// <summary>小游戏结算。参数：游戏ID、结果（0 不分胜负 / 1 胜 / 2 负）</summary>
        public static event Action<long, int> GameFinished;
        /// <summary>送礼成功。参数：NPC ID、礼物道具ID、数量</summary>
        public static event Action<long, long, int> GiftGiven;
        /// <summary>购买道具成功。参数：道具ID、件数</summary>
        public static event Action<long, int> ItemBought;

        public static void ReportEnterZone(long sceneId) => EnterZone?.Invoke(sceneId);
        public static void ReportExitZone(long sceneId) => ExitZone?.Invoke(sceneId);
        public static void ReportZoneStay(long sceneId, int seconds) => ZoneStay?.Invoke(sceneId, seconds);
        public static void ReportNpcClicked(long npcId) => NpcClicked?.Invoke(npcId);
        public static void ReportNpcTalked(long npcId) => NpcTalked?.Invoke(npcId);
        public static void ReportDialogueFinished(long dialogueId) => DialogueFinished?.Invoke(dialogueId);
        public static void ReportGameFinished(long gameId, int result = 0) => GameFinished?.Invoke(gameId, result);
        public static void ReportGiftGiven(long npcId, long itemId, int count = 1) => GiftGiven?.Invoke(npcId, itemId, count);
        public static void ReportItemBought(long itemId, int count = 1) => ItemBought?.Invoke(itemId, count);
    }
}
