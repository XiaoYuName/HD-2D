using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using XFramework;

public sealed class QuestDatabaseValidationReport
{
    public readonly List<string> Errors = new();
    public readonly List<string> Warnings = new();
}

/// <summary>任务 SO 数据校验；各枚举类型通过静态策略表注册，新增类型时只需补一条映射。</summary>
public static class QuestDatabaseValidator
{
    delegate void TriggerValidation(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report);
    delegate void ObjectiveValidation(QuestObjectiveSpec data, string owner, HashSet<long> questIds,
        QuestConfigSourceMode sourceMode, QuestDatabaseValidationReport report);
    delegate void RewardValidation(QuestRewardSpec data, string owner, QuestDatabaseValidationReport report);

    static readonly Dictionary<QuestTriggerType, TriggerValidation> TriggerValidators = new()
    {
        { QuestTriggerType.None, ValidateNoTriggerArguments },
        { QuestTriggerType.Auto, ValidateNoTriggerArguments },
        { QuestTriggerType.EnterZone, ValidateSceneTrigger },
        { QuestTriggerType.ExitZone, ValidateSceneTrigger },
        { QuestTriggerType.EnterZoneStay, ValidateSceneStayTrigger },
        { QuestTriggerType.ClickNpc, ValidateNpcTrigger },
        { QuestTriggerType.DialogNpc, ValidateNpcTrigger },
        { QuestTriggerType.MiniGameEnd, ValidateGameTrigger },
        { QuestTriggerType.MiniGameResult, ValidateGameTrigger },
        { QuestTriggerType.RandomChance, ValidateChanceTrigger },
        { QuestTriggerType.PlotEnd, ValidatePlotTrigger },
    };

    static readonly Dictionary<QuestObjType, ObjectiveValidation> ObjectiveValidators = new()
    {
        { QuestObjType.DayPassed, ValidateDayObjective },
        { QuestObjType.Dialog, ValidateDialogObjective },
        { QuestObjType.CompleteQuest, ValidateQuestObjective },
        { QuestObjType.HoldItem, ValidateItemObjective },
        { QuestObjType.CharacterProp, ValidateCharacterObjective },
        { QuestObjType.CompleteGame, ValidateGameObjective },
        { QuestObjType.DialogNpc, ValidateNpcObjective },
        { QuestObjType.DialogNpcWithItem, ValidateNpcItemObjective },
        { QuestObjType.GiveGift, ValidateGiftObjective },
        { QuestObjType.BuyItem, ValidateBuyObjective },
    };

    static readonly Dictionary<QuestRewardType, RewardValidation> RewardValidators = new()
    {
        { QuestRewardType.Item, ValidateItemReward },
        { QuestRewardType.Coin, ValidateNoRewardArguments },
        { QuestRewardType.GameCoin, ValidateNoRewardArguments },
        { QuestRewardType.Affection, ValidateAffectionReward },
    };

