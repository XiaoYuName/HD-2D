using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 相机与角色之间的遮挡物自动半透明，对应原作的 RegionCamera。
///
/// 原作逻辑（RegionCamera.CheckAntiShelter）：
///   FixedUpdate 从相机沿 forward 打 Physics.RaycastAll(50)，
///   命中 tag == "Anti_Shelter" 的物体就换成 Map_Element_Trasnprent 材质，
///   _Alpha 1 → 0.3（0.3 秒 OutQuad）；不再命中则 0.3 → 1（0.2 秒）后还原原材质。
///   原作还会把 sortingLayerName 在 "Defaulf" / "Decoration" 之间切（两处拼写都是原作里的）。
///
/// 这里用 Mathf.MoveTowards 代替 DOTween，避免为一个淡入淡出引入额外依赖；
/// 排序层切换保留为可选项，因为本工程还没有建 "Decoration" 排序层。
/// </summary>
[RequireComponent(typeof(Camera))]
public class RegionCameraAntiShelter : MonoBehaviour
{
    private class Entry
    {
        public SpriteRenderer Renderer;
        public Material Original;
        public Material Instance;
        public float Alpha;
        public bool Fading;      // true = 正在恢复不透明
    }

    [Header("原作参数")]
    [SerializeField] private string antiShelterTag = "Anti_Shelter";
    [SerializeField] private float rayDistance = 50f;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private float transparentAlpha = 0.3f;
    [SerializeField] private float fadeOutSeconds = 0.3f;   // 变透明
    [SerializeField] private float fadeInSeconds = 0.2f;    // 恢复

    [Header("排序层（本工程暂未建，留空则不切）")]
    [SerializeField] private string transparentSortingLayer = "";
    [SerializeField] private string defaultSortingLayer = "";

    private readonly Dictionary<int, Entry> active = new();
    private readonly List<int> stale = new();
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private void FixedUpdate()
    {
        if (transparentMaterial == null) return;

        stale.Clear();
        foreach (var kv in active) stale.Add(kv.Key);

        int n = Physics.RaycastNonAlloc(transform.position, transform.forward, hits, rayDistance,
                                        ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var tr = hits[i].transform;
            if (tr == null || !tr.CompareTag(antiShelterTag)) continue;

            int id = tr.GetInstanceID();
            if (active.TryGetValue(id, out var got)) { got.Fading = false; stale.Remove(id); continue; }

            var sr = tr.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            var inst = new Material(transparentMaterial);
            inst.SetFloat(AlphaId, 1f);
            var e = new Entry { Renderer = sr, Original = sr.sharedMaterial, Instance = inst, Alpha = 1f, Fading = false };
            sr.sharedMaterial = inst;
            if (!string.IsNullOrEmpty(transparentSortingLayer)) sr.sortingLayerName = transparentSortingLayer;
            active[id] = e;
        }

        foreach (var id in stale) if (active.TryGetValue(id, out var e)) e.Fading = true;
    }

    private void LateUpdate()
    {
        if (active.Count == 0) return;

        stale.Clear();
        foreach (var kv in active)
        {
            var e = kv.Value;
            if (e.Renderer == null) { stale.Add(kv.Key); continue; }

            float target = e.Fading ? 1f : transparentAlpha;
            float dur = e.Fading ? fadeInSeconds : fadeOutSeconds;
            float step = dur <= 0f ? Mathf.Infinity : (1f - transparentAlpha) / dur;
            e.Alpha = Mathf.MoveTowards(e.Alpha, target, step * Time.deltaTime);
            e.Instance.SetFloat(AlphaId, e.Alpha);

            // 恢复完成才换回原材质，和原作 OnComplete 的时机一致
            if (e.Fading && e.Alpha >= 0.999f)
            {
                e.Renderer.sharedMaterial = e.Original;
                if (!string.IsNullOrEmpty(defaultSortingLayer)) e.Renderer.sortingLayerName = defaultSortingLayer;
                stale.Add(kv.Key);
            }
        }
        foreach (var id in stale)
        {
            if (active.TryGetValue(id, out var e) && e.Instance != null) DestroyImmediate(e.Instance);
            active.Remove(id);
        }
    }

    private void OnDisable()
    {
        foreach (var kv in active)
        {
            var e = kv.Value;
            if (e.Renderer != null) e.Renderer.sharedMaterial = e.Original;
            if (e.Instance != null) DestroyImmediate(e.Instance);
        }
        active.Clear();
    }
}
