using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    [Serializable]
    public class QuestInfo
    {
        [HorizontalGroup("标识ID"), ShowInInspector] public Guid Guid;
        [HorizontalGroup("ID")] public long ID;
        [LabelText("子任务")] public QuestObjInfoBase[] taskList;
        [LabelText("获取时间")] public DateTime CreateTime;
    
        /// <summary>
        /// 必须附带无参构造函数，保证序列化/反序列化无异常
        /// </summary>
        public QuestInfo()
        {
            
        }

        public QuestInfo(long id, int count)
        {
            Guid = System.Guid.NewGuid();
            ID = id;
            CreateTime = DateTime.Now;
        }
    }
}