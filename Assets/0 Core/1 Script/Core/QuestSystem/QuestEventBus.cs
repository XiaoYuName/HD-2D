using System;

namespace XFramework
{
    /// <summary>
    /// 任务系统的事件源。每种事件一个带真实签名的委托，订阅者只会收到自己关心的那一种，
    /// 不需要在回调里判类型、也不需要一个万能载荷结构体。
    ///
    /// 业务系统在关键点调 <c>ReportXxx</c> 一行即可，任务系统不反向侵入其它系统。
    /// 场景进出/停留由 <see cref="QuestManager"/> 自己监听 GameSceneManager 上报，业务侧不用管；
    /// 天数、道具数量、角色好感这些**已有各自的变化回调**（GameDataManager / InventoryManager / CharacterManager），
    /// 任务目标直接订那边，不用在这里再转一手。
    /// </summary>
    public static class QuestEventBus
    {
        // 区域事件都带「大场景ID + 小场景ID」两级：两张表的 ID 区间是重叠的（10001 既是大场景商业街、
        // 又是小场景公寓卧室），只传一个 ID 分不清是哪一级。小场景ID 传 0 表示这次说的是大场景本身。

        /// <summary>进入区域。参数：大场景ID、小场景ID（0 = 进的是大场景本身）</summary>
        public static event Action<long, long> EnterZone;
        /// <summary>离开区域。参数：大场景ID、小场景ID（0 = 离开的是大场景本身）</summary>
        public static event Action<long, long> ExitZone;
        /// <summary>在区域中停留。参数：大场景ID、小场景ID（0 = 大场景本身）、已停留秒数</summary>
        public static event Action<long, long, int> ZoneStay;
        /// <summary>点击 NPC。参数：NPC ID</summary>
        public static event Action<long> NpcClicked;
        /// <summary>与 NPC 对话。参数：NPC ID</summary>
        public static event Action<long> NpcTalked;
        /// <summary>一段对话播完。参数：对话ID</summary>
        public static event Action<long> DialogueFinished;
        /// <summary>小游戏结算。参数：小游戏类型、本局结果</summary>
        public static event Action<MiniGameType, MiniGameResult> MiniGameFinished;
        /// <summary>送礼成功。参数：NPC ID、礼物道具ID、数量</summary>
        public static event Action<long, long, int> GiftGiven;
        /// <summary>购买道具成功。参数：道具ID、件数</summary>
        public static event Action<long, int> ItemBought;
        /// <summary>任务交付完成。参数：任务ID。由 <see cref="QuestManager"/> 自己上报。</summary>
        public static event Action<long> QuestCompleted;

        public static void ReportEnterZone(long mapSceneId, long sceneId) => EnterZone?.Invoke(mapSceneId, sceneId);
        public static void ReportExitZone(long mapSceneId, long sceneId) => ExitZone?.Invoke(mapSceneId, sceneId);
        public static void ReportZoneStay(long mapSceneId, long sceneId, int seconds) => ZoneStay?.Invoke(mapSceneId, sceneId, seconds);
        public static void ReportNpcClicked(long npcId) => NpcClicked?.Invoke(npcId);
        public static void ReportNpcTalked(long npcId) => NpcTalked?.Invoke(npcId);
        public static void ReportDialogueFinished(long dialogueId) => DialogueFinished?.Invoke(dialogueId);
        /// <summary>小游戏结算上报。<b>每个小游戏在自己的结算入口调一行</b>，不分胜负的传 <see cref="MiniGameResult.None"/>。</summary>
        public static void ReportMiniGameFinished(MiniGameType game, MiniGameResult result) => MiniGameFinished?.Invoke(game, result);

        /// <summary>有胜负的小游戏用这个重载，省得调用方自己转枚举。</summary>
        public static void ReportMiniGameFinished(MiniGameType game, bool win)
            => ReportMiniGameFinished(game, win ? MiniGameResult.Win : MiniGameResult.Lose);
        public static void ReportGiftGiven(long npcId, long itemId, int count) => GiftGiven?.Invoke(npcId, itemId, count);
        public static void ReportItemBought(long itemId, int count) => ItemBought?.Invoke(itemId, count);
        public static void ReportQuestCompleted(long questId) => QuestCompleted?.Invoke(questId);
    }
}
