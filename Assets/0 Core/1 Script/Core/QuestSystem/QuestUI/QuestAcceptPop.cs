using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XFramework.QuestSystem.UI
{
    public class QuestAcceptPop : UIBase
    {
        [SerializeField] Image icon;
        [SerializeField] LocalizeStringEvent titleText;
        [SerializeField] LocalizeStringEvent questNameText;
        [SerializeField] LocalizeStringEvent objDescText;
        [SerializeField] LocalizeStringEvent tipsText;

        public override void Init()
        {
            
        }
    }
}
