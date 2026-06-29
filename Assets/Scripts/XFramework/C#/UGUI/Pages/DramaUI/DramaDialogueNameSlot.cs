using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Localization.Components;
using XFramework;

public class DramaDialogueNameSlot : UIBase
{
    [OnValueChanged("ChangeDirection"),LabelText("输出方向")]
    public LlustrationDirection Direction;
    private RectTransform rectTransform;
    private LocalizeStringEvent contentStringEvent;
    
    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        rectTransform = Get<RectTransform>("");
        contentStringEvent = Get<LocalizeStringEvent>("Content");
    }


    public void ChangeDirection(LlustrationDirection direction)
    {
        if (rectTransform == null)
        {
            rectTransform = Get<RectTransform>("");
        }
        if (rectTransform == null) return;
        Direction = direction;
        switch (direction)
        {
            case LlustrationDirection.Left:
                rectTransform.pivot = new Vector2(0, 1f);
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(0, 1);
                rectTransform.anchoredPosition = new Vector2(0, 0);
                break;
            case LlustrationDirection.Right:
                rectTransform.pivot = new Vector2(1, 1f);
                rectTransform.anchorMin = new Vector2(1, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.anchoredPosition = new Vector2(0, 0);
                break;
            case LlustrationDirection.Crent:
                rectTransform.pivot = new Vector2(0.5f, 1f);
                rectTransform.anchorMin = new Vector2(0.5f, 1f);
                rectTransform.anchorMax = new Vector2(0.5f, 1f);
                rectTransform.anchoredPosition = new Vector2(0, 0);
                break;
        }
    }

    public void SetContent(LocalSelectedData localSelectedData)
    {
        contentStringEvent.SetText(localSelectedData.Table,localSelectedData.Value);
    }
    
    public void SetContent(string table,string value)
    {
        contentStringEvent.SetText(table,value);
    }
}
