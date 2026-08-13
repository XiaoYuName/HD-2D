

using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 所有UI的抽象基类:
    ///     定义了初始化,打开,和关闭的函数方法,以便外部调用
    /// </summary>
    public abstract class  UIBase : MonoBehaviour
    {
        [LabelText("是否是打开状态")]
        public bool isOpen { get; private set; }

        public UIPageData uiPageData;
        
        protected readonly float tweenTime = 0.25f;
        protected TweenerCore<Vector3,Vector3,VectorOptions> tween;
        [LabelText("动画Root")]
        public RectTransform TweenerRoot;
        
        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public abstract void Init();

        public virtual void Release()
        {
            assetReleaser?.Release();
        }

        /// <summary>
        /// 通用UI打开方法,提供重写
        /// </summary>
        public virtual void Open()
        {
            isOpen = true;
            gameObject.SetActive(true);
            tween?.Kill();
            if (uiPageData is { IsTween: true })
            {
                if (TweenerRoot == null)
                {
                    TweenerRoot = transform.GetComponent<RectTransform>(); 
                }

                TweenerRoot.localScale = Vector3.zero;
                tween = TweenerRoot.DOScale(Vector3.one, tweenTime);
            }

            if (uiPageData is { IsMouseRightHide: true })
            {
                UISystem.Instance.PushStackUI(this);
            }

            // 引导要知道"哪个界面打开了"(触发条件、等界面打开再继续)。打开界面的入口有同步/异步/协程好几个重载,
            // 但最后都会走到这里,所以挂这一处就够,业务界面不用各自上报。
            if (TutorialManager.IsInitialized && uiPageData != null)
            {
                TutorialManager.Instance.NotifyUIOpened(uiPageData.PageID);
            }
        }

        /// <summary>
        /// 通用UI关闭方法,提供重写
        /// </summary>
        public virtual void Close()
        {
            isOpen = false;
            tween?.Kill();
            if (uiPageData is { IsTween: true })
            {
                if (TweenerRoot == null)
                {
                    TweenerRoot = transform.GetComponent<RectTransform>(); 
                }
                tween =  TweenerRoot.DOScale(Vector3.zero, tweenTime).OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });
            }
            else
            {
                gameObject.SetActive(false);
            }

            if (uiPageData is { IsMouseRightHide: true })
            {
                UISystem.Instance.RemoveStackUI(this);
            }

            Release();
        }
        
        protected virtual void OnDestroy()
        {
            // Close 未被调用（切场景、父节点销毁等）时的兜底。
            // AssetReleaser.Release 可重复调用，不会重复释放同一批 key。
            Release();
        }

        /// <summary>
        /// 获取子物体对象
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns></returns>
        protected GameObject Get(string path)
        {
            return transform.Find(path).gameObject;
        }

        /// <summary>
        /// 获取自身子物体组件
        /// </summary>
        /// <param name="path">路径</param>
        /// <typeparam name="T">组件</typeparam>
        /// <returns></returns>
        protected T Get<T>(string path) where T: Component
        {
            try
            {
                if (String.IsNullOrEmpty(path))
                {
                    return transform.GetComponent<T>();
                }

                return transform.Find(path).GetComponent<T>();
            }
            catch (Exception)
            {
                Debug.LogError("Path :" +path + "路径不存在");
                return null;
            }
            
        }
        
        /// <summary>
        /// 绑定一个Button 
        /// </summary>
        /// <param name="button">Button对象</param>
        /// <param name="func">绑定事件</param>
        /// <param name="audio_id">Audio 音效名称</param>
        protected virtual void Bind(Button button, Action func,string audio_id)
        {
            button.onClick.RemoveAllListeners();

            void UnityAction()
            {
                func?.Invoke();
                if (!string.IsNullOrEmpty(audio_id))
                {
                    AudioManager.Instance.PlayAudio(audio_id);
                }

                NotifyTutorialClick(button);
            }

            button.onClick.AddListener(UnityAction);
        }

        protected virtual void BindAGVClick(AGVButton button, Action func, string audio_id)
        {
            button.OnClick.RemoveAllListeners();
            void UnityAction()
            {
                func?.Invoke();
                if (!string.IsNullOrEmpty(audio_id))
                {
                    AudioManager.Instance.PlayAudio(audio_id);
                }

                NotifyTutorialClick(button);
            }
            button.OnClick.AddListener(UnityAction);
        }

        /// <summary>
        /// 把按钮点击报给引导系统。所有按钮都走 <see cref="Bind"/>/<see cref="BindAGVClick"/> 绑事件,
        /// 所以在这两处统一上报,引导就能判断"玩家点的是不是当前这一步要他点的按钮" ——
        /// 不用去给目标按钮临时挂监听(那种做法会被业务下一次 Bind 的 RemoveAllListeners 清掉)。
        /// </summary>
        private static void NotifyTutorialClick(Component button)
        {
            if (button == null || !TutorialManager.IsInitialized)
            {
                return;
            }

            TutorialManager.Instance.NotifyClick(button.gameObject);
        }


        #region LoadAssets

        
        private AssetReleaser assetReleaser;

        /// <summary>
        /// 加载资源并托管引用：面板关闭或销毁时自动 FreeAsset，调用方无需手动配对释放。
        /// Image 图标优先用 image.SetIcon(key)（IconLoadExtension），额外支持异步与换图时提前归还。
        /// </summary>
        protected T LoadAsset<T>(string key) where T : UnityEngine.Object
        {
            TrackAssetKey(key);
            return AssetsManager.Instance.LoadAssets<T>(key);
        }

        /// <summary>
        /// 托管版 UniTask 异步加载，释放时机同 LoadAsset。
        /// </summary>
        protected UniTask<T> LoadAssetUniTask<T>(string key) where T : UnityEngine.Object
        {
            TrackAssetKey(key);
            return AssetsManager.Instance.LoadAssetsUniTask<T>(key);
        }

        protected void TrackAssetKey(string key)
        {
            if (assetReleaser == null)
                assetReleaser = gameObject.AddComponent<AssetReleaser>();
            assetReleaser.Track(key);
        }

        #endregion
        
    }
}

