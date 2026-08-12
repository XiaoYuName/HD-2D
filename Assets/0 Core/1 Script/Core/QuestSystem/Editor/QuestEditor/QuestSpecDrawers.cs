using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using XFramework;

static class QuestDrawerUI
{
    const string StylePath = "Assets/0 Core/1 Script/Core/QuestSystem/Editor/QuestEditor/QuestEditorWindow.uss";

    public static VisualElement Root()
    {
        VisualElement root = new();
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (styleSheet != null) root.styleSheets.Add(styleSheet);
        return root;
    }

    /// <summary>添加并立即绑定可动态重建的普通参数字段。</summary>
    public static void Add(VisualElement root, SerializedProperty property, string relativeName, string label)
    {
        SerializedProperty relative = property.FindPropertyRelative(relativeName);
        PropertyField field = new(relative, label);
        root.Add(field);
        // 参数类型切换后字段是动态加入的，必须显式绑定才能立即生成实际输入控件。
        field.Bind(relative.serializedObject);
    }

    public static void AddReference(VisualElement root, SerializedProperty property, string relativeName,
        string label, QuestReferenceKind kind, bool allowNone = false)
    {
        SerializedProperty reference = property.FindPropertyRelative(relativeName);
        QuestDatabaseData database =
            AssetDatabase.LoadAssetAtPath<QuestDatabaseData>(QuestLubanMigration.DatabaseAssetPath);

        if (kind == QuestReferenceKind.Item)
        {
            QuestItemSearchField search = new(label);
            search.Bind(reference.longValue, database, allowNone, value =>
            {
                Undo.RecordObject(reference.serializedObject.targetObject, $"修改{label}");
                reference.serializedObject.Update();
                reference.longValue = value;
                reference.serializedObject.ApplyModifiedProperties();
            });
            root.Add(search);
            return;
        }

        QuestReferenceDropdown dropdown = new() { label = label };
        dropdown.Bind(reference.longValue, kind, database, allowNone, value =>
        {
            Undo.RecordObject(reference.serializedObject.targetObject, $"修改{label}");
            reference.serializedObject.Update();
            reference.longValue = value;
            reference.serializedObject.ApplyModifiedProperties();
        });
        root.Add(dropdown);
    }

    public static void AddObjectiveType(VisualElement root, SerializedProperty property, string label,
        System.Action changed = null)
    {
        QuestObjectiveTypeDropdown dropdown = new() { label = label };
        dropdown.Bind((QuestObjType)property.intValue,
            value => SetEnum(property, label, (int)value, changed));
        root.Add(dropdown);
    }

    public static void AddTriggerType(VisualElement root, SerializedProperty property, string label,
        System.Action changed = null)
    {
        QuestTriggerTypeDropdown dropdown = new() { label = label };
        dropdown.Bind((QuestTriggerType)property.intValue,
            value => SetEnum(property, label, (int)value, changed));
        root.Add(dropdown);
    }

    public static void AddRewardType(VisualElement root, SerializedProperty property, string label,
        System.Action changed = null)
    {
        QuestRewardTypeDropdown dropdown = new() { label = label };
        dropdown.Bind((QuestRewardType)property.intValue,
            value => SetEnum(property, label, (int)value, changed));
        root.Add(dropdown);
    }

    static void SetEnum(SerializedProperty property, string label, int value, System.Action changed)
    {
        Undo.RecordObject(property.serializedObject.targetObject, $"修改{label}");
        property.serializedObject.Update();
        property.intValue = value;
        property.serializedObject.ApplyModifiedProperties();
        changed?.Invoke();
    }

    public static VisualElement Body()
    {
        VisualElement body = new();
        body.AddToClassList("quest-drawer-body");
        return body;
    }
}

