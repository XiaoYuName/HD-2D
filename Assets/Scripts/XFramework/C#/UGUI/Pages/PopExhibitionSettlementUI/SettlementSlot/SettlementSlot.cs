using XFramework;

public partial class SettlementSlot : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        merchandiseRuntimeSlot.Init();
    }

    public void Release()
    {
        merchandiseRuntimeSlot.Release();
        
    }

    public void SetData(FactoryMerchandiseItemInfo itemInfo)
    {
        merchandiseRuntimeSlot.SetData(itemInfo);
        coinVal.SetVar("value",itemInfo.GetValue() * itemInfo.Count);
        countVal.SetVar("value",itemInfo.Count);
    }
}
