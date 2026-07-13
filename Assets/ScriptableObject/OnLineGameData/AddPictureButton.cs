using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public class AddPictureButton : UIBase,IPointerClickHandler
{
    public UnityEvent OnClick;

    private RawImage rawImage;

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        rawImage = Get<RawImage>("RawImage");
    }

    public void Release()
    {
        
    }

    public void SetData(ItemInfo itemInfo)
    {
        
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
