using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public class HorLayout : MonoBehaviour
{
    [SerializeField] float spacing = 10f;
    [SerializeField] List<RectTransform> items = new();

    [Button]
    void UpdateElement()
    {
        items = VerLayout.GetFirstLevelComponents<RectTransform>(transform);
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
        float xOffset = 0f;
        foreach (RectTransform item in items)
        {
            if (item == null)
                continue;

            item.anchoredPosition = new Vector2(xOffset, 0f);
            xOffset += item.sizeDelta.x + spacing;
        }
    }

    public void Clear()
    {
        foreach (RectTransform item in items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        items.Clear();
    }
}
