/*
 * Author:Misaka-Mikoto
 * Date: 2021-02-08
 * Url:https://github.com/Misaka-Mikoto-Tech/ScratchImage
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using XFramework;

/// <summary>
/// 可以刮开的图像
/// </summary>
public class ScratchImage : UIBase
{
    public struct StatData
    {
        public float    fillPercent;  // 填充百分比（非0值）
        public float    avgVal;       // 平均值
    }

    /// <summary>
    /// 直方图桶的数量，必须与shader中定义的一致, 且小于256
    /// </summary>
    public const int HISTOGRAM_BINS = 128;
    /// <summary>
    /// 用来控制透明度的RT相比Image尺寸的比例，值越小性能越高，但是精度和效果也越差
    /// </summary>
    public const float ALPHA_RT_SCALE = 0.4f;
    /// <summary>
    /// 每一批次的实例数量上限（太多有些设备会有异常）
    /// </summary>
    public const int INSTANCE_COUNT_PER_BATCH = 200;
    /// <summary>
    /// compute shader 单个线程组的最小边长，RT 尺寸必须不小于它，否则 Dispatch 数量会变成 0
    /// </summary>
    public const int MIN_HISTOGRAM_GROUP_SIZE = 16;
    /// <summary>
    /// 大于等于该桶索引就算作"已刮开"（对应灰度 0.5 以上）
    /// </summary>
    public const int SCRATCHED_BIN_THRESHOLD = HISTOGRAM_BINS / 2;
    /// <summary>
    /// 没有新笔画时的复查间隔，避免 GPU 数据延迟导致漏判完成
    /// </summary>
    public const float RECHECK_INTERVAL = 0.2f;

    public Camera uiCamera;
    /// <summary>
    /// 蒙版贴图
    /// </summary>
    public Image maskImage;
    /// <summary>
    /// 笔刷贴图
    /// </summary>
    public Texture2D brushTex;

    /// <summary>
    /// 笔刷尺寸
    /// </summary>
    [Range(1f, 200f)]
    public float brushSize = 50f;
    /// <summary>
    /// 绘制步进精度(值过大会变成点链，过小则有性能压力)
    /// TODO 改成根据brushSize自动计算
    /// </summary>
    [Range(1f, 20f)]
    public float paintStep = 5f;
    /// <summary>
    /// 笔刷移动检测阈值
    /// </summary>
    [Range(1f, 10f)]
    public float moveThreshhold = 2f;
    /// <summary>
    /// 笔刷不透明度
    /// </summary>
    [Range(0f, 1f)]
    public float brushAlpha = 1f;
    /// <summary>
    /// 绘图材质
    /// </summary>
    public Material paintMaterial;
    /// <summary>
    /// 用来生成直方图数据的shader
    /// </summary>
    public ComputeShader histogramShader;

    /// <summary>
    /// 直方图数据
    /// </summary>
    private uint[]          _histogramData;
    private ComputeBuffer   _histogramBuffer;
    private int             _clearShaderKrnl;
    private int             _histogramShaderKrnl;
    private Vector2Int      _histogramShaderGroupSize;
    private Vector2         _texScaledSize;

    private RenderTexture   _rt;
    private CommandBuffer   _cb;
    private Material        _runtimePaintMaterial;
    private Material        _runtimeMaskMaterial;
    private Material        _originMaskMaterial;
    private RectTransform   _scratchRectTransform;
    private Action<ScratchImage> _completedCallback;
    private bool            _isScratchActive;
    private bool            _isDirty;
    private bool            _hasScratchPoint;
    private bool            _isCompleted;
    private float           _completeRatio = 1f;
    private Vector2         _beginPos;
    private Vector2         _endPos;
    
    private Mesh            _quad;
    private Mesh            _maskQuad;
    private Matrix4x4       _matrixProj;
    private Matrix4x4[]     _arrInstancingMatrixs;
    /// <summary>
    /// 蒙版图里真正可以被刮开的面积占整个 Rect 的比例（图片透明的部分永远刮不到）
    /// </summary>
    private float           _maskAreaRatio = 1f;
    private bool            _needsCompleteCheck;
    private float           _lastCompleteCheckTime;

    private int             _propIDMainTex;
    private int             _propIDBrushAlpha;
    private Vector2         _lastPoint;
    private Vector2         _maskSize;

    public Vector2 rtSize => new Vector2(_rt.width, _rt.height);
    public bool IsScratchActive => _isScratchActive;
    public bool HasScratchContext => _cb != null && _rt != null && _runtimePaintMaterial != null && _runtimeMaskMaterial != null;
    public bool IsCompleted => _isCompleted;
    /// <summary>
    /// 当前刮开进度（0~1），已经按蒙版图的实际可刮面积做过归一化
    /// </summary>
    public float ScratchProgress { get; private set; }

    public override void Init()
    {
    }

    public bool SetData(
        Camera camera,
        Image targetMaskImage,
        Texture2D targetBrushTex = null,
        Material targetPaintMaterial = null,
        ComputeShader targetHistogramShader = null,
        float completeRatio = 1f,
        Action<ScratchImage> completed = null)
    {
        if (maskImage == targetMaskImage && HasScratchContext)
        {
            uiCamera = camera;
            _completeRatio = Mathf.Clamp01(completeRatio);
            _completedCallback = completed;
            _isScratchActive = !_isCompleted;
            return true;
        }

        ReleaseScratchContext();

        uiCamera = camera;
        maskImage = targetMaskImage;
        if (targetBrushTex != null)
        {
            brushTex = targetBrushTex;
        }

        if (targetPaintMaterial != null)
        {
            paintMaterial = targetPaintMaterial;
        }

        if (targetHistogramShader != null)
        {
            histogramShader = targetHistogramShader;
        }

        _completedCallback = completed;
        _completeRatio = Mathf.Clamp01(completeRatio);
        _isCompleted = false;
        InitScratchContext();
        ResetMask();
        _isScratchActive = _cb != null && _rt != null && _runtimePaintMaterial != null && _runtimeMaskMaterial != null;
        return _isScratchActive;
    }

    public void SetScratchActive(bool isActive)
    {
        _isScratchActive = isActive && HasScratchContext;
        if (!_isScratchActive)
        {
            _isDirty = false;
            _hasScratchPoint = false;
        }
    }


    /// <summary>
    /// 重置蒙版
    /// </summary>
    public void ResetMask()
    {
        if (_cb == null || _rt == null)
        {
            return;
        }

        SetupPaintContext(true);
        Graphics.ExecuteCommandBuffer(_cb);
        _isDirty = false;
        _isCompleted = false;
        _needsCompleteCheck = false;
        ScratchProgress = 0f;
    }

    /// <summary>
    /// 立即结算一次完成度。停止刮擦（例如熨斗移开）时调用，
    /// 避免最后一笔刚好刮够、但因为没有新笔画而漏判。
    /// </summary>
    public void EvaluateScratchComplete()
    {
        if (_isCompleted || !HasScratchContext || !_needsCompleteCheck)
        {
            return;
        }

        _lastCompleteCheckTime = Time.unscaledTime;
        CheckScratchComplete();
    }

    public void CompleteScratch()
    {
        if (_isCompleted)
        {
            return;
        }

        if (_cb == null || _rt == null)
        {
            return;
        }

        SetupCompleteContext();
        Graphics.ExecuteCommandBuffer(_cb);
        RenderTexture.active = null;
        _isDirty = false;
        _hasScratchPoint = false;
        _isScratchActive = false;
        _isCompleted = true;
        _needsCompleteCheck = false;
        ScratchProgress = 1f;
        _completedCallback?.Invoke(this);
    }

    /// <summary>
    /// 获取刮开的统计信息
    /// </summary>
    /// <returns></returns>
    public StatData GetStatData()
    {
        int dispatchCount = UpdateHistogram();
        if (dispatchCount <= 0)
        {
            return new StatData();
        }

        float sum = 0;
        float binScale = (256 / HISTOGRAM_BINS);
        for (int i = 0; i < HISTOGRAM_BINS; i++)
        {
            int count = (int)_histogramData[i];
            sum += i * binScale * count;
        }

        StatData ret = new StatData();
        ret.fillPercent = 1.0f - _histogramData[0] / (dispatchCount * 1.0f); // 非0值比例
        ret.avgVal = sum / dispatchCount;
        // 由于桶的数量小于256，shader最大只统计到 127 * 2 = 254, 无法显示255的数据，因此此处把结果给缩放一下
        ret.avgVal *= 255.0f / ((HISTOGRAM_BINS - 1) * binScale);
        return ret;
    }

    /// <summary>
    /// 已经刮开（灰度超过一半）的像素占整张 RT 的比例。
    /// 比 avgVal 更贴近"刮掉了多少面积"，不会因为笔刷边缘的半透明而被拉低。
    /// </summary>
    public float GetScratchedRatio()
    {
        int dispatchCount = UpdateHistogram();
        if (dispatchCount <= 0)
        {
            return 0f;
        }

        long scratched = 0;
        for (int i = SCRATCHED_BIN_THRESHOLD; i < HISTOGRAM_BINS; i++)
        {
            scratched += _histogramData[i];
        }

        return Mathf.Clamp01(scratched / (float)dispatchCount);
    }

    /// <summary>
    /// 跑一遍直方图 compute，返回参与统计的像素总数（0 表示统计不可用）
    /// </summary>
    private int UpdateHistogram()
    {
        if (_rt == null || histogramShader == null || _histogramShaderKrnl == -1 || _histogramBuffer == null)
        {
            return 0;
        }

        int dispatchX = _rt.width / _histogramShaderGroupSize.x;
        int dispatchY = _rt.height / _histogramShaderGroupSize.y;
        if (dispatchX <= 0 || dispatchY <= 0)
        {
            return 0;
        }

        // compute shader 要把 _rt 当普通贴图采样，这里必须先解绑渲染目标，
        // 否则 _rt 同时是绘制目标又是采样源，读到的可能是上一帧的旧数据。
        RenderTexture.active = null;

        // histogramShader 是所有布料共用的同一份资源资产，参数绑在资产上而不是实例上。
        // 只在初始化时绑一次的话，后一块布会把前一块布的 _Tex/_HistogramBuffer 覆盖掉，
        // 于是每块布统计到的都是别人的 RT——这正是"刮满了也不结算"的根因。
        // 所以每次 Dispatch 前都要重新绑定自己的资源。
        histogramShader.SetBuffer(_clearShaderKrnl, "_HistogramBuffer", _histogramBuffer);
        histogramShader.SetTexture(_histogramShaderKrnl, "_Tex", _rt);
        histogramShader.SetBuffer(_histogramShaderKrnl, "_HistogramBuffer", _histogramBuffer);
        histogramShader.SetVector("_TexScaledSize", _texScaledSize);

        histogramShader.Dispatch(_clearShaderKrnl, HISTOGRAM_BINS / _histogramShaderGroupSize.x, 1, 1);
        histogramShader.Dispatch(_histogramShaderKrnl, dispatchX, dispatchY, 1);

        // AsyncGPUReadback.Request does supported at OpenglES
        _histogramBuffer.GetData(_histogramData);

        return dispatchX * _histogramShaderGroupSize.x * dispatchY * _histogramShaderGroupSize.y;
    }

    public override void Release()
    {
        ReleaseScratchContext();
        base.Release();
    }

    private void Update()
    {
        if (!_isScratchActive)
        {
            return;
        }

        CheckInput();
    }

    void LateUpdate()
    {
        if (!_isScratchActive)
        {
            return;
        }

        if (BuildCommands())
        {
            Graphics.ExecuteCommandBuffer(_cb);
            _beginPos = _endPos;
            _needsCompleteCheck = true;
            _lastCompleteCheckTime = Time.unscaledTime;
            CheckScratchComplete();
            return;
        }

        // 停笔（鼠标不动）时也要按间隔复查一次：
        // 判定依赖 GPU 回读，最后一笔的结果有可能要晚一帧才拿得到，
        // 只在有新笔画时判定会出现"明明刮够了却不结算，移开再移回来才生效"。
        if (_needsCompleteCheck && Time.unscaledTime - _lastCompleteCheckTime >= RECHECK_INTERVAL)
        {
            _lastCompleteCheckTime = Time.unscaledTime;
            CheckScratchComplete();
        }
    }

    private bool BuildCommands()
    {
        if (!_isDirty || _runtimePaintMaterial == null)
            return false;

        _runtimePaintMaterial.SetTexture(_propIDMainTex, brushTex != null ? brushTex : Texture2D.whiteTexture);
        _runtimePaintMaterial.SetFloat(_propIDBrushAlpha, brushAlpha);

        Vector2 fromToVec = _endPos - _beginPos;
        Vector2 dir = fromToVec.normalized;
        float len = fromToVec.magnitude;

        float offset = 0;
        int instCount = 0;

        SetupPaintContext(false);

        while (offset <= len)
        {
            if (instCount >= INSTANCE_COUNT_PER_BATCH)
            {
                _cb.DrawMeshInstanced(_quad, 0, _runtimePaintMaterial, 0, _arrInstancingMatrixs, instCount);
                instCount = 0;
            }

            Vector2 tmpPt = _beginPos + dir * offset;
            tmpPt -= Vector2.one * brushSize * 0.5f; // 将笔刷居中到绘制点
            offset += paintStep;

            _arrInstancingMatrixs[instCount++] = Matrix4x4.TRS(new Vector3(tmpPt.x, tmpPt.y, 0), Quaternion.identity, Vector3.one * brushSize);
        }

        if(instCount > 0)
        {
            _cb.DrawMeshInstanced(_quad, 0, _runtimePaintMaterial, 0, _arrInstancingMatrixs, instCount);
        }

        _isDirty = false;
        return true;
    }

    private void InitScratchContext()
    {
        _clearShaderKrnl = -1;
        _histogramShaderKrnl = -1;

        if (maskImage == null)
        {
            Debug.LogWarning("ScratchImage SetData 缺少 MaskImage。");
            return;
        }

        _lastPoint = Vector2.zero;
        _hasScratchPoint = false;
        _scratchRectTransform = maskImage.rectTransform;

        _quad = new Mesh();
        _quad.SetVertices(new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0)
        });

        _quad.SetUVs(0, new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 0),
                new Vector2(1, 1)
            });

        _quad.SetIndices(new int[] { 0, 1, 2, 3, 2, 1 }, MeshTopology.Triangles, 0, false);
        _quad.UploadMeshData(true);


        _maskSize = _scratchRectTransform.rect.size;
        //Debug.LogFormat("mask image size:{0}*{1}", maskSize.x, maskSize.y);

        // RT 每边都不能小于一个线程组，否则直方图 Dispatch 数量会算成 0，完成度永远统计不出来
        int rtWidth = Mathf.Max(MIN_HISTOGRAM_GROUP_SIZE, Mathf.RoundToInt(_maskSize.x * ALPHA_RT_SCALE));
        int rtHeight = Mathf.Max(MIN_HISTOGRAM_GROUP_SIZE, Mathf.RoundToInt(_maskSize.y * ALPHA_RT_SCALE));
        _rt = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.R8, 0);
        // 不开 MSAA：多重采样的 RT 要 resolve 之后才能被 compute shader 正确采样，
        // 而 resolve 发生在 Canvas 绘制时，会导致完成度统计读到上一帧的数据。
        _rt.antiAliasing = 1;
        _rt.autoGenerateMips = false;

        _arrInstancingMatrixs = new Matrix4x4[INSTANCE_COUNT_PER_BATCH];
        _matrixProj = Matrix4x4.Ortho(0, _maskSize.x, 0, _maskSize.y, -1f, 1f);

        _propIDMainTex = Shader.PropertyToID("_MainTex");
        _propIDBrushAlpha = Shader.PropertyToID("_BrushAlpha");

        _runtimePaintMaterial = CreateRuntimePaintMaterial();
        if (_runtimePaintMaterial == null)
        {
            Debug.LogWarning("ScratchImage 缺少绘制材质或 Unlit/PaintOnRT Shader。");
            return;
        }

        _runtimePaintMaterial.enableInstancing = true;

        _originMaskMaterial = maskImage.material;
        _runtimeMaskMaterial = CreateRuntimeMaskMaterial();
        if (_runtimeMaskMaterial == null)
        {
            Debug.LogWarning("ScratchImage 缺少 UI/Default-RevertMask Shader。");
            return;
        }

        _runtimeMaskMaterial.SetTexture("_AlphaTex", _rt);
        maskImage.material = _runtimeMaskMaterial;
        maskImage.SetMaterialDirty();

        _cb = new CommandBuffer() { name = "PaintOncb" };

        // setup histogram compute shader
        _clearShaderKrnl = -1;
        if (histogramShader != null)
        {
            _histogramBuffer = new ComputeBuffer(HISTOGRAM_BINS, 4);
            _histogramData = new uint[HISTOGRAM_BINS];

            _clearShaderKrnl = histogramShader.FindKernel("HistogramClear");
            _histogramShaderKrnl = histogramShader.FindKernel("Histogram");

            uint x, y, z;
            histogramShader.GetKernelThreadGroupSizes(_histogramShaderKrnl, out x, out y, out z);
            _histogramShaderGroupSize = new Vector2Int(Mathf.Max(1, (int)x), Mathf.Max(1, (int)y));

            // 要求shader执行的宽高小于真实的纹理尺寸，以避免uv溢出
            _texScaledSize = new Vector2(
                _rt.width / _histogramShaderGroupSize.x * _histogramShaderGroupSize.x,
                _rt.height / _histogramShaderGroupSize.y * _histogramShaderGroupSize.y);
        }

        MeasureMaskArea();
    }

    /// <summary>
    /// 把蒙版图的 alpha 画进 RT 量一遍，得到"这张图里到底有多少面积是能被刮开的"。
    /// 布料图片是不规则形状，Rect 四角本来就是透明的，
    /// 不做这一步的话完成度分母永远是整个矩形，玩家刮干净了也到不了阈值。
    /// </summary>
    private void MeasureMaskArea()
    {
        _maskAreaRatio = 1f;

        if (_cb == null || _rt == null || _runtimePaintMaterial == null)
        {
            return;
        }

        if (histogramShader == null || _histogramShaderKrnl == -1)
        {
            return;
        }

        Sprite sprite = maskImage != null ? maskImage.sprite : null;
        if (sprite == null)
        {
            return;
        }

        _maskQuad = CreateMaskQuad(sprite);
        if (_maskQuad == null)
        {
            return;
        }

        _runtimePaintMaterial.SetTexture(_propIDMainTex, sprite.texture);
        _runtimePaintMaterial.SetFloat(_propIDBrushAlpha, 1f);

        SetupPaintContext(true);
        _cb.DrawMesh(
            _maskQuad,
            Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_maskSize.x, _maskSize.y, 1f)),
            _runtimePaintMaterial,
            0,
            0);
        Graphics.ExecuteCommandBuffer(_cb);

        float ratio = GetScratchedRatio();
        if (ratio > 0.01f)
        {
            _maskAreaRatio = ratio;
        }
    }

    /// <summary>
    /// 构造一个铺满整个 Rect、UV 对齐到图集中该 Sprite 区域的四边形
    /// </summary>
    private Mesh CreateMaskQuad(Sprite sprite)
    {
        Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);

        Mesh mesh = new Mesh();
        mesh.SetVertices(new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0)
        });

        mesh.SetUVs(0, new Vector2[]
        {
            new Vector2(outerUV.x, outerUV.y),
            new Vector2(outerUV.x, outerUV.w),
            new Vector2(outerUV.z, outerUV.y),
            new Vector2(outerUV.z, outerUV.w)
        });

        mesh.SetIndices(new int[] { 0, 1, 2, 3, 2, 1 }, MeshTopology.Triangles, 0, false);
        mesh.UploadMeshData(true);
        return mesh;
    }

    private void SetupPaintContext(bool clearRT)
    {
        _cb.Clear();
        _cb.SetRenderTarget(_rt);

        if (clearRT)
        {
            _cb.ClearRenderTarget(true, true, Color.clear);
        }

        _cb.SetViewProjectionMatrices(Matrix4x4.identity, _matrixProj);
    }

    private void SetupCompleteContext()
    {
        _cb.Clear();
        _cb.SetRenderTarget(_rt);
        _cb.ClearRenderTarget(true, true, Color.white);
    }

    private void CheckScratchComplete()
    {
        if (_isCompleted)
        {
            return;
        }

        if (_completeRatio <= 0f)
        {
            CompleteScratch();
            return;
        }

        if (histogramShader == null || _histogramShaderKrnl == -1)
        {
            return;
        }

        float scratchedRatio = GetScratchedRatio();
        // 蒙版图透明的地方（布料轮廓之外）永远刮不到，
        // 所以要按实际可刮面积归一化，否则玩家看着已经刮干净了、比例却永远到不了阈值。
        ScratchProgress = _maskAreaRatio > 0.01f
            ? Mathf.Clamp01(scratchedRatio / _maskAreaRatio)
            : scratchedRatio;

        if (ScratchProgress >= _completeRatio)
        {
            CompleteScratch();
        }
    }

    private void CheckInput()
    {
        if (_scratchRectTransform == null)
            return;

        int mouseStatus = 0;// 0：none, 1:down, 2:hold, 3:up

        if (Input.GetMouseButtonDown(0)) // 按下鼠标
            mouseStatus = 1;
        else if (Input.GetMouseButton(0)) // 移动鼠标或者处于按下状态
            mouseStatus = 2;
        else if (Input.GetMouseButtonUp(0)) // 释放鼠标
            mouseStatus = 3;

        if (mouseStatus == 0)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _scratchRectTransform,
                Input.mousePosition,
                uiCamera,
                out Vector2 localPt))
        {
            return;
        }

        //Debug.Log($"pt:{localPt}, status:{mouseStatus}");

        Rect rect = _scratchRectTransform.rect;
        if (!rect.Contains(localPt))
        {
            _hasScratchPoint = false;
            return;
        }

        Vector2 pixelPt = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPt.x) * _maskSize.x,
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPt.y) * _maskSize.y);

        if (!_hasScratchPoint)
        {
            _beginPos = pixelPt;
            _endPos = pixelPt;
            _lastPoint = pixelPt;
            _hasScratchPoint = true;
            return;
        }

        switch (mouseStatus)
        {
            case 1:
                _beginPos = pixelPt;
                _endPos = pixelPt;
                _lastPoint = pixelPt;
                _hasScratchPoint = true;
                break;
            case 2:
                if (Vector2.Distance(pixelPt, _lastPoint) > moveThreshhold)
                {
                    _beginPos = _lastPoint;
                    _endPos = pixelPt;
                    _lastPoint = pixelPt;
                    _isDirty = true;
                }
                break;
            case 3:
                _beginPos = _lastPoint;
                _endPos = pixelPt;
                _lastPoint = pixelPt;
                _isDirty = true;
                _hasScratchPoint = false;
                break;
        }
    }

    private Material CreateRuntimePaintMaterial()
    {
        if (paintMaterial != null)
        {
            return new Material(paintMaterial);
        }

        Shader shader = Shader.Find("Unlit/PaintOnRT");
        return shader == null ? null : new Material(shader);
    }

    private Material CreateRuntimeMaskMaterial()
    {
        Shader shader = Shader.Find("UI/Default-RevertMask");
        return shader == null ? null : new Material(shader);
    }

    private void ReleaseScratchContext()
    {
        _isScratchActive = false;
        _isDirty = false;
        _hasScratchPoint = false;
        _isCompleted = false;
        _needsCompleteCheck = false;
        _maskAreaRatio = 1f;
        ScratchProgress = 0f;
        _completedCallback = null;

        if (maskImage != null && maskImage.material == _runtimeMaskMaterial)
        {
            maskImage.material = _originMaskMaterial;
            maskImage.SetMaterialDirty();
        }

        if(_rt != null)
            Destroy(_rt);

        if(_quad != null)
            Destroy(_quad);

        if (_maskQuad != null)
            Destroy(_maskQuad);

        if (_cb != null)
            _cb.Dispose();

        if (_histogramBuffer != null)
            _histogramBuffer.Release();

        if (_runtimePaintMaterial != null)
            Destroy(_runtimePaintMaterial);

        if (_runtimeMaskMaterial != null)
            Destroy(_runtimeMaskMaterial);

        _rt = null;
        _quad = null;
        _maskQuad = null;
        _cb = null;
        _histogramBuffer = null;
        _histogramData = null;
        _runtimePaintMaterial = null;
        _runtimeMaskMaterial = null;
        _originMaskMaterial = null;
        _scratchRectTransform = null;
        _histogramShaderKrnl = -1;
        _clearShaderKrnl = -1;
    }
}
