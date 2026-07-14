using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 线上玩法管理器
    /// </summary>
    public class OnLineGameManager : MonoSingleton<OnLineGameManager>,ISaveable
    {
        /// <summary>
        /// 缓存的消息列表
        /// </summary>
        private List<MessageData> MessageDataList = new List<MessageData>();
        
        #region ISavable

        public string GUID => "OnLineGameManager";

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        public void SaveData(GameSaveData data)
        {
            data.MessageDataList = MessageDataList;
        }

        public void LoadData(GameSaveData data)
        {
            if (data?.MessageDataList == null)
            {
                MessageDataList = new List<MessageData>();
            }
            else
            {
                MessageDataList = new List<MessageData>(data.MessageDataList);
            }
            OnMessageListUpdate?.Invoke(new List<MessageData>(MessageDataList));
        }

        #endregion

        #region SendMessage

        public void SendMessage(MessageData message)
        {
            MessageDataList.Add(message);
            OnMessageDataUpdate?.Invoke(message);
        }

        #endregion

        #region Event

        private Action<List<MessageData>> OnMessageListUpdate;

        public void RegisterOnMessageListUpdate(Action<List<MessageData>> onMessageListUpdate,bool isUpdate)
        {
            OnMessageListUpdate += onMessageListUpdate;
            if (isUpdate)
            {
                onMessageListUpdate?.Invoke(new List<MessageData>(MessageDataList));
            }
        }

        public void UnRegisterOnMessageListUpdate(Action<List<MessageData>> onMessageListUpdate)
        {
            OnMessageListUpdate -= onMessageListUpdate;
        }
        
        private Action<MessageData> OnMessageDataUpdate;

        public void RegisterOnMessageDataUpdate(Action<MessageData> onMessageDataUpdate, bool isUpdate)
        {
            OnMessageDataUpdate += onMessageDataUpdate;
        }

        public void UnRegisterOnMessageDataUpdate(Action<MessageData> onMessageDataUpdate)
        {
            OnMessageDataUpdate -= onMessageDataUpdate;
        }

        #endregion

    }

    [System.Serializable]
    public class MessageData
    {
        [LabelText("随机消息的ID")]
        public long MessageID;
        [LabelText("上传的道具ID")]
        public List<ItemInfo> MessagePicture;
    }
}

