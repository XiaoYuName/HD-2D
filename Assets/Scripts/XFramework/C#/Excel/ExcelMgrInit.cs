using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XFramework
{
    public partial class ExcelMgr
    {
        private TextAsset textAsset;
        
        public void InitData()
        {
            textAsset = AssetsManager.Instance.LoadAssets<TextAsset>("Assets/AddressableAssets/Remote/Configs/JsonConfigs/CharacterLevelData.json");
CharacterLevelDataHelper.InitData(textAsset.text);

        }
    }
}

