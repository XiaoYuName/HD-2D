using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class PhotoSceneConfigPanel : MonoBehaviour
{   // 照片预览图面板
    [SerializeField] Image bgImage, characterImage;

    [Header("光影效果")]
    [Tooltip("光影预设等静态数据来源")]
    [SerializeField] PhotoStudioGameConfig config;
    [Tooltip("使用 UI/UIPhotoLighting 着色器的材质；留空则运行时自动查找该着色器")]
    [SerializeField] Material lightingMaterial;
    [Tooltip("置于最上层、铺满拍摄区域的暗角叠加层，用于让暗角统一作用于背景与角色")]
    [SerializeField] RawImage overlayImage;

    Material bgMatInst, charMatInst, overlayMatInst;    // 运行时材质实例，避免污染共享材质资源
    long currentLightingId;     // 当前应用的光影ID（0 表示尚未应用）

    static readonly int BrightnessID = Shader.PropertyToID("_Brightness");
    static readonly int ContrastID = Shader.PropertyToID("_Contrast");
    static readonly int SaturationID = Shader.PropertyToID("_Saturation");
    static readonly int TemperatureID = Shader.PropertyToID("_Temperature");
    static readonly int GradeColorID = Shader.PropertyToID("_GradeColor");
    static readonly int VignetteIntensityID = Shader.PropertyToID("_VignetteIntensity");
    static readonly int VignetteStartID = Shader.PropertyToID("_VignetteStart");
    static readonly int VignetteColorID = Shader.PropertyToID("_VignetteColor");

    public long CurrentLightingId => currentLightingId;
    public int LightingCount => config.LightingPresets.Count;

    // 按四项ID初始化预览：背景 + 角色(姿势ID+服装ID解析) + 光影
    public void Init(long bgId, long lightingId, long postureId, long clothesId)
    {
        SelectBackground(bgId);
        SetCharacterSprite(config.GetCharacterSprite(postureId, clothesId));
        ApplyLighting(lightingId);
    }
    // 按ID选择背景图
    public void SelectBackground(long bgId)
    {
        PhotoSceneBgConfig bg = config.GetSceneBg(bgId);
        bgImage.sprite = bg.bgSprite; 
    }

    // 直接设置角色图（角色图由姿势+服装组合在外部解析后传入）
    public void SetCharacterSprite(Sprite sprite)
    {
        characterImage.sprite = sprite; 
    }

    // 按ID选择光影预设并应用到背景与角色
    public void ApplyLighting(long lightingId)
    {
        PhotoLightingPreset preset = config.GetLighting(lightingId);
        if(preset == null)
            return;
        currentLightingId = lightingId;

        EnsureMaterials();
        // 调色（亮度/对比度/饱和度/色温/染色）逐图应用，背景与角色用同一套预设即等效于对合成画面调色
        ApplyPresetToMaterial(bgMatInst, preset);
        ApplyPresetToMaterial(charMatInst, preset);
        // 暗角由最上层叠加层统一处理，从而对背景与角色一起生效
        ApplyVignetteToOverlay(overlayMatInst, preset);
    }

    // 切换到下一套光影（可绑定到按钮）
    [Button("下一个光影")]
    public void NextLighting()
    {
        var ids = new List<long>(config.LightingPresets.Keys);
        if(ids.Count == 0)
            return;
        int cur = ids.IndexOf(currentLightingId);
        ApplyLighting(ids[(cur + 1) % ids.Count]);
    }

    // 逐图调色，不含暗角（暗角统一交给叠加层）
#if UNITY_EDITOR
    // 把角色图当前的位置与尺寸烘焙成“父物体占比”锚点：anchorMin/Max 用比例、offset 归零。
    // 在编辑器里摆好角色图后点一下即可，之后父物体无论改多宽多高，角色图都保持同等占比。
    [Button("固定角色图为父物体占比")]
    void BakeCharacterAnchors()
    {
        if(characterImage == null)
        {
            Debug.LogWarning("[PhotoSceneConfigPanel] 未设置 characterImage（Char）。");
            return;
        }

        RectTransform rt = characterImage.rectTransform;
        UnityEditor.Undo.RecordObject(rt, "Bake Character Anchors");
        if(!UIAnchorTool.SetProportionalAnchors(rt))
        {
            Debug.LogWarning("[PhotoSceneConfigPanel] 固定占比失败：角色图缺少 RectTransform 父物体或父物体尺寸为 0。");
            return;
        }

        UnityEditor.EditorUtility.SetDirty(rt);
        if(!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[PhotoSceneConfigPanel] 已固定角色图占比：anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}。");
    }

    // 一键在拍摄区域最上层创建并绑定暗角叠加层（铺满背景所在区域，不挡点击）
    [Button("创建/绑定暗角叠加层")]
    void CreateOverlayImage()
    {
        if(overlayImage != null)
        {
            Debug.LogWarning("[PhotoSceneConfigPanel] 暗角叠加层已存在，无需重复创建。");
            return;
        }

        Transform parent = bgImage != null ? bgImage.transform.parent : transform;

        GameObject go = new GameObject("LightingOverlay", typeof(RectTransform));
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Lighting Overlay");
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        // 铺满父物体
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();  // 置于最上层，覆盖背景与角色

        RawImage raw = go.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        overlayImage = raw;
        UnityEditor.EditorUtility.SetDirty(this);
        if(!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[PhotoSceneConfigPanel] 暗角叠加层已创建并绑定。");
    }
#endif

    // 逐图调色，不含暗角（暗角统一交给叠加层）
    void ApplyPresetToMaterial(Material m, PhotoLightingPreset p)
    {
        if(m == null)
            return;
        m.SetFloat(BrightnessID, p.brightness);
        m.SetFloat(ContrastID, p.contrast);
        m.SetFloat(SaturationID, p.saturation);
        m.SetFloat(TemperatureID, p.temperature);
        m.SetColor(GradeColorID, p.gradeColor);
        m.SetFloat(VignetteIntensityID, 0f);
    }

    // 叠加层只负责暗角，使其对背景与角色统一生效
    void ApplyVignetteToOverlay(Material m, PhotoLightingPreset p)
    {
        m.SetFloat(VignetteIntensityID, p.vignetteIntensity);
        m.SetFloat(VignetteStartID, p.vignetteStart);
        m.SetColor(VignetteColorID, p.vignetteColor);
    }

    void EnsureMaterials()
    {
        bgMatInst = EnsureGraphicMaterial(bgImage, bgMatInst, "UI/UIPhotoLighting", lightingMaterial);
        charMatInst = EnsureGraphicMaterial(characterImage, charMatInst, "UI/UIPhotoLighting", lightingMaterial);
        overlayMatInst = EnsureGraphicMaterial(overlayImage, overlayMatInst, "UI/UIVignetteOverlay", null);
    }

    Material EnsureGraphicMaterial(Graphic g, Material inst, string shaderName, Material template)
    {
        if(g == null)
            return inst;

        if(inst == null)
        {
            if(template != null)
            {
                inst = new Material(template);
            }
            else
            {
                Shader sh = Shader.Find(shaderName);
                if(sh == null)
                {
                    Debug.LogWarning($"[PhotoSceneConfigPanel] 未找到 {shaderName} 着色器，光影效果不可用。");
                    return null;
                }
                inst = new Material(sh);
            }
            inst.hideFlags = HideFlags.DontSave;
        }

        if(g.material != inst)
            g.material = inst;
        return inst;
    }

    void OnDestroy()
    {
        DestroyMat(ref bgMatInst, bgImage);
        DestroyMat(ref charMatInst, characterImage);
        DestroyMat(ref overlayMatInst, overlayImage);
    }

    void DestroyMat(ref Material inst, Graphic g)
    {
        if(inst == null)
            return;
        if(g != null && g.material == inst)
            g.material = null;    // 恢复默认 UI 材质
        if(Application.isPlaying)
            Destroy(inst);
        else
            DestroyImmediate(inst);
        inst = null;
    }
}
