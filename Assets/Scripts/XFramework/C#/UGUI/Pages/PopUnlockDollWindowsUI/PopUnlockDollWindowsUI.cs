using System.Collections.Generic;
using XFramework;

public partial class PopUnlockDollWindowsUI : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    private List<long> itemIDs = new List<long>();


    public void AddItemInfo(long itemInfo)
    {
        itemIDs.Add(itemInfo);
    }
    
    
}
