using System.Collections.Generic;
using System;
using UnityEngine;


public static class QuestObjFactory
{
    private static readonly Dictionary<QuestObjType, Func<QuestObjInfoBase>> Registry = new()
    {
        { QuestObjType.EnterZone, () => new EnterZoneQuestObjInfo() },
        // { TaskType.Interact, () => new InteractTask() },
        // { TaskType.Dialog, () => new TalkToNpcTask() },
        // { TaskType.Kill, () => new KillTask() },
        // { TaskType.Collect, () => new CollectItemTask() },
        // { TaskType.MultiInteract, () => new MutiInteractTask() },
    };

    public static QuestObjInfoBase Create(QuestObjData data)
    {
        if (!Registry.TryGetValue(data.type, out var creator))
        {
            Debug.LogError($"Unknown Task Type: {data.type}");
            return null;
        }

        var task = creator();
        task.Init(data); // 传入配置
        return task;
    }

    public static void Register(QuestObjType type, Func<QuestObjInfoBase> creator)
    {
        Registry[type] = creator;
    }
}
