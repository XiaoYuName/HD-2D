using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 光栅滚动（Raster Scroll）驱动组件，配合 <c>XFramework/UI/RasterScroll</c> 使用。
///
/// 红白机做「立体弯道」靠的是逐扫描线改背景 X 寄存器，那张「第 N 行推多远」的表
/// 是 CPU 每帧重算并喂给 PPU 的。本组件就是那张表的等价物：
/// 把偏移量烘成一张 1×N 的 RHalf 查找表贴给 <c>_OffsetTex</c>，一个纹素 = 一条扫描线。
///
/// 三种偏移来源（<see cref="OffsetSource"/>）：
///   None  —— 不用表，只靠材质上的弯道 / 波动 / 滚动三项（单向弯道够用时最省）
///   Curve —— 把一条 AnimationCurve 烘进表，做水波、旗帜、造型固定的弯道
///   Track —— 按赛道曲率实时重算整张表：沿视线方向对曲率做二次积分再做透视除法，
///            所以画面里能同时出现 S 弯（一段左弯接一段右弯），这是单个 _CurveAmount 做不到的
///
/// 也可以完全绕开上面三种，自己每帧调 <see cref="SetOffsets"/> / <see cref="SetRow"/> 写表。
/// </summary>
[AddComponentMenu("UI/Raster Scroll (光栅滚动)")]
[RequireComponent(typeof(Graphic))]
public class UIRasterScroll : MonoBehaviour
{
    public enum OffsetSource
    {
        [LabelText("不用偏移表")] None,
        [LabelText("静态曲线")] Curve,
        [LabelText("赛道曲率")] Track,
    }

    static readonly int OffsetTexId = Shader.PropertyToID("_OffsetTex");
    static readonly int OffsetScaleId = Shader.PropertyToID("_OffsetScale");
    static readonly int CurveAmountId = Shader.PropertyToID("_CurveAmount");
    static readonly int ScrollXId = Shader.PropertyToID("_ScrollX");
    static readonly int LineSpeedId = Shader.PropertyToID("_LineSpeed");
    static readonly int LineDensityId = Shader.PropertyToID("_LineDensity");
    static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    static readonly int UVRectId = Shader.PropertyToID("_UVRect");


    [Title("偏移表")]
    [LabelText("偏移来源"), EnumToggleButtons]
    [SerializeField] OffsetSource source = OffsetSource.None;

    [LabelText("扫描线条数"), PropertyRange(2, 1024)]
    [Tooltip("查找表的纹素数，建议和材质上的「扫描线条数」一致；红白机是 240")]
    [SerializeField] int lineCount = 240;

    [LabelText("偏移表幅度"), PropertyRange(-1f, 1f)]
    [Tooltip("表里的值是归一化的 -1~1，乘上它才是实际 UV 偏移")]
    [SerializeField] float offsetScale = 0.35f;

