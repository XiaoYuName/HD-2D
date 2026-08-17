using DG.Tweening;
using UnityEngine;

/// <summary>
/// 入夜时跟随角色的聚光灯，对应原作 RegionManager 里挂在 MainCharacter 下的 "Point Light"
/// （名字叫 Point 但 m_Type = 0，其实是 Spot），由 EnvironmentCtrl.InNight/InDay 控制强度：
///
///   InNight: DOVirtual.Float(spotLight.intensity, 300f, 5f, ...)  然后 SetActive(true)
///   InDay:   渐降到 0 后 SetActive(false)
///
/// 原作序列化值：Spot / 强度 300 / range 30 / 锥角 30 / 颜色 (1, 0.949, 0.890) / 无阴影，
/// 挂点 localPosition (0, 100, -30)、俯角 70°。
/// 但 (0,100,-30) 距角色 104 单位而 range 只有 30，光锥够不到地面——那组位置是失效的遗留值。
/// 这里保留方向（同一比例）、颜色、锥角、无阴影，把距离缩到 range 覆盖得到的位置。
/// </summary>
[ExecuteAlways]
public class CharacterNightLight : MonoBehaviour
{
    [Header("原作序列化值")]
    [SerializeField] private Color lightColor = new Color(1f, 0.9490196f, 0.8901961f, 1f);
    [SerializeField] private float range = 30f;
    [SerializeField] private float spotAngle = 30f;

    [Header("强度")]
    [Tooltip("原作是 300，但那是配合 104 单位距离的；这里距离缩短了，强度要同步下调")]
    [SerializeField] private float nightIntensity = 40f;
    [SerializeField] private float fadeSeconds = 5f;   // 原作 DOVirtual.Float(..., 5f, ...)

    [Header("挂点")]
    [Tooltip("原作方向 (0, 100, -30) 的等比缩放；保持俯角 70 度")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 19.1f, -5.73f);
    [SerializeField] private float pitch = 70f;

    [Header("状态")]
    [SerializeField] private bool isNight = true;

    private Light spot;
    private Tween tween;

    public bool IsNight => isNight;

    private void OnEnable()
    {
        EnsureLight();
        // 进场直接给到位；渐变只发生在 SetNight 切换时（和原作一致）
        spot.intensity = isNight ? nightIntensity : 0f;
        spot.gameObject.SetActive(isNight);
    }

    private void OnDisable()
    {
        if (tween != null && tween.IsActive()) tween.Kill();
    }

    private void EnsureLight()
    {
        if (spot != null) return;

        var t = transform.Find("NightSpotLight");
        if (t == null)
        {
            var go = new GameObject("NightSpotLight");
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        t.localPosition = localOffset;
        t.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        spot = t.GetComponent<Light>();
        if (spot == null) spot = t.gameObject.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = lightColor;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = 0f;
        spot.shadows = LightShadows.None;   // 原作 m_Shadows.m_Type = 0
    }

    /// <summary>
    /// 对应原作 EnvironmentCtrl 的 InNight / InDay：
    ///   InNight: SetActive(true) 后 DOVirtual.Float(intensity, 300f, 5f).SetEase(Ease.Linear)
    ///   InDay:   渐降到 0，OnComplete 里 SetActive(false)
    /// </summary>
    public void SetNight(bool night)
    {
        isNight = night;
        EnsureLight();

        if (tween != null && tween.IsActive()) tween.Kill();

        if (!Application.isPlaying)
        {
            spot.intensity = night ? nightIntensity : 0f;
            spot.gameObject.SetActive(night);
            return;
        }

        if (night)
        {
            spot.gameObject.SetActive(true);
            tween = DOVirtual.Float(spot.intensity, nightIntensity, fadeSeconds, x => { if (spot != null) spot.intensity = x; })
                             .SetEase(Ease.Linear);
        }
        else
        {
            tween = DOVirtual.Float(spot.intensity, 0f, fadeSeconds, x => { if (spot != null) spot.intensity = x; })
                             .SetEase(Ease.Linear)
                             .OnComplete(() => { if (spot != null) spot.gameObject.SetActive(false); });
        }
    }

    private void Update()
    {
        // 编辑态没有 tween，直接跟着 Inspector 的开关走
        if (Application.isPlaying) return;
        if (spot == null) { EnsureLight(); return; }
        spot.intensity = isNight ? nightIntensity : 0f;
        spot.gameObject.SetActive(isNight);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            spot = null;
            EnsureLight();
            spot.intensity = isNight ? nightIntensity : 0f;
            spot.gameObject.SetActive(isNight);
        };
    }
#endif
}
