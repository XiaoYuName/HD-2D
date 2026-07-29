using XFramework;

public partial class MoldSlot : UIBase
{
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
    }

    public void SetData(ClothingPaintTubeMoldData  data)
    {
        gameIcon.sprite = data.GameIcon;
        gameMask.sprite = data.IconMask;
        gameIcon.SetNativeSize();
        gameMask.SetNativeSize();
        gameMask.fillAmount = 0;
    }
}
