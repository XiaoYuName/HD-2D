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
        /// <summary>
        /// 面板是不是还活着。
        ///
        /// 退出 Play / 关 UI 时 Unity 先销毁 GameObject，剧情的收尾逻辑（Director 的 finally）
        /// 才走到这里，这时候碰 <c>transform</c> 会抛 MissingReferenceException。
        /// Unity 重载过 <c>==</c>，已销毁的对象和 null 比较为 true。
        /// </summary>
        private bool Alive => backgroundImage != null;

        public async UniTask ChangeAsync(long backgroundId, Sprite sprite, EBgTransitionKind kind, float inSeconds, float outSeconds,
            CancellationToken ct)
        {
            if (!Alive)
            {
                return;
            }

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

            // 先无条件恢复不透明：ReleaseAll 收尾时把 alpha 压成 0 了，
            // 不恢复的话下一本剧本的背景是隐形的（这个坑只在连播时才现）。
            // 转场分支要做淡入的话，在各自 case 里从 0 推到 1。
            Color c = backgroundImage.color;
            c.a = 1f;
            backgroundImage.color = c;

            switch (kind)
            {
                case EBgTransitionKind.None:
                    break;   // 瞬切，上面已经就位

                case EBgTransitionKind.Fade:
                case EBgTransitionKind.VenetianBlind:
                case EBgTransitionKind.Comb:
                    // TODO 转场动画还没做，目前都当瞬切。做的时候注意 inSeconds/outSeconds
                    // 已经被 Handler 按播放模式缩放过了（Skip 时是 0），直接用就行
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
            return Alive ? backgroundImage.transform : null;
        }

        /// <summary>把还在跑的背景动画立刻推到终点。剧本结束 / 跳转时调。</summary>
        public void CompleteAllTweens()
        {
            if (!Alive)
            {
                return;
            }

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
            if (!Alive)
            {
                return;
            }

            DOTween.Kill(backgroundImage.transform);

            Transform root = backgroundImage.transform;
            root.localPosition = Vector3.zero;
            root.localEulerAngles = Vector3.zero;
            root.localScale = Vector3.one;

            // ★ 先透明再清贴图，两步都要做。
            // RawImage 的 texture 为 null 时会按 color 画一个纯色矩形 ——
            // 只清贴图不清 alpha 的话，剧情结束瞬间整屏变成一块白板。
            // 旧工程收尾时也是这两句：mBGTexture.color = ALPHA0 + texture = null
            Color c = backgroundImage.color;
            c.a = 0f;
            backgroundImage.color = c;

            backgroundImage.texture = null;
        }
    }
}

