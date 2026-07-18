using UnityEngine;
using XFramework;

public partial class FlySlot : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(Color color,int index)
    {
        flySlot.color = color;
        nameString.SetVar("value",index);
    }

    public void SetData(FlyItemSlotData flySlotData)
    {
        flySlot.color = flySlotData.Color;
        nameString.SetVar("value",flySlotData.Index);
    }
}
