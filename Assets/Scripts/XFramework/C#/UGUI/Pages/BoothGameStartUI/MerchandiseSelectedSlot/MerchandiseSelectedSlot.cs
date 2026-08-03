using System;
using XFramework;

public partial class MerchandiseSelectedSlot : UIBase
{
    private Action<MerchandiseSelectedSlot> OnReleased;

    public FactoryMerchandiseItemInfo CurrentData { get; private set; }

    public override void Init()
    {
        InitAutoBind();
        
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        merchandiseRuntimeSlot.Init();
    }

    public override void Release()
    {
        merchandiseRuntimeSlot.Release();
        OnReleased = null;
        base.Release();
    }

    public void SetData(FactoryMerchandiseItemInfo itemInfo,Action<MerchandiseSelectedSlot> onReleased)
    {
        this.CurrentData = itemInfo;
        nameSlot.text = $"{itemInfo.GetName()} X{itemInfo.Count}";
        merchandiseRuntimeSlot.SetData(itemInfo);
        this.OnReleased = onReleased;
        subButton.OnLongPress.RemoveAllListeners();
        subButton.OnLongPress.AddListener(Released);
    }
    
    private void Released()
    {
        OnReleased?.Invoke(this);
    }
}
