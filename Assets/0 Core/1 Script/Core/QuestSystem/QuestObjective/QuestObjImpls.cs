using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    #region 状态型：订阅对应的变化事件，随事件重算，条件回退进度也回退，不进存档

    /// <summary><c>DayPassed:天数</c></summary>
    public class DayPassedQuestObj : StateQuestObj
    {
        [JsonIgnore] int needDay;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.DayPassed);
            needDay = config.GetInt(0, 1);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckPositive(needDay, "天数", config);

        protected override void Subscribe()
            => GameDataManager.Instance.RegisterPlayerDataDayChange(OnDayChanged);

        public override void UnsubsEvents()
            => GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);

        void OnDayChanged(PlayerData _) => Recalc();

        protected override int Evaluate() => GameDataManager.Instance.PlayerData.Day >= needDay ? 1 : 0;
    }

    /// <summary><c>Dialog:对话ID</c></summary>
    public class DialogQuestObj : StateQuestObj
    {
        [JsonIgnore] long dialogueId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.Dialog);
            dialogueId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckId(dialogueId, "对话ID", config);

        protected override void Subscribe() => QuestEventBus.DialogueFinished += OnDialogueFinished;

        public override void UnsubsEvents() => QuestEventBus.DialogueFinished -= OnDialogueFinished;

        void OnDialogueFinished(long finishedId)
        {
            if (finishedId == dialogueId) Recalc();
        }

        protected override int Evaluate() => DramaManager.Instance.HasDialogue(dialogueId) ? 1 : 0;
    }

    /// <summary><c>CompleteQuest:任务ID</c></summary>
    public class CompleteQuestQuestObj : StateQuestObj
    {
        [JsonIgnore] long targetQuestId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.CompleteQuest);
            targetQuestId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckQuest(targetQuestId, config);

        protected override void Subscribe() => QuestEventBus.QuestCompleted += OnQuestCompleted;

        public override void UnsubsEvents() => QuestEventBus.QuestCompleted -= OnQuestCompleted;

        void OnQuestCompleted(long completedId)
        {
            if (completedId == targetQuestId) Recalc();
        }

        protected override int Evaluate() => QuestManager.Instance.IsQuestCompleted(targetQuestId) ? 1 : 0;
    }

    /// <summary><c>HoldItem:道具ID[:数量]</c> —— 卖掉会掉回去。</summary>
    public class HoldItemQuestObj : StateQuestObj
    {
        [JsonIgnore] long itemId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.HoldItem);
            itemId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckItem(itemId, config);

        protected override void Subscribe()
            => InventoryManager.Instance.RegisterItemIDChangeCallBack(itemId, OnItemChanged, false);

        public override void UnsubsEvents()
            => InventoryManager.Instance.UnregisterItemIDChangeCallBack(itemId, OnItemChanged);

        void OnItemChanged(List<ItemInfo> _) => Recalc();

        protected override int Evaluate() => InventoryManager.Instance.GetItemCount(itemId);
    }

    /// <summary><c>NpcProp:NPC ID:数值[:属性类型]</c>（属性类型不写默认好感）</summary>
    public class NpcPropQuestObj : StateQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] CharacterPropType propType;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(2, QuestObjUsage.NpcProp);
            npcId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
            propType = config.GetEnum(2, CharacterPropType.Goodwill);
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckCharacter(npcId, config);

        protected override void Subscribe()
            => CharacterManager.Instance.RegisterCharacterBagChange(npcId, OnCharacterChanged, false);

        public override void UnsubsEvents()
            => CharacterManager.Instance.UnregisterCharacterBagChange(npcId, OnCharacterChanged);

        void OnCharacterChanged(CharacterBag _) => Recalc();

        protected override int Evaluate()
        {
            CharacterBag bag = CharacterManager.Instance.GetCharacterBag(npcId);
            return bag == null ? 0 : bag.GetPropertyValue(propType);
        }
    }

    #endregion

    #region 累计型：只订自己那一种事件，只增不减，次数进存档

    /// <summary><c>CompleteGame:小游戏ID:局数[:结果]</c>（结果 0 不限 / 1 胜 / 2 负，不写为 0）</summary>
    public class CompleteGameQuestObj : CountQuestObj
    {
        [JsonIgnore] long gameId;
        [JsonIgnore] int needResult;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(2, QuestObjUsage.CompleteGame);
            gameId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
            needResult = config.GetInt(2, 0);
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckId(gameId, "小游戏ID", config);

        public override void SubsEvents() => QuestEventBus.MiniGameFinished += OnMiniGameFinished;
        public override void UnsubsEvents() => QuestEventBus.MiniGameFinished -= OnMiniGameFinished;

        void OnMiniGameFinished(long finishedGameId, int result)
        {
            if (finishedGameId != gameId) return;
            if (needResult != 0 && result != needResult) return;
            Advance(1);
        }
    }

    /// <summary><c>DialogNpc:NPC ID[:次数]</c></summary>
    public class DialogNpcQuestObj : CountQuestObj
    {
        [JsonIgnore] long npcId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.DialogNpc);
            npcId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckCharacter(npcId, config);

        public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
        public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

        void OnNpcTalked(long talkedNpcId)
        {
            if (talkedNpcId == npcId) Advance(1);
        }
    }

    /// <summary>
    /// <c>DialogNpcWithItem:NPC ID:道具ID[:次数]</c>
    /// —— 携带道具与 NPC 对话，每次消耗 1 个；道具不够就不算数也不扣。
    /// </summary>
    public class DialogNpcWithItemQuestObj : CountQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] long itemId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(2, QuestObjUsage.DialogNpcWithItem);
            npcId = config.GetLong(0, 0);
            itemId = config.GetLong(1, 0);
            need = Mathf.Max(1, config.GetInt(2, 1));
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckCharacter(npcId, config)
             & QuestConfigValidator.CheckItem(itemId, config);

        public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
        public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

        void OnNpcTalked(long talkedNpcId)
        {
            if (talkedNpcId != npcId || IsComplete) return;
            if (!InventoryManager.Instance.ConsumeItem(itemId, 1)) return;
            Advance(1);
        }
    }

    /// <summary><c>GiveGift:NPC ID:礼物ID[:次数]</c>（礼物ID 填 0 = 任意礼物）</summary>
    public class GiveGiftQuestObj : CountQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] long itemId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(2, QuestObjUsage.GiveGift);
            npcId = config.GetLong(0, 0);
            itemId = config.GetLong(1, 0);
            need = Mathf.Max(1, config.GetInt(2, 1));
        }

        public override bool Validate(QuestArgs config)
        {
            bool ok = QuestConfigValidator.CheckCharacter(npcId, config);
            if (itemId > 0) ok &= QuestConfigValidator.CheckItem(itemId, config);
            return ok;
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
        [JsonIgnore] long itemId;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            config.Require(1, QuestObjUsage.BuyItem);
            itemId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config)
            => itemId <= 0 || QuestConfigValidator.CheckItem(itemId, config);

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
