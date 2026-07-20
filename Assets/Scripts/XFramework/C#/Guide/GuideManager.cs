using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class GuideManager : MonoSingleton<GuideManager>,ISaveable,IGameInitialized
    {
        #region ISaveable

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        public string GUID => "GuideManager";
        public void SaveData(GameSaveData data)
        {
            data.ClawMachineGameData = ClawMachineGameData;
        }

        public void LoadData(GameSaveData data)
        {
            if (data is { ClawMachineGameData: not null })
            {
                ClawMachineGameData = new ClawMachineGameData()
                {
                    DollNumber = data.ClawMachineGameData.DollNumber,
                    ResetNumber = data.ClawMachineGameData.ResetNumber,
                    LastResetTimer = data.ClawMachineGameData.LastResetTimer,
                };
            }
            else
            {
                ClawMachineGameData = new ClawMachineGameData()
                {
                    DollNumber = ClawMachineSettingData.DollRandomNumber,
                    ResetNumber = ClawMachineSettingData.DayResetLimit,
                    LastResetTimer = DateTime.Now,
                };
            }
        }

        #endregion

        /// <summary>
        /// 初始化脚本函数
        /// </summary>
        /// <returns></returns>
        public async UniTask Initialized()
        {
            ClawMachineSettingData = await AssetsManager.Instance.LoadAssetsUniTask<ClawMachineSettingData>(AssetKeys.ClawMachineGuideSettingPath);
        }

        public async UniTask Release()
        {
            await UniTask.CompletedTask;
        }

        private void Update()
        {
            if (ClawMachineGameData == null) return;
            var resetTime = ClawMachineGameData.LastResetTimer.AddDays(1);
            NextAutoResetTime = resetTime - DateTime.Now;

            if (NextAutoResetTime < TimeSpan.Zero)
            {
                ClawMachineGameData.LastResetTimer = DateTime.Now;
                AddDollResetNumber(1);
            }
        }


        #region 娃娃机

        #region 娃娃机设定

        public ClawMachineSettingData ClawMachineSettingData { get; private set; }

        #endregion

        #region 娃娃机数据
        public ClawMachineGameData  ClawMachineGameData { get; private set; }
        
        /// <summary>
        /// 下次重置时间
        /// </summary>
        public TimeSpan NextAutoResetTime { get; private set; }
        
        
        
        private Action<ClawMachineGameData>   onClawMachineGameDataChange;

    

        public void RegisterClawMachineGameDataChange(Action<ClawMachineGameData>    callback)
        {
            onClawMachineGameDataChange += callback;
            callback?.Invoke(ClawMachineGameData);
        }

        public void UnregisterClawMachineGameDataChange(Action<ClawMachineGameData>   callback)
        {
            onClawMachineGameDataChange -= callback;
            callback?.Invoke(ClawMachineGameData);
        }

        private Action<ClawMachineGameData> onClawMachineDollResetChange;

        public void RegisterClawMachineDollResetChange(Action<ClawMachineGameData> callback)
        {
            onClawMachineDollResetChange += callback;
            callback?.Invoke(ClawMachineGameData);
        }

        public void UnregisterClawMachineDollResetChange(Action<ClawMachineGameData> callback)
        {
            onClawMachineDollResetChange -= callback;
        }


        #endregion
        
        #region 娃娃机图鉴

        public List<DollCatalogData> GetDollCatalogData()
        {
            return LubanManager.Instance.TbDollCatalogData.DataList.ToList();
        }

        #endregion

        #region 娃娃机数据设置

        public void UpdateDollGameNumber(int number)
        {
            ClawMachineGameData.DollNumber -= number;
            if (ClawMachineGameData.DollNumber <= 0)
            {
                ClawMachineGameData.DollNumber = ClawMachineSettingData.DollRandomNumber;
                onClawMachineGameDataChange?.Invoke(ClawMachineGameData);
                onClawMachineDollResetChange?.Invoke(ClawMachineGameData);
            }

            onClawMachineGameDataChange?.Invoke(ClawMachineGameData);
        }

        public void UpdateDollResetNumber(int number)
        {
            ClawMachineGameData.ResetNumber -= number;
            ClawMachineGameData.DollNumber = ClawMachineSettingData.DollRandomNumber;
            onClawMachineGameDataChange?.Invoke(ClawMachineGameData);
            onClawMachineDollResetChange?.Invoke(ClawMachineGameData);
        }

        public void AddDollResetNumber(int number)
        {
            ClawMachineGameData.DollNumber += number;
            ClawMachineGameData.DollNumber = ClawMachineSettingData.DollRandomNumber;
            onClawMachineGameDataChange?.Invoke(ClawMachineGameData);
        }

        #endregion

        #endregion

        
    }

    [System.Serializable]
    public class ClawMachineGameData
    {
        [HorizontalGroup("Data"),LabelText("剩余娃娃次数")]
        public int DollNumber;
        
        [HorizontalGroup("Data"),LabelText("剩余重置次数")]
        public int ResetNumber;

        [LabelText("上次重置时间")]
        public DateTime LastResetTimer;
    }
}

