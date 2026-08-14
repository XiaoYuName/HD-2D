using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 引导<b>正常播完</b>之后要做的一件事：接着放段剧情、打开某个界面、发个事件等等。
    /// 中途被打断（目标丢了、读档、切场景）<b>不会</b>执行 —— 那种情况玩家没走完流程，
    /// 后面的剧情不该凭空冒出来。
    ///
    /// 每种动作一个类，字段各管各的，Inspector 里选了哪种就只显示哪种的字段。
    /// 加新动作 = 加一个子类，不用改 <see cref="TutorialManager"/>。
    /// </summary>
    [Serializable]
    public abstract class TutorialEndAction
    {
        /// <summary>列表里显示的标题。</summary>
        public abstract string EditorTitle { get; }

        /// <summary>干活。异常由调用方兜住，一个动作炸了不影响后面的。</summary>
        public abstract void Execute();
    }

    /// <summary>播一段剧情。</summary>
    [Serializable]
    public class TutorialPlayDramaAction : TutorialEndAction
    {
        [LabelText("剧情ID")]
        public long DramaID;

        public override string EditorTitle => $"播剧情 {DramaID}";

        public override void Execute()
        {
            if (DramaID <= 0)
            {
                Debug.LogError("引导结束动作:剧情ID没填");
                return;
            }

            if (!DramaManager.IsInitialized)
            {
                Debug.LogError("引导结束动作:DramaManager 还没初始化,剧情播不了");
                return;
            }

            DramaManager.Instance.StartDramaRuntime(DramaID);
        }
    }

    /// <summary>打开一个界面。</summary>
    [Serializable]
    public class TutorialOpenUIAction : TutorialEndAction
    {
        [LabelText("界面"), ValueDropdown(nameof(GetPageIds), AppendNextDrawer = true)]
        public string PageID;

        public override string EditorTitle => $"打开界面 {PageID}";

        public override void Execute()
        {
            if (string.IsNullOrEmpty(PageID))
            {
                Debug.LogError("引导结束动作:界面没选");
                return;
            }

            UISystem.Instance.OpenUI(PageID);
        }

        private static System.Collections.Generic.IEnumerable<string> GetPageIds()
        {
            return TutorialConfigUtility.GetPageIds();
        }
    }

    /// <summary>接着播下一段引导。分段配、串起来播的场合用。</summary>
    [Serializable]
    public class TutorialStartNextAction : TutorialEndAction
    {
        [LabelText("下一段引导ID")]
        public long TutorialID;

        public override string EditorTitle => $"接着播引导 {TutorialID}";

        public override void Execute()
        {
            if (TutorialID <= 0)
            {
                Debug.LogError("引导结束动作:下一段引导ID没填");
                return;
            }

            TutorialManager.Instance.StartTutorial(TutorialID);
        }
    }

    /// <summary>
    /// 发一个自定义事件。业务那边自己监听去做事，
    /// 引导系统不用认识每一个业务系统 —— 要发奖励、解锁功能这类都走它。
    /// </summary>
    [Serializable]
    public class TutorialTriggerEventAction : TutorialEndAction
    {
        [LabelText("事件名")]
        [InfoBox("业务里 TutorialManager.Instance.TriggerEvent 收得到；同名的引导触发条件也会被它点着")]
        public string EventKey;

        public override string EditorTitle => $"发事件 {EventKey}";

        public override void Execute()
        {
            if (string.IsNullOrEmpty(EventKey))
            {
                Debug.LogError("引导结束动作:事件名没填");
                return;
            }

            TutorialManager.Instance.TriggerEvent(EventKey);
        }
    }
}
