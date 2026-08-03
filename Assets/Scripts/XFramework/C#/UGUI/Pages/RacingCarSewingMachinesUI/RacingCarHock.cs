using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 缝纫机针头（Hock）：赛车小游戏里的「车」。
///
/// 两件事互不干扰，所以分开做：
///   横向移动 —— 每帧改 anchoredPosition.x，限制在初始位置左右 <see cref="maxOffset"/> 内。
///                路面是靠逐扫描线偏移弯的，针头本身不需要跟着弯，只需要能左右挪。
///   针头往复 —— 一条 Yoyo 无限循环的 PrimeTween，独立于移动逻辑，不受输入影响。
///                真机的针杆是曲柄连杆驱动的，位移接近正弦，所以用 InOutSine 而不是 Linear。
///
/// 输入两条通道都可以关掉，把 <see cref="Steer"/> 交给外部（AI/摇杆/倾斜）驱动即可。
/// </summary>
[AddComponentMenu("MiniGame/Racing Car Hock (缝纫机针头)")]
[RequireComponent(typeof(RectTransform))]
public class RacingCarHock : MonoBehaviour
{
    [Title("横向移动")]
    [LabelText("移动速度(像素/秒)"), MinValue(0f)]
    [SerializeField] float moveSpeed = 1100f;

    [LabelText("最大横向偏移(像素)"), MinValue(0f)]
    [Tooltip("相对启动时位置的左右极限。按路面在屏幕底部的半宽来给")]
    [SerializeField] float maxOffset = 420f;

    [LabelText("转向响应"), PropertyRange(1f, 40f)]
    [Tooltip("越小越「重」，松开按键后还会滑一段；越大越跟手")]
    [SerializeField] float steerResponse = 12f;

    [LabelText("松手回中速度(像素/秒)"), MinValue(0f)]
    [Tooltip("0 = 松手停在原地。给个正值就会缓缓回到中间")]
    [SerializeField] float autoCenterSpeed = 0f;

    [Title("离心力")]
    [LabelText("路面驱动器")]
    [Tooltip("从它取车头处曲率和车速。留空则没有离心力——那样不操作就是完美路线，弯道等于不存在")]
    [SerializeField] UIRasterScroll road;

    [LabelText("离心力系数(像素/弧度)"), MinValue(0f)]
    [Tooltip("过弯时被甩向外侧的强度，游戏难度主要靠它调。\n" +
             "量纲：曲率(弧度/米) × 车速(米/秒) = 弧度/秒，乘上它得到每秒被推开多少像素。\n" +
             "40 大约是「20m/s 通过半径 12m 的弯，每秒外推 67px」，约为移动速度的 12%")]
    [SerializeField] float centrifugalGain = 40f;

    [LabelText("离心力上限(占移动速度)"), PropertyRange(0f, 1f)]
    [Tooltip("外推速度最多允许吃掉移动速度的百分之多少，超出部分截断。\n" +
             "这是可玩性的硬保障，不是手感微调：曲率是美术手拖出来的自由量，" +
             "线上有一个折角就能算出超过移动速度的外推速度，那一段玩家打死方向也追不回中线，必然 0 分。\n" +
             "0.35 = 无论赛道画成什么样，玩家永远留有 65% 的余量把针头打回来")]
    [SerializeField] float centrifugalCapRatio = 0.35f;

    [Title("输入")]
    [LabelText("键盘 A/D ←/→")]
    [SerializeField] bool keyboardInput = true;

    [LabelText("鼠标/触摸拖动")]
    [SerializeField] bool pointerInput = true;

    [LabelText("拖动灵敏度"), MinValue(0f)]
    [SerializeField] float pointerSensitivity = 1f;

    [Title("针头往复")]
    [LabelText("针头节点")]
    [Tooltip("留空则自动取第一个子节点。动的是它，不是整个 Hock")]
    [SerializeField] RectTransform needle;

