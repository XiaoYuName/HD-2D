using System;
using UnityEngine.EventSystems;
using XFramework;

public partial class MerchandiseSlot : UIBase
{
    private Action<MerchandiseSlot> OnSelected;
    private Action<MerchandiseSlot> OnReleased;
    
    public override void Init()
    {
        InitAutoBind();
        
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        merchandiseRuntimeSlot.Init();
    }

    public void Release()
    {
        merchandiseRuntimeSlot.Release();
        SetSelected(false);
    }

    public void SetData(FactoryMerchandiseItemInfo itemInfo,Action<MerchandiseSlot> onSelected,Action<MerchandiseSlot> onReleased)
    {
        nameSlot.text = itemInfo.GetName();
        merchandiseRuntimeSlot.SetData(itemInfo);
        this.OnSelected = onSelected;
        this.OnReleased = onReleased;
        
        selectedImg.OnLongPress.RemoveAllListeners();
        selectedImg.OnLongPress.AddListener(Pressed);
        subButton.OnLongPress.RemoveAllListeners();
        subButton.OnLongPress.AddListener(Released);
    }

    public void SetSelectedNumber(int number, int maxVal)
    {
        selectedNumberVal.text = $"{number}/{maxVal}";
    }

    private void Pressed()
    {
        OnSelected?.Invoke(this);
    }

    private void Released()
    {
        OnReleased?.Invoke(this);
    }

    public void SetSelected(bool selected)
    {
        selectedImg.targetGraphic.enabled = selected;
        subButton.gameObject.SetActive(selected);
        selectedNumber.gameObject.SetActive(selected);
    }
}
