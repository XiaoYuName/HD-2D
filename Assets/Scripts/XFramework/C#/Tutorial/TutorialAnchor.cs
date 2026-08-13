using Sirenix.OdinInspector;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 引导锚点：挂在"路径写不出来"的节点上（背包格子、随机列表项……），
    /// 让引导配置能用一个固定 Key 找到它。
    ///
    /// 普通界面上的固定按钮<b>不要</b>挂这个 —— 那种直接在配置里写
    /// <see cref="TutorialTargetType.UIPath"/> + 节点路径就行，界面一行代码都不用改。
    /// </summary>
    [AddComponentMenu("XFramework/Tutorial/Tutorial Anchor")]
    public class TutorialAnchor : MonoBehaviour
    {
        [LabelText("锚点Key"), Tooltip("引导步骤表 AnchorKey 列填的就是这个值")]
        [SerializeField]
        private string anchorKey;

        /// <summary>
        /// 运行时改 Key：列表项这种"第几个格子"要区分的场合，
        /// SetData 的时候顺手调一下，比在预制体上写死灵活。
        /// </summary>
        public void SetAnchorKey(string key)
        {
            if (anchorKey == key)
            {
                return;
            }

            // 换 Key 等于换身份，旧的先注销掉，不然引导会按旧 Key 找到这个已经变了内容的节点
            if (isActiveAndEnabled)
            {
                TutorialAnchorRegistry.Unregister(anchorKey, transform as RectTransform);
            }

            anchorKey = key;

            if (isActiveAndEnabled)
            {
                TutorialAnchorRegistry.Register(anchorKey, transform as RectTransform);
            }
        }

        private void OnEnable()
        {
            TutorialAnchorRegistry.Register(anchorKey, transform as RectTransform);
        }

        private void OnDisable()
        {
            TutorialAnchorRegistry.Unregister(anchorKey, transform as RectTransform);
        }
    }
}
