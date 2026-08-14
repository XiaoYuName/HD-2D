using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 对话历史（Log）的容器。<b>每个存档槽独立</b>，跟着 <c>GameSaveData</c> 存读。
    ///
    /// 是<b>滚动</b>的：超过 <see cref="Capacity"/> 就丢最老的。不设上限的话，
    /// 一个玩到后期的存档能攒下上万条，存档 JSON 会明显变大、每次存读都要多花时间，
    /// 而玩家真正会往回翻的只有最近这些。
    ///
    /// 已读标记<b>不在这儿</b> —— 那是跨存档共享的，见 <see cref="DramaReadMarks"/>。
    /// </summary>
    public sealed class DramaHistory
    {
        /// <summary>默认保留条数。</summary>
        public const int DefaultCapacity = 500;

        private readonly List<DramaHistoryEntry> entries = new List<DramaHistoryEntry>();

        private int capacity = DefaultCapacity;

        /// <summary>保留多少条。调小会立刻把超出的老条目裁掉。</summary>
        public int Capacity
        {
            get => capacity;
            set
            {
                capacity = Mathf.Max(1, value);
                Trim();
            }
        }

        /// <summary>按发生顺序，最老的在前。UI 一般要倒着显示。</summary>
        public IReadOnlyList<DramaHistoryEntry> Entries => entries;

        public int Count => entries.Count;

        public void Add(DramaHistoryEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entries.Add(entry);
            Trim();
        }

        /// <summary>清空。新开档时由 <see cref="Restore"/> 顺带做掉，一般不用单独调。</summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// 读档：整个换成存档里的那份。
        /// 传 null（新开档 / 老存档没有这个字段）就是清空。
        /// </summary>
        public void Restore(IReadOnlyList<DramaHistoryEntry> saved)
        {
            entries.Clear();

            if (saved != null)
            {
                for (int i = 0; i < saved.Count; i++)
                {
                    // 坏档 / 手改过的存档里可能有 null，别让它们混进来最后在 UI 那边空引用
                    if (saved[i] != null)
                    {
                        entries.Add(saved[i]);
                    }
                }
            }

            Trim();
        }

        /// <summary>
        /// 存档：拷一份出去。
        ///
        /// <b>必须是拷贝</b>：存档对象会一直被 SaveGameManager 拿着，
        /// 直接把内部列表交出去的话，之后每记一条历史都会顺带改到"已经存好"的那份。
        /// </summary>
        public List<DramaHistoryEntry> Snapshot()
        {
            return new List<DramaHistoryEntry>(entries);
        }

        private void Trim()
        {
            if (entries.Count <= capacity)
            {
                return;
            }

            entries.RemoveRange(0, entries.Count - capacity);
        }
    }
}
