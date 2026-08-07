using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Localization;

namespace XFramework
{
    #region 状态型：订阅对应的变化事件，进度每次现算，条件回退进度也回退，不进存档

    /// <summary><c>DayPassed:天数</c></summary>
    public class DayPassedQuestObj : StateQuestObj
    {
        [JsonIgnore] int needDay;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.DayPassed;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            needDay = config.GetInt(0, 1);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckPositive(needDay, "天数", config);

        public override void SubsEvents()
            => GameDataManager.Instance.RegisterPlayerDataDayChange(OnDayChanged);

        public override void UnsubsEvents()
            => GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);

        void OnDayChanged(PlayerData _) => NotifyChanged();

        protected override int Evaluate() => GameDataManager.Instance.PlayerData.Day >= needDay ? 1 : 0;

        // 天数不是「要几个」而是「到第几天」，Value 换成目标天数
        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.Value, needDay, false);
    }

    /// <summary><c>Dialog:对话ID</c></summary>
    public class DialogQuestObj : StateQuestObj
    {
        [JsonIgnore] long dialogueId;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.Dialog;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            dialogueId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckId(dialogueId, "对话ID", config);

        public override void SubsEvents() => QuestEventBus.DialogueFinished += OnDialogueFinished;
        public override void UnsubsEvents() => QuestEventBus.DialogueFinished -= OnDialogueFinished;

        void OnDialogueFinished(long finishedId)
        {
            if (finishedId == dialogueId) NotifyChanged();
        }

        protected override int Evaluate() => DramaManager.Instance.HasDialogue(dialogueId) ? 1 : 0;
    }

    /// <summary><c>CompleteQuest:任务ID</c></summary>
    public class CompleteQuestQuestObj : StateQuestObj
    {
        [JsonIgnore] long targetQuestId;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.CompleteQuest;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            targetQuestId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckQuest(targetQuestId, config);

        public override void SubsEvents() => QuestEventBus.QuestCompleted += OnQuestCompleted;
        public override void UnsubsEvents() => QuestEventBus.QuestCompleted -= OnQuestCompleted;

        void OnQuestCompleted(long completedId)
        {
            if (completedId == targetQuestId) NotifyChanged();
        }

        protected override int Evaluate() => QuestManager.Instance.IsQuestCompleted(targetQuestId) ? 1 : 0;

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.QuestName, QuestManager.Instance.GetQuestData(targetQuestId).Name, false);
    }

    /// <summary><c>HoldItem:道具ID[:数量]</c> —— 卖掉会掉回去。</summary>
    public class HoldItemQuestObj : StateQuestObj
    {
        [JsonIgnore] long itemId;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.HoldItem;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            itemId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckItem(itemId, config);

        public override void SubsEvents()
            => InventoryManager.Instance.RegisterItemIDChangeCallBack(itemId, OnItemChanged, false);

        public override void UnsubsEvents()
            => InventoryManager.Instance.UnregisterItemIDChangeCallBack(itemId, OnItemChanged);

        void OnItemChanged(List<ItemInfo> _) => NotifyChanged();

        protected override int Evaluate() => InventoryManager.Instance.GetItemCount(itemId);

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
    }

    /// <summary><c>NpcProp:NPC ID:数值[:属性类型]</c>（属性类型不写默认好感）</summary>
    public class NpcPropQuestObj : StateQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] CharacterPropType propType;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.NpcProp;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
            npcId = config.GetLong(0, 0);
            need = Mathf.Max(1, config.GetInt(1, 1));
            propType = config.GetEnum(2, CharacterPropType.Goodwill);
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckCharacter(npcId, config);

        public override void SubsEvents()
            => CharacterManager.Instance.RegisterCharacterBagChange(npcId, OnCharacterChanged, false);

        public override void UnsubsEvents()
            => CharacterManager.Instance.UnregisterCharacterBagChange(npcId, OnCharacterChanged);

        void OnCharacterChanged(CharacterBag _) => NotifyChanged();

        protected override int Evaluate()
            => CharacterManager.Instance.GetCharacterBag(npcId).GetPropertyValue(propType);

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            desc.SetVar(QuestLocVar.PropName, QuestLocText.Get(QuestLocKey.Prop.Of(propType)), false);
        }
    }

    #endregion

    #region 累计型：只订自己那一种事件，只增不减，次数进存档

    /// <summary><c>CompleteGame:小游戏ID:局数[:结果]</c>（结果 0 不限 / 1 胜 / 2 负，不写为 0）</summary>
    public class CompleteGameQuestObj : CountQuestObj
    {
        [JsonIgnore] long gameId;
        [JsonIgnore] int needResult;

        [JsonIgnore] protected override string DescKey => needResult switch
        {
            1 => QuestLocKey.Obj.CompleteGameWin,
            2 => QuestLocKey.Obj.CompleteGameLose,
            _ => QuestLocKey.Obj.CompleteGame,
        };

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
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

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.DialogNpc;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
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

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
    }

    /// <summary>
    /// <c>DialogNpcWithItem:NPC ID:道具ID[:次数]</c>
    /// —— 携带道具与 NPC 对话，每次消耗 1 个；道具不够就不算数也不扣。
    /// </summary>
    public class DialogNpcWithItemQuestObj : CountQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] long itemId;

        [JsonIgnore] protected override string DescKey => QuestLocKey.Obj.DialogNpcWithItem;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
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

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }
    }

    /// <summary><c>GiveGift:NPC ID:礼物ID[:次数]</c>（礼物ID 填 0 = 任意礼物）</summary>
    public class GiveGiftQuestObj : CountQuestObj
    {
        [JsonIgnore] long npcId;
        [JsonIgnore] long itemId;

        [JsonIgnore] protected override string DescKey
            => itemId > 0 ? QuestLocKey.Obj.GiveGift : QuestLocKey.Obj.GiveGiftAny;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
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

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            if (itemId > 0) desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }
    }

    /// <summary><c>BuyItem:道具ID[:件数]</c>（道具ID 填 0 = 任意道具）</summary>
    public class BuyItemQuestObj : CountQuestObj
    {
        [JsonIgnore] long itemId;

        [JsonIgnore] protected override string DescKey
            => itemId > 0 ? QuestLocKey.Obj.BuyItem : QuestLocKey.Obj.BuyItemAny;

        public override void Init(QuestArgs config)
        {
            QuestId = config.QuestId;
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

        protected override void SetDescVars(LocalizedString desc)
        {
            if (itemId > 0) desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }
    }

    #endregion
}
