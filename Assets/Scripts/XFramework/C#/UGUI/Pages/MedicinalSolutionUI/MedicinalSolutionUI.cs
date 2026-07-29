using System.Collections.Generic;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

public partial class MedicinalSolutionUI : UIBase
{
    [TitleGroup("灌注配置")]
    [LabelText("模具容量(ml)"),Tooltip("模具灌满(fillAmount=1)时相当于多少毫升")]
    public int MoldCapacityMl = 500;
    [LabelText("灌注速度(ml/秒)"),Tooltip("按住模具时每秒灌进去多少,决定玩家的操作手感")]
    public float PourRateMl = 200f;
    [LabelText("颜料瓶倾倒角度")]
    public float PourAngle = -65f;
    [LabelText("倾倒/回位耗时")]
    public float TiltDuration = 0.2f;
    [LabelText("毫升误差范围"),Tooltip("灌注结束时和配方要求相差在这个范围内就算达标")]
    public int MlTolerance = 20;

    private MedicinalSolutionSettingData Setting;

    /// <summary>本次随机到的配方需求</summary>
    public MedicinalSolutionData CurrentData { get; private set; }
    /// <summary>当前选中的模具(单选)</summary>
    public PaintTubeMoldSlot SelectedMoldSlot { get; private set; }
    /// <summary>当前选中的颜料(多选),按点击先后顺序排列</summary>
    public List<PaintTubeColorSlot> SelectedColorSlots { get; } = new List<PaintTubeColorSlot>();

    /// <summary>已经灌进模具的毫升数</summary>
    public int PouredMl => Mathf.RoundToInt(moldSlot.Fill * MoldCapacityMl);
    /// <summary>本次配方要求的毫升数</summary>
    public int RequiredMl => CurrentData?.Ml ?? 0;
    /// <summary>灌进去的量是否落在配方要求的误差范围内</summary>
    public bool IsMlSatisfied => CurrentData != null && Mathf.Abs(PouredMl - RequiredMl) <= MlTolerance;
    /// <summary>手正按在模具上灌注中</summary>
    public bool IsPouring => isPouring;

    // 按住期间一直跑的灌注动画,松手就 Stop 掉停在当前液面
    private Sequence pourSequence;
    // 颜料瓶的倾倒/回位
    private Sequence tiltSequence;
    private bool isPouring;

    // 复用,避免每次灌注都产生垃圾
    private readonly List<BottleSlot> pourBottleList = new List<BottleSlot>();
    private readonly List<Color> pourColorList = new List<Color>();

    /// <summary>每秒能灌进去多少(换算成 fillAmount)</summary>
    private float PourRateFill => MoldCapacityMl > 0 ? PourRateMl / MoldCapacityMl : 0f;

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
        moldSlot.OnPressDown.RemoveAllListeners();
        moldSlot.OnPressDown.AddListener(OnPressDownMold);
        moldSlot.OnPressUp.RemoveAllListeners();
        moldSlot.OnPressUp.AddListener(OnPressUpMold);
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
        ResetPourProgress();

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

        // 换了模具就当重新调配
        ResetPourProgress();
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
            ResetPourProgress();
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

