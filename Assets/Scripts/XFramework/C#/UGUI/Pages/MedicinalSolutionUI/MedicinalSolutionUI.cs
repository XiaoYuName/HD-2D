using System.Collections.Generic;
using UnityEngine;
using XFramework;

public partial class MedicinalSolutionUI : UIBase
{
    private MedicinalSolutionSettingData Setting;

    /// <summary>本次随机到的配方需求</summary>
    public MedicinalSolutionData CurrentData { get; private set; }
    /// <summary>当前选中的模具(单选)</summary>
    public PaintTubeMoldSlot SelectedMoldSlot { get; private set; }
    /// <summary>当前选中的颜料(多选),按点击先后顺序排列</summary>
    public List<PaintTubeColorSlot> SelectedColorSlots { get; } = new List<PaintTubeColorSlot>();

    /// <summary>
    /// 本次配方最多能选几种颜料。
    /// 还要受瓶子数量限制,不然多选出来的颜料没有瓶子能显示,状态和界面就不一致了
    /// </summary>
    private int MaxColorCount => Mathf.Min(CurrentData?.PaintTubeColorList?.Count ?? 0, bottleSlotList.Count);

    public override void Init()
    {
        InitAutoBind();

        foreach (var slot in paintTubeColorSlotList)
        {
            slot.Init();
            slot.OnSelect.RemoveAllListeners();
            slot.OnSelect.AddListener(OnClickColorSlot);
        }

        foreach (var slot in paintTubeMoldSlotList)
        {
            slot.Init();
            slot.OnSelect.RemoveAllListeners();
            slot.OnSelect.AddListener(OnClickMoldSlot);
        }
        foreach (var slot in bottleSlotList)
        {
            slot.Init();
            slot.gameObject.SetActive(false);
        }
        moldSlot.Init();
        moldSlot.gameObject.SetActive(false);
        medicinalSolutionGameDataInfoUI.Init();

        // 在这里写其它初始化逻辑。重新生成 UI 绑定时，这个文件不会被覆盖。
        Bind(btnTuichu,Close,"");
    }

    /// <summary>
    /// 通用UI打开方法,提供重写
    /// </summary>
    public override void Open()
    {
        base.Open();
        Setting = LoadAsset<MedicinalSolutionSettingData>(AssetKeys.MedicinalSolutionSettingDataPath);
        RefreshRandomSolution();
    }

    /// <summary>
    /// 通用UI关闭方法,提供重写
    /// </summary>
    public override void Close()
    {
        base.Close();
        ClearSelected();
    }

    /// <summary>
    /// 随机一条配方作为本次需求,并刷新左侧配方步骤提示
    /// </summary>
    private void RefreshRandomSolution()
    {
        ClearSelected();

        if (Setting == null || Setting.MiniGameSolutionList == null || Setting.MiniGameSolutionList.Count == 0)
        {
            CurrentData = null;
            Debug.LogError("药水配置为空,无法开始游戏");
            return;
        }

        CurrentData = Setting.MiniGameSolutionList[Random.Range(0, Setting.MiniGameSolutionList.Count)];
        medicinalSolutionGameDataInfoUI.SetData(CurrentData, Setting);
    }

    private void ClearSelected()
    {
        if (SelectedMoldSlot != null)
        {
            SelectedMoldSlot.SetSelected(false);
            SelectedMoldSlot = null;
        }
        RefreshMoldSlot();

        foreach (var slot in SelectedColorSlots)
        {
            slot.SetSelected(false);
        }
        SelectedColorSlots.Clear();
        RefreshBottleSlots();
    }

    /// <summary>
    /// 按当前选中的颜料刷新桌上的瓶子:选了几种就显示几个瓶子,多余的隐藏。
    /// 选中/取消/顶掉最早那个 都走这里,不用各自单独维护瓶子的显隐
    /// </summary>
    private void RefreshBottleSlots()
    {
        for (int i = 0; i < bottleSlotList.Count; i++)
        {
            var bottle = bottleSlotList[i];
            var colorData = i < SelectedColorSlots.Count && Setting != null
                ? Setting.GetColorData(SelectedColorSlots[i].Type)
                : null;

            if (colorData == null)
            {
                bottle.gameObject.SetActive(false);
                continue;
            }

            bottle.SetData(colorData);
            bottle.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 模具单选:点已选中的就取消,点别的就把选中态换过去
    /// </summary>
    private void OnClickMoldSlot(PaintTubeMoldSlot slot)
    {
        if (SelectedMoldSlot != null)
        {
            SelectedMoldSlot.SetSelected(false);
        }

        // 点的还是已经选中的那个 -> 变成没选中
        SelectedMoldSlot = SelectedMoldSlot == slot ? null : slot;
        SelectedMoldSlot?.SetSelected(true);
        RefreshMoldSlot();
    }

    /// <summary>
    /// 按当前选中的模具刷新桌上的模具:选中就显示对应模具,没选中就隐藏
    /// </summary>
    private void RefreshMoldSlot()
    {
        var moldData = SelectedMoldSlot != null && Setting != null
            ? Setting.GetMoldData(SelectedMoldSlot.Type)
            : null;

        if (moldData == null)
        {
            moldSlot.gameObject.SetActive(false);
            return;
        }

        moldSlot.SetData(moldData);
        moldSlot.gameObject.SetActive(true);
    }

    /// <summary>
    /// 颜料多选:上限为本次配方需要的颜料数量,点已选中的就取消
    /// </summary>
    private void OnClickColorSlot(PaintTubeColorSlot slot)
    {
        // 已经选中了 -> 取消选中,对应的瓶子也跟着收掉
        if (SelectedColorSlots.Remove(slot))
        {
            slot.SetSelected(false);
            RefreshBottleSlots();
            return;
        }

        if (MaxColorCount <= 0)
        {
            return;
        }

        // 选满之后再点新的,顶掉最早选中的那个,玩家不用先手动取消
        while (SelectedColorSlots.Count >= MaxColorCount)
        {
            var oldest = SelectedColorSlots[0];
            SelectedColorSlots.RemoveAt(0);
            oldest.SetSelected(false);
        }

        SelectedColorSlots.Add(slot);
        slot.SetSelected(true);
        RefreshBottleSlots();
    }

    /// <summary>
    /// 当前选择是否与配方要求完全一致(留给后面的调配流程用)
    /// </summary>
    public bool IsSelectionMatched()
    {
        if (CurrentData == null || SelectedMoldSlot == null)
        {
            return false;
        }

        if (SelectedMoldSlot.Type != CurrentData.MoldClass)
        {
            return false;
        }

        var needList = CurrentData.PaintTubeColorList;
        if (needList == null || SelectedColorSlots.Count != needList.Count)
        {
            return false;
        }

        foreach (var type in needList)
        {
            if (!SelectedColorSlots.Exists(item => item.Type == type))
            {
                return false;
            }
        }

        return true;
    }
}
