using System;
using Drama.Runtime;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 对话历史（Log）里的一条：<b>只有几个数，一个字的正文都不存</b>。
    /// 要显示的时候拿这几个数回剧本里现取（见 <see cref="DramaHistoryResolver"/>）。
    ///
    /// 原工程也是这么干的（<c>UI_Drama.mLogDataIdxList</c> 就是个 <c>List&lt;int&gt;</c>，
    /// 文字每次从 <c>mDramaData.eventAy[idx]</c> 现读）。存完整文本引用的版本量过：
    /// 500 条要 101 KB，而当时整个存档才 162 KB —— 一个 Log 把存档撑大六成，不值。
    ///
    /// <b>两个字段各管一件事，缺一不可：</b>
    ///   <see cref="LineKey"/> 认的是"哪一句话"，剧本重导、插句子、改顺序都不影响它；
    ///   <see cref="ActionIndex"/> 认的是"第几条指令"，重导就失效，但它能<b>消歧</b> ——
    ///   同一句文本在一本剧本里出现两次时，只有下标分得清是哪一次。
    /// 所以解析时先按下标取、拿身份校验一下；对不上再按身份全本找回来。
    /// </summary>
    [Serializable]
    public class DramaHistoryEntry
    {
        [LabelText("剧本ID")]
        public long DramaId;

        /// <summary>指令下标，指向剧本里的一条 <see cref="TalkAction"/>。剧本重导后会失效。</summary>
        [LabelText("指令下标")]
        public int ActionIndex = -1;

        /// <summary>
        /// 台词身份，见 <see cref="DramaLineKey"/>。
        /// <see cref="DramaLineKey.None"/> = 这句算不出身份（正文为空），或者是<b>老存档</b>
        /// 里没有这个字段的条目 —— 两种情况都只能靠下标认。
        /// </summary>
        [LabelText("台词身份")]
        public ulong LineKey;

        /// <summary>
        /// 从一条正在播出的台词记一笔。<c>Index</c> 是导出器写的，正常剧本都有；
        /// 没有（手搓的指令）就返回 null，记不了也不该记。
        /// </summary>
        public static DramaHistoryEntry From(long dramaId, TalkAction talk)
        {
            if (talk == null || talk.Index < 0 || dramaId <= 0)
            {
                return null;
            }

            return new DramaHistoryEntry
            {
                DramaId = dramaId,
                ActionIndex = talk.Index,
                LineKey = DramaLineKey.Of(dramaId, talk.Text),
            };
        }

        /// <summary>
        /// 还有得认吗。下标和身份至少得留一个。
        ///
        /// 写成方法不是属性 —— 属性会被 Newtonsoft 一起写进存档，白占地方。
        /// </summary>
        public bool IsValid() => DramaId > 0 && (ActionIndex >= 0 || LineKey != DramaLineKey.None);

        public override string ToString() => $"{DramaId}#{ActionIndex}";
    }
}
