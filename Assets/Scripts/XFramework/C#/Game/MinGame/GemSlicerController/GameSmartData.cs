using Slicer2D;
using UnityEngine;

/// <summary>
/// 挂在宝石预制体根节点上，把这一局切割需要的组件收集好交给 GemCutController。
/// </summary>
public class GameSmartData : MonoBehaviour
{
    /// <summary>目标切割路径（虚线）。</summary>
    public GemCutTarget GemCutTarget { get; private set; }

    /// <summary>可切割的宝石本体。</summary>
    public Sliceable2D Gem { get; private set; }

    public void Init()
    {
        GemCutTarget = GetComponentInChildren<GemCutTarget>();
        Gem = GetComponentInChildren<Sliceable2D>();
    }
}