    public static QuestDatabaseValidationReport Validate(QuestDatabaseData database)
    {
        QuestDatabaseValidationReport report = new();
        HashSet<long> allQuestIds = Ids(database.Quests.Select(data => data.id), "任务", report);
        HashSet<long> allObjectiveIds = Ids(database.Objectives.Select(data => data.id), "目标", report);
        HashSet<long> allConditionIds = Ids(database.Conditions.Select(data => data.id), "条件", report);
        Ids(database.Categories.Select(data => data.id), "类别", report);

        bool onlyScriptableObject = database.SourceMode == QuestConfigSourceMode.ScriptableObjectOnly;
        HashSet<long> questIds = onlyScriptableObject
            ? database.Quests.Where(data => data.enabled).Select(data => data.id).ToHashSet()
            : allQuestIds;
        HashSet<long> objectiveIds = onlyScriptableObject
            ? database.Objectives.Where(data => data.enabled).Select(data => data.id).ToHashSet()
            : allObjectiveIds;
        HashSet<long> conditionIds = onlyScriptableObject
            ? database.Conditions.Where(data => data.enabled).Select(data => data.id).ToHashSet()
            : allConditionIds;

        foreach (QuestDefinition quest in database.Quests.Where(data => !onlyScriptableObject || data.enabled))
        {
            if (quest.name == null || quest.name.IsEmpty) report.Warnings.Add($"任务 {quest.id} 未配置名称本地化引用");
            if (quest.desc == null || quest.desc.IsEmpty) report.Warnings.Add($"任务 {quest.id} 未配置描述本地化引用");
            ValidateLocalizationPrefix(quest.name, QuestLocKey.Prefix.Quest, $"任务 {quest.id} 名称", report);
            ValidateLocalizationPrefix(quest.desc, QuestLocKey.Prefix.Quest, $"任务 {quest.id} 描述", report);
            if (quest.icon == null || !quest.icon.RuntimeKeyIsValid()) report.Warnings.Add($"任务 {quest.id} 未配置图标");
            if (quest.objectiveIds.Count == 0) report.Warnings.Add($"任务 {quest.id} 没有目标，将在领取后立即完成");
            foreach (QuestTriggerSpec trigger in quest.triggers) ValidateTrigger(trigger, $"任务 {quest.id}", report);
            foreach (long id in quest.objectiveIds)
                CheckReference(objectiveIds, id, $"任务 {quest.id} 引用目标 {id}", database.SourceMode, report);
            if (quest.acceptConditionId > 0)
                CheckReference(conditionIds, quest.acceptConditionId,
                    $"任务 {quest.id} 引用接受条件 {quest.acceptConditionId}", database.SourceMode, report);
            ValidateRewards(quest.rewards, $"任务 {quest.id}", report);
        }

        foreach (QuestObjectiveDefinition objective in database.Objectives
                     .Where(data => !onlyScriptableObject || data.enabled))
        {
            if (objective.desc == null || objective.desc.IsEmpty) report.Warnings.Add($"目标 {objective.id} 未配置描述");
            ValidateLocalizationPrefix(objective.desc, QuestLocKey.Prefix.Objective, $"目标 {objective.id} 描述", report);
            ValidateObjective(objective.objective, $"目标 {objective.id}", questIds, database.SourceMode, report);
            ValidateRewards(objective.rewards, $"目标 {objective.id}", report);
            if (!objective.hasExtra) continue;
            if (objective.extraDesc == null || objective.extraDesc.IsEmpty)
                report.Warnings.Add($"目标 {objective.id} 启用了超额目标但没有描述");
            ValidateLocalizationPrefix(objective.extraDesc, QuestLocKey.Prefix.Objective,
                $"目标 {objective.id} 超额描述", report);
            ValidateObjective(objective.extraObjective, $"目标 {objective.id} 的超额目标",
                questIds, database.SourceMode, report);
            ValidateRewards(objective.extraRewards, $"目标 {objective.id} 的超额目标", report);
        }

        foreach (QuestConditionDefinition condition in database.Conditions
                     .Where(data => !onlyScriptableObject || data.enabled))
        {
            foreach (long id in condition.questPrerequisites)
                CheckReference(questIds, id, $"条件 {condition.id} 引用前置任务 {id}",
                    database.SourceMode, report);
            if (condition.day < 0) report.Errors.Add($"条件 {condition.id} 的天数不能为负数");
            if (condition.items.Any(item => item.itemId <= 0 || item.count <= 0))
                report.Errors.Add($"条件 {condition.id} 有无效的道具持有要求");
            if (condition.characterProps.Any(item => item.npcId <= 0 || item.value <= 0))
                report.Errors.Add($"条件 {condition.id} 有无效的角色属性要求");
        }

        foreach (QuestCategoryDefinition category in database.Categories
                     .Where(data => !onlyScriptableObject || data.enabled))
        {
            ValidateLocalizationPrefix(category.name, QuestLocKey.Prefix.Category, $"类别 {category.id} 名称", report);
            ValidateLocalizationPrefix(category.desc, QuestLocKey.Prefix.Category, $"类别 {category.id} 描述", report);
            foreach (long id in category.questIds)
                CheckReference(questIds, id, $"类别 {category.id} 引用任务 {id}", database.SourceMode, report);
            ValidateRewards(category.rewards, $"类别 {category.id}", report);
        }

        QuestRewardType[] usedRewardTypes = database.Quests
            .Where(data => !onlyScriptableObject || data.enabled).SelectMany(data => data.rewards)
            .Concat(database.Objectives.Where(data => !onlyScriptableObject || data.enabled)
                .SelectMany(data => data.rewards.Concat(data.extraRewards)))
            .Concat(database.Categories.Where(data => !onlyScriptableObject || data.enabled)
                .SelectMany(data => data.rewards))
            .Where(data => data.type != QuestRewardType.Item && data.type != QuestRewardType.None)
            .Select(data => data.type).Distinct().ToArray();
        HashSet<QuestRewardType> presentations = database.RewardPresentations
            .Where(data => !onlyScriptableObject || data.enabled).Select(data => data.type).ToHashSet();
        foreach (QuestRewardType type in usedRewardTypes)
        {
            if (!presentations.Contains(type)) report.Warnings.Add($"奖励类型 {type} 没有 SO 显示配置，将回退 Luban");
        }

        foreach (QuestRewardPresentation presentation in database.RewardPresentations
                     .Where(data => !onlyScriptableObject || data.enabled))
            ValidateLocalizationPrefix(presentation.name, QuestLocKey.Prefix.RewardName,
                $"奖励显示 {presentation.type} 名称", report);

        return report;
    }

