using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 一个任务类别的配置，面板左侧一个 Tab 就是一个类别。
    /// 类别下的任务全部完成时发一次类别奖励，发过的记在存档里。
    /// 一个任务允许挂在多个类别下，所以这里只存任务ID，不反向独占。
    /// </summary>
    [Serializable]
    public class QuestCategory
    {
        [SerializeField, QuestHidden] long id;
        [SerializeField, QuestLabel("备注")] string remark;
        [SerializeField, QuestLabel("名称")] LocKeyRef name = new();
        [SerializeField, QuestLabel("描述")] LocKeyRef desc = new();
        [SerializeField, QuestLabel("图标")] AssetReferenceSprite icon;

        [SerializeField, QuestLabel("任务列表"), QuestRef(QuestRefKind.Quest)]
        List<long> questIds = new();

        [SerializeField, QuestLabel("类别奖励")] List<IQuestReward> rewards = new();

        public long Id => id;
        public string Remark => remark;
        public AssetReferenceSprite Icon => icon;
        public IReadOnlyList<long> QuestIds => questIds;
        public IReadOnlyList<IQuestReward> Rewards => rewards;

        public string Name => name.Get();
        public string Desc => desc.Get();

#if UNITY_EDITOR
        /// <summary>只由 <see cref="QuestConfig.EditorSyncIds"/> 调，把字典 Key 写进资产。</summary>
        public void EditorSetId(long value) => id = value;
#endif

        public void Validate() => QuestRewards.Validate(rewards, $"任务类别 {Id}");

        public override string ToString() => $"QuestCategory {Id} ({remark})";
    }
}
