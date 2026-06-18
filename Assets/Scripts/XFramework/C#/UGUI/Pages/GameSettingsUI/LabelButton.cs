using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using XFramework;

public class LabelButton : UIBase,IPointerClickHandler
{
    private LocalizeStringEvent LocalizeStringEvent;
    private Action<LabelData> action;
    private Action<PhotoLabelData> photoAction;
    private Action<LocalSelectedData> localSelectedAction;
    private Image image;
    [ShowInInspector, LabelText("默认颜色")] 
    public Color NormalColor;

    [ShowInInspector, LabelText("选中颜色")] 
    public Color SelectedColor;
    
    public LabelData LabelData { get; private set; }

    public PhotoLabelData PhotoLabelData { get; private set; }

    public LocalSelectedData SelectedData { get; private set; }
    
    public bool IsSelected { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        LocalizeStringEvent = Get<LocalizeStringEvent>("Tex");
        image = Get<Image>("");
    }

    public void SetData(LabelData data,Action<LabelData> action)
    {
        this.LabelData = data;
        
        LocalizeStringEvent.StringReference.SetReference(data.LabelButtonTable,data.LabelButtonName);
        LocalizeStringEvent.RefreshString();
        
        this.action = action;
        this.photoAction = null;
        localSelectedAction = null;
    }

    public void SetData(PhotoLabelData data, Action<PhotoLabelData> action)
    {
        this.PhotoLabelData = data;
        LocalizeStringEvent.StringReference.SetReference(data.LabelButtonTable,data.LabelButtonName);
        LocalizeStringEvent.RefreshString();
        
        this.photoAction = action;
        this.action = null;
        localSelectedAction = null;
    }

    public void SetData(LocalSelectedData data, Action<LocalSelectedData> action)
    {
        this.SelectedData = data;
        localSelectedAction = action;
        LocalizeStringEvent.StringReference.SetReference(data.Table,data.Value);
        LocalizeStringEvent.RefreshString();
        this.photoAction = null;
        this.action = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        action?.Invoke(LabelData);
        photoAction?.Invoke(PhotoLabelData);
        localSelectedAction?.Invoke(SelectedData);
    }
    
    
    public void SetSelected(bool isSelect)
    {
        IsSelected = isSelect;
        image.color = IsSelected ? SelectedColor : NormalColor;
    }
}