    static void ValidateLocalizationPrefix(LocalizedString value, string prefix, string owner,
        QuestDatabaseValidationReport report)
    {
        if (value == null || value.IsEmpty) return;
        string key = QuestLocalizationEditorUtility.GetKey(value);
        if (key.StartsWith(prefix, StringComparison.Ordinal)) return;
        report.Warnings.Add($"{owner}的多语言 Key 应以 {prefix} 开头，当前为 {key}");
    }

    static HashSet<long> Ids(IEnumerable<long> values, string label, QuestDatabaseValidationReport report)
    {
        HashSet<long> result = new();
        foreach (long value in values)
        {
            if (value <= 0) report.Errors.Add($"{label}存在非正数 ID：{value}");
            else if (!result.Add(value)) report.Errors.Add($"{label} ID 重复：{value}");
        }
        return result;
    }

    static void CheckReference(HashSet<long> ids, long id, string message,
        QuestConfigSourceMode sourceMode, QuestDatabaseValidationReport report)
    {
        if (ids.Contains(id)) return;
        if (sourceMode == QuestConfigSourceMode.ScriptableObjectOnly) report.Errors.Add(message + "，SO 中不存在");
        else report.Warnings.Add(message + "，SO 中不存在，将回退 Luban");
    }

    static void ValidateTrigger(QuestTriggerSpec trigger, string owner, QuestDatabaseValidationReport report)
    {
        if (trigger == null)
        {
            report.Errors.Add(owner + " 包含空触发");
            return;
        }

        if (TriggerValidators.TryGetValue(trigger.type, out TriggerValidation validation))
            validation(trigger, owner, report);
        else report.Errors.Add($"{owner} 的触发类型 {trigger.type} 未注册校验策略");
    }

    static void ValidateObjective(QuestObjectiveSpec objective, string owner, HashSet<long> questIds,
        QuestConfigSourceMode sourceMode, QuestDatabaseValidationReport report)
    {
        if (objective == null || objective.type == QuestObjType.None)
        {
            report.Errors.Add(owner + " 未选择有效类型");
            return;
        }

        if (ObjectiveValidators.TryGetValue(objective.type, out ObjectiveValidation validation))
            validation(objective, owner, questIds, sourceMode, report);
        else report.Errors.Add($"{owner} 的目标类型 {objective.type} 未注册校验策略");
    }

