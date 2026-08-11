using Newtonsoft.Json;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 成没成型目标的实例基类：**零字段**，达成与否问 <see cref="FlagObjData.IsMet"/>。
    /// 子类只需要写订阅 —— 事件命中一律 <see cref="QuestObjInfoBase.NotifyChanged"/>。
    /// </summary>
    public abstract class FlagObjInfo : QuestObjInfoBase
    {
        [JsonIgnore] FlagObjData FlagData => (FlagObjData)Data;

        [JsonIgnore] public override bool IsComplete => FlagData.IsMet();

        // 成没成型的文案没有 {Progress} 占位符，这里只给日志看
        [JsonIgnore] public override string ProgressText => IsComplete ? "1/1" : "0/1";
    }

    /// <summary>现在有多少型目标的实例基类：**零字段**，当前量问 <see cref="AmountObjData.GetAmount"/>，现算现比。</summary>
    public abstract class AmountObjInfo : QuestObjInfoBase
    {
        [JsonIgnore] AmountObjData AmountData => (AmountObjData)Data;

        [JsonIgnore] public override bool IsComplete => AmountData.GetAmount() >= AmountData.Need;

        [JsonIgnore]
        public override string ProgressText
        {
            get
            {
                AmountObjData data = AmountData;
                return $"{Mathf.Min(data.GetAmount(), data.Need)}/{data.Need}";
            }
        }
    }

    /// <summary>累计几次型目标的实例基类：次数只增不减，进存档。子类在事件回调里调 <see cref="Advance"/>。</summary>
    public abstract class CountObjInfo : QuestObjInfoBase
    {
        /// <summary>已累计的次数。</summary>
        public int count;

        [JsonIgnore] CountObjData CountData => (CountObjData)Data;

        [JsonIgnore] public override bool IsComplete => count >= CountData.Need;

        [JsonIgnore] public override string ProgressText => $"{Mathf.Min(count, CountData.Need)}/{CountData.Need}";

        protected void Advance(int delta)
        {
            int need = CountData.Need;
            if (delta <= 0 || count >= need) return;

            count = Mathf.Min(count + delta, need);
            NotifyChanged();
        }
    }
}