[CustomPropertyDrawer(typeof(QuestTriggerSpec))]
public class QuestTriggerSpecDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        SerializedProperty type = property.FindPropertyRelative("type");
        VisualElement body = QuestDrawerUI.Body();
        QuestDrawerUI.AddTriggerType(root, type, "触发类型", Build);
        root.Add(body);

        void Build()
        {
            body.Clear();
            switch ((QuestTriggerType)type.enumValueIndex)
            {
                case QuestTriggerType.EnterZone:
                case QuestTriggerType.ExitZone:
                    QuestDrawerUI.AddReference(body, property, "mapSceneId", "大场景", QuestReferenceKind.MapScene);
                    QuestDrawerUI.AddReference(body, property, "sceneId", "小场景", QuestReferenceKind.Scene, true);
                    break;
                case QuestTriggerType.EnterZoneStay:
                    QuestDrawerUI.AddReference(body, property, "mapSceneId", "大场景", QuestReferenceKind.MapScene);
                    QuestDrawerUI.AddReference(body, property, "sceneId", "小场景", QuestReferenceKind.Scene, true);
                    QuestDrawerUI.Add(body, property, "staySeconds", "停留秒数");
                    break;
                case QuestTriggerType.ClickNpc:
                case QuestTriggerType.DialogNpc:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    break;
                case QuestTriggerType.MiniGameEnd:
                    QuestDrawerUI.Add(body, property, "gameType", "小游戏");
                    break;
                case QuestTriggerType.MiniGameResult:
                    QuestDrawerUI.Add(body, property, "gameType", "小游戏");
                    QuestDrawerUI.Add(body, property, "gameResult", "结果");
                    break;
                case QuestTriggerType.RandomChance:
                    QuestDrawerUI.Add(body, property, "permille", "触发概率（千分比）");
                    break;
                case QuestTriggerType.PlotEnd:
                    QuestDrawerUI.Add(body, property, "plotId", "剧情 ID");
                    body.Add(new HelpBox("剧情完成记录尚未实现，该触发当前不会命中。", HelpBoxMessageType.Warning));
                    break;
                case QuestTriggerType.None:
                    body.Add(new HelpBox("None 只检查接受条件；Auto 会在被动重扫时检查。", HelpBoxMessageType.Info));
                    break;
            }
        }

        Build();
        root.TrackPropertyValue(type, _ => Build());
        return root;
    }
}

[CustomPropertyDrawer(typeof(QuestObjectiveSpec))]
public class QuestObjectiveSpecDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        SerializedProperty type = property.FindPropertyRelative("type");
        VisualElement body = QuestDrawerUI.Body();

        void Build()
        {
            body.Clear();
            switch ((QuestObjType)type.enumValueIndex)
            {
                case QuestObjType.DayPassed:
                    QuestDrawerUI.Add(body, property, "count", "需要天数");
                    break;
                case QuestObjType.Dialog:
                    QuestDrawerUI.AddReference(body, property, "dialogueId", "对话", QuestReferenceKind.Dialogue);
                    break;
                case QuestObjType.CompleteQuest:
                    QuestDrawerUI.AddReference(body, property, "questId", "任务", QuestReferenceKind.Quest);
                    break;
                case QuestObjType.HoldItem:
                    QuestDrawerUI.AddReference(body, property, "itemId", "道具", QuestReferenceKind.Item);
                    QuestDrawerUI.Add(body, property, "count", "持有数量");
                    break;
                case QuestObjType.CharacterProp:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    QuestDrawerUI.Add(body, property, "characterPropType", "角色属性");
                    QuestDrawerUI.Add(body, property, "value", "需要数值");
                    break;
                case QuestObjType.CompleteGame:
                    QuestDrawerUI.Add(body, property, "gameType", "小游戏");
                    QuestDrawerUI.Add(body, property, "gameResult", "限定结果（None=不限）");
                    QuestDrawerUI.Add(body, property, "count", "完成局数");
                    break;
                case QuestObjType.DialogNpc:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    QuestDrawerUI.Add(body, property, "count", "对话次数");
                    break;
                case QuestObjType.DialogNpcWithItem:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    QuestDrawerUI.AddReference(body, property, "itemId", "消耗道具", QuestReferenceKind.Item);
                    QuestDrawerUI.Add(body, property, "count", "对话次数");
                    break;
                case QuestObjType.GiveGift:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    QuestDrawerUI.AddReference(body, property, "itemId", "礼物（0=任意）", QuestReferenceKind.Item, true);
                    QuestDrawerUI.Add(body, property, "count", "赠礼次数");
                    break;
                case QuestObjType.BuyItem:
                    QuestDrawerUI.AddReference(body, property, "itemId", "道具（0=任意）", QuestReferenceKind.Item, true);
                    QuestDrawerUI.Add(body, property, "count", "购买件数");
                    break;
                default:
                    body.Add(new HelpBox("请选择有效的任务目标类型。", HelpBoxMessageType.Error));
                    break;
            }
        }

        QuestDrawerUI.AddObjectiveType(root, type, "目标类型", Build);
        root.Add(body);
        Build();
        root.TrackPropertyValue(type, _ => Build());
        return root;
    }
}

