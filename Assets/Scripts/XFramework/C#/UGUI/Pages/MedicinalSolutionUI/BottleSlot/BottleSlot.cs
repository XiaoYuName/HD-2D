using UnityEngine;
using XFramework;

public partial class BottleSlot : UIBase
{
    /// <summary>瓶子里还剩多少颜料 0~1</summary>
    public float Fill => mask.fillAmount;

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingPaintTubeColorData tubeColorData)
    {
        mask.color = tubeColorData.Color;
        // 每次重新拿出来的颜料瓶都是满的
        mask.fillAmount = 1f;
    }

    /// <summary>倒出去的时候由 MedicinalSolutionUI 驱动</summary>
    public void SetFill(float value)
    {
        mask.fillAmount = Mathf.Clamp01(value);
    }
}
