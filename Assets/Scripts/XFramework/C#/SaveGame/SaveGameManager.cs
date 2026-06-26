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
        public List<UserSaveSummary> Users { get; private set; }

        /// <summary>
        /// 当前用户对象
        /// </summary>
        public UserSaveSummary SelectUserSaveSummary { get; private set; }
        
        [SerializeReference] GameSaveData gameSaveData;

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

        public void Save()
        {
            Save(SelectUserSaveSummary);
        }

        /// <summary>
        /// 保存用户数据
        /// </summary>
        /// <param name="userSaveSummary">用户</param>
        private void Save(UserSaveSummary userSaveSummary)
        {
            RefreshUserSummary(userSaveSummary);
            foreach (var SaveItem in Saveables)
            {
                SaveItem.SaveData(gameSaveData);
            }
            var path = JsonSavePath + "/Sava_GameData"+ "/User" + userSaveSummary.UserID+ ".scriptable";
            var JsonData = JsonConvert.SerializeObject(gameSaveData, Formatting.Indented);
            if (!Directory.Exists(JsonSavePath+ "/Sava_GameData"))
            {
                Directory.CreateDirectory(JsonSavePath+ "/Sava_GameData");
            }
            File.WriteAllText(path, JsonData);

            SaveUsers();
        }

        #endregion
        
        #region 加载用户数据
        /// <summary>
        /// 加载用户数据
        /// </summary>
        /// <param name="userSaveSummary">用户</param>
        public void Load(UserSaveSummary userSaveSummary)
        {
            SelectUserSaveSummary = userSaveSummary;
            var path = JsonSavePath + "/Sava_GameData"+ "/User" + userSaveSummary.UserID + ".scriptable";

            gameSaveData = File.Exists(path)
                ? JsonConvert.DeserializeObject<GameSaveData>(File.ReadAllText(path)) ?? GameSaveData.Create()
                : GameSaveData.Create();

            foreach (var saveItem in Saveables)
                saveItem.LoadData(gameSaveData);
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
                List<UserSaveSummary> slotData = JsonConvert.DeserializeObject<List<UserSaveSummary>>(JsonData);
                if (slotData is not { Count: > 0 })
                {
                    Users = new List<UserSaveSummary>();
                    UsersChangeAction?.Invoke(Users);
                    return;
                }

                Users = slotData;
            }
            else
            {
                Users = new List<UserSaveSummary>();
            }
            UsersChangeAction?.Invoke(Users);
        }

        #endregion

        #region 增加用户
        

        public void CreatUser(int idx, string UserName)
        {
            UserSaveSummary newUserSaveSummary = new()
            {
                UserID = idx,
                UserName = UserName,
                CreateTime = DateTime.Now,
                PreviewGoldNumber = GameDataManager.Instance.GameSettingsData.StarGoldNumber,
                PreviewDay = 1,
                PreviewWeek = 1,
            };

            int index = Users.FindIndex(temp => temp.UserID == idx);
            if (index >= 0)
                Users[index] = newUserSaveSummary;
            else
                Users.Add(newUserSaveSummary);

            // 新档：用 Create() 生成默认存档下发给各管理器，再落盘
            SelectUserSaveSummary = newUserSaveSummary;
            gameSaveData = GameSaveData.Create();
            foreach (var saveItem in Saveables)
                saveItem.LoadData(gameSaveData);

            Save(newUserSaveSummary);
            LoadUsers();
            GameManager.Instance.EnterGame(newUserSaveSummary);
        }

        public void SaveUser(int idx, UserSaveSummary newUserSaveSummary)
        {
            if (Users.Any(temp => temp.UserID == idx))
            {
                newUserSaveSummary.UserID = idx;
                int index =  Users.FindIndex(temp => temp.UserID == idx);
                Users[index] = newUserSaveSummary;
            }
            else
            {
                newUserSaveSummary.UserID = idx;
                Users.Add(newUserSaveSummary);
            }
            SaveUsers();
            Save(newUserSaveSummary);
            LoadUsers();
            Load(newUserSaveSummary);
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

        private Action<List<UserSaveSummary>> UsersChangeAction;

        /// <summary>
        /// 注册所有用户变化回调
        /// </summary>
        /// <param name="callBack"></param>
        public void RegionUsersChange(Action<List<UserSaveSummary>> callBack)
        {
            if (UsersChangeAction == null)
            {
                UsersChangeAction = new Action<List<UserSaveSummary>>(callBack);
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
        public void URegionUsersChange(Action<List<UserSaveSummary>> callBack)
        {
            UsersChangeAction -= callBack;
        }

        #endregion

        #region 摘要同步

        private void RefreshUserSummary(UserSaveSummary summary)
        {
            if (summary == null)
                return;

            PlayerData playerData = GameDataManager.Instance.PlayerData;

            if (playerData == null)
            {
                Debug.LogWarning("刷新存档摘要失败：PlayerData == null");
                return;
            }

            summary.UserName = playerData.UserName;
            summary.PreviewGoldNumber = playerData.GetProperty(PropertyType.Gold);
            summary.PreviewDay = playerData.Day;
            summary.PreviewWeek = playerData.Week;

            int index = Users.FindIndex(temp => temp.UserID == summary.UserID);

            if (index >= 0)
            {
                Users[index] = summary;
            }
            else
            {
                Users.Add(summary);
            }
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

