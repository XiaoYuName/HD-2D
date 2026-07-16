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
        public List<PriavateMessageBag> PrivateMessageDataList { get; private set; } = new();

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
                PrivateMessageDataList = new List<PriavateMessageBag>();
            }
            else
            {
                PrivateMessageDataList = new List<PriavateMessageBag>(data.PrivateMessageDataList);
            }

            if (data?.ExhibitionPromotionDataList == null)
            {
                exhibitionPromotionBagList = new List<ExhibitionPromotionBag>();
                foreach (var exhibitionPromotionData in LubanManager.Instance.TbExhibitionPromotionData.DataList)
                {
                    exhibitionPromotionBagList.Add(new ExhibitionPromotionBag()
                    {
                        ExhibitionPromotionID = exhibitionPromotionData.ID,
                        ExhibitionPromotionState = StateType.Lock,
                    });
                }
            }
            else
            {
                exhibitionPromotionBagList = new List<ExhibitionPromotionBag>(exhibitionPromotionBagList);
                foreach (var exhibitionPromotionData in LubanManager.Instance.TbExhibitionPromotionData.DataList)
                {
                    if (exhibitionPromotionBagList.Any(temp => temp.ExhibitionPromotionID != exhibitionPromotionData.ID))
                    {
                        exhibitionPromotionBagList.Add(new ExhibitionPromotionBag()
                        {
                            ExhibitionPromotionID = exhibitionPromotionData.ID,
                            ExhibitionPromotionState = StateType.Lock,
                        });
                    }
                }
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

        public PriavateMessageData GetMessageData(long messageID)
        {
            PriavateMessageData messageData = null;
            try
            {
                messageData = LubanManager.Instance.TbPriavateMessageData.Get(messageID);
            }
            catch (Exception e)
            {
                Debug.Log("没有找到对应的私信消息数据 :"  + messageID + " error :" + e.Message);
                return null;
            }
            return messageData;
        }

        public List<PriavateMessageBag> GetPrivateMessageDataList()
        {
            if (PrivateMessageDataList.Count <= 0)
            {
                for (int i = 0; i < GameDataManager.Instance.onLineGameData.PrivateMessageNumber; i++)
                {
                   var data = LubanManager.Instance.TbPriavateMessageData.DataList[Random.Range(0, LubanManager.Instance.TbPriavateMessageData.DataList.Count)];
                   PrivateMessageDataList.Add(new PriavateMessageBag()
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

        #region 展会相关

        /// <summary>
        /// 获取距离当前最近即将开始的展会数据
        /// </summary>
        /// <returns></returns>
        public ExhibitionInfoData GetRecentExhibitionInfo()
        {
            DateTime localTime =
                GameDataManager.Instance.PlayerData.GameDateTime;

            // Luban datetime 默认使用中国时区 UTC+8
            long currentTimestamp = new DateTimeOffset(
                DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified),
                TimeSpan.FromHours(8)
            ).ToUnixTimeSeconds();

            ExhibitionInfoData nearest = null;

            foreach (var config in LubanManager.Instance
                         .TbExhibitionInfoData.DataList)
            {
                // 小于或等于当前时间，说明已经开始
                if (config.StartDateTime <= currentTimestamp)
                    continue;

                // 找开始时间最接近当前时间的一条
                if (nearest == null ||
                    config.StartDateTime < nearest.StartDateTime)
                {
                    nearest = config;
                }
            }

            return nearest;
        }

        public TimeSpan GetTimeUntil(long timestamp)
        {
            DateTime localTime =
                GameDataManager.Instance.PlayerData.GameDateTime;

            long localTimestamp = new DateTimeOffset(
                DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified),
                TimeSpan.FromHours(8)
            ).ToUnixTimeSeconds();

            TimeSpan result = TimeSpan.FromSeconds(timestamp - localTimestamp);

            return result < TimeSpan.Zero ? TimeSpan.Zero : result;
        }

        #endregion

        #region 展会宣发
        private List<ExhibitionPromotionBag> exhibitionPromotionBagList = new List<ExhibitionPromotionBag>();
        
        private Action<List<ExhibitionPromotionBag>> OnExhibitionPromotionBagUpdate;

        public void RegisterOnExhibitionPromotionBagUpdate(Action<List<ExhibitionPromotionBag>> callback)
        {
            OnExhibitionPromotionBagUpdate += callback;
            callback?.Invoke(exhibitionPromotionBagList);
        }

        public void UnRegisterOnExhibitionPromotionBagUpdate(Action<List<ExhibitionPromotionBag>> callback)
        {
            OnExhibitionPromotionBagUpdate -= callback;
            callback?.Invoke(exhibitionPromotionBagList);
        }


        public void BuyExhibitionPromotionBag(long  exhibitionPromotionID)
        {
            if (exhibitionPromotionBagList.Any(temp => temp.ExhibitionPromotionID == exhibitionPromotionID))
            {
              var index  = exhibitionPromotionBagList.FindIndex(temp => temp.ExhibitionPromotionID == exhibitionPromotionID);
              exhibitionPromotionBagList[index].ExhibitionPromotionState = StateType.Unlock;
              OnExhibitionPromotionBagUpdate?.Invoke(exhibitionPromotionBagList);
            }
        }

        public ExhibitionPromotionData GetExhibitionPromotionData(long exhibitionPromotionID)
        {
            try
            {
                return LubanManager.Instance.TbExhibitionPromotionData.Get(exhibitionPromotionID);
            }
            catch (Exception e)
            {
                Debug.LogError($"没有找到对应{exhibitionPromotionID} 的宣发数据");
                return null;
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
    public class PriavateMessageBag
    {
        [LabelText("私信消息ID")]
        public long PrivateMessageID;
        
    }

    [System.Serializable]
    public class ExhibitionPromotionBag
    {
        [LabelText("宣发ID")]
        public long ExhibitionPromotionID;
        [LabelText("购买状态")]
        public StateType  ExhibitionPromotionState;
    }
}

