using Drama.Runtime;

namespace XFramework
{
    /// <summary>
    /// 一条台词的<b>身份</b>：剧本ID + 正文的多语言键，压成一个 64 位数。
    ///
    /// 已读标记（<see cref="DramaReadMarks"/>）和对话历史（<see cref="DramaHistoryEntry"/>）
    /// 用的是同一个身份 —— 两边都要回答"存档里记的这一句，是现在剧本里的哪一句"。
    ///
    /// <b>为什么不用指令下标</b>：下标是导出产物的编号，策划改一次图、重导一次，
    /// 整本的编号就全变了（<see cref="DramaRestorePoint"/> 那边同样警告过）。
    /// 而正文键是策划填的、跟着这句话本身走，改顺序、插句子都不影响它。
    ///
    /// <b>为什么带上剧本ID</b>：两本剧本共用同一条文本（"……" 这种）时不该互相算作同一句。
    ///
    /// <b>为什么存哈希不存原文键</b>：长度固定、省地方（已读上万条时差别明显），
    /// 而且不用管键里有什么字符。碰撞概率在十万条量级是 ~1e-10，
    /// 真撞了的后果也只是某一句被当成已读 / 历史里显示成另一句，不影响存档结构。
    /// </summary>
    public static class DramaLineKey
    {
        /// <summary>算不出身份时的值（正文为空的台词）。0 同时也是老存档里"没有这个字段"的值。</summary>
        public const ulong None = 0UL;

        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>
        /// 算身份。FNV-1a 64。
        ///
        /// 正文为空的台词返回 <see cref="None"/> —— 那种句子没有内容可以认，
        /// 硬算的话所有空台词会撞成同一个身份，比认不出来更糟。
        /// </summary>
        public static ulong Of(long dramaId, in LocalizedRef text)
        {
            if (text.IsEmpty)
            {
                return None;
            }

            ulong hash = Offset;
            ulong id = unchecked((ulong)dramaId);

            for (int i = 0; i < 8; i++)
            {
                hash = Mix(hash, (byte)(id >> (i * 8)));
            }

            hash = Mix(hash, text.Table);

            // 表和键之间塞个分隔符：不塞的话 ("ab","c") 和 ("a","bc") 会算出同一个值
            hash = Mix(hash, (byte)'\n');

            return Mix(hash, text.Key);
        }

        private static ulong Mix(ulong hash, byte value)
        {
            return (hash ^ value) * Prime;
        }

        /// <summary>每个字符按两个字节喂进去 —— 只取低字节的话，中文键之间会撞得很厉害。</summary>
        private static ulong Mix(ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return hash;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                hash = Mix(hash, (byte)(c & 0xFF));
                hash = Mix(hash, (byte)(c >> 8));
            }

            return hash;
        }
    }
}
