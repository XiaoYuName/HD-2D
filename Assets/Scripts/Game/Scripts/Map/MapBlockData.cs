using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 地图块数据，字段名照原作 Ghostmon.MapBlockData 的序列化结构：
///
///   AreaSign: 1
///   blockCoord: {x: 26, y: 3}
///   tilemapData:
///   - name: TM_21
///     sortingOrder: 21
///     fill_Tile: {RuleTile}
///     fill_Coords: [{x:416,y:57,z:0}, ...]      ← 全局格子坐标
///
/// 关键点：**Block 预制体里没有 Tilemap**，它只描述"这些格子用哪个 RuleTile 填、在哪一层"，
/// 运行时由区域加载器画进全局的 Tilemap_Root。装饰（草/花/树）则是块自己的 SpriteRenderer 子物体。
/// </summary>
public class MapBlockData : MonoBehaviour
{
    [Serializable]
    public class TilemapFill
    {
        public string name;
        public int sortingOrder;
        public TileBase fill_Tile;
        public List<Vector3Int> fill_Coords = new();
    }

    public int AreaSign = 1;
    public Vector2Int blockCoord;
    public List<TilemapFill> tilemapData = new();

    /// <summary>把本块的地表描述画进目标 Grid 下对应 sortingOrder 的 Tilemap。</summary>
    public void Apply(Transform tilemapRoot)
    {
        if (tilemapRoot == null) return;

        foreach (var d in tilemapData)
        {
            if (d.fill_Tile == null || d.fill_Coords == null) continue;

            Tilemap target = null;
            foreach (var tm in tilemapRoot.GetComponentsInChildren<Tilemap>())
            {
                var tr = tm.GetComponent<TilemapRenderer>();
                if (tr != null && tr.sortingOrder == d.sortingOrder) { target = tm; break; }
            }
            if (target == null)
            {
                Debug.LogWarning($"[MapBlockData] {name}: 找不到 sortingOrder={d.sortingOrder} 的 Tilemap", this);
                continue;
            }

            foreach (var c in d.fill_Coords) target.SetTile(c, d.fill_Tile);
        }
    }
}
