using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 游戏存储管理器
    /// </summary>
    public class SaveGameManager : MonoSingleton<SaveGameManager>,IGameInitialized
    {
        /// <summary>
        /// 缓存所有的存储对象
        /// </summary>
        private List<ISaveable> Saveables = new List<ISaveable>();
        /// <summary>
        /// 缓存存储Json文件的位置
        /// </summary>
        private static string JsonSavePath;

        /// <summary>
        /// 存档下所有的用户列表
        /// </summary>
        public List<User> Users { get; private set; }

        /// <summary>
        /// 当前用户对象
        /// </summary>
        public User SelectUser { get; private set; }
        
        [LabelText("默认用户配置")] 
        public User DefaultUser;
        
        /// <summary>
        /// 注册函数将自身要存储的信息注册到ISaveablesList中
        /// </summary>
        /// <param name="saveable"></param>
        public void RegisterSaveable(ISaveable saveable)
        {
            if (!Saveables.Contains(saveable))
            {
                Saveables.Add(saveable);
            }
        }
        
        #region 保存用户数据

        /// <summary>
        /// 保存用户数据
        /// </summary>
        /// <param name="user">用户</param>
        public void Save(User user)
        {
            UserSlotData User = new UserSlotData();
            foreach (var SaveItem in Saveables)
            {
                User.UserDatas.Add(SaveItem.GUID, SaveItem.GenerateSaveData());
            }

            var path = JsonSavePath + "/Sava_GameData"+ "/User" + user.UserID+ ".scriptable";
            var JsonData = JsonConvert.SerializeObject(User, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath+ "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath+ "/Sava_GameData");
            }
            File.WriteAllText(path, JsonData);
            SaveUsers();
        }
        
        /// <summary>
        /// 协程同步保存数据
        /// </summary>
        /// <param name="user">用户</param>
        /// <returns></returns>
        public IEnumerator AutoSave(User user)
        {
            UserSlotData User = new UserSlotData();
            foreach (var SaveItem in Saveables)
            {
                User.UserDatas.Add(SaveItem.GUID, SaveItem.GenerateSaveData());
            }

            var path = JsonSavePath + "/Sava_GameData"+ "/User" + user + ".scriptable";
            var JsonData = JsonConvert.SerializeObject(User, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath+ "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath+ "/Sava_GameData");
            }
            
            var Task = File.WriteAllTextAsync(path, JsonData);
            yield return new WaitUntil(() => Task.IsCompleted);
            SaveUsers();
        }

        /// <summary>
        /// 异步保存数据
        /// </summary>
        /// <param name="user">用户</param>
        private async void TaskSave(User user)
        {
            UserSlotData User = new UserSlotData();
            foreach (var SaveItem in Saveables)
            {
                User.UserDatas.Add(SaveItem.GUID, SaveItem.GenerateSaveData());
            }

            var path = JsonSavePath + "/Sava_GameData"+ "/User" + user.UserID+ ".scriptable";
            var JsonData = JsonConvert.SerializeObject(User, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath+ "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath+ "/Sava_GameData");
            }
            await File.WriteAllTextAsync(path, JsonData);
            Debug.Log("异步保存数据成功"+DateTime.Now); 
            TaskSaveUsers();
        }

        #endregion
        
        #region 加载用户数据
        /// <summary>
        /// 加载用户数据
        /// </summary>
        /// <param name="user">用户</param>
        public void Load(User user)
        {
            SelectUser = user;
            var path = JsonSavePath + "/Sava_GameData"+ "/User" + user.UserID + ".scriptable";
            if (File.Exists(path))
            {
                var JsonData = File.ReadAllText(path);
                UserSlotData slotData =  JsonConvert.DeserializeObject<UserSlotData>(JsonData);
                if (slotData == null) return;
                foreach (var SaveItem in Saveables)
                {
                    SaveItem.RestoreData(slotData.UserDatas.ContainsKey(SaveItem.GUID)
                        ? slotData.UserDatas[SaveItem.GUID]
                        : new GameSaveData());
                }
            }

        }
        
        /// <summary>
        /// 删除用户数据
        /// </summary>
        /// <param name="UID"></param>
        public void Delete(int UID)
        {
            var path = JsonSavePath + "/Sava_GameData"+ "/User" + UID + ".scriptable";
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        

        #endregion
        
        #region 保存用户

        /// <summary>
        /// 保存所有用户
        /// </summary>
        private void SaveUsers()
        {
            var path = JsonSavePath + "/Sava_GameData" + "/Logic" + ".scriptable";
            var JsonData = JsonConvert.SerializeObject(Users, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath + "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath + "/Sava_GameData");
            }
            File.WriteAllText(path, JsonData);

        }

        /// <summary>
        /// 异步保存所有用户
        /// </summary>
        private async void TaskSaveUsers()
        {
            var path = JsonSavePath + "/Sava_GameData" + "/Logic" + ".scriptable";
            var JsonData = JsonConvert.SerializeObject(Users, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath + "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath + "/Sava_GameData");
            }

            await File.WriteAllTextAsync(path, JsonData);
            Debug.Log("异步保存用户信息成功");
        }

        #endregion

        #region 加载用户

        /// <summary>
        /// 加载所有用户
        /// </summary>
        private void LoadUsers()
        {
            var path = JsonSavePath + "/Sava_GameData" + "/Logic" + ".scriptable";
            if (File.Exists(path))
            {
                var JsonData = File.ReadAllText(path);
                List<User> slotData = JsonConvert.DeserializeObject<List<User>>(JsonData);
                if (slotData is not { Count: > 0 })
                {
                    Users = new List<User>();
                    UsersChangeAction?.Invoke(Users);
                    return;
                }

                Users = slotData;
            }
            else
            {
                Users = new List<User>();
            }
            UsersChangeAction?.Invoke(Users);
        }

        #endregion

        #region 增加用户

        /// <summary>
        /// 创建一个新用户
        /// </summary>
        /// <param name="UserName"></param>
        public void CreatUser(string UserName)
        {
            User newUser = new User();
            newUser.UserID = Users.Count;
            Users.Add(newUser);
            SaveUsers();
            Save(newUser);
            LoadUsers();
        }

        public void CreatUser(int idx, string UserName)
        {
            if (Users.Any(temp => temp.UserID == idx))
            {
                User newUser = DefaultUser;
                newUser.UserID = idx;
                newUser.UserName = UserName;
                newUser.CreateTime = DateTime.Now;
                for (int i = 0; i < Users.Count; i++)
                {
                    if (Users[i].UserID == idx)
                    {
                        Users[i] = newUser;
                        SaveUsers();
                        Save(newUser);
                        LoadUsers();
                        GameManager.Instance.EnterGame(newUser);
                        return;
                    }
                }
            }
            else
            {
                User newUser = DefaultUser;
                newUser.UserID = idx;
                newUser.UserName = UserName;
                newUser.CreateTime = DateTime.Now;
                Users.Add(newUser);
                SaveUsers();
                Save(newUser);
                LoadUsers();
                GameManager.Instance.EnterGame(newUser);
            }
        }

        /// <summary>
        /// 删除一个已有存档
        /// </summary>
        /// <param name="UID">已有存档的用户唯一标识UID</param>
        public void DeleteUser(int UID)
        {
            if (Users.Any(temp => temp.UserID == UID))
            {
                int index = Users.FindIndex(temp => temp.UserID == UID);
                Delete(Users[index].UserID);
                Users.RemoveAt(index);
                SaveUsers();
                LoadUsers();
            }
        }

        #endregion

        #region Enven 事件回调函数

        private Action<List<User>> UsersChangeAction;

        /// <summary>
        /// 注册所有用户变化回调
        /// </summary>
        /// <param name="callBack"></param>
        public void RegionUsersChange(Action<List<User>> callBack)
        {
            if (UsersChangeAction == null)
            {
                UsersChangeAction = new Action<List<User>>(callBack);
            }
            else
            {
                UsersChangeAction += callBack;
            }
            callBack?.Invoke(Users);
        }

        /// <summary>
        /// 反注册所有用户变化回调
        /// </summary>
        /// <param name="callBack"></param>
        public void URegionUsersChange(Action<List<User>> callBack)
        {
            UsersChangeAction -= callBack;
        }

        #endregion

        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            JsonSavePath = Application.persistentDataPath;
            LoadUsers();
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 释放脚本函数
        /// </summary>
        public async UniTask Release()
        {
            await UniTask.CompletedTask;
        }
    }
}