    [LabelText("行程(像素)"), MinValue(0f)]
    [SerializeField] float strokeDistance = 42f;

    [LabelText("单程时间(秒)"), MinValue(0.01f)]
    [Tooltip("一下或一上所需时间，一个完整循环是它的两倍。0.09 约等于每分钟 330 针")]
    [SerializeField] float strokeDuration = 0.09f;

    [LabelText("启用时自动扎针")]
    [SerializeField] bool playOnEnable = true;

    RectTransform _rect;
    Canvas _canvas;
    float _baseX;
    float _needleBaseY;
    float _offset;      // 相对 _baseX 的横向偏移
    float _steer;       // 平滑后的转向量，-1~1
    Tween _needleTween;

    /// <summary>
    /// 转向输入，-1(左) ~ 1(右)。
    /// 注意 <see cref="keyboardInput"/> 开着时每帧会被键盘覆盖；要由外部（AI/摇杆/倾斜）驱动请先关掉它。
    /// </summary>
    public float Steer { get; set; }

    /// <summary>当前横向偏移（像素），相对中位。</summary>
    public float Offset => _offset;

    /// <summary>
    /// 中位：针头不受力时停的 anchoredPosition.x，也是 <see cref="Offset"/> 的零点。
    /// 默认取场景里的初始位置；需要把针尖对准路面中线时改它，而不是直接改 anchoredPosition
    /// ——后者每帧都会被 Update 用「中位 + 偏移」覆盖掉。
    /// </summary>
    public float HomeX
    {
        get
        {
            EnsureInit();
            return _baseX;
        }
        set
        {
            EnsureInit();
            _baseX = value;
        }
    }

    /// <summary>当前横向偏移归一化到 -1~1，压线/出界判定用这个比用像素稳。</summary>
    public float Offset01 => maxOffset > 0f ? _offset / maxOffset : 0f;

    /// <summary>针头是否正在扎。</summary>
    public bool IsStitching => _needleTween.isAlive;

    void Awake() => EnsureInit();

    /// <summary>
    /// 幂等初始化。做成随时可调而不是只放在 Awake 里，是因为跨 GameObject 的 Awake 顺序 Unity 不保证：
    /// 挂在路面节点上的 RacingStitchScore 会在 OnEnable 里调本组件校准中位，
    /// 那时本组件的 Awake 可能还没跑，_rect 是空的。
    /// </summary>
    void EnsureInit()
    {
        if(_rect != null)
            return;

        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _baseX = _rect.anchoredPosition.x;

        if(needle == null && _rect.childCount > 0)
            needle = _rect.GetChild(0) as RectTransform;

        if(needle != null)
            _needleBaseY = needle.anchoredPosition.y;
    }

    void OnEnable()
    {
        EnsureInit();

        if(playOnEnable)
            StartStitching();
    }

    void OnDisable()
    {
        StopStitching();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 键盘走「舵量」通道：有平滑、受 moveSpeed 限速，松手会滑一小段
        if(keyboardInput)
            Steer = ReadKeyboardAxis();

        _steer = Mathf.MoveTowards(_steer, Mathf.Clamp(Steer, -1f, 1f), steerResponse * dt);
        _offset += _steer * moveSpeed * dt;

        // 拖动走「直接位移」通道：不过平滑也不限速，手指移多少就移多少，
        // 折算成舵量再平滑的话会明显跟不上手，UI 拖拽必须是 1:1 的
        if(pointerInput)
            _offset += ReadPointerDelta();

        _offset += ReadCentrifugal() * dt;

        if(autoCenterSpeed > 0f && Mathf.Approximately(Steer, 0f))
            _offset = Mathf.MoveTowards(_offset, 0f, autoCenterSpeed * dt);

        _offset = Mathf.Clamp(_offset, -maxOffset, maxOffset);

        Vector2 p = _rect.anchoredPosition;
        p.x = _baseX + _offset;
        _rect.anchoredPosition = p;
    }