        // 换了颜料就当重新调配
        ResetPourProgress();
        RefreshBottleSlots();
    }

    /// <summary>
    /// 把这一局的灌注进度清空:动画掐掉、瓶子摆正灌满、模具清空
    /// </summary>
    private void ResetPourProgress()
    {
        isPouring = false;
        pourSequence.Stop();
        tiltSequence.Stop();

        moldSlot.SetFill(0f, 0);
        foreach (var bottle in bottleSlotList)
        {
            bottle.transform.localRotation = Quaternion.identity;
            bottle.SetFill(1f);
        }
    }

    /// <summary>
    /// 按住桌上的模具就一直灌。可以灌超,灌多少由玩家自己把握
    /// </summary>
    private void OnPressDownMold(MoldSlot slot)
    {
        if (isPouring || CurrentData == null)
        {
            return;
        }

        if (SelectedMoldSlot == null || SelectedColorSlots.Count == 0)
        {
            Debug.Log("要先选好模具和颜料才能开始灌注");
            return;
        }

        CollectPourTargets();
        if (pourBottleList.Count == 0)
        {
            return;
        }

        float from = moldSlot.Fill;
        if (from >= 1f)
        {
            Debug.Log("模具已经满了,灌不下了");
            return;
        }

        if (PourRateFill <= 0f)
        {
            Debug.LogError("灌注速度或模具容量配置为 0,灌不动");
            return;
        }

        isPouring = true;

        // 选中的颜料瓶一起倾过来
        tiltSequence.Stop();
        tiltSequence = TiltBottles(PourAngle);
        // 多种颜料一起倒,模具里就是它们混出来的颜色
        moldSlot.SetFillColor(MixPourColor());

        // 一路灌到满,松手时 Stop 掉就停在当前液面
        pourSequence.Stop();
        pourSequence = Sequence.Create()
            .Chain(Tween.Custom(this, from, 1f, (1f - from) / PourRateFill, (self, value) =>
            {
                self.moldSlot.SetFill(value, Mathf.RoundToInt(value * self.MoldCapacityMl));
                self.SyncBottleFill();
            }, Ease.Linear))
            .ChainCallback(this, self => self.EndPour("模具已满"));
    }

    /// <summary>松手停止灌注</summary>
    private void OnPressUpMold(MoldSlot slot)
    {
        if (!isPouring)
        {
            return;
        }

        pourSequence.Stop();
        EndPour("松手");
    }

    /// <summary>灌注结束:颜料瓶摆回去,然后判定这次灌得对不对</summary>
    private void EndPour(string reason)
    {
        isPouring = false;
        tiltSequence.Stop();
        tiltSequence = TiltBottles(0f);

        Debug.Log($"{reason}:已灌 {PouredMl}ml / 需要 {RequiredMl}ml(允许误差 ±{MlTolerance}ml)");

        if (IsMlSatisfied)
        {
            OnPourSucceed();
        }
        else
        {
            OnPourFailed();
        }
    }

    /// <summary>
    /// 灌注量达标。后续逻辑(结算/奖励/播放成功表现)写在这里。
    /// 注意配方本身对不对要看 IsSelectionMatched(),这里只保证毫升数达标
    /// </summary>
    private void OnPourSucceed()
    {
        Debug.Log($"灌注达标:{PouredMl}ml,配方{(IsSelectionMatched() ? "正确" : "错误")}");
    }

    /// <summary>
    /// 灌注量不达标。后续逻辑(提示重来/扣次数/播放失败表现)写在这里。
    /// 多了还是少了直接比 PouredMl 和 RequiredMl 就行
    /// </summary>
    private void OnPourFailed()
    {
        string detail = PouredMl > RequiredMl ? "灌多了" : "灌少了";
        Debug.Log($"灌注不达标({detail}):{PouredMl}ml / 需要 {RequiredMl}ml");
    }

    /// <summary>
    /// 一满瓶 = 一满模具,所以瓶里剩的就是模具还没灌满的那部分。
    /// 两瓶一起倒时两瓶都按同样的速度减少(只是表现,不影响模具灌进去的量)
    /// </summary>
    private void SyncBottleFill()
    {
        float remain = 1f - moldSlot.Fill;
        foreach (var bottle in pourBottleList)
        {
            bottle.SetFill(remain);
        }
    }

    /// <summary>
    /// 选中的颜料瓶一起倾到指定角度。
    /// 在模具左边的往右倾、在右边的往左倾,瓶口始终朝着模具,
    /// 这样美术挪动瓶子的摆放位置也不用改代码
    /// </summary>
    private Sequence TiltBottles(float angle)
    {
        var sequence = Sequence.Create();
        float moldX = moldSlot.transform.position.x;
        foreach (var bottle in pourBottleList)
        {
            float signedAngle = bottle.transform.position.x <= moldX ? angle : -angle;
            sequence = sequence.Group(Tween.LocalRotation(bottle.transform,
                Quaternion.Euler(0f, 0f, signedAngle), TiltDuration, Ease.OutQuad));
        }
        return sequence;
    }

    /// <summary>
    /// 按选中顺序收集这次要倒的瓶子和它们的颜色。
    /// SelectedColorSlots[i] 对应 bottleSlotList[i](RefreshBottleSlots 就是这么摆的)
    /// </summary>
    private void CollectPourTargets()
    {
        pourBottleList.Clear();
        pourColorList.Clear();

        for (int i = 0; i < bottleSlotList.Count && i < SelectedColorSlots.Count; i++)
        {
            pourBottleList.Add(bottleSlotList[i]);
            pourColorList.Add(Setting.GetColorValue(SelectedColorSlots[i].Type));
        }
    }

    /// <summary>选中的颜料混出来的颜色(直接取平均)</summary>
    private Color MixPourColor()
    {
        if (pourColorList.Count == 0)
        {
            return Color.white;
        }

        Color mix = pourColorList[0];
        for (int i = 1; i < pourColorList.Count; i++)
        {
            mix += pourColorList[i];
        }
        return mix / pourColorList.Count;
    }

    /// <summary>
    /// 当前选择是否与配方要求完全一致(只看模具和颜料,不含毫升)
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