    static void ValidateRewards(IEnumerable<QuestRewardSpec> rewards, string owner,
        QuestDatabaseValidationReport report)
    {
        foreach (QuestRewardSpec reward in rewards)
        {
            if (reward == null || reward.type == QuestRewardType.None)
            {
                report.Errors.Add(owner + " 包含无效奖励");
                continue;
            }

            if (reward.amount <= 0) report.Errors.Add($"{owner} 的 {reward.type} 奖励数值必须大于 0");
            if (RewardValidators.TryGetValue(reward.type, out RewardValidation validation))
                validation(reward, owner, report);
            else report.Errors.Add($"{owner} 的奖励类型 {reward.type} 未注册校验策略");
        }
    }

    static void ValidateNoTriggerArguments(QuestTriggerSpec _, string __, QuestDatabaseValidationReport ___) { }

    static void ValidateSceneTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.mapSceneId <= 0) report.Errors.Add(owner + " 的场景触发缺少大场景 ID");
    }

    static void ValidateSceneStayTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.mapSceneId <= 0 || data.staySeconds <= 0) report.Errors.Add(owner + " 的场景停留触发参数无效");
    }

    static void ValidateNpcTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0) report.Errors.Add(owner + " 的 NPC 触发缺少 NPC ID");
    }

    static void ValidateGameTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.gameType == MiniGameType.None) report.Errors.Add(owner + " 的小游戏触发未选择游戏类型");
    }

    static void ValidateChanceTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.permille <= 0 || data.permille > 1000) report.Errors.Add(owner + " 的随机触发概率应为 1~1000");
    }

    static void ValidatePlotTrigger(QuestTriggerSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.plotId <= 0) report.Errors.Add(owner + " 的剧情触发缺少剧情 ID");
    }

    static void ValidateDayObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.count <= 0) report.Errors.Add(owner + " 的天数必须大于 0");
    }

    static void ValidateDialogObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.dialogueId <= 0) report.Errors.Add(owner + " 缺少对话 ID");
    }

    static void ValidateQuestObjective(QuestObjectiveSpec data, string owner, HashSet<long> questIds,
        QuestConfigSourceMode sourceMode, QuestDatabaseValidationReport report)
    {
        if (data.questId <= 0) report.Errors.Add(owner + " 缺少任务 ID");
        else CheckReference(questIds, data.questId, owner + $" 引用任务 {data.questId}", sourceMode, report);
    }

    static void ValidateItemObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.itemId <= 0 || data.count <= 0) report.Errors.Add(owner + " 的道具/数量无效");
    }

    static void ValidateCharacterObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0 || data.value <= 0) report.Errors.Add(owner + " 的角色/属性数值无效");
    }

    static void ValidateGameObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.gameType == MiniGameType.None || data.count <= 0) report.Errors.Add(owner + " 的小游戏/局数无效");
    }

    static void ValidateNpcObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0 || data.count <= 0) report.Errors.Add(owner + " 的 NPC/次数无效");
    }

    static void ValidateNpcItemObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0 || data.itemId <= 0 || data.count <= 0)
            report.Errors.Add(owner + " 的 NPC/道具/次数无效");
    }

    static void ValidateGiftObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0 || data.count <= 0) report.Errors.Add(owner + " 的 NPC/赠礼次数无效");
    }

    static void ValidateBuyObjective(QuestObjectiveSpec data, string owner, HashSet<long> _,
        QuestConfigSourceMode __, QuestDatabaseValidationReport report)
    {
        if (data.count <= 0) report.Errors.Add(owner + " 的购买数量必须大于 0");
    }

    static void ValidateNoRewardArguments(QuestRewardSpec _, string __, QuestDatabaseValidationReport ___) { }

    static void ValidateItemReward(QuestRewardSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.itemId <= 0) report.Errors.Add(owner + " 的道具奖励缺少道具 ID");
    }

    static void ValidateAffectionReward(QuestRewardSpec data, string owner, QuestDatabaseValidationReport report)
    {
        if (data.npcId <= 0) report.Errors.Add(owner + " 的好感度奖励缺少 NPC ID");
    }
}
