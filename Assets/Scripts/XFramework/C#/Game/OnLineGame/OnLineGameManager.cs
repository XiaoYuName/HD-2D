using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private List<MessageData> MessageDataList = new();

        /// <summary>
        /// 私信消息列表
        /// </summary>
        public List<PrivateMessageData> PrivateMessageDataList { get; private set; } = new();

        #region ISavable

        public string GUID => "OnLineGameManager";

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        public void SaveData(GameSaveData data)
        {
            data.MessageDataList = MessageDataList;
            data.PrivateMessageDataList = PrivateMessageDataList;
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

            if (data?.PrivateMessageDataList == null)
            {
                PrivateMessageDataList = new List<PrivateMessageData>();
            }
            else
            {
                PrivateMessageDataList = new List<PrivateMessageData>(data.PrivateMessageDataList);
            }
        }

        #endregion

        #region SendMessage

        public void SendMessage(MessageData message)
        {
            MessageDataList.Add(message);
            OnMessageDataUpdate?.Invoke(message);
        }
        
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

        #endregion

        #region PrivateMessageData

        public List<PrivateMessageData> GetPrivateMessageDataList()
        {
            if (PrivateMessageDataList.Count <= 0)
            {
                for (int i = 0; i < GameDataManager.Instance.onLineGameData.PrivateMessageNumber; i++)
                {
                   var data = LubanManager.Instance.TbPriavateMessageData.DataList[Random.Range(0, LubanManager.Instance.TbPriavateMessageData.DataList.Count)];
                   PrivateMessageDataList.Add(new PrivateMessageData()
                   {
                       PrivateMessageID = data.ID,
                   });
                }
            }
            return PrivateMessageDataList;
        }

        public void CompletePrivateMessage(long privateMessageID)
        {
            if (PrivateMessageDataList.Any(temp => temp.PrivateMessageID == privateMessageID))
            {
                
            }
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

    [System.Serializable]
    public class PrivateMessageData
    {
        [LabelText("私信消息ID")]
        public long PrivateMessageID;
        
    }
}

