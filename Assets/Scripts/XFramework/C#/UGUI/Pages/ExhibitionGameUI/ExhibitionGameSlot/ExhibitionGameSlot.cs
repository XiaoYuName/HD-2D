using UnityEngine;
using XFramework;

public partial class ExhibitionGameSlot : UIBase
{
    public FactoryMerchandiseItemInfo ItemInfo { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        merchandiseRuntimeSlot.Init();
    }

    public void SetData(FactoryMerchandiseItemInfo itemInfo)
    {
        this.ItemInfo = itemInfo;
        if (ItemInfo == null)
        {
            SetEmpty();
        }
        else
        {
            noneRect.gameObject.SetActive(false); 
            merchandiseRuntimeSlot.SetData(ItemInfo);
        }
    }

    public void SetColor(Color color)
    {
        
    }
    
    private void SetEmpty()
    {
       noneRect.gameObject.SetActive(true); 
    }

   
}