    [LabelText("静态偏移曲线"), ShowIf("@source == OffsetSource.Curve")]
    [Tooltip("横轴 = 归一化行号（0 为画面底部），纵轴 = 归一化偏移量（-1~1）")]
    [SerializeField] AnimationCurve offsetCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [Title("赛道", titleAlignment: TitleAlignments.Left)]
    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("曲率曲线")]
    [Tooltip("横轴 = 赛道里程（0~1 首尾相接循环），纵轴 = 曲率（负左弯 / 正右弯）")]
    [SerializeField] AnimationCurve trackCurvature = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.5f, 0f), new Keyframe(0.75f, -1f), new Keyframe(1f, 0f));

    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("赛道长度"), MinValue(0.01f)]
    [Tooltip("跑完曲率曲线一整圈所需的里程，越大弯道越舒缓")]
    [SerializeField] float trackLength = 60f;

    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("最远深度"), PropertyRange(2f, 40f)]
    [Tooltip("地平线处深度趋于无穷，横向偏移会被放大成一条断裂拖影；这里给它封顶。越小地平线附近越收敛，越大越狂野")]
    [SerializeField] float maxDepth = 12f;

    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("自动行驶")]
    [SerializeField] bool autoDrive = true;

    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("行驶速度"), MinValue(0f)]
    [SerializeField] float driveSpeed = 6f;

    [ShowIf("@source == OffsetSource.Track")]
    [LabelText("同步虚线速度")]
    [Tooltip("勾上后把行驶速度换算进材质的「虚线推进速度」，路面虚线不会和弯道脱节")]
    [SerializeField] bool syncLineSpeed = true;

    [Title("图集适配")]
    [LabelText("自动计算 UV 区域")]
    [Tooltip("从 Image 的 Sprite 反推它在图集里的 UV 范围，保证越界回绕只在本图内发生")]
    [SerializeField] bool autoUVRect = true;

    Graphic _graphic;
    Material _material;
    Texture2D _lut;
    float[] _rows;
    Color[] _pixels;
    bool _rowsDirty;

    /// <summary>已行驶里程。<see cref="autoDrive"/> 关掉时由外部推进（接油门/车速）。</summary>
    public float Travel { get; set; }

    /// <summary>弯道强度，-1~1。不使用偏移表时用它做单向弯道。</summary>
    public float CurveAmount
    {
        get => _material != null ? _material.GetFloat(CurveAmountId) : 0f;
        set { if(_material != null) _material.SetFloat(CurveAmountId, value); }
    }

    /// <summary>横向滚动速度（贴图宽/秒）。</summary>
    public float ScrollX
    {
        get => _material != null ? _material.GetFloat(ScrollXId) : 0f;
        set { if(_material != null) _material.SetFloat(ScrollXId, value); }
    }

    /// <summary>地平线所在的 v（0=底部 1=顶部），决定远近权重与透视分布。</summary>
    public float Horizon
    {
        get => _material != null ? _material.GetFloat(HorizonId) : 1f;
        set { if(_material != null) _material.SetFloat(HorizonId, Mathf.Clamp(value, 0.001f, 1f)); }
    }

    /// <summary>当前偏移表的行数，索引 0 为画面底部。</summary>
    public int LineCount => lineCount;

    void Awake()
    {
        _graphic = GetComponent<Graphic>();

        // 克隆材质：多个实例各自持有独立的弯道/行驶状态，且不写脏材质资源
        if(_graphic.material != null)
        {
            _material = new Material(_graphic.material) { name = _graphic.material.name + " (RasterScroll)" };
            _graphic.material = _material;
        }
    }

    void OnEnable()
    {
        EnsureLut();
        RefreshUVRect();

        if(_material != null)
            _material.SetFloat(OffsetScaleId, source == OffsetSource.None ? 0f : offsetScale);

        if(source == OffsetSource.Curve)
            BakeCurve();
    }

    void OnDisable()
    {
        // 表贴图跟随组件生命周期，避免频繁启停时堆积
        if(_lut != null)
        {
            Destroy(_lut);
            _lut = null;
        }
    }

    void OnDestroy()
    {
        if(_material != null)
            Destroy(_material);
    }

    void Update()
    {
        if(source != OffsetSource.Track)
            return;

        if(autoDrive)
            Travel += driveSpeed * Time.deltaTime;

        BuildTrack();
    }

    void LateUpdate()
    {
        // 动态写入的行数据统一在帧末上传一次，避免一帧内多次 Apply
        if(_rowsDirty)
            Flush();
    }

    /// <summary>整表覆盖，长度不足的部分补 0，超出部分忽略。值域 -1~1（乘 _OffsetScale 后才是 UV 偏移）。</summary>
    public void SetOffsets(IList<float> offsets)
    {
        EnsureLut();

        int n = _rows.Length;
        int m = offsets?.Count ?? 0;
        for(int i = 0; i < n; i++)
            _rows[i] = i < m ? offsets[i] : 0f;

        _rowsDirty = true;
    }

    /// <summary>写单行。<paramref name="line"/> 从画面底部往上数，越界自动忽略。</summary>
    public void SetRow(int line, float offset)
    {
        EnsureLut();

        if(line < 0 || line >= _rows.Length)
            return;

        _rows[line] = offset;
        _rowsDirty = true;
    }

    /// <summary>把写入的行数据上传到 GPU。LateUpdate 会自动调用，手动改完想立刻生效才需要主动调。</summary>
    public void Flush()
    {
        if(_lut == null)
            return;

        int n = _rows.Length;
        for(int i = 0; i < n; i++)
            _pixels[i].r = _rows[i] * 0.5f + 0.5f; // -1~1 映射到 0~1，与手绘灰度图约定一致

        _lut.SetPixels(_pixels);
        _lut.Apply(false, false);
        _rowsDirty = false;
    }

    /// <summary>按 <see cref="offsetCurve"/> 重烘整表。</summary>
    [Button("重新烘焙曲线"), ShowIf("@source == OffsetSource.Curve"), PropertyOrder(10)]
    public void BakeCurve()
    {
        EnsureLut();

        int n = _rows.Length;
        for(int i = 0; i < n; i++)
            _rows[i] = offsetCurve?.Evaluate((i + 0.5f) / n) ?? 0f;

        Flush();
    }

    /// <summary>
    /// 按赛道曲率重算整张表。
    ///
    /// 逐行从近到远推进：先由屏幕行号反解该行对应的深度 z（透视下 z 正比于 1/离地平线距离），
    /// 再沿 z 对曲率做二次积分得到车道中心的「世界横向位移」，最后除以 z 做透视除法变成屏幕横向位移。
    /// 常曲率时结果正比于 z，也就是越远推得越狠——这正是红白机那张偏移表里存的东西。
    /// </summary>
    public void BuildTrack()
    {
        EnsureLut();

        int n = _rows.Length;
        float horizon = Mathf.Max(Horizon, 1e-3f);
        float invLength = 1f / Mathf.Max(trackLength, 0.01f);
        float zMax = Mathf.Max(maxDepth, 1.01f);
        float nearFloor = 1f / zMax;

        // 常曲率下屏幕位移的最大值 = 0.5 * zMax，拿它做固定归一化，
        // 幅度就不会随曲率曲线的形状逐帧跳变（不能用当帧最大值归一化）
        float norm = 1f / (0.5f * zMax);

        float slope = 0f;   // 世界横向位移对 z 的导数
        float lateral = 0f; // 世界横向位移
        float prevZ = 1f;

        for(int i = 0; i < n; i++)
        {
            float v = (i + 0.5f) / n;
            float near = 1f - Mathf.Clamp01(v / horizon);

            // 软饱和而不是 Mathf.Max(near, nearFloor)：硬截断会让地平线附近若干行共用同一个深度，
            // 于是它们的偏移完全相同，和下面正在扫开的行之间裂开一道可见的接缝
            near = nearFloor + near * near / (near + nearFloor);
            float z = 1f / near;
            float step = z - prevZ;
            prevZ = z;

            float k = trackCurvature?.Evaluate(Mathf.Repeat((Travel + z) * invLength, 1f)) ?? 0f;
            slope += k * step;
            lateral += slope * step;

            _rows[i] = lateral / z * norm;
        }

        _rowsDirty = true;

        // 虚线相位是 frac(z * _LineDensity + t * _LineSpeed)，而 Travel 走的就是同一套 z 单位，
        // 所以速度乘上密度即可让虚线与弯道推进严格同步
        if(syncLineSpeed && _material != null)
            _material.SetFloat(LineSpeedId, driveSpeed * _material.GetFloat(LineDensityId));
    }

    /// <summary>重新从 Sprite 反推图集 UV 区域，换图后调用。</summary>
    public void RefreshUVRect()
    {
        if(_material == null)
            return;

        Sprite sprite = autoUVRect ? (_graphic as Image)?.sprite : null;
        Texture tex = sprite != null ? sprite.texture : null;
        if(tex == null)
        {
            _material.SetVector(UVRectId, new Vector4(0f, 0f, 1f, 1f));
            return;
        }

        Rect r = sprite.textureRect;
        _material.SetVector(UVRectId,
            new Vector4(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
    }

    void EnsureLut()
    {
        int n = Mathf.Clamp(lineCount, 2, 1024);

        if(_lut != null && _lut.width == n)
            return;

        if(_lut != null)
            Destroy(_lut);

        // RHalf 而不是 R8：8bit 只有 1/256 精度，弯道这种缓变量会出现肉眼可见的阶梯
        // 线性(linear:true)避免 Gamma 空间下被当颜色做二次转换；Point + Clamp 保证一个纹素严格对应一条扫描线
        _lut = new Texture2D(n, 1, TextureFormat.RHalf, false, true)
        {
            name = "RasterScrollLUT",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        _rows = new float[n];
        _pixels = new Color[n];
        for(int i = 0; i < n; i++)
            _pixels[i] = new Color(0.5f, 0f, 0f, 1f);

        if(_material != null)
            _material.SetTexture(OffsetTexId, _lut);
    }
}
