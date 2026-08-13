using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 引导锚点登记表：给"路径写不出来"的目标节点用 —— 背包格子、随机生成的列表项之类，
    /// 层级路径每次运行都不一样，配置表里没法写死。
    ///
    /// 配置走 <see cref="TutorialTargetType.UIPath"/> 是首选（业务界面零改动）；
    /// 只有动态节点才在预制体上挂一个 <see cref="TutorialAnchor"/>，这里是它的落点。
    /// </summary>
    public static class TutorialAnchorRegistry
    {
        /// <summary>
        /// 一个 Key 允许对应多个节点：对象池里的格子会反复启停，
        /// 同一帧里旧的还没注销、新的已经注册上来是正常状态，取的时候挑当前有效的那个。
        /// </summary>
        private static readonly Dictionary<string, List<RectTransform>> anchors =
            new Dictionary<string, List<RectTransform>>();

        public static void Register(string anchorKey, RectTransform target)
        {
            if (string.IsNullOrEmpty(anchorKey) || target == null)
            {
                return;
            }

            if (!anchors.TryGetValue(anchorKey, out List<RectTransform> list))
            {
                list = new List<RectTransform>();
                anchors[anchorKey] = list;
            }

            if (!list.Contains(target))
            {
                list.Add(target);
            }
        }

        public static void Unregister(string anchorKey, RectTransform target)
        {
            if (string.IsNullOrEmpty(anchorKey) || target == null)
            {
                return;
            }

            if (anchors.TryGetValue(anchorKey, out List<RectTransform> list))
            {
                list.Remove(target);
            }
        }

        /// <summary>
        /// 取这个 Key 当前可用的节点：只认还活着、并且在场景里显示着的，
        /// 被回收进对象池（关掉）的格子不能当引导目标。
        /// </summary>
        public static bool TryGet(string anchorKey, out RectTransform target)
        {
            target = null;

            if (string.IsNullOrEmpty(anchorKey) || !anchors.TryGetValue(anchorKey, out List<RectTransform> list))
            {
                return false;
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                RectTransform candidate = list[i];
                if (candidate == null)
                {
                    list.RemoveAt(i);
                    continue;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    target = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>切场景 / 退到主菜单时清干净，免得留着一堆已销毁节点。</summary>
        public static void Clear()
        {
            anchors.Clear();
        }
    }
}
