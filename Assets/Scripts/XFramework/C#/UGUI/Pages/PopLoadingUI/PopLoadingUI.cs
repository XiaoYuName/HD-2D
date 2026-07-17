using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class PopLoadingUI : UIBase
    {
        private CanvasGroup _canvasGroup;
        private Sequence _sequence;
        private Canvas _canvas;
        private TextMeshProUGUI _text;
        private TypewriterComponent _typewriter;
        private CancellationTokenSource cancellationToken;

        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public override void Init()
        {
            _canvas = Get<Canvas>("");
            _canvasGroup = Get<CanvasGroup>("UIMask");
            _text = Get<TextMeshProUGUI>("UIMask/Tip");
            _typewriter = Get<TypewriterComponent>("UIMask/Tip");

        }

        /// <summary>
        /// 通用UI打开方法,提供重写
        /// </summary>
        public override void Open()
        {
            base.Open();
            cancellationToken = new CancellationTokenSource();
            _typewriter.gameObject.SetActive(false);
        }
        

        public void FadeIn(float duration, UICanvasLayer layer = UICanvasLayer.UITop, int OrderInLayer = 60)
        {
            _sequence?.Kill();
            _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer), layer);
            _canvas.sortingOrder = OrderInLayer;
            _canvasGroup.blocksRaycasts = true;
            _sequence.Append(_canvasGroup.DOFade(1, duration));
        }

        public void FadeOut(float duration, UICanvasLayer layer = UICanvasLayer.UITop, int OrderInLayer = 60)
        {
            _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer), layer);
            _canvas.sortingOrder = OrderInLayer;
            _canvasGroup.blocksRaycasts = true;
            _sequence?.Kill();
            _sequence.Append(_canvasGroup.DOFade(0, duration))
                .OnComplete(() => { _canvasGroup.blocksRaycasts = false; });
        }

        public async UniTask FadeInAsync(float duration, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer), layer);
            _canvas.sortingOrder = OrderInLayer;
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _canvasGroup.blocksRaycasts = true;
            _sequence.Append(_canvasGroup.DOFade(1, duration));
            await _sequence.AsyncWaitForCompletion();
        }

        public async UniTask FadeOutAsync(float duration, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            _canvas.sortingLayerName = Enum.GetName(typeof(UICanvasLayer), layer);
            _canvas.sortingOrder = OrderInLayer;
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.Append(_canvasGroup.DOFade(0, duration));
            await _sequence.AsyncWaitForCompletion();
            _canvasGroup.blocksRaycasts = false;
        }

        public void Fade(float duration, Action callback, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            FadeIn(duration, layer, OrderInLayer);
            callback?.Invoke();
            FadeOut(duration, layer, OrderInLayer);
        }

        public async UniTask FadeAsync(float duration, Action action, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            await FadeInAsync(duration, layer, OrderInLayer);
            action?.Invoke();
            await FadeOutAsync(duration, layer, OrderInLayer);
        }

        public async UniTask FadeAsync(float duration, Func<UniTask> action, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            await FadeInAsync(duration, layer, OrderInLayer);
            await action();
            await FadeOutAsync(duration, layer, OrderInLayer);
        }

        public async UniTask FadeAsync(float duration, List<UniTask> actions, UICanvasLayer layer = UICanvasLayer.UITop,
            int OrderInLayer = 60)
        {
            await FadeInAsync(duration, layer, OrderInLayer);
            foreach (var function in actions)
            {
                await function;
            }

            await FadeOutAsync(duration, layer, OrderInLayer);
        }
        
        public async UniTask ShowLabel(string label)
        {
           

            _typewriter.gameObject.SetActive(true);
            _typewriter.ShowText(label);

            // 等待文字显示完成
            await UniTask.WaitWhile(
                () => _typewriter.IsShowingText,
                cancellationToken: cancellationToken.Token);

            // 可选：完整显示后停留一段时间
            await UniTask.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken: cancellationToken.Token);

            _typewriter.StartDisappearingText();

            // 等待文字隐藏完成
            await UniTask.WaitWhile(
                () => _typewriter.IsHidingText,
                cancellationToken: cancellationToken.Token
            );

            _typewriter.gameObject.SetActive(false);
        }

    }
}

