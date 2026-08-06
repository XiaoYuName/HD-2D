using UnityEngine;
using Sirenix.OdinInspector;
using System;

// 参考
[Serializable]
public class QuestData
{
    public long id;
    public string nameKey, descKey;
    public string remark;
    public QuestObjData[] questObjDataList;
    public QuestRewardData[] rewardDataList;
    public QuestRewardData[] extraRewardDataList;
}