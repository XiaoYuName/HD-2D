using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>自适应画作尺寸的动态矩形光晕：呼吸明暗，并让高光沿边缘缓慢流动。</summary>
public sealed class MachiRoomArtworkGlow : MaskableGraphic
{
    [LabelText("表面流光宽度"), MinValue(1f), SerializeField] float glowWidth = 96f;
    [LabelText("外发光宽度"), MinValue(1f), SerializeField] float outerGlowWidth = 32f;
    [LabelText("外发光强度"), Range(0f, 2f), SerializeField] float outerGlowIntensity = 0.38f;
    [LabelText("流光柔和度"), Range(0.01f, 1f), SerializeField] float sweepSoftness = 0.18f;
    [LabelText("流光角度"), Range(-80f, 80f), SerializeField] float surfaceAngle = -25f;
    [LabelText("呼吸周期"), MinValue(0.1f), SerializeField] float pulseDuration = 2.8f;
    [LabelText("流光宽度呼吸"), Range(0f, 0.5f), SerializeField] float pulseScale = 0.18f;
    [LabelText("亮度呼吸幅度"), Range(0f, 0.5f), SerializeField] float pulseIntensity = 0.22f;
    [LabelText("流光速度"), MinValue(0.01f), SerializeField] float sweepSpeed = 0.12f;
    [LabelText("流光强度"), Range(0f, 2f), SerializeField] float sweepIntensity = 0.75f;
    [LabelText("表面圆角"), MinValue(0f), SerializeField] float cornerRadius = 3f;
    [LabelText("光晕圆角"), MinValue(0f), SerializeField] float glowCornerRadius = 64f;
    [LabelText("内侧边光宽度"), MinValue(0f), SerializeField] float innerRimWidth = 18f;
    [LabelText("内侧边光渗入"), Range(0.05f, 1f), SerializeField] float innerRimSpread = 0.35f;
    [LabelText("流光边缘柔化"), MinValue(0.5f), SerializeField] float edgeSoftness = 12f;
    [LabelText("外发光衰减"), MinValue(1.05f), SerializeField] float falloff = 2.2f;
    [SerializeField] Shader glowShader;

    static readonly int InnerSizeId = Shader.PropertyToID("_InnerSize");
    static readonly int OuterGlowWidthId = Shader.PropertyToID("_OuterGlowWidth");
    static readonly int OuterGlowIntensityId = Shader.PropertyToID("_OuterGlowIntensity");
    static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
    static readonly int GlowCornerRadiusId = Shader.PropertyToID("_GlowCornerRadius");
    static readonly int InnerRimWidthId = Shader.PropertyToID("_InnerRimWidth");
    static readonly int InnerRimSpreadId = Shader.PropertyToID("_InnerRimSpread");
    static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
    static readonly int SweepSoftnessId = Shader.PropertyToID("_SweepSoftness");
    static readonly int SurfaceAngleId = Shader.PropertyToID("_SurfaceAngle");
    static readonly int PulseDurationId = Shader.PropertyToID("_PulseDuration");
    static readonly int PulseScaleId = Shader.PropertyToID("_PulseScale");
    static readonly int PulseIntensityId = Shader.PropertyToID("_PulseIntensity");
    static readonly int SweepSpeedId = Shader.PropertyToID("_SweepSpeed");
    static readonly int SweepIntensityId = Shader.PropertyToID("_SweepIntensity");
    static readonly int AnimTimeId = Shader.PropertyToID("_AnimTime");

    Material glowMat;
    float animationStartTime;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (glowShader == null)
            return;

        StartGlowMaterial();
        animationStartTime = Time.unscaledTime;
        UpdateLayoutParameters();
    }

    protected override void OnDisable()
    {
        material = null;
        if (glowMat != null)
        {
            if (Application.isPlaying)
                Destroy(glowMat);
            else
                DestroyImmediate(glowMat);
        }

        glowMat = null;
        base.OnDisable();
    }

    void LateUpdate()
    {
        if (glowMat != null)
            glowMat.SetFloat(AnimTimeId, Time.unscaledTime - animationStartTime);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateLayoutParameters();
    }

    void StartGlowMaterial()
    {
        glowMat = new Material(glowShader)
        {
            name = $"{nameof(MachiRoomArtworkGlow)} (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        material = glowMat;
        UpdateMaterialSettings();
    }

    void UpdateLayoutParameters()
    {
        if (glowMat == null)
            return;

        Vector2 size = rectTransform.rect.size;
        glowMat.SetVector(InnerSizeId, size);
        glowMat.SetFloat(SweepWidthId, glowWidth / Mathf.Max(size.y, 1f));
        SetVerticesDirty();
    }

    void UpdateMaterialSettings()
    {
        glowMat.SetFloat(OuterGlowWidthId, outerGlowWidth);
        glowMat.SetFloat(OuterGlowIntensityId, outerGlowIntensity);
        glowMat.SetFloat(CornerRadiusId, cornerRadius);
        glowMat.SetFloat(GlowCornerRadiusId, glowCornerRadius);
        glowMat.SetFloat(InnerRimWidthId, innerRimWidth);
        glowMat.SetFloat(InnerRimSpreadId, innerRimSpread);
        glowMat.SetFloat(EdgeSoftnessId, edgeSoftness);
        glowMat.SetFloat(FalloffId, falloff);
        glowMat.SetFloat(SweepSoftnessId, sweepSoftness);
        glowMat.SetFloat(SurfaceAngleId, surfaceAngle);
        glowMat.SetFloat(PulseDurationId, pulseDuration);
        glowMat.SetFloat(PulseScaleId, pulseScale);
        glowMat.SetFloat(PulseIntensityId, pulseIntensity);
        glowMat.SetFloat(SweepSpeedId, sweepSpeed);
        glowMat.SetFloat(SweepIntensityId, sweepIntensity);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float margin = outerGlowWidth * 1.5f;
        Rect meshRect = new Rect(rect.xMin - margin, rect.yMin - margin, rect.width + margin * 2f, rect.height + margin * 2f);
        Vector2 uvMin = new Vector2(-margin / Mathf.Max(rect.width, 1f), -margin / Mathf.Max(rect.height, 1f));
        Vector2 uvMax = new Vector2(1f + margin / Mathf.Max(rect.width, 1f), 1f + margin / Mathf.Max(rect.height, 1f));
        Color32 vertexColor = color;
        vh.AddVert(new Vector3(meshRect.xMin, meshRect.yMin), vertexColor, new Vector2(uvMin.x, uvMin.y));
        vh.AddVert(new Vector3(meshRect.xMin, meshRect.yMax), vertexColor, new Vector2(uvMin.x, uvMax.y));
        vh.AddVert(new Vector3(meshRect.xMax, meshRect.yMax), vertexColor, new Vector2(uvMax.x, uvMax.y));
        vh.AddVert(new Vector3(meshRect.xMax, meshRect.yMin), vertexColor, new Vector2(uvMax.x, uvMin.y));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (glowMat == null)
            return;

        UpdateMaterialSettings();
        UpdateLayoutParameters();
    }
#endif
}
