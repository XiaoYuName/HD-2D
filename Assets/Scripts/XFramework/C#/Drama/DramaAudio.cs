using Drama.Runtime.Services;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// <see cref="IDramaAudio"/> 的本工程实现，纯转发给 <see cref="AudioManager"/>。
    ///
    /// BGM 走 BGM 轨，台词语音走人声轨（Human）——两条轨在 XMixer 里是分开的音量组，
    /// 玩家把"语音"关掉不会连 BGM 一起静音。
    /// </summary>
    public sealed class DramaAudio : IDramaAudio
    {
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            AudioManager.Instance.PlayBGM(clip);
        }

        public void PlayVoice(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            AudioManager.Instance.PlayAudio(clip, AudioType.Human);
        }

        public void StopVoice()
        {
            AudioManager.Instance.StopHuman();
        }
    }
}
