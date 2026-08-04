using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using XFramework;

namespace XFramework
{
    /// <summary>
    /// 测试管理工具，用于AI直接预览小游戏或UI效果，不需要走正常流程
    /// </summary>
    public class AIRuntimeManager : MonoSingleton<AIRuntimeManager>
    {
        public RacingCarSewingMachinesUI CarUI;

        private void Start()
        {
            Init().Forget();
        }

        private async UniTask Init()
        {
            await Addressables.InitializeAsync();
            await PlayerInputManager.Instance.Initialized();
            CarUI.Init();
            ClothingBag clothingBag = new ClothingBag();
            clothingBag.clothingID = 10001;

            CarUI.SetData(clothingBag);
        }
    }
}

