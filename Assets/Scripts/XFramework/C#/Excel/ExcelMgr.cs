using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace XFramework
{
    public partial class ExcelMgr : MonoSingleton<ExcelMgr>,IGameInitialized
    {
        
        public UniTask Initialized()
        {
            InitData();
            return UniTask.CompletedTask;
        }

        
        public UniTask Release()
        {
            return UniTask.CompletedTask;
        }
    
    }
}
