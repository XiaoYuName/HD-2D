using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization;

namespace XFramework
{
    /// <summary>
    /// 一步引导的配置。字段按枚举条件显示：选了「界面节点」才出现界面/路径，
    /// 选了「自定义尺寸」才出现尺寸，免得一屏几十个格子分不清哪个该填。
    /// </summary>
    [Serializable]
    public class TutorialStepConfig
    {
        #region 目标

        [BoxGroup("目标"), LabelText("这一步在教什么(备注)")]
        public string Description;

        [BoxGroup("目标"), LabelText("高亮谁"), EnumToggleButtons]
        public TutorialTargetType TargetType = TutorialTargetType.UIPath;

        [BoxGroup("目标"), LabelText("目标界面")]
        [ShowIf(nameof(TargetType), TutorialTargetType.UIPath)]
        [ValueDropdown(nameof(GetPageIds), AppendNextDrawer = true)]
        public string TargetPageID;

        [BoxGroup("目标"), LabelText("目标节点")]
        [ShowIf(nameof(TargetType), TutorialTargetType.UIPath)]
        [ValueDropdown(nameof(GetTargetPaths), AppendNextDrawer = true)]
        [InfoBox("先选目标界面，这里会列出它预制体里的所有节点", InfoMessageType.None, VisibleIf = nameof(NeedPickPageFirst))]
        public string TargetPath;

        [BoxGroup("目标"), LabelText("锚点Key")]
        [ShowIf(nameof(TargetType), TutorialTargetType.Anchor)]
        [InfoBox("动态生成的 UI 节点(背包格子这类)才用锚点：在节点上挂 TutorialAnchor 并填同样的 Key。\n" +
                 "场景里的东西(Q版小人、场景物件)用「屏幕固定位置」更省事，不用挂组件")]
        public string AnchorKey;

        [BoxGroup("目标"), LabelText("洞的位置")]
        [ShowIf(nameof(TargetType), TutorialTargetType.ScreenRect)]
        [InfoBox("屏幕中心为原点。PlayMode 里选中 TutorialUI(Clone)/UIMask/ShotMask/Unmask 拖到位，" +
                 "把 Inspector 的 Pos X/Y 和 Width/Height 抄回来即可", InfoMessageType.None,
            VisibleIf = nameof(IsScreenRect))]
        public Vector2 HolePosition;

        #endregion

        #region 挖洞

        [BoxGroup("挖洞"), LabelText("洞的大小"), EnumToggleButtons]
        [ShowIf(nameof(NeedsTargetNode))]
        public TutorialMaskFitType MaskFitType = TutorialMaskFitType.FitTarget;

        [BoxGroup("挖洞"), LabelText("洞的形状图"), PreviewField(56, ObjectFieldAlignment.Left)]
        [ShowIf(nameof(HasHole))]
        [InfoBox("留空=用预制体上那张方形九宫格。人物立绘这类异形洞把剪影图拖进来，洞的形状跟图的 alpha 走，注意要硬边",
            InfoMessageType.None, VisibleIf = nameof(HasHole))]
        public Sprite MaskSprite;

        [BoxGroup("挖洞"), LabelText("洞的尺寸")]
        [ShowIf(nameof(ShowMaskCustomSize))]
        public Vector2 MaskSize = new Vector2(200f, 200f);

        [BoxGroup("挖洞"), LabelText("四边扩边")]
        [ShowIf(nameof(ShowMaskPadding))]
        public TutorialMaskPadding MaskPadding;

        [BoxGroup("挖洞"), LabelText("洞的偏移")]
        [ShowIf(nameof(NeedsTargetNode))]
        public Vector2 MaskOffset;

        [BoxGroup("挖洞"), LabelText("洞内点击透传给目标")]
        [ShowIf(nameof(HasHole))]
        public bool ClickThrough = true;

        #endregion

        #region 提示气泡 / 指引图标

        [BoxGroup("提示"), LabelText("提示文案")]
        public LocalizedString TipText;

        [BoxGroup("提示"), LabelText("气泡位置")]
        [ShowIf(nameof(ShowTipOptions))]
        public TutorialTipPos TipPosType = TutorialTipPos.Auto;

        [BoxGroup("提示"), LabelText("指引图标"), EnumToggleButtons]
        public TutorialHandType HandType = TutorialHandType.None;

        [BoxGroup("提示"), LabelText("图标位置")]
        [ShowIf(nameof(ShowHandOptions))]
        [InfoBox("屏幕中心为原点。PlayMode 里选中 TutorialUI(Clone)/UIMask/Hand 下的图标拖到位，把 Inspector 的 Pos X/Y 抄过来即可",
            InfoMessageType.None, VisibleIf = nameof(ShowHandOptions))]
        public Vector2 HandPosition;

        [BoxGroup("提示"), LabelText("图标旋转")]
        [ShowIf(nameof(ShowHandOptions))]
        public float HandRotation;

