using System;
using Drama.Runtime;
using Sirenix.OdinInspector;

namespace XFramework
{
    /// <summary>
    /// 对话历史（Log）里的一条：<b>只有「哪一本」和「第几条指令」两个数</b>。
    ///
    /// 台词的正文 / 语音 / 说话人 / 名字颜色<b>一个都不存</b>，要显示的时候拿这两个数
    /// 回剧本里现取（见 <see cref="DramaHistoryResolver"/>）。原工程也是这么干的
    /// （<c>UI_Drama.mLogDataIdxList</c> 就是个 <c>List&lt;int&gt;</c>，文字每次从
    /// <c>mDramaData.eventAy[idx]</c> 现读）。
    ///
    /// 存引用的版本量过：500 条要 101 KB，而当时整个存档才 162 KB —— 一个 Log 把存档撑大六成，
    /// 不值。现在 500 条约 27 KB，而且剧本改了文案，回看历史也跟着更新。
    ///
    /// <b>代价：下标是导出产物的编号，剧本重导会变。</b>和 <see cref="DramaRestorePoint"/>
    /// 一个性质，这里同样不做迁移 —— 解析不出台词的条目会被跳过（见解析器），
    /// 最坏情况是老存档里的历史显示的是改版后同一位置的另一句话。
    /// </summary>
    [Serializable]
    public class DramaHistoryEntry
    {
        [LabelText("剧本ID")]
        public long DramaId;

        /// <summary>指令下标，指向剧本里的一条 <see cref="TalkAction"/>。</summary>
        [LabelText("指令下标")]
        public int ActionIndex = -1;

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
            };
        }

        /// <summary>
        /// 指得着东西吗。
        ///
        /// 写成方法不是属性 —— 属性会被 Newtonsoft 一起写进存档，白占地方。
        /// </summary>
        public bool IsValid() => DramaId > 0 && ActionIndex >= 0;

        public override string ToString() => $"{DramaId}#{ActionIndex}";
    }
}
