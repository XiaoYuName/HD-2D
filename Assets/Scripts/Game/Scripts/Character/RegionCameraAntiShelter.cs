using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 相机与角色之间的遮挡物自动半透明，对应原作的 RegionCamera。
///
/// 原作实现（RegionCamera.CheckAntiShelter / AddToCache / RemoveFromCache）：
///   FixedUpdate 从相机沿 forward 打 Physics.RaycastAll(50)，
///   命中 tag == "Anti_Shelter" 的物体 → 换成 Map_Element_Trasnprent 材质，
///   _Alpha 1 → 0.3（0.3 秒 OutQuad）；不再命中 → 0.3 → 1（0.2 秒 OutQuad），
///   OnComplete 里还原原材质并把 sortingLayerName 改回 "Decoration"。
///   （材质名 Trasnprent、排序层 Defaulf 的拼写都是原作里就有的。）
/// </summary>
[RequireComponent(typeof(Camera))]
public class RegionCameraAntiShelter : MonoBehaviour
{
    [Header("原作参数")]
    [SerializeField] private string antiShelterTag = "Anti_Shelter";
    [SerializeField] private float rayDistance = 50f;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private float transparentAlpha = 0.3f;
    [SerializeField] private float fadeOutSeconds = 0.3f;
    [SerializeField] private float fadeInSeconds = 0.2f;

    [Header("排序层（本工程还没建这两层，留空则不切）")]
    [SerializeField] private string transparentSortingLayer = "";
    [SerializeField] private string defaultSortingLayer = "";

    private readonly Dictionary<int, RaycastHit> hitCache = new();
    private readonly Dictionary<int, Material> matCache = new();
    private readonly List<int> recoverCache = new();     // 正在恢复中，避免又被抓回去
    private List<int> unsafeCache = new();
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private void FixedUpdate()
    {
        if (transparentMaterial == null) return;
        CheckAntiShelter();
    }

    private void CheckAntiShelter()
    {
        RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, rayDistance,
                                               ~0, QueryTriggerInteraction.Collide);
        unsafeCache = hitCache.Keys.ToList();

        foreach (var rh in hits)
        {
            if (rh.transform == null || !rh.transform.CompareTag(antiShelterTag)) continue;
            int id = rh.transform.GetInstanceID();
            if (recoverCache.Contains(id)) continue;

            if (!hitCache.ContainsKey(id)) AddToCache(rh);
            else unsafeCache.Remove(id);
        }

        foreach (int id in unsafeCache) RemoveFromCache(id);
    }

    private void AddToCache(RaycastHit rh)
    {
        var sr = rh.transform.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        int id = rh.transform.GetInstanceID();
        matCache[id] = sr.sharedMaterial;
        hitCache[id] = rh;
        SetMaterialTransparent(rh.transform);
    }

    private void RemoveFromCache(int id)
    {
        if (hitCache.TryGetValue(id, out var rh) && rh.transform != null) SetMaterialDefault(rh.transform);
        hitCache.Remove(id);
        matCache.Remove(id);
    }

    private void SetMaterialTransparent(Transform go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null || transparentMaterial == null) return;

        if (!string.IsNullOrEmpty(transparentSortingLayer)) sr.sortingLayerName = transparentSortingLayer;
        sr.material = transparentMaterial;   // .material 会自动实例化，各自独立淡入淡出
        DOVirtual.Float(1f, transparentAlpha, fadeOutSeconds, x =>
        {
            if (sr != null) sr.material.SetFloat(AlphaId, x);
        }).SetEase(Ease.OutQuad);
    }

    private void SetMaterialDefault(Transform go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        int id = go.GetInstanceID();
        if (sr == null || !matCache.TryGetValue(id, out var original)) return;

        recoverCache.Add(id);
        DOVirtual.Float(transparentAlpha, 1f, fadeInSeconds, x =>
        {
            if (sr != null) sr.material.SetFloat(AlphaId, x);
        }).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            recoverCache.Remove(id);
            if (sr == null) return;
            sr.sharedMaterial = original;
            if (!string.IsNullOrEmpty(defaultSortingLayer)) sr.sortingLayerName = defaultSortingLayer;
        });
    }

    private void OnDisable()
    {
        foreach (var kv in hitCache)
        {
            if (kv.Value.transform == null) continue;
            var sr = kv.Value.transform.GetComponent<SpriteRenderer>();
            if (sr != null && matCache.TryGetValue(kv.Key, out var m)) sr.sharedMaterial = m;
        }
        hitCache.Clear();
        matCache.Clear();
        recoverCache.Clear();
    }
}
