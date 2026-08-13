using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一条还原好的历史：原始的 (剧本ID, 下标) 加上从剧本里现取出来的台词。
    /// </summary>
    public readonly struct DramaHistoryLine
    {
        public readonly long DramaId;
        public readonly int ActionIndex;

        /// <summary>
        /// 台词本体。<b>和播出时是同一个结构</b>，所以 Log 的条目可以直接复用对话框那套
        /// 显示逻辑（<see cref="DramaSpeakerName"/> 取名字、正文/语音走多语言绑定）。
        /// </summary>
        public readonly DialogueLine Line;

        public DramaHistoryLine(long dramaId, int actionIndex, in DialogueLine line)
        {
            DramaId = dramaId;
            ActionIndex = actionIndex;
            Line = line;
        }
    }

    /// <summary>
    /// 把历史里的 (剧本ID, 指令下标) 还原成台词。
    ///
    /// 存档里只有两个数（见 <see cref="DramaHistoryEntry"/>），文字要回剧本里现取；
    /// 而 Log 在主菜单也能打开，那时候剧本根本不在内存里 —— 所以这一步是<b>异步</b>的，
    /// 用到的剧本临时加载、用完立刻还回去。
    /// </summary>
    public static class DramaHistoryResolver
    {
        /// <summary>找到某条历史对应的那句台词。找不到返回 null。</summary>
        public static TalkAction Find(DramaScript script, DramaHistoryEntry entry, out bool moved)
        {
            moved = false;

            if (script == null || entry == null)
            {
                return null;
            }

            // ① 快路径：按下标取，再拿身份校一下是不是同一句。
            //    剧本没动过时永远走这条，而且同一句文本在本里出现多次时只有它分得清是哪一次
            if (script.GetAction(entry.ActionIndex) is TalkAction byIndex &&
                (entry.LineKey == DramaLineKey.None ||
                 DramaLineKey.Of(entry.DramaId, byIndex.Text) == entry.LineKey))
            {
                return byIndex;
            }

            // ② 剧本重导过：下标已经指到别的句子上了（甚至不是台词），改按身份全本找。
            //    没有身份可认的老条目（LineKey = None）到这儿就只能放弃 —— 硬按下标取
            //    会静默显示成另一句话，那比不显示更糟
            if (entry.LineKey == DramaLineKey.None)
            {
                return null;
            }

            for (int i = 0; i < script.ActionCount; i++)
            {
                if (script.GetAction(i) is TalkAction candidate &&
                    DramaLineKey.Of(entry.DramaId, candidate.Text) == entry.LineKey)
                {
                    moved = true;
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 把一条台词指令摊成可显示的结构。
        /// 和 <c>TalkActionHandler</c> 里构造 <see cref="DialogueLine"/> 的口径一致：
        /// 全是引用，一个都不解析（Log 开着切语言要能跟着刷新）。
        /// </summary>
        public static DialogueLine ToLine(TalkAction talk)
        {
            if (talk == null)
            {
                return default;
            }

            return new DialogueLine
            {
                TextRef        = talk.Text,
                Speaker        = talk.Speaker,
                ActorId        = talk.ActorId,
                SpeakerNameRef = talk.SpeakerName,
                NameColor      = talk.NameColor,
                Balloon        = talk.Balloon,
                VoiceRef       = talk.Voice,
            };
        }

        /// <summary>
        /// 批量还原，顺序和传进来的一致（最老的在前，UI 一般要倒着排）。
        ///
        /// 同一本剧本只加载一次 —— 历史是一本接一本攒出来的，分组之后通常只有一两本。
        /// 加载走 <c>AssetsManager</c> 的引用计数，正在播的那一本这里加载到的就是同一个实例，
        /// 用完还一次，不会把人家的引用还掉。
        /// </summary>
        public static async UniTask<List<DramaHistoryLine>> ResolveAsync(IReadOnlyList<DramaHistoryEntry> entries)
        {
            List<DramaHistoryLine> result = new List<DramaHistoryLine>(entries?.Count ?? 0);

            if (entries == null || entries.Count == 0)
            {
                return result;
            }

            Dictionary<long, DramaScript> scripts = new Dictionary<long, DramaScript>();
            List<string> loadedKeys = new List<string>();
            int missed = 0;
            int moved = 0;

            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DramaHistoryEntry entry = entries[i];
                    if (entry == null || !entry.IsValid())
                    {
                        continue;
                    }

                    if (!scripts.TryGetValue(entry.DramaId, out DramaScript script))
                    {
                        script = await LoadScriptAsync(entry.DramaId, loadedKeys);

                        // 加载失败也记进去（值是 null）：省得同一本反复试
                        scripts[entry.DramaId] = script;
                    }

                    TalkAction talk = Find(script, entry, out bool byKey);

                    if (talk == null)
                    {
                        missed++;
                        continue;
                    }

                    if (byKey)
                    {
                        moved++;
                    }

                    result.Add(new DramaHistoryLine(entry.DramaId, entry.ActionIndex, ToLine(talk)));
                }
            }
            finally
            {
                // 一一还回去。漏一次这本剧本就永远留在内存里了
                for (int i = 0; i < loadedKeys.Count; i++)
                {
                    AssetsManager.Instance.FreeAsset(loadedKeys[i]);
                }
            }

            // 汇总报一条，不要每条都报 —— 剧本一改可能几百条同时对不上，刷屏没意义
            if (missed > 0)
            {
                Debug.LogWarning($"[Drama] 对话历史有 {missed}/{entries.Count} 条还原不出来，已跳过。" +
                                 "多半是剧本重新导出时把这些台词删了 / 改了正文的多语言键");
            }
            else if (moved > 0)
            {
                Debug.Log($"[Drama] 对话历史有 {moved}/{entries.Count} 条按台词身份找回（剧本重导过，下标已经对不上），显示不受影响");
            }

            return result;
        }

        /// <summary>加载一本剧本，key 记进 <paramref name="loadedKeys"/> 等着还。</summary>
        private static async UniTask<DramaScript> LoadScriptAsync(long dramaId, List<string> loadedKeys)
        {
            string key = DramaDirector.ScriptKeyOf(dramaId);

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[Drama] 对话历史里的剧本 {dramaId} 在剧情表里查不到，这一段历史显示不出来");
                return null;
            }

            DramaScript script = await AssetsManager.Instance.LoadAssetsUniTask<DramaScript>(key);

            // ★ 无论加载成没成功都要记 key：失败时 AA 那边的引用一样已经挂上了，
            //   不还的话这个 key 的计数会一直留着（DramaManager.LoadScriptAsync 同理）
            loadedKeys.Add(key);

            if (script == null)
            {
                Debug.LogWarning($"[Drama] 对话历史加载剧本 {dramaId} 失败：{key}");
            }

            return script;
        }
    }
}
