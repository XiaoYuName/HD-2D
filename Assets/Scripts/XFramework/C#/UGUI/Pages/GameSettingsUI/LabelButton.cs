using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using XFramework;

public class LabelButton : UIBase,IPointerClickHandler
{
    private LocalizeStringEvent LocalizeStringEvent;
    private Action<LabelData> action;
    
    public LabelData LabelData { get; private set; }
    
    public bool IsSelected { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        LocalizeStringEvent = Get<LocalizeStringEvent>("Tex");
    }

    public void SetData(LabelData data,Action<LabelData> action)
    {
        this.LabelData = data;
        LocalizeStringEvent.SetEntry(data.LabelButtonName);
        this.action = action;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        action?.Invoke(LabelData);
    }
    
    
    public void SetSelected(bool isSelect)
    {
        IsSelected = isSelect;
    }
}
