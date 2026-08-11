using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace XFramework
{
    // 每种目标 = 一个静态数据类 ＋ 一个嵌套的 Info（运行时实例）。
    // Info 嵌在里面是为了能直接读外层的私有参数，回调因此都是普通命名方法，不需要闭包。

    #region 成没成型：判定是布尔，现问世界，所以「领任务前就已经满足」也算达成

    /// <summary><c>Dialog:对话ID</c></summary>
    public class DialogObjData : FlagObjData
    {
        long dialogueId;

        public override void Init(QuestArgs config)
        {
            dialogueId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckId(dialogueId, QuestFieldName.DialogueId, config);

        public override bool IsMet() => DramaManager.Instance.HasDialogue(dialogueId);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : FlagObjInfo
        {
            DialogObjData ObjData => (DialogObjData)Data;

            public override void SubsEvents() => QuestEventBus.DialogueFinished += OnDialogueFinished;
            public override void UnsubsEvents() => QuestEventBus.DialogueFinished -= OnDialogueFinished;

            void OnDialogueFinished(long finishedId)
            {
                if (finishedId == ObjData.dialogueId) NotifyChanged();
            }
        }
    }

    /// <summary><c>CompleteQuest:任务ID</c></summary>
    public class CompleteQuestObjData : FlagObjData
    {
        long targetQuestId;

        public override void Init(QuestArgs config)
        {
            targetQuestId = config.GetLong(0, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckQuest(targetQuestId, config);

        public override bool IsMet() => QuestManager.Instance.IsQuestCompleted(targetQuestId);

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.QuestName, QuestManager.Instance.GetQuestData(targetQuestId).Name, false);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : FlagObjInfo
        {
            CompleteQuestObjData ObjData => (CompleteQuestObjData)Data;

            public override void SubsEvents() => QuestEventBus.QuestCompleted += OnQuestCompleted;
            public override void UnsubsEvents() => QuestEventBus.QuestCompleted -= OnQuestCompleted;

            void OnQuestCompleted(long completedId)
            {
                if (completedId == ObjData.targetQuestId) NotifyChanged();
            }
        }
    }

    #endregion

    #region 现在有多少型：量现问世界，卖掉道具、好感掉了都会跟着回退

    /// <summary><c>DayPassed:天数</c> —— 天数本身就是进度，所以归在这一族，界面能显示「1/2」。</summary>
    public class DayPassedObjData : AmountObjData
    {
        int needDay;

        public override int Need => needDay;

        public override void Init(QuestArgs config)
        {
            needDay = Mathf.Max(1, config.GetInt(0, 1));
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckPositive(needDay, QuestFieldName.Day, config);

        public override int GetAmount() => GameDataManager.Instance.PlayerData.Day;

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : AmountObjInfo
        {
            public override void SubsEvents()
                => GameDataManager.Instance.RegisterPlayerDataDayChange(OnDayChanged);

            public override void UnsubsEvents()
                => GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);

            void OnDayChanged(PlayerData _) => NotifyChanged();
        }
    }

    /// <summary><c>HoldItem:道具ID[:数量]</c> —— 卖掉会掉回去。</summary>
    public class HoldItemObjData : AmountObjData
    {
        long itemId;
        int needCount = 1;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            itemId = config.GetLong(0, 0);
            needCount = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckItem(itemId, config);

        public override int GetAmount() => InventoryManager.Instance.GetItemCount(itemId);

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : AmountObjInfo
        {
            HoldItemObjData ObjData => (HoldItemObjData)Data;

            public override void SubsEvents()
                => InventoryManager.Instance.RegisterItemIDChangeCallBack(ObjData.itemId, OnItemChanged, false);

            public override void UnsubsEvents()
                => InventoryManager.Instance.UnregisterItemIDChangeCallBack(ObjData.itemId, OnItemChanged);

            void OnItemChanged(List<ItemInfo> _) => NotifyChanged();
        }
    }

    /// <summary><c>CharacterProp:NPC ID:数值[:属性类型]</c>（属性类型不写默认好感）</summary>
    public class CharacterPropObjData : AmountObjData
    {
        long npcId;
        int needValue = 1;
        CharacterPropType propType;

        public override int Need => needValue;

        public override void Init(QuestArgs config)
        {
            npcId = config.GetLong(0, 0);
            needValue = Mathf.Max(1, config.GetInt(1, 1));
            propType = config.GetEnum(2, CharacterPropType.Goodwill);
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckCharacter(npcId, config);

        public override int GetAmount()
            => CharacterManager.Instance.GetCharacterBag(npcId).GetPropertyValue(propType);

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            desc.SetVar(QuestLocVar.PropName, QuestLocText.Get(QuestLocKey.Prop.Of(propType)), false);
        }

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : AmountObjInfo
        {
            CharacterPropObjData ObjData => (CharacterPropObjData)Data;

            public override void SubsEvents()
                => CharacterManager.Instance.RegisterCharacterBagChange(ObjData.npcId, OnCharacterChanged, false);

            public override void UnsubsEvents()
                => CharacterManager.Instance.UnregisterCharacterBagChange(ObjData.npcId, OnCharacterChanged);

            void OnCharacterChanged(CharacterBag _) => NotifyChanged();
        }
    }

    #endregion

    #region 累计几次型：只订自己那一种事件，只增不减，次数进存档

    /// <summary><c>CompleteGame:小游戏ID:局数[:结果]</c>（结果 0 不限 / 1 胜 / 2 负，不写为 0）</summary>
    public class CompleteGameObjData : CountObjData
    {
        long gameId;
        int needCount = 1;
        int needResult;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            gameId = config.GetLong(0, 0);
            needCount = Mathf.Max(1, config.GetInt(1, 1));
            needResult = config.GetInt(2, 0);
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckId(gameId, QuestFieldName.GameId, config);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            CompleteGameObjData ObjData => (CompleteGameObjData)Data;

            public override void SubsEvents() => QuestEventBus.MiniGameFinished += OnMiniGameFinished;
            public override void UnsubsEvents() => QuestEventBus.MiniGameFinished -= OnMiniGameFinished;

            void OnMiniGameFinished(long finishedGameId, int result)
            {
                CompleteGameObjData data = ObjData;
                if (finishedGameId != data.gameId) return;
                if (data.needResult != 0 && result != data.needResult) return;

                Advance(1);
            }
        }
    }

    /// <summary><c>DialogNpc:NPC ID[:次数]</c></summary>
    public class DialogNpcObjData : CountObjData
    {
        long npcId;
        int needCount = 1;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            npcId = config.GetLong(0, 0);
            needCount = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config) => QuestConfigValidator.CheckCharacter(npcId, config);

        protected override void SetDescVars(LocalizedString desc)
            => desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            DialogNpcObjData ObjData => (DialogNpcObjData)Data;

            public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
            public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

            void OnNpcTalked(long talkedNpcId)
            {
                if (talkedNpcId == ObjData.npcId) Advance(1);
            }
        }
    }

    /// <summary>
    /// <c>DialogNpcWithItem:NPC ID:道具ID[:次数]</c>
    /// —— 携带道具与 NPC 对话，每次消耗 1 个；道具不够就不算数也不扣。
    /// </summary>
    public class DialogNpcWithItemObjData : CountObjData
    {
        long npcId;
        long itemId;
        int needCount = 1;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            npcId = config.GetLong(0, 0);
            itemId = config.GetLong(1, 0);
            needCount = Mathf.Max(1, config.GetInt(2, 1));
        }

        public override bool Validate(QuestArgs config)
            => QuestConfigValidator.CheckCharacter(npcId, config)
             & QuestConfigValidator.CheckItem(itemId, config);

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            DialogNpcWithItemObjData ObjData => (DialogNpcWithItemObjData)Data;

            public override void SubsEvents() => QuestEventBus.NpcTalked += OnNpcTalked;
            public override void UnsubsEvents() => QuestEventBus.NpcTalked -= OnNpcTalked;

            void OnNpcTalked(long talkedNpcId)
            {
                DialogNpcWithItemObjData data = ObjData;
                if (talkedNpcId != data.npcId) return;

                // 道具不够就当这次对话没发生：不扣、不推进
                if (!InventoryManager.Instance.ConsumeItem(data.itemId, 1)) return;

                Advance(1);
            }
        }
    }

    /// <summary><c>GiveGift:NPC ID:礼物ID[:次数]</c>（礼物ID 填 0 = 任意礼物）</summary>
    public class GiveGiftObjData : CountObjData
    {
        long npcId;
        long itemId;
        int needCount = 1;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            npcId = config.GetLong(0, 0);
            itemId = config.GetLong(1, 0);
            needCount = Mathf.Max(1, config.GetInt(2, 1));
        }

        public override bool Validate(QuestArgs config)
        {
            bool ok = QuestConfigValidator.CheckCharacter(npcId, config);
            if (itemId > 0) ok &= QuestConfigValidator.CheckItem(itemId, config);
            return ok;
        }

        protected override void SetDescVars(LocalizedString desc)
        {
            desc.SetVar(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId), false);
            if (itemId > 0) desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            GiveGiftObjData ObjData => (GiveGiftObjData)Data;

            public override void SubsEvents() => QuestEventBus.GiftGiven += OnGiftGiven;
            public override void UnsubsEvents() => QuestEventBus.GiftGiven -= OnGiftGiven;

            void OnGiftGiven(long givenNpcId, long givenItemId, int count)
            {
                GiveGiftObjData data = ObjData;
                if (givenNpcId != data.npcId) return;
                if (data.itemId > 0 && givenItemId != data.itemId) return;

                Advance(Mathf.Max(1, count));
            }
        }
    }

    /// <summary><c>BuyItem:道具ID[:件数]</c>（道具ID 填 0 = 任意道具）</summary>
    public class BuyItemObjData : CountObjData
    {
        long itemId;
        int needCount = 1;

        public override int Need => needCount;

        public override void Init(QuestArgs config)
        {
            itemId = config.GetLong(0, 0);
            needCount = Mathf.Max(1, config.GetInt(1, 1));
        }

        public override bool Validate(QuestArgs config)
            => itemId <= 0 || QuestConfigValidator.CheckItem(itemId, config);

        protected override void SetDescVars(LocalizedString desc)
        {
            if (itemId > 0) desc.SetVar(QuestLocVar.ItemName, QuestLocText.ItemName(itemId), false);
        }

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            BuyItemObjData ObjData => (BuyItemObjData)Data;

            public override void SubsEvents() => QuestEventBus.ItemBought += OnItemBought;
            public override void UnsubsEvents() => QuestEventBus.ItemBought -= OnItemBought;

            void OnItemBought(long boughtItemId, int count)
            {
                BuyItemObjData data = ObjData;
                if (data.itemId > 0 && boughtItemId != data.itemId) return;

                Advance(Mathf.Max(1, count));
            }
        }
    }

    #endregion
}
