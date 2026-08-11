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

            // ★ 换图先掐掉背景自己还在跑的变换动画。
            // 对齐旧工程：SetBGPic 第一行就是 LeanTween.cancel(mBGTexture.gameObject)，
            // 所以"背景推镜到一半换了张图"时推镜会停住。
            //
            // 注意是 Kill（停在当前值）而不是 Complete（推到终点）—— LeanTween.cancel 是前者。
            // 也刻意【不】归零 transform：旧工程只在整段剧情收尾时才归零，
            // 剧本想让新背景从原点开始的话会自己接一条「背景位置 (0,0) 时长 0」。
            DOTween.Kill(backgroundImage.transform);

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
            // 背景的位移/旋转/缩放都是 Handler 直接建在这个 Transform 上的，
            // 按 target 收就能全收到
            DOTween.Complete(backgroundImage.transform, withCallbacks: true);
        }

        /// <summary>
        /// 清空背景并释放资源。
        ///
        /// <b>transform 必须归零</b>：位移 / 旋转 / 缩放是跨指令持续的状态，
        /// 不归零的话上一本剧本的推镜会带到下一本去（这个 bug 只在连播时才现）。
        /// 旧工程也是在整段剧情收尾时才做这一步，换图时不做。
        /// </summary>
        public void ReleaseAll()
        {
            DOTween.Kill(backgroundImage.transform);

            Transform root = backgroundImage.transform;
            root.localPosition = Vector3.zero;
            root.localEulerAngles = Vector3.zero;
            root.localScale = Vector3.one;

            backgroundImage.texture = null;
        }
    }
}