[CustomPropertyDrawer(typeof(QuestRewardSpec))]
public class QuestRewardSpecDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        SerializedProperty type = property.FindPropertyRelative("type");
        VisualElement body = QuestDrawerUI.Body();
        QuestDrawerUI.AddRewardType(root, type, "奖励类型", Build);
        root.Add(body);

        void Build()
        {
            body.Clear();
            switch ((QuestRewardType)type.enumValueIndex)
            {
                case QuestRewardType.Item:
                    QuestDrawerUI.AddReference(body, property, "itemId", "道具", QuestReferenceKind.Item);
                    QuestDrawerUI.Add(body, property, "amount", "数量");
                    break;
                case QuestRewardType.Coin:
                case QuestRewardType.GameCoin:
                    QuestDrawerUI.Add(body, property, "amount", "数值");
                    break;
                case QuestRewardType.Affection:
                    QuestDrawerUI.AddReference(body, property, "npcId", "NPC", QuestReferenceKind.Npc);
                    QuestDrawerUI.Add(body, property, "amount", "好感度");
                    break;
                default:
                    body.Add(new HelpBox("请选择有效的奖励类型。", HelpBoxMessageType.Error));
                    break;
            }
        }

        Build();
        root.TrackPropertyValue(type, _ => Build());
        return root;
    }
}

[CustomPropertyDrawer(typeof(QuestItemRequirement))]
public class QuestItemRequirementDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        QuestDrawerUI.AddReference(root, property, "itemId", "道具", QuestReferenceKind.Item);
        QuestDrawerUI.Add(root, property, "count", "持有数量");
        return root;
    }
}

[CustomPropertyDrawer(typeof(QuestCharacterRequirement))]
public class QuestCharacterRequirementDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        QuestDrawerUI.AddReference(root, property, "npcId", "NPC", QuestReferenceKind.Npc);
        QuestDrawerUI.Add(root, property, "propType", "角色属性");
        QuestDrawerUI.Add(root, property, "value", "需要数值");
        return root;
    }
}

[CustomPropertyDrawer(typeof(QuestObjectiveDefinition))]
public class QuestObjectiveDefinitionDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        VisualElement root = QuestDrawerUI.Root();
        QuestDrawerUI.Add(root, property, "enabled", "启用 SO 配置");
        QuestDrawerUI.Add(root, property, "id", "ID");
        QuestDrawerUI.Add(root, property, "remark", "备注");
        QuestDrawerUI.Add(root, property, "desc", "目标描述");
        QuestDrawerUI.Add(root, property, "objective", "主目标");
        QuestDrawerUI.Add(root, property, "rewards", "目标奖励");
        SerializedProperty hasExtra = property.FindPropertyRelative("hasExtra");
        root.Add(new PropertyField(hasExtra, "启用超额目标"));

        VisualElement extra = QuestDrawerUI.Body();
        root.Add(extra);
        void Build()
        {
            extra.Clear();
            if (!hasExtra.boolValue) return;
            QuestDrawerUI.Add(extra, property, "extraDesc", "超额描述");
            QuestDrawerUI.Add(extra, property, "extraObjective", "超额目标");
            QuestDrawerUI.Add(extra, property, "extraRewards", "超额奖励");
        }

        Build();
        root.TrackPropertyValue(hasExtra, _ => Build());
        return root;
    }
}
