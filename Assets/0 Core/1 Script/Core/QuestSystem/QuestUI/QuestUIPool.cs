using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 「按模板克隆一列」这件事在面板里要做四遍（类别页签、任务卡、目标行、奖励图标），收在这里。
    ///
    /// 模板是预制体里自带的一个隐藏子物体，不走 Addressables —— 面板的子项只有这个面板用，
    /// 单独拆成 AA 资源反而多三个 key 要维护。
    /// </summary>
    public class QuestUIPool<T> where T : Component
    {
        readonly T template;
        readonly Transform parent;
        readonly List<T> items = new();

        public IReadOnlyList<T> Items => items;

        public QuestUIPool(T template)
        {
            this.template = template;
            parent = template.transform.parent;
            template.gameObject.SetActive(false);
        }

        /// <summary>克隆到指定条数，多的隐藏起来留着复用。</summary>
        public void Resize(int count)
        {
            while (items.Count < count)
            {
                T item = Object.Instantiate(template, parent);
                item.gameObject.SetActive(true);
                items.Add(item);
            }

            for (int i = 0; i < items.Count; i++) items[i].gameObject.SetActive(i < count);
        }

        public void Clear() => Resize(0);
    }
}
