using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerLayout : MonoBehaviour
{
    [SerializeField] float spacing = 10f;
    [SerializeField] List<RectTransform> items;

    [Button]
    void UpdateElement()
    {
        items = new();
        items.AddRange(GetFirstLevelComponents<RectTransform>(transform));
    }
    public static List<T> GetFirstLevelComponents<T>(Transform parent) where T : Component
    {
        var list = new List<T>();
        foreach (Transform child in parent)
        {
            T comp = child.GetComponent<T>();
            if (comp != null)
            {
                list.Add(comp);
            }
        }
        return list;
    }
    public void Add(RectTransform item)
    {
        item.SetParent(transform, false);
        items.Add(item);
        RefreshLayout();
    }

    public void Remove(RectTransform item)
    {
        if (items.Remove(item))
            RefreshLayout();
    }
    [Button("Refresh Layout")]
    public void RefreshLayout()
    {
        float yOffset = 0f;
        foreach (RectTransform item in items)
        {
            if (item == null)
                continue;

            Vector2 targetPos = new(0, -yOffset);
            item.anchoredPosition = targetPos;
            yOffset += item.sizeDelta.y + spacing;
        }
        /*
        seq.Stop();
        seq = Sequence.Create();
        foreach (RectTransform item in items)
        {
            if (item == null)
                continue;

            Vector2 targetPos = new(0, -yOffset);
            seq.Group(Tween.UIAnchoredPosition(item, targetPos, moveTS));
            yOffset += item.sizeDelta.y + spacing;
        }*/
    }
    // 清除
    public void Clear()
    {
        foreach (var item in items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        items.Clear();
    }
}
