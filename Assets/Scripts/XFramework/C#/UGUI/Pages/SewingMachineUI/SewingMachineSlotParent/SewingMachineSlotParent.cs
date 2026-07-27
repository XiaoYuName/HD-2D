using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using XFramework;

public partial class SewingMachineSlotParent : UIBase
{
    [LabelText("类型")]
    public ParentType ParentType;

    [LabelText("吸附重叠比例")]
    [Range(0.01f, 1f)]
    public float SnapOverlapRatio = 0.2f;

    [LabelText("刮刮乐效果渲染器")]
    public ScratchImage ScratchImage;
    
    private Image image;
    public Image Image => image;

    public override void Init()
    {
        InitAutoBind();
        image = GetComponent<Image>();
        ScratchImage = GetComponent<ScratchImage>();
        if (ScratchImage == null)
        {
            ScratchImage = gameObject.AddComponent<ScratchImage>();
        }
    }

    public override void Release()
    {
        ScratchImage?.Release();
        base.Release();
    }

    public bool StartScratch(Camera camera, SewingMachineSlot slot)
    {
        if (slot == null)
        {
            return false;
        }

        if (ScratchImage == null)
        {
            ScratchImage = GetComponent<ScratchImage>();
        }

        if (ScratchImage == null)
        {
            ScratchImage = gameObject.AddComponent<ScratchImage>();
        }

        Image slotMaskImage = slot.GetComponent<Image>();
        if (slotMaskImage == null)
        {
            Debug.LogWarning($"缝纫机玩法：{slot.name} 缺少 Image，无法作为刮刮乐遮罩。", slot);
            return false;
        }

        bool isStarted = ScratchImage.SetData(camera, slotMaskImage);
        if (!isStarted)
        {
            Debug.LogWarning($"缝纫机玩法：{name} 的刮刮乐初始化失败，请检查 ScratchImage Shader/材质配置。", this);
        }

        return isStarted;
    }

    public void StopScratch()
    {
        ScratchImage?.SetScratchActive(false);
    }
    
}

public enum ParentType
{
    [LabelText("布料1")]
    Clot_01,
    [LabelText("布料2")]
    Clot_02,
    [LabelText("布料3")]
    Clot_03,
    [LabelText("布料4")]
    Clot_04,
    [LabelText("布料5")]
    Clot_05,
    [LabelText("布料6")]
    Clot_06,
    [LabelText("布料7")]
    Clot_07,
    
}
