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
            data.DollGuideDataList = new List<GuideBag>(DollGuideBags);
        }

        public void LoadData(GameSaveData data)
        {
            if (data is { DollGuideDataList: not null })
            {
                DollGuideBags = new List<GuideBag>(data.DollGuideDataList);
            }
            else
            {
                DollGuideBags = new List<GuideBag>();
                foreach (var dollCatalogData in LubanManager.Instance.TbDollCatalogData.DataList)
                {
                    GuideBag dollBag = new GuideBag();
                    dollBag.Id = dollCatalogData.ID;
                    dollBag.StateType = StateType.Lock;
                    
                    DollGuideBags.Add(dollBag);
                }
            }

            if (data is { ClawMachineGameData: not null })
            {
                ClawMachineGameData = new ClawMachineGameData()
                {
                    DollNumber = data.ClawMachineGameData.DollNumber,
                    ResetNumber = data.ClawMachineGameData.ResetNumber,
                };
            }
            else
            {
                ClawMachineGameData = new ClawMachineGameData()
                {
                    DollNumber = ClawMachineSettingData.DollRandomNumber,
                    ResetNumber = ClawMachineSettingData.DayResetLimit,
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



        #region 娃娃机

        #region 娃娃机设定

        public ClawMachineSettingData ClawMachineSettingData { get; private set; }

        #endregion

        #region 娃娃机数据
        public ClawMachineGameData  ClawMachineGameData { get; private set; }
        
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

        #endregion
        
        #region 娃娃机图鉴
        private List<GuideBag> DollGuideBags = new List<GuideBag>();

        public List<DollCatalogData> GetDollCatalogData()
        {
            return LubanManager.Instance.TbDollCatalogData.DataList.ToList();
        }

        private Action<List<GuideBag>>   onGuideChange;
        
        public void RegisterDollGuidChange(Action<List<GuideBag>> callback)
        {
            onGuideChange += callback;
            callback?.Invoke(DollGuideBags);
        }

        public void UnregisterDollGuidChange(Action<List<GuideBag>> callback)
        {
            onGuideChange -= callback;
            callback?.Invoke(DollGuideBags);
        }

        public string CombinationDollImagePath(string imageName)
        {
            return $"{AssetsPaths.DollTexturePath}{imageName}";
        }

        #endregion

        #endregion

        
    }


    [System.Serializable]
    public class GuideBag
    {
        public long Id;
    
        public StateType StateType;
    }

    [System.Serializable]
    public class ClawMachineGameData
    {
        [HorizontalGroup("Data"),LabelText("剩余娃娃次数")]
        public int DollNumber;
        
        [HorizontalGroup("Data"),LabelText("剩余重置次数")]
        public int ResetNumber;
    }
}

