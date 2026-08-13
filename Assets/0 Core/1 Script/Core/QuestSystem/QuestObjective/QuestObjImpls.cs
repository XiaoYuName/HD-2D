using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace XFramework
{
    // 每种目标 = 一个静态数据类 ＋ 一个嵌套的 Info（运行时实例）。
    // Info 嵌在里面是为了能直接读外层的私有参数，回调因此都是普通命名方法，不需要闭包。
    // 加一种目标 = 写一个子类，Inspector 的类型下拉里自动就有了，不用再登记工厂。

    #region 成没成型：判定是布尔，现问世界，所以「领任务前就已经满足」也算达成

    [QuestTypeInfo("做过某段对话（领任务前就聊过也算）")]
    public class DialogObjData : FlagObjData
    {
        [SerializeField, QuestLabel("对话"), QuestRef(QuestRefKind.Dialogue)]
        long dialogueId;

        public override bool Validate(string owner)
            => QuestConfigValidator.CheckId(dialogueId, QuestFieldName.DialogueId, owner);

        public override bool IsMet() => true;

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

    [QuestTypeInfo("完成另一个任务")]
    public class CompleteQuestObjData : FlagObjData
    {
        [SerializeField, QuestLabel("任务"), QuestRef(QuestRefKind.Quest)] long targetQuestId;

        public override bool Validate(string owner) => QuestConfigValidator.CheckQuest(targetQuestId, owner);

        public override bool IsMet() => QuestManager.Instance.IsQuestCompleted(targetQuestId);

        protected override void SetDescVars(LocVars vars)
            => vars.Set(QuestLocVar.QuestName, QuestManager.Instance.GetQuestData(targetQuestId).Name);

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

    [QuestTypeInfo("到第几天（天数本身就是进度，界面能显示 1/2）")]
    public class DayPassedObjData : AmountObjData
    {
        [SerializeField, QuestLabel("需要天数"), QuestMin(1)] int needDay = 1;

        public override int Need => needDay;

        public override bool Validate(string owner)
            => QuestConfigValidator.CheckPositive(needDay, QuestFieldName.Day, owner);

        public override int GetAmount() => GameDataManager.Instance.PlayerData.Day;

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : AmountObjInfo
        {
            public override void SubsEvents()
                => GameDataManager.Instance.RegisterPlayerDataDayChange(OnDayChanged);

            public override void UnsubsEvents()
            {
                if (GameDataManager.IsInitialized)
                    GameDataManager.Instance.UnregisterPlayerDataDayChange(OnDayChanged);
            }

            void OnDayChanged(PlayerData _) => NotifyChanged();
        }
    }

    [QuestTypeInfo("持有若干个某道具 —— 卖掉会掉回去")]
    public class HoldItemObjData : AmountObjData
    {
        [SerializeField, QuestLabel("道具"), QuestRef(QuestRefKind.Item)] long itemId;
        [SerializeField, QuestLabel("持有数量"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner) => QuestConfigValidator.CheckItem(itemId, owner);

        public override int GetAmount() => InventoryManager.Instance.GetItemCount(itemId);

        protected override void SetDescVars(LocVars vars)
            => vars.Set(QuestLocVar.ItemName, QuestLocText.ItemName(itemId));

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

    [QuestTypeInfo("某 NPC 的某项属性（好感度…）达到数值")]
    public class CharacterPropObjData : AmountObjData
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] long npcId;
        [SerializeField, QuestLabel("角色属性")] CharacterPropType propType = CharacterPropType.Goodwill;
        [SerializeField, QuestLabel("需要数值"), QuestMin(1)] int needValue = 1;

        public override int Need => needValue;

        public override bool Validate(string owner) => QuestConfigValidator.CheckCharacter(npcId, owner);

        public override int GetAmount()
            => CharacterManager.Instance.GetCharacterBag(npcId).GetPropertyValue(propType);

        protected override void SetDescVars(LocVars vars)
        {
            vars.Set(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId));
            vars.Set(QuestLocVar.PropName, QuestLocText.Get(QuestLocKey.Prop.Of(propType)));
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

    [QuestTypeInfo("完成若干局某小游戏，可限定胜负（结果填 None = 不限）")]
    public class CompleteGameObjData : CountObjData
    {
        [SerializeField, QuestLabel("小游戏")] MiniGameType gameType;
        [SerializeField, QuestLabel("限定结果（None=不限）")] MiniGameResult needResult;
        [SerializeField, QuestLabel("完成局数"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner) => QuestConfigValidator.CheckMiniGame(gameType, owner);

        public override QuestObjInfoBase CreateInfo() => new Info();

        public class Info : CountObjInfo
        {
            CompleteGameObjData ObjData => (CompleteGameObjData)Data;

            public override void SubsEvents() => QuestEventBus.MiniGameFinished += OnMiniGameFinished;
            public override void UnsubsEvents() => QuestEventBus.MiniGameFinished -= OnMiniGameFinished;

            void OnMiniGameFinished(MiniGameType finishedGame, MiniGameResult result)
            {
                CompleteGameObjData data = ObjData;
                if (finishedGame != data.gameType) return;
                if (data.needResult != MiniGameResult.None && result != data.needResult) return;

                Advance(1);
            }
        }
    }

    [QuestTypeInfo("和某 NPC 对话若干次")]
    public class DialogNpcObjData : CountObjData
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] long npcId;
        [SerializeField, QuestLabel("对话次数"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner) => QuestConfigValidator.CheckCharacter(npcId, owner);

        protected override void SetDescVars(LocVars vars)
            => vars.Set(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId));

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

    [QuestTypeInfo("携带道具与 NPC 对话，每次消耗 1 个；道具不够就不算数也不扣")]
    public class DialogNpcWithItemObjData : CountObjData
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] long npcId;
        [SerializeField, QuestLabel("消耗道具"), QuestRef(QuestRefKind.Item)] long itemId;
        [SerializeField, QuestLabel("对话次数"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner)
            => QuestConfigValidator.CheckCharacter(npcId, owner)
             & QuestConfigValidator.CheckItem(itemId, owner);

        protected override void SetDescVars(LocVars vars)
        {
            vars.Set(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId));
            vars.Set(QuestLocVar.ItemName, QuestLocText.ItemName(itemId));
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

    [QuestTypeInfo("给某 NPC 送礼若干次（礼物留 0 = 任意礼物）")]
    public class GiveGiftObjData : CountObjData
    {
        [SerializeField, QuestLabel("NPC"), QuestRef(QuestRefKind.Npc)] long npcId;
        [SerializeField, QuestLabel("礼物（0=任意）"), QuestRef(QuestRefKind.Item)] long itemId;
        [SerializeField, QuestLabel("赠礼次数"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner)
        {
            bool ok = QuestConfigValidator.CheckCharacter(npcId, owner);
            if (itemId > 0) ok &= QuestConfigValidator.CheckItem(itemId, owner);
            return ok;
        }

        protected override void SetDescVars(LocVars vars)
        {
            vars.Set(QuestLocVar.CharacterName, QuestLocText.CharacterName(npcId));
            if (itemId > 0) vars.Set(QuestLocVar.ItemName, QuestLocText.ItemName(itemId));
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

    [QuestTypeInfo("购买若干件道具（道具留 0 = 任意道具）")]
    public class BuyItemObjData : CountObjData
    {
        [SerializeField, QuestLabel("道具（0=任意）"), QuestRef(QuestRefKind.Item)] long itemId;
        [SerializeField, QuestLabel("购买件数"), QuestMin(1)] int needCount = 1;

        public override int Need => needCount;

        public override bool Validate(string owner) => itemId <= 0 || QuestConfigValidator.CheckItem(itemId, owner);

        protected override void SetDescVars(LocVars vars)
        {
            if (itemId > 0) vars.Set(QuestLocVar.ItemName, QuestLocText.ItemName(itemId));
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
