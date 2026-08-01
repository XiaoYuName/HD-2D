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
/// 两种用法：
///   静态 —— 填 <see cref="offsetCurve"/>，OnEnable 自动烘焙（做水波、旗帜、固定造型的弯道）；
///   动态 —— 每帧调 <see cref="SetOffsets"/> 或 <see cref="SetRow"/> + <see cref="Flush"/>
///           上传实时算出的路面曲率（真正的赛车玩法，方向盘/车速直接改这张表）。
///
/// 不需要偏移表时可以不挂本组件，材质上的弯道 / 波动 / 滚动三项已能独立工作；
/// 但打了图集的 Sprite 建议挂上，否则 <c>_UVRect</c> 不对会串到相邻图。
/// </summary>
[AddComponentMenu("UI/Raster Scroll (光栅滚动)")]
[RequireComponent(typeof(Graphic))]
public class UIRasterScroll : MonoBehaviour
{
    static readonly int OffsetTexId = Shader.PropertyToID("_OffsetTex");
    static readonly int OffsetScaleId = Shader.PropertyToID("_OffsetScale");
    static readonly int CurveAmountId = Shader.PropertyToID("_CurveAmount");
    static readonly int ScrollXId = Shader.PropertyToID("_ScrollX");
    static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    static readonly int UVRectId = Shader.PropertyToID("_UVRect");

    [Title("偏移表")]
    [LabelText("扫描线条数"), PropertyRange(2, 1024)]
    [Tooltip("查找表的纹素数，建议和材质上的「扫描线条数」一致；红白机是 240")]
    [SerializeField] int lineCount = 240;

    [LabelText("静态偏移曲线")]
    [Tooltip("横轴 = 归一化行号（0 为画面底部），纵轴 = 偏移量（-1~1，单位是一整个贴图宽）。留空则表全为 0")]
    [SerializeField] AnimationCurve offsetCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [LabelText("偏移表幅度"), PropertyRange(-1f, 1f)]
    [SerializeField] float offsetScale = 0.1f;

    [Title("图集适配")]
    [LabelText("自动计算 UV 区域")]
    [Tooltip("从 Image 的 Sprite 反推它在图集里的 UV 范围，保证越界回绕只在本图内发生")]
    [SerializeField] bool autoUVRect = true;

    Graphic _graphic;
    Material _material;
    Texture2D _lut;
    float[] _rows;
    bool _rowsDirty;

    /// <summary>弯道强度，-1~1。赛车玩法里直接接方向盘/赛道曲率。</summary>
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

    /// <summary>地平线所在的 v（0=底部 1=顶部），决定远近权重的分布。</summary>
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

        // 克隆材质：多个实例各自持有独立的弯道/滚动状态，且不写脏材质资源
        if(_graphic.material != null)
        {
            _material = new Material(_graphic.material) { name = _graphic.material.name + " (RasterScroll)" };
            _graphic.material = _material;
        }
    }

    void OnEnable()
    {
        EnsureLut();
        BakeCurve();
        RefreshUVRect();
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

    void LateUpdate()
    {
        // 动态写入的行数据统一在帧末上传一次，避免一帧内多次 Apply
        if(_rowsDirty)
            Flush();
    }

    /// <summary>整表覆盖，长度不足的部分补 0，超出部分忽略。值域 -1~1（单位 = 一整个贴图宽）。</summary>
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
        Color[] pixels = new Color[n];
        for(int i = 0; i < n; i++)
            pixels[i] = new Color(_rows[i] * 0.5f + 0.5f, 0f, 0f, 1f); // -1~1 映射到 0~1，与手绘灰度图约定一致

        _lut.SetPixels(pixels);
        _lut.Apply(false, false);
        _rowsDirty = false;
    }

    /// <summary>按 <see cref="offsetCurve"/> 重烘整表。</summary>
    [Button("重新烘焙曲线"), PropertyOrder(10)]
    public void BakeCurve()
    {
        EnsureLut();

        int n = _rows.Length;
        for(int i = 0; i < n; i++)
            _rows[i] = offsetCurve?.Evaluate((i + 0.5f) / n) ?? 0f;

        Flush();

        if(_material != null)
            _material.SetFloat(OffsetScaleId, offsetScale);
    }

    /// <summary>重新从 Sprite 反推图集 UV 区域，换图后调用。</summary>
    public void RefreshUVRect()
    {
        if(_material == null)
            return;

        if(!autoUVRect)
        {
            _material.SetVector(UVRectId, new Vector4(0f, 0f, 1f, 1f));
            return;
        }

        Sprite sprite = (_graphic as Image)?.sprite;
        Texture tex = sprite != null ? sprite.texture : null;
        if(sprite == null || tex == null)
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

        if(_material != null)
            _material.SetTexture(OffsetTexId, _lut);
    }
}
