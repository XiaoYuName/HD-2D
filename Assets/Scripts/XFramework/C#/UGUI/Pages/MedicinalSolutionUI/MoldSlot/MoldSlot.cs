using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using XFramework;

public partial class MoldSlot : UIBase,IPointerDownHandler,IPointerUpHandler
{
    /// <summary>按下开始灌注</summary>
    public UnityEvent<MoldSlot> OnPressDown = new UnityEvent<MoldSlot>();
    /// <summary>松手停止灌注</summary>
    public UnityEvent<MoldSlot> OnPressUp = new UnityEvent<MoldSlot>();

    /// <summary>已经灌进去多少 0~1</summary>
    public float Fill => gameMask.fillAmount;
    /// <summary>灌进去的颜料颜色</summary>
    public Color FillColor => gameMask.color;

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
        SetMlText(0);
    }

    /// <summary>
    /// 灌注的时候由 MedicinalSolutionUI 驱动。
    /// 毫升数由外面算好传进来,模具自己不需要知道容量是多少
    /// </summary>
    public void SetFill(float value, int ml)
    {
        gameMask.fillAmount = Mathf.Clamp01(value);
        SetMlText(ml);
    }

    private void SetMlText(int ml)
    {
        mlText.SetText($"{ml}ml");
    }

    public void SetFillColor(Color color)
    {
        gameMask.color = color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnPressDown?.Invoke(this);
    }

    // 手指移出去之后再松开也会收到,所以不会出现一直灌的情况
    public void OnPointerUp(PointerEventData eventData)
    {
        OnPressUp?.Invoke(this);
    }
}
