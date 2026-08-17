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
    private float target;
    private float velocity;

    public bool IsNight => isNight;

    private void OnEnable()
    {
        EnsureLight();
        // 编辑态直接给到位，运行时才做渐变
        spot.intensity = isNight ? nightIntensity : 0f;
        target = spot.intensity;
        spot.gameObject.SetActive(isNight);
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

    /// <summary>对应原作的 InNight / InDay。</summary>
    public void SetNight(bool night)
    {
        isNight = night;
        EnsureLight();
        if (night) spot.gameObject.SetActive(true);
        target = night ? nightIntensity : 0f;
    }

    private void Update()
    {
        if (spot == null) { EnsureLight(); return; }
        if (!Application.isPlaying)
        {
            spot.intensity = isNight ? nightIntensity : 0f;
            spot.gameObject.SetActive(isNight);
            return;
        }

        if (Mathf.Approximately(spot.intensity, target)) return;

        float step = (fadeSeconds <= 0f) ? Mathf.Infinity : nightIntensity / fadeSeconds;
        spot.intensity = Mathf.MoveTowards(spot.intensity, target, step * Time.deltaTime);

        // 白天渐降到 0 之后关掉，和原作 OnComplete 里的 SetActive(false) 一致
        if (!isNight && spot.intensity <= 0.001f) spot.gameObject.SetActive(false);
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
