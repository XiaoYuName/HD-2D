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
        /// <summary>
        /// 从剧本里取出某条台词。
        ///
        /// 取不到（下标越界、或者那个位置现在不是台词了）就返回 false ——
        /// 剧本重导过、存档比剧本老的时候会碰到，属于预期内的事，不该报错更不该崩。
        /// </summary>
        public static bool TryResolve(DramaScript script, int actionIndex, out DialogueLine line)
        {
            line = default;

            if (script == null || script.GetAction(actionIndex) is not TalkAction talk)
            {
                return false;
            }

            // 和 TalkActionHandler 里构造 DialogueLine 的口径一致：全是引用，一个都不解析
            line = new DialogueLine
            {
                TextRef        = talk.Text,
                Speaker        = talk.Speaker,
                ActorId        = talk.ActorId,
                SpeakerNameRef = talk.SpeakerName,
                NameColor      = talk.NameColor,
                Balloon        = talk.Balloon,
                VoiceRef       = talk.Voice,
            };

            return true;
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

                    if (TryResolve(script, entry.ActionIndex, out DialogueLine line))
                    {
                        result.Add(new DramaHistoryLine(entry.DramaId, entry.ActionIndex, line));
                    }
                    else
                    {
                        missed++;
                    }
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

            // 汇总报一条，不要每条都报 —— 剧本一改可能几百条同时失效，刷屏没意义
            if (missed > 0)
            {
                Debug.LogWarning($"[Drama] 对话历史有 {missed}/{entries.Count} 条还原不出来，已跳过。" +
                                 "多半是剧本重新导出过，存档里的指令下标不再指向原来那句台词");
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
