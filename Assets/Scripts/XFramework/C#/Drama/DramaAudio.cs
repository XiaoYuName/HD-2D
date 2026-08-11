using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaAudio"/> 的本工程实现。
    ///
    /// BGM 走 BGM 轨，台词语音走人声轨（Human）——两条轨在 XMixer 里是分开的音量组，
    /// 玩家把"语音"关掉不会连 BGM 一起静音。
    /// </summary>
    public sealed class DramaAudio : IDramaAudio
    {
        private readonly DramaLocalization localization;

        /// <summary>
        /// 当前这条语音的加载令牌。<see cref="StopVoice"/> 一掐，还在路上的加载就作废，
        /// 不会等它回来之后再"补播"一句已经翻过页的台词。
        /// </summary>
        private CancellationTokenSource voiceTokenSource;

        /// <param name="localization">语音引用 → clip 的解析走它，和台词文本共用同一套多语言配置。</param>
        public DramaAudio(DramaLocalization localization)
        {
            this.localization = localization;
        }

        /// <summary>
        /// 播 BGM。剧本里的 MusicId 就是音频配置表的 ID，
        /// <see cref="AudioManager.PlayAudio(string, float)"/> 会自己查表并按 audioType 分派轨道，
        /// 所以这里既不查表也不加载，原样透传。
        /// </summary>
        public void PlayMusic(string musicId)
        {
            if (string.IsNullOrEmpty(musicId))
            {
                return;
            }

            AudioManager.Instance.PlayAudio(musicId);
        }

        /// <summary>
        /// 播台词语音。<b>即发即忘</b>——接口要求不能让调用方等，
        /// 台词得立刻显示出来，语音晚几帧进来可以接受。
        /// </summary>
        public void PlayVoice(LocalizedRef reference)
        {
            // 新的一句先掐掉上一句（包括上一句还没加载完的那次请求）
            StopVoice();

            if (reference.IsEmpty)
            {
                return;
            }

            voiceTokenSource = new CancellationTokenSource();
            PlayVoiceAsync(reference, voiceTokenSource.Token).Forget();
        }

        public void StopVoice()
        {
            if (voiceTokenSource != null)
            {
                voiceTokenSource.Cancel();
                voiceTokenSource.Dispose();
                voiceTokenSource = null;
            }

            AudioManager.Instance.StopHuman();
        }

        private async UniTaskVoid PlayVoiceAsync(LocalizedRef reference, CancellationToken ct)
        {
            try
            {
                AudioClip clip = await localization.ResolveVoiceAsync(reference, ct);

                // 加载期间可能已经翻页 / 剧情已经结束，这时候不能再播
                if (clip != null && !ct.IsCancellationRequested)
                {
                    AudioManager.Instance.PlayAudio(clip, AudioType.Human);
                }
            }
            catch (OperationCanceledException)
            {
                // 被 StopVoice 掐掉了，正常路径
            }
        }
    }
}