        #endregion

        #region 完成条件

        [BoxGroup("完成条件"), LabelText("怎么算过"), EnumToggleButtons]
        public TutorialFinishType FinishType = TutorialFinishType.ClickTarget;

        [BoxGroup("完成条件"), LabelText("等这个界面打开")]
        [ShowIf(nameof(FinishType), TutorialFinishType.UIOpen)]
        [ValueDropdown(nameof(GetPageIds), AppendNextDrawer = true)]
        public string FinishPageID;

        [BoxGroup("完成条件"), LabelText("等这个事件")]
        [ShowIf(nameof(FinishType), TutorialFinishType.Event)]
        [InfoBox("业务里调 TutorialManager.Instance.TriggerEvent(\"事件名\") 推进")]
        public string FinishEventKey;

        [BoxGroup("完成条件"), LabelText("等待秒数"), MinValue(0.1f)]
        [ShowIf(nameof(FinishType), TutorialFinishType.Delay)]
        public float DelaySeconds = 1f;

        #endregion

        /// <summary>这一步要不要挖洞。None 的步骤只画一层遮罩加提示文字。</summary>
        public bool HasHole => TargetType != TutorialTargetType.None;

        /// <summary>
        /// 洞要不要跟着一个节点走。屏幕固定位置的洞不需要 —— 也正因为没有节点，
        /// 那种步骤用不了「点击目标」，得用「点击洞内区域」或者自定义事件来推进。
        /// </summary>
        public bool NeedsTargetNode =>
            TargetType == TutorialTargetType.UIPath || TargetType == TutorialTargetType.Anchor;

        private bool IsScreenRect => TargetType == TutorialTargetType.ScreenRect;

        /// <summary>
        /// 显示条件尽量用属性而不是 "@..." 表达式：Odin 的表达式编译器在某些组合下
        /// （尤其 long 和 int 字面量比较）会生成非法 IL，Inspector 上直接糊一大片异常。
        /// </summary>
        // 屏幕固定位置的洞永远要自己填尺寸；跟节点走的洞只有选了「自定义尺寸」才填
        private bool ShowMaskCustomSize =>
            IsScreenRect || (NeedsTargetNode && MaskFitType == TutorialMaskFitType.Custom);

        private bool ShowMaskPadding => NeedsTargetNode && MaskFitType == TutorialMaskFitType.FitTarget;

        private bool ShowTipOptions => TipText != null && !TipText.IsEmpty;

        private bool ShowHandOptions => HandType != TutorialHandType.None;

        private bool NeedPickPageFirst => string.IsNullOrEmpty(TargetPageID);

        /// <summary>列表里每一项显示的标题。</summary>
        public string EditorTitle =>
            string.IsNullOrEmpty(Description) ? "(未命名步骤)" : Description;

        #region 编辑器下拉

        private static IEnumerable<string> GetPageIds()
        {
            return TutorialConfigUtility.GetPageIds();
        }

        /// <summary>
        /// 节点路径下拉：按选中的界面找到同名预制体，把它里面所有节点的相对路径列出来。
        /// 路径手打错是这套配置最容易踩的坑（界面结构一改就失效），能选就别打字。
        /// </summary>
        private IEnumerable<string> GetTargetPaths()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(TargetPageID))
            {
                return System.Array.Empty<string>();
            }

            // UI 预制体的文件名就是 PageID（MainUI.prefab / CommonUI.prefab），按名字找最省事
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{TargetPageID} t:Prefab");
            foreach (string guid in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(assetPath) != TargetPageID)
                {
                    continue;
                }

                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    continue;
                }

                List<string> paths = new List<string>();
                CollectPaths(prefab.transform, string.Empty, paths);
                return paths;
            }

            return System.Array.Empty<string>();
#else
            return System.Array.Empty<string>();
#endif
        }

        private static void CollectPaths(Transform node, string prefix, List<string> results)
        {
            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                string path = string.IsNullOrEmpty(prefix) ? child.name : $"{prefix}/{child.name}";

                // 只有 RectTransform 能当挖洞目标，普通 Transform 列出来也没法用
                if (child is RectTransform)
                {
                    results.Add(path);
                }

                CollectPaths(child, path, results);
            }
        }

        #endregion
    }

    /// <summary>洞的四边扩边。比 Vector4 的 xyzw 好认。</summary>
    [Serializable]
    public struct TutorialMaskPadding
    {
        [HorizontalGroup, LabelText("左"), LabelWidth(24)]
        public float Left;

        [HorizontalGroup, LabelText("右"), LabelWidth(24)]
        public float Right;

        [HorizontalGroup, LabelText("上"), LabelWidth(24)]
        public float Top;

        [HorizontalGroup, LabelText("下"), LabelWidth(24)]
        public float Bottom;
    }
}
