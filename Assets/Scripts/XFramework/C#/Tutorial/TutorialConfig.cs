using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 一段引导：什么时候起、播几步、播过还播不播。
    /// </summary>
    [Serializable]
    public class TutorialConfig
    {
        // 显示条件一律走 bool 属性，别写成 "@ID <= 0" 这种表达式：
        // Odin 的表达式编译器碰上 long 和 int 字面量比较会生成非法 IL，Inspector 里直接糊一大片异常
        [BoxGroup("基本"), LabelText("引导ID"), DelayedProperty]
        [InfoBox("ID 是存档里记「这段教过了」的凭据，配好之后别再改", InfoMessageType.Warning, VisibleIf = nameof(ShowIdWarning))]
        public long ID;

        [BoxGroup("基本"), LabelText("备注")]
        public string Description;

        [BoxGroup("基本"), LabelText("播过还播吗"), EnumToggleButtons]
        public TutorialRepeatType RepeatType = TutorialRepeatType.Once;

        [BoxGroup("基本"), LabelText("优先级")]
        [InfoBox("同一个触发点上有多条能播时，数大的先播")]
        public int Priority;

        #region 触发

        [BoxGroup("触发"), LabelText("什么时候起"), EnumToggleButtons]
        public TutorialTriggerType TriggerType = TutorialTriggerType.Manual;

        [BoxGroup("触发"), LabelText("进入哪个小场景(SceneID)")]
        [ShowIf(nameof(TriggerType), TutorialTriggerType.EnterScene)]
        public long TriggerSceneID;

        [BoxGroup("触发"), LabelText("打开哪个界面")]
        [ShowIf(nameof(TriggerType), TutorialTriggerType.OpenUI)]
        [ValueDropdown(nameof(GetPageIds), AppendNextDrawer = true)]
        public string TriggerPageID;

        [BoxGroup("触发"), LabelText("哪段剧情播完(DramaID)")]
        [ShowIf(nameof(TriggerType), TutorialTriggerType.DramaFinish)]
        public long TriggerDramaID;

        [BoxGroup("触发"), LabelText("哪个自定义事件")]
        [ShowIf(nameof(TriggerType), TutorialTriggerType.Event)]
        [InfoBox("业务里调 TutorialManager.Instance.TriggerEvent(\"事件名\") 起这段引导")]
        public string TriggerEventKey;

        [BoxGroup("触发"), LabelText("额外解锁条件ID")]
        [InfoBox("解锁条件表的ID，0=无条件。判定还没接入，目前配了也不会挡住", InfoMessageType.Warning,
            VisibleIf = nameof(ShowUnlockWarning))]
        public long UnlockConditionID;

        private bool ShowIdWarning => ID <= 0;

        private bool ShowUnlockWarning => UnlockConditionID > 0;

        #endregion

        [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = nameof(TutorialStepConfig.EditorTitle),
            DraggableItems = true, ShowIndexLabels = true)]
        [LabelText("步骤")]
        public List<TutorialStepConfig> Steps = new List<TutorialStepConfig>();

        /// <summary>
        /// 触发索引用的 Key：把触发方式和它那个强类型参数拼成一个字符串，
        /// <see cref="TutorialManager"/> 拿它做字典查找。
        /// </summary>
        public string GetTriggerKey()
        {
            return TriggerType switch
            {
                TutorialTriggerType.EnterScene => TriggerKeyOf(TriggerType, TriggerSceneID.ToString()),
                TutorialTriggerType.OpenUI => TriggerKeyOf(TriggerType, TriggerPageID),
                TutorialTriggerType.DramaFinish => TriggerKeyOf(TriggerType, TriggerDramaID.ToString()),
                TutorialTriggerType.Event => TriggerKeyOf(TriggerType, TriggerEventKey),
                _ => TriggerKeyOf(TriggerType, string.Empty),
            };
        }

        public static string TriggerKeyOf(TutorialTriggerType triggerType, string triggerParam)
        {
            return $"{(int)triggerType}|{triggerParam?.Trim()}";
        }

        /// <summary>列表里每一项显示的标题。</summary>
        public string EditorTitle =>
            $"[{ID}] {(string.IsNullOrEmpty(Description) ? "(未命名引导)" : Description)}";

        private static IEnumerable<string> GetPageIds()
        {
            return typeof(UIKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue())
                .OrderBy(id => id);
        }
    }
}
