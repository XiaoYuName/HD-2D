using System;
using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

public class PhotoSlotUI : UIBase,IPointerClickHandler
{
    private RawImage _rawImage;
    private Action<PhotoSlotUI> _action;
    private UIEffect _effect;

    public ActionCGData currentData { get; private set; }
    public bool Selected { get; private set; }

    /// <summary>
    /// 初始化方法,一般不需要手动调用
    /// </summary>
    public override void Init()
    {
        _rawImage = Get<RawImage>("Sprite_CG");
        _effect = Get<UIEffect>("");
    }

    public void SetData(ActionCGData cgData,Action<PhotoSlotUI> action)
    {
        currentData = cgData;
        _rawImage.texture = AssetsManager.Instance.LoadAssets<Sprite>(cgData.minSpritePath).texture;
        this._action = action;
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("OnPointerClick");
        _action?.Invoke(this);
    }

    public void SetSelected(bool isSelected)
    {
        Selected = isSelected;
        if (isSelected)
        {
            _effect.edgeMode = EdgeMode.Plain;
        }
        else
        {
            _effect.edgeMode = EdgeMode.None;
        }
    }
}
