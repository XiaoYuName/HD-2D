using UnityEngine;
using XFramework;

public partial class EditorButtonGroups : UIBase
{
    public CanvasGroup CanvasGroup { get; private set; }
    public RectTransform Rect { get; private set; }

    private PcbItemSlot pcbItemSlot;
    
    public override void Init()
    {
        InitAutoBind();
        CanvasGroup = GetComponent<CanvasGroup>();
        Rect = GetComponent<RectTransform>();
        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(rotationButton,RotationClick,"");
        Bind(deleteButton,DeleteClick,"");
    }

    public void SetData(PcbItemSlot pcbSlotData)
    {
        pcbItemSlot = pcbSlotData;
    }

    private void RotationClick()
    {
        if (pcbItemSlot != null)
        {
            
        }
    }

    private void DeleteClick()
    {
        if (pcbItemSlot != null)
        {
            
        }
    }

}