    #region 横向移动
    float ReadKeyboardAxis()
    {
        if(Keyboard.current == null)
            return 0f;

        float raw = 0f;
        if(Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            raw -= 1f;
        if(Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            raw += 1f;
        return raw;
    }

    /// <summary>
    /// 过弯时把针头甩向弯道外侧，返回每秒的横向速度（像素/秒）。
    ///
    /// 没有这一项的话，路面的近端恒定在屏幕同一个位置（偏移表的二次积分从近平面起算，
    /// 底部那一行的偏移必然是 0），针头也钉着不动，于是「不操作」就是完美路线，弯道形同虚设。
    /// 补上它之后，弯越急、车越快，外推越狠，玩家必须反打方向盘才守得住线——这才是这个玩法的核心压力来源。
    ///
    /// 曲率左转为正，而左转时车被甩向右边，所以这里不取反。
    ///
    /// 结果按 <see cref="centrifugalCapRatio"/> 截断在移动速度的一个比例内：
    /// 外推速度一旦超过 <see cref="moveSpeed"/>，玩家的操控就完全失效了（按住方向键净位移还是朝外），
    /// 而曲率是赛道资产里的自由量，光靠调系数保证不了这一点，必须在这里封顶。
    /// </summary>
    float ReadCentrifugal()
    {
        if(road == null || centrifugalGain <= 0f)
            return 0f;

        float push = road.CurvatureAtCar * road.DriveSpeed * centrifugalGain;
        float cap = moveSpeed * Mathf.Clamp01(centrifugalCapRatio);
        return Mathf.Clamp(push, -cap, cap);
    }

    float ReadPointerDelta()
    {
        if(Pointer.current == null || !Pointer.current.press.isPressed)
            return 0f;

        // 除以 scaleFactor 把屏幕像素换算成 Canvas 单位，否则换个分辨率手感就变了
        float scale = _canvas != null ? Mathf.Max(_canvas.scaleFactor, 1e-4f) : 1f;
        return Pointer.current.delta.ReadValue().x / scale * pointerSensitivity;
    }

    /// <summary>把针头拉回启动位置。换赛道 / 重开一局时调。</summary>
    public void ResetPosition()
    {
        EnsureInit();

        _offset = 0f;
        _steer = 0f;
        Steer = 0f;

        Vector2 p = _rect.anchoredPosition;
        p.x = _baseX;
        _rect.anchoredPosition = p;
    }
    #endregion

    #region 针头往复
    /// <summary>开始扎针。重复调用安全，会先停掉上一条。</summary>
    [Button("开始扎针"), PropertyOrder(100)]
    public void StartStitching()
    {
        EnsureInit();
        StopStitching();

        if(needle == null || strokeDistance <= 0f)
            return;

        // Yoyo 无限循环 = 一下一上一下；InOutSine 贴近曲柄连杆的位移曲线，
        // 上下端点速度为零、中段最快，看起来才像真机在扎
        _needleTween = Tween.UIAnchoredPositionY(
            needle,
            _needleBaseY - strokeDistance,
            strokeDuration,
            Ease.InOutSine,
            cycles: -1,
            cycleMode: CycleMode.Yoyo);
    }

    /// <summary>停止扎针并把针头归位。</summary>
    [Button("停止扎针"), PropertyOrder(101)]
    public void StopStitching()
    {
        _needleTween.Stop();

        if(needle != null)
        {
            Vector2 p = needle.anchoredPosition;
            p.y = _needleBaseY;
            needle.anchoredPosition = p;
        }
    }

    /// <summary>按车速调整针频：车越快扎得越密。传入 0~1。</summary>
    public void SetStitchRate(float speed01)
    {
        float target = Mathf.Lerp(0.16f, 0.05f, Mathf.Clamp01(speed01));
        if(Mathf.Approximately(target, strokeDuration))
            return;

        strokeDuration = target;
        if(IsStitching)
            StartStitching();
    }
    #endregion
}
