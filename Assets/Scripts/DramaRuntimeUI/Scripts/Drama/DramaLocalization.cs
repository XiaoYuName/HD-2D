using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaLocalization"/> 的本工程实现，接 Unity Localization。
    ///
    /// 台词文本走 String Table（同步查），台词语音走 Asset Table（异步查）。
    /// 两边的表名和 Key 都由编辑器工程的 LocalizationNode / LocalizationAudioNode 写进剧本，
    /// 这里不做任何映射，原样透传。
    /// </summary>
    public sealed class DramaLocalization : IDramaLocalization
    {
        /// <summary>
        /// 同步查表。接口要求同步是对的——每句台词 await 一次表查询既难写又容易掉帧，
        /// 表已经由 <see cref="PreloadStringTablesAsync"/> 拉进内存了，这里纯查询不会有 IO。
        ///
        /// 查不到时 Localization 会返回一段 "No translation found ..." 的占位串，
        /// 刻意不拦：让缺的 Key 直接显示在对话框上，比静默变空字符串好查。
        /// </summary>
        public string Resolve(LocalizedRef reference)
        {
            if (reference.IsEmpty)
            {
                return string.Empty;
            }

            return LanguageManager.Instance.GetLocalizedString(reference.Table, reference.Key);
        }

        /// <summary>
        /// 取台词语音。取不到返回 null，Handler 会当这句没配语音处理。
        ///
        /// <b>走 <see cref="LanguageManager.GetLocalizedAsset{T}"/>，和台词文本同一个入口。</b>
        /// 它内部是<b>同步</b>取（<c>GetLocalizedAsset</c> 而不是 <c>...Async</c>），
        /// 之所以敢同步：本段剧本用到的 Asset Table 已经被
        /// <see cref="PreloadAssetTablesAsync"/> 预热进内存了，这里是纯查询、不会有 IO。
        /// 和上面 <see cref="Resolve"/> 同步查字符串是同一个理由。
        ///
        /// 方法签名保持 async 是因为包的接口这么定的（别的工程可能真要异步加载），
        /// 这边只是同步跑完立刻返回。
        ///
        /// 返回的 AudioClip 由 Asset Table 自己持有，随表卸载走，
        /// <b>不要</b>拿去 AssetsManager.FreeAsset（那边没有这个 Key 的引用计数）。
        /// </summary>
        public UniTask<AudioClip> ResolveVoiceAsync(LocalizedRef reference, CancellationToken ct)
        {
            if (reference.IsEmpty || ct.IsCancellationRequested)
            {
                return UniTask.FromResult<AudioClip>(null);
            }

            AudioClip clip = LanguageManager.Instance.GetLocalizedAsset<AudioClip>(reference.Table, reference.Key);
            if (clip == null)
            {
                Debug.LogWarning($"[Drama] 语音资源缺失：{reference}");
            }

            return UniTask.FromResult(clip);
        }

        public UniTask PreloadStringTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct)
        {
            return PreloadAsync(tables, isAssetTable: false, ct);
        }

        public UniTask PreloadAssetTablesAsync(IReadOnlyCollection<string> tables, CancellationToken ct)
        {
            return PreloadAsync(tables, isAssetTable: true, ct);
        }

        /// <summary>
        /// 预热本段剧本用到的表。
        ///
        /// 必须先等 Localization 自己初始化完（选定语言、加载 Locale），
        /// 否则 PreloadTables 会按空语言去拉表。
        /// </summary>
        private static async UniTask PreloadAsync(IReadOnlyCollection<string> tables, bool isAssetTable, CancellationToken ct)
        {
            if (tables == null || tables.Count == 0)
            {
                return;
            }

            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);

            List<TableReference> references = new List<TableReference>(tables.Count);
            foreach (string table in tables)
            {
                if (!string.IsNullOrEmpty(table))
                {
                    references.Add(table);
                }
            }

            if (references.Count == 0)
            {
                return;
            }

            AsyncOperationHandle handle = isAssetTable
                ? LocalizationSettings.AssetDatabase.PreloadTables(references)
                : LocalizationSettings.StringDatabase.PreloadTables(references);

            await handle.ToUniTask(cancellationToken: ct);
        }
    }
}
