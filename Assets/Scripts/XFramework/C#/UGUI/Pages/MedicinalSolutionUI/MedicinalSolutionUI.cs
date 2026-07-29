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
    [LabelText("每次灌入(ml)"),Tooltip("按住时每个间隔灌进去多少。灌进去的量永远是它的整数倍,玩家才好停在准确的刻度上")]
    public int PourStepMl = 10;
    [LabelText("灌注间隔(秒)"),Tooltip("按住时每隔多久灌一次")]
    public float PourInterval = 0.12f;
    [LabelText("颜料瓶倾倒角度")]
    public float PourAngle = -65f;
    [LabelText("倾倒/回位耗时")]
    public float TiltDuration = 0.2f;
    [LabelText("毫升误差范围"),Tooltip("灌注结束时和配方要求相差在这个范围内就算达标。步进是 10ml 的话这里填 0 也能精确达成")]
    public int MlTolerance = 10;

    private MedicinalSolutionSettingData Setting;

    /// <summary>本次随机到的配方需求</summary>
    public MedicinalSolutionData CurrentData { get; private set; }
    /// <summary>当前选中的模具(单选)</summary>
    public PaintTubeMoldSlot SelectedMoldSlot { get; private set; }
    /// <summary>当前选中的颜料(多选),按点击先后顺序排列</summary>
    public List<PaintTubeColorSlot> SelectedColorSlots { get; } = new List<PaintTubeColorSlot>();

    /// <summary>已经灌进模具的毫升数。它是本体,模具的 fillAmount 是由它换算出来的</summary>
    public int PouredMl => pouredMl;
    /// <summary>本次配方要求的毫升数</summary>
    public int RequiredMl => CurrentData?.Ml ?? 0;
    /// <summary>灌进去的量是否落在配方要求的误差范围内</summary>
    public bool IsMlSatisfied => CurrentData != null && Mathf.Abs(PouredMl - RequiredMl) <= MlTolerance;
    /// <summary>灌超了,已经救不回来了</summary>
    public bool IsMlOverflow => CurrentData != null && PouredMl > RequiredMl + MlTolerance;
    /// <summary>手正按在模具上灌注中</summary>
    public bool IsPouring => isPouring;

    // 按住期间的下一次灌注,松手就 Stop 掉
    private Tween pourTickTween;
    // 颜料瓶的倾倒/回位
    private Sequence tiltSequence;
    private bool isPouring;
    private int pouredMl;

    // 复用,避免每次灌注都产生垃圾
    private readonly List<BottleSlot> pourBottleList = new List<BottleSlot>();
    private readonly List<Color> pourColorList = new List<Color>();

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

    private CharacterBag CurrentBag;
    private ClothingBag ClothingBag;
    
    public void SetData(CharacterBag characterBag, ClothingBag clothingBag)
    {
        this.CurrentBag = characterBag;
        this.ClothingBag = clothingBag;
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
        pouredMl = 0;
        pourTickTween.Stop();
        tiltSequence.Stop();

        moldSlot.SetFill(0f, 0);
        foreach (var bottle in bottleSlotList)
        {
            bottle.transform.localRotation = Quaternion.identity;
            bottle.SetFill(1f);
        }
    }

    /// <summary>
    /// 按住桌上的模具就每隔 PourInterval 灌 PourStepMl。
    /// 用固定步进而不是匀速连续,灌进去的量永远是 PourStepMl 的整数倍,玩家才停得准
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

        if (pouredMl >= MoldCapacityMl)
        {
            Debug.Log("模具已经满了,灌不下了");
            return;
        }

        if (PourStepMl <= 0 || MoldCapacityMl <= 0)
        {
            Debug.LogError("灌注步进或模具容量配置为 0,灌不动");
            return;
        }

        isPouring = true;

        // 选中的颜料瓶一起倾过来
        tiltSequence.Stop();
        tiltSequence = TiltBottles(PourAngle);
        // 多种颜料一起倒,模具里就是它们混出来的颜色
        moldSlot.SetFillColor(MixPourColor());

        // 按下就先灌一下,不用干等第一个间隔
        PourOneStep();
    }

    /// <summary>松手停止灌注</summary>
    private void OnPressUpMold(MoldSlot slot)
    {
        if (!isPouring)
        {
            return;
        }

        EndPour("松手");
    }

    /// <summary>
    /// 灌一格。灌超了直接判失败,灌满了就结束,否则排下一格
    /// </summary>
    private void PourOneStep()
    {
        pouredMl = Mathf.Min(pouredMl + PourStepMl, MoldCapacityMl);
        ApplyPouredMl();

        // 超过要求的量就当场失败,不用等松手
        if (IsMlOverflow)
        {
            StopPour();
            OnPourFailed();
            return;
        }

        if (pouredMl >= MoldCapacityMl)
        {
            EndPour("模具已满");
            return;
        }

        pourTickTween.Stop();
        pourTickTween = Tween.Delay(this, Mathf.Max(PourInterval, 0.01f), self =>
        {
            if (self.isPouring)
            {
                self.PourOneStep();
            }
        });
    }

    /// <summary>
    /// 把毫升数换算成模具和颜料瓶的填充表现。
    /// 一满瓶 = 一满模具,所以瓶里剩的就是模具还没灌满的那部分;
    /// 两瓶一起倒时两瓶都按同样的速度减少(只是表现,不影响模具灌进去的量)
    /// </summary>
    private void ApplyPouredMl()
    {
        float fill = MoldCapacityMl > 0 ? Mathf.Clamp01((float)pouredMl / MoldCapacityMl) : 0f;
        moldSlot.SetFill(fill, pouredMl);

        float remain = 1f - fill;
        foreach (var bottle in pourBottleList)
        {
            bottle.SetFill(remain);
        }
    }

    /// <summary>停止灌注:掐掉下一格的计时,颜料瓶摆回去</summary>
    private void StopPour()
    {
        isPouring = false;
        pourTickTween.Stop();
        tiltSequence.Stop();
        tiltSequence = TiltBottles(0f);
    }

    /// <summary>
    /// 灌注结束(松手或模具满了)。
    /// 只有达标才结算,没灌够什么都不做 —— 玩家可以接着按住继续灌,
    /// 失败只在灌超的时候判(见 PourOneStep)
    /// </summary>
    private void EndPour(string reason)
    {
        StopPour();

        Debug.Log($"{reason}:已灌 {PouredMl}ml / 需要 {RequiredMl}ml(允许误差 ±{MlTolerance}ml)");

        if (IsMlSatisfied)
        {
            OnPourSucceed();
        }
    }

    /// <summary>
    /// 灌注量达标。后续逻辑(结算/奖励/播放成功表现)写在这里。
    /// 注意配方本身对不对要看 IsSelectionMatched(),这里只保证毫升数达标
    /// </summary>
    private void OnPourSucceed()
    {
        Debug.Log($"灌注达标:{PouredMl}ml,配方{(IsSelectionMatched() ? "正确" : "错误")}");
        if (IsSelectionMatched())
        {
            CharacterManager.Instance.ClothingUlock(CurrentBag.CharacterID,ClothingBag.clothingID);
            UIUtility.PopCompleteWindow(Close);
            var ui = UISystem.Instance.GetUI<GarmentMakingUI>("GarmentMakingUI");
            if (ui != null)
            {
                ui.OptionClothing();
            }
        }
        else
        {
            // 量灌对了但配方选错了,一样算失败
            ShowFailWindow();
        }
    }

    /// <summary>
    /// 灌超了,倒进去的颜料收不回来所以当场失败。后续逻辑(扣次数/播放失败表现)写在这里
    /// </summary>
    private void OnPourFailed()
    {
        Debug.Log($"灌超了:{PouredMl}ml / 需要 {RequiredMl}ml(允许误差 ±{MlTolerance}ml)");
        ShowFailWindow();
    }

    /// <summary>
    /// 弹失败窗:重试把台面清空重来,退出直接关掉玩法面板。
    /// 玩法面板不关,失败窗自带黑底遮罩挡住下面的点击,重试时窗口自己会关
    /// </summary>
    private void ShowFailWindow()
    {
        UIUtility.PopFailWindow(true, RetryGame, Close);
    }

    /// <summary>
    /// 重试:配方不变,只把选中的模具/颜料和灌注进度清掉重来。
    /// 想改成"重试时换一条新配方"就把这里换成 RefreshRandomSolution()
    /// </summary>
    private void RetryGame()
    {
        ClearSelected();
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
