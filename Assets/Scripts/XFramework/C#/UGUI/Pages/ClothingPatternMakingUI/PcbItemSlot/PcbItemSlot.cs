using UnityEngine;
using XFramework;

public partial class PcbItemSlot : UIBase
{
    public RectTransform Rect { get; private set; }

    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Rect = GetComponent<RectTransform>();
    }

    public void SetData(PcbSlotData data)
    {
        image.sprite = LoadAsset<Sprite>(GamePathTools.CombinationPcbIconPath(data.MaxIconName));
    }
}
