using UnityEngine;

namespace XFramework
{
    #region 状态型：随时重算，条件回退进度也回退，不订阅事件、不进存档

    /// <summary><c>DayPassed:天数</c></summary>
    public class DayPassedQuestObj : StateQuestObj
    {
        int needDay;

        protected override void OnInit()
        {
            Config.Require(1, "DayPassed:天数");
            needDay = Config.GetInt(0, 1);
        }

        protected override int Evaluate() => GameDataManager.Instance.PlayerData.Day >= needDay ? 1 : 0;
    }

    /// <summary><c>Dialog:对话ID</c></summary>
    public class DialogQuestObj : StateQuestObj
    {
        long dialogueId;

        protected override void OnInit()
        {
            Config.Require(1, "Dialog:对话ID");
            dialogueId = Config.GetLong(0);
        }

        protected override int Evaluate() => DramaManager.Instance.HasDialogue(dialogueId) ? 1 : 0;
    }

    /// <summary><c>CompleteQuest:任务ID</c></summary>
    public class CompleteQuestQuestObj : StateQuestObj
    {
        long targetQuestId;

        protected override void OnInit()
        {
            Config.Require(1, "CompleteQuest:任务ID");
            targetQuestId = Config.GetLong(0);
        }

        protected override int Evaluate() => QuestManager.Instance.IsQuestCompleted(targetQuestId) ? 1 : 0;
    }

    /// <summary><c>HoldItem:道具ID[:数量]</c> —— 卖掉会掉回去。</summary>
    public class HoldItemQuestObj : StateQuestObj
    {
        long itemId;

        protected override void OnInit()
        {
            Config.Require(1, "HoldItem:道具ID[:数量]");
            itemId = Config.GetLong(0);
            need = Mathf.Max(1, Config.GetInt(1, 1));
        }

        protected override int Evaluate() => InventoryManager.Instance.GetItemCount(itemId);
    }

    /// <summary><c>NpcProp:NPC ID:数值[:属性类型]</c>（属性类型不写默认好感）</summary>
    public class NpcPropQuestObj : StateQuestObj
    {
        long npcId;
        CharacterPropType propType;

        protected override void OnInit()
        {
            Config.Require(2, "NpcProp:NPC ID:数值[:属性类型]");
            npcId = Config.GetLong(0);
            need = Mathf.Max(1, Config.GetInt(1, 1));
            propType = Config.GetEnum(2, CharacterPropType.Goodwill);
        }

        protected override int Evaluate()
        {
            CharacterBag bag = CharacterManager.Instance.GetCharacterBag(npcId);
            return bag == null ? 0 : bag.GetPropertyValue(propType);
        }
    }

    #endregion

    #region 累计型：只订自己那一种事件，只增不减，进度进存档

    /// <summary><c>CompleteGame:游戏ID:局数[:结果]</c>（结果 0 不限 / 1 胜 / 2 负，不写为 0）</summary>
    public class CompleteGameQuestObj : CountQuestObj
    {
        long gameId;
        int needResult;

        protected override void OnInit()
        {
            Config.Require(2, "CompleteGame:游戏ID:局数[:结果]");
            gameId = Config.GetLong(0);
            need = Mathf.Max(1, Config.GetInt(1, 1));
            needResult = Config.GetInt(2);
        }

        public override void SubsEvents() => QuestEventBus.GameFinished += OnGameFinished;
        public override void UnsubsEvents() => QuestEventBus.GameFinished -= OnGameFinished;

        void OnGameFinished(long finishedGameId, int result)
        {
            if (finishedGameId != gameId) return;
            if (needResult != 0 && result != needResult) return;
            Advance();
        }
    }

    /// <summary><c>DialogNpc:NPC ID[:次数]</c></summary>
    public class DialogNpcQuestObj : CountQuestObj
    {
        long npcId;

        protected override void OnInit()
        {
            Config.Require(1, "DialogNpc:NPC ID[:次数]");
            npcId = Config.GetLong(0);
            need = Mathf.Max(1, Config.GetInt(1, 1));
        }

        public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
        public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

        void OnNpcTalked(long talkedNpcId)
        {
            if (talkedNpcId == npcId) Advance();
        }
    }

    /// <summary>
    /// <c>DialogNpcWithItem:NPC ID:道具ID[:次数]</c>
    /// —— 携带道具与 NPC 对话，每次消耗 1 个；道具不够就不算数也不扣。
    /// </summary>
    public class DialogNpcWithItemQuestObj : CountQuestObj
    {
        long npcId;
        long itemId;

        protected override void OnInit()
        {
            Config.Require(2, "DialogNpcWithItem:NPC ID:道具ID[:次数]");
            npcId = Config.GetLong(0);
            itemId = Config.GetLong(1);
            need = Mathf.Max(1, Config.GetInt(2, 1));
        }

        public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
        public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

        void OnNpcTalked(long talkedNpcId)
        {
            if (talkedNpcId != npcId || IsComplete) return;
            if (!InventoryManager.Instance.ConsumeItem(itemId, 1)) return;
            Advance();
        }
    }

    /// <summary><c>GiveGift:NPC ID:礼物ID[:次数]</c>（礼物ID 填 0 = 任意礼物）</summary>
    public class GiveGiftQuestObj : CountQuestObj
    {
        long npcId;
        long itemId;

        protected override void OnInit()
        {
            Config.Require(2, "GiveGift:NPC ID:礼物ID[:次数]（礼物ID 填 0 表示任意）");
            npcId = Config.GetLong(0);
            itemId = Config.GetLong(1);
            need = Mathf.Max(1, Config.GetInt(2, 1));
        }

        public override void SubsEvents() => QuestEventBus.GiftGiven += OnGiftGiven;
        public override void UnsubsEvents() => QuestEventBus.GiftGiven -= OnGiftGiven;

        void OnGiftGiven(long givenNpcId, long givenItemId, int count)
        {
            if (givenNpcId != npcId) return;
            if (itemId > 0 && givenItemId != itemId) return;
            Advance(Mathf.Max(1, count));
        }
    }

    /// <summary><c>BuyItem:道具ID[:件数]</c>（道具ID 填 0 = 任意道具）</summary>
    public class BuyItemQuestObj : CountQuestObj
    {
        long itemId;

        protected override void OnInit()
        {
            Config.Require(1, "BuyItem:道具ID[:件数]");
            itemId = Config.GetLong(0);
            need = Mathf.Max(1, Config.GetInt(1, 1));
        }

        public override void SubsEvents() => QuestEventBus.ItemBought += OnItemBought;
        public override void UnsubsEvents() => QuestEventBus.ItemBought -= OnItemBought;

        void OnItemBought(long boughtItemId, int count)
        {
            if (itemId > 0 && boughtItemId != itemId) return;
            Advance(Mathf.Max(1, count));
        }
    }

    #endregion
}
