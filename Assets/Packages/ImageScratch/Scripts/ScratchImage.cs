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
    private Matrix4x4       _matrixProj;
    private Matrix4x4[]     _arrInstancingMatrixs;

    private int             _propIDMainTex;
    private int             _propIDBrushAlpha;
    private Vector2         _lastPoint;
    private Vector2         _maskSize;

    public Vector2 rtSize => new Vector2(_rt.width, _rt.height);
    public bool IsScratchActive => _isScratchActive;
    public bool HasScratchContext => _cb != null && _rt != null && _runtimePaintMaterial != null && _runtimeMaskMaterial != null;

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
        _isDirty = false;
        _hasScratchPoint = false;
        _isScratchActive = false;
        _isCompleted = true;
        _completedCallback?.Invoke(this);
    }

    /// <summary>
    /// 获取刮开的统计信息
    /// </summary>
    /// <returns></returns>
    public StatData GetStatData()
    {
        if (_rt == null || _histogramShaderKrnl == -1)
        {
            Debug.LogError("invalid compute shader");
            return new StatData();
        }

        histogramShader.Dispatch(_clearShaderKrnl, HISTOGRAM_BINS / _histogramShaderGroupSize.x, 1, 1);

        int dispatchX = _rt.width / _histogramShaderGroupSize.x;
        int dispatchY = _rt.height / _histogramShaderGroupSize.y;
        histogramShader.Dispatch(_histogramShaderKrnl, dispatchX, dispatchY, 1);

        // AsyncGPUReadback.Request does supported at OpenglES
        _histogramBuffer.GetData(_histogramData);

        int dispatchWidth = dispatchX * _histogramShaderGroupSize.x;
        int dispatchHeight = dispatchY * _histogramShaderGroupSize.y;
        int dispatchCount = dispatchWidth * dispatchHeight;

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

        if(BuildCommands())
        {
            Graphics.ExecuteCommandBuffer(_cb);
            _beginPos = _endPos;
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

        int rtWidth = Mathf.Max(1, Mathf.RoundToInt(_maskSize.x * ALPHA_RT_SCALE));
        int rtHeight = Mathf.Max(1, Mathf.RoundToInt(_maskSize.y * ALPHA_RT_SCALE));
        _rt = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.R8, 0);
        _rt.antiAliasing = 2;
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
            histogramShader.SetBuffer(_clearShaderKrnl, "_HistogramBuffer", _histogramBuffer);

            _histogramShaderKrnl = histogramShader.FindKernel("Histogram");
            histogramShader.SetTexture(_histogramShaderKrnl, "_Tex", _rt);
            histogramShader.SetBuffer(_histogramShaderKrnl, "_HistogramBuffer", _histogramBuffer);

            // setup _TexScaledSize
            {
                uint x, y, z;
                histogramShader.GetKernelThreadGroupSizes(_histogramShaderKrnl, out x, out y, out z);
                uint dispatchWidth = (uint)(_rt.width / x * x);
                uint dispatchHeight = (uint)(_rt.height / y * y);

                _histogramShaderGroupSize = new Vector2Int((int)x, (int)y);

                // 要求shader执行的宽高小于真实的纹理尺寸，以避免uv溢出
                histogramShader.SetVector("_TexScaledSize", new Vector2(dispatchWidth, dispatchHeight));
            }
        }
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
        if (_isCompleted || _completeRatio <= 0f)
        {
            CompleteScratch();
            return;
        }

        if (_completeRatio >= 1f || histogramShader == null || _histogramShaderKrnl == -1)
        {
            return;
        }

        StatData statData = GetStatData();
        float effectiveFillPercent = Mathf.Clamp01(statData.avgVal / 255f);
        if (effectiveFillPercent >= _completeRatio)
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
