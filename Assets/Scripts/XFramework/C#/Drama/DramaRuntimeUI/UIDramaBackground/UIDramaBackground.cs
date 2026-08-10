using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Drama.Runtime;
using Drama.Runtime.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Drama.UI
{
    public class UIDramaBackground : UIBackground,IDramaBackground
    {
        private RawImage backgroundImage;
        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public override void Init()
        {
            base.Init();
            backgroundImage = Get<RawImage>("RawImage");
            
        }

        /// <summary>
        /// 换背景图。<paramref name="sprite"/> 由 Handler 通过
        /// <see cref="IDramaAssetProvider.LoadBackgroundAsync"/> 加载好后传进来，
        /// 实现方不需要自己碰资源系统。
        ///
        /// 转场为 <see cref="EBgTransitionKind.None"/> 时应当瞬切，两个时长都忽略。
        /// </summary>
        public async UniTask ChangeAsync(long backgroundId, Sprite sprite, EBgTransitionKind kind, float inSeconds, float outSeconds,
            CancellationToken ct)
        {
            Debug.Log($"Kind : {kind}, inSeconds : {inSeconds} , outSeconds : {outSeconds}");
            backgroundImage.texture = sprite.texture;
            backgroundImage.SetNativeSize();
            switch (kind)
            {
                case EBgTransitionKind.None:
                    backgroundImage.color = new Color(backgroundImage.color.r, backgroundImage.color.g, backgroundImage.color.b, 1);
                    break;
                case EBgTransitionKind.Fade:
                    break;
                case EBgTransitionKind.VenetianBlind:
                    break;
                case EBgTransitionKind.Comb:
                    break;
            }
            
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 背景的根节点，位移 / 旋转 / 缩放都是 DOTween 直接动它。
        /// 拿不到（背景还没建出来）返回 null，Handler 会跳过这条指令。
        /// </summary>
        public Transform GetRoot(long backgroundId)
        {
            return backgroundImage.transform;
        }

        /// <summary>把还在跑的背景动画立刻推到终点。剧本结束 / 跳转时调。</summary>
        public void CompleteAllTweens()
        {
            
        }

        /// <summary>清空背景并释放资源。</summary>
        public void ReleaseAll()
        {
        }
    }
}

