using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework
{
    /// <summary>
    /// 女主粉丝界面
    /// </summary>
    public class FanPageUI : UIBase
    {
        private ScrollRect mScrollRect;
        private Scrollbar mScrollbar;
        private CancellationTokenSource _tokenSource;

        private List<MessageSlot> messageSlot = new List<MessageSlot>();
        
        /// <summary>
        /// 初始化方法,一般不需要手动调用
        /// </summary>
        public override void Init()
        {
            mScrollRect = Get<ScrollRect>("mScrollRect");
            mScrollbar = Get<Scrollbar>("mScrollbar");
        }

        /// <summary>
        /// 通用UI打开方法,提供重写
        /// </summary>
        public override void Open()
        {
            base.Open();
            _tokenSource = new CancellationTokenSource();
            OnLineGameManager.Instance.RegisterOnMessageListUpdate(OnMessageListUpdate,true);
            OnLineGameManager.Instance.RegisterOnMessageDataUpdate(OnMessageDataUpdate,true);
        }

        /// <summary>
        /// 通用UI关闭方法,提供重写
        /// </summary>
        public override void Close()
        {
            base.Close();
            OnLineGameManager.Instance.UnRegisterOnMessageListUpdate(OnMessageListUpdate);
            OnLineGameManager.Instance.UnRegisterOnMessageDataUpdate(OnMessageDataUpdate);
            if (_tokenSource != null)
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _tokenSource = null;
            }

            foreach (var item in messageSlot)
            {
                item.Release();
                AssetsManager.Instance.FreeGameObject(item.gameObject);
            }
            messageSlot.Clear();
        }

        private void OnMessageListUpdate(List<MessageData> messages)
        {
            GenerateMessage(messages).Forget();
        }

        private void OnMessageDataUpdate(MessageData messageData)
        {
            GenerateMessageData(messageData).Forget();
        }

        private async UniTask GenerateMessage(List<MessageData> messages)
        {
            if(messages == null || messages.Count == 0)return;
            foreach (var VARIABLE in messages)
            {
                await GenerateMessageData(VARIABLE);
            }
        }

        private async UniTask GenerateMessageData(MessageData messageData)
        {
            var obj = AssetsManager.Instance.Instantiate(AssetKeys.MessageSlotPath);
            obj.transform.SetParent(mScrollRect.content, false);
            obj.transform.SetAsFirstSibling();
            obj.transform.localScale = Vector3.zero;
            var slot = obj.GetComponent<MessageSlot>();
            slot.Init();
            slot.SetData(messageData);
            messageSlot.Add(slot);
            await UniTask.WaitForEndOfFrame(this);
            Canvas.ForceUpdateCanvases();
            // 先计算 MessageSlot 内部文本、图片等嵌套布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(slot.transform as RectTransform);

            // 再让外层列表读取 MessageSlot 的最终高度
            LayoutRebuilder.ForceRebuildLayoutImmediate(mScrollRect.content);
            
            Canvas.ForceUpdateCanvases();
            await obj.transform.DOScale(Vector3.one, 0.3f).AsyncWaitForCompletion();
        }
    }
}

