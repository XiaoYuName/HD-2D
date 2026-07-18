using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public partial class ExhibitionGameUI : UIBase
{
    /// <summary>
    /// 当前对局数据的副本/范例
    /// </summary>
    private List<FactoryMerchandiseItemInfo> ExhibitionItems;
    [LabelText("周边槽位")]
    public List<ExhibitionGameSlot>  ExhibitionSlots;
    [LabelText("周边主体色")]
    public List<Color> SlotColors;
    
    public override void Init()
    {
        InitAutoBind();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        foreach (var slot in ExhibitionSlots)
        {
            slot.Init();
        }
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate += UpdateGameTimer;
        ExhibitionItems = new List<FactoryMerchandiseItemInfo>(ExhibitionManager.Instance.OnSelectedFactory);
        ApplyItem();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ExhibitionManager.Instance.ExhibitionGameTimerUpdate -= UpdateGameTimer;
    }


    private void UpdateGameTimer(float GameTime)
    {
        timeVal.text = $"{GameTime}s";
    }


    private void ApplyItem()
    {
        for (int i = 0; i < ExhibitionSlots.Count; i++)
        {
            if (i < SlotColors.Count - 1)
            {
                ExhibitionSlots[i].SetColor(SlotColors[i]);
            }
            else
            {
                ExhibitionSlots[i].SetColor(SlotColors[0]);
            }

            if (i < ExhibitionItems.Count - 1)
            {
                ExhibitionSlots[i].SetData(ExhibitionItems[i]);
            }
            else
            {
                ExhibitionSlots[i].SetData(null);
            }
        }
    }
}
