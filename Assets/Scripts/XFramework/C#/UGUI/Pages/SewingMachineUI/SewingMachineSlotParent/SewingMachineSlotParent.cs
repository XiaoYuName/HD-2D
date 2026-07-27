using Sirenix.OdinInspector;
using System;
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
    private Action<SewingMachineSlotParent> scratchCompletedCallback;
    public Image Image => image;
    public bool IsScratchCompleted { get; private set; }

    public override void Init()
    {
        InitAutoBind();
        image = GetComponent<Image>();
        IsScratchCompleted = false;
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

    public bool StartScratch(
        Camera camera,
        SewingMachineSlot slot,
        float completeRatio,
        Action<SewingMachineSlotParent> completedCallback)
    {
        if (IsScratchCompleted)
        {
            return false;
        }

        if (slot == null)
        {
            return false;
        }

        scratchCompletedCallback = completedCallback;

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

        bool isStarted = ScratchImage.SetData(camera, slotMaskImage, completeRatio: completeRatio, completed: OnScratchCompleted);
        if (!isStarted)
        {
            Debug.LogWarning($"缝纫机玩法：{name} 的刮刮乐初始化失败，请检查 ScratchImage Shader/材质配置。", this);
        }

        return isStarted;
    }

    public void StopScratch()
    {
        if (ScratchImage == null)
        {
            return;
        }

        // 先结算一次再停：最后一笔可能刚好刮够，
        // 只靠"有新笔画才判定"会漏掉，表现为移开熨斗后这块布永远不算完成。
        ScratchImage.EvaluateScratchComplete();
        ScratchImage.SetScratchActive(false);
    }

    /// <summary>
    /// 当前这块布的刮开进度（0~1），已按布料的实际可刮面积归一化
    /// </summary>
    public float ScratchProgress => ScratchImage != null ? ScratchImage.ScratchProgress : 0f;

    private void OnScratchCompleted(ScratchImage scratchImage)
    {
        if (IsScratchCompleted)
        {
            return;
        }

        IsScratchCompleted = true;
        scratchCompletedCallback?.Invoke(this);
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
