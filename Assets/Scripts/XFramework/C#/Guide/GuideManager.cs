using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using XFramework;

namespace XFramework
{
    public class GuideManager : MonoSingleton<GuideManager>,ISaveable
    {
        
        #region ISaveable

        public void Start()
        {
            ((ISaveable)this).RegisterSaveable();
        }

        public string GUID => "GuideManager";
        public void SaveData(GameSaveData data)
        {
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
        
    }


    [System.Serializable]
    public class GuideBag
    {
        public long Id;
    
        public StateType StateType;
    }
}

