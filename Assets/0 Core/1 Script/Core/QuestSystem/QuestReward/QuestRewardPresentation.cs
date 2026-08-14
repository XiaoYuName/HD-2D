using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace XFramework
{
    /// <summary>
    /// 一种奖励类型在 UI 上的图标和名字（金币、游戏币、好感度…）。道具奖励的图标在道具表里，不进这份配置。
    /// </summary>
    [Serializable]
    public class QuestRewardPresentation
    {
        [SerializeField, QuestHidden] QuestRewardType type;
        [SerializeField, QuestLabel("备注")] string remark;
        [SerializeField, QuestLabel("显示名称")] LocKeyRef name = new();
        [SerializeField, QuestLabel("显示图标")] AssetReferenceSprite icon;

        public QuestRewardType Type => type;
        public string Remark => remark;
        public AssetReferenceSprite Icon => icon;

        public string Name => name.IsValid() ? name.Get() : Type.ToString();

#if UNITY_EDITOR
        /// <summary>只由 <see cref="QuestConfig.EditorSyncIds"/> 调，把字典 Key 写进资产。</summary>
        public void EditorSetType(QuestRewardType value) => type = value;
#endif

        public override string ToString() => $"QuestRewardPresentation {Type} ({remark})";
    }
}
