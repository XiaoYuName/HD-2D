using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 刺绣模拟小游戏的总配置。
/// <para>
/// 字典 key 是服装 Id（例如 1001、1002），value 是该服装对应的一局刺绣棋盘。
/// 运行时只读取此资产，不依赖编辑器生成的临时对象。
/// </para>
/// </summary>
[CreateAssetMenu(
    fileName = nameof(DressMakingEmbroiderySimulationGameConfig),
    menuName = "MiniGame/DressMaking/" + nameof(DressMakingEmbroiderySimulationGameConfig))]
public class DressMakingEmbroiderySimulationGameConfig : SerializedScriptableObject
{
    public const int CurrentSchemaVersion = 5;

    public const string StitchTextureFolder =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DressMaking/"
        + "DressMakingEmbroiderySimulationGamePanel/Textures/EmbroideryFills";
    public const string DefaultStitchTexturePath = StitchTextureFolder + "/EmbroideryFill_CreamDiagonal.png";
    public const string PreviewSpriteFolder =
        "Assets/AddressableAssets/Remote/Prefabs/UGUI/DressMaking/"
        + "DressMakingEmbroiderySimulationGamePanel/Textures/EmbroideryPreviews";

    public const string DefaultConfigPath =
        "Assets/AddressableAssets/Remote/Configs/MiniGame/DressMaking/"
        + nameof(DressMakingEmbroiderySimulationGameConfig) + ".asset";

    [SerializeField, HideInInspector]
    private int schemaVersion;

    [Title("服装 → 刺绣关卡")]
    [LabelText("关卡字典（Key = 服装Id）")]
    [DictionaryDrawerSettings(KeyLabel = "服装Id", ValueLabel = "刺绣关卡")]
    [SerializeField]
    public Dictionary<long, DressMakingEmbroideryLevelData> dataDict = new();

    [Title("玩法规则（所有关卡统一）")]
    [LabelText("必须从数字块起绣")]
    [Tooltip("打开后一次刺绣只能从数字块按下起笔；关闭则任意未完成块都能起笔。")]
    public bool mustStartFromNumberBlock = true;

    [Title("运行时默认值")]
    [LabelText("未填充区域颜色")]
    [SerializeField]
    public Color defaultEmptyColor = new(0.18f, 0.65f, 0.62f, 1f);

    [LabelText("区域边界颜色")]
    [SerializeField]
    public Color defaultOutlineColor = new(0.88f, 0.95f, 0.95f, 0.75f);

    [LabelText("区域边界宽度")]
    [MinValue(0f)]
    [SerializeField]
    public float outlineWidth = 1.5f;

    /// <summary>服装 Id → 关卡数据。返回原字典，便于运行时避免额外分配。</summary>
    public Dictionary<long, DressMakingEmbroideryLevelData> DataDict => dataDict;

    /// <summary>显式命名的数据字典别名，便于运行时代码表达查询意图。</summary>
    public Dictionary<long, DressMakingEmbroideryLevelData> GetDataDict() => dataDict;

    /// <summary><see cref="DataDict"/> 的语义别名，兼容旧代码中的 LevelDict 命名。</summary>
    public Dictionary<long, DressMakingEmbroideryLevelData> LevelDict => dataDict;

    /// <summary>统一玩法开关：一次刺绣是否必须从数字块起笔。</summary>
    public bool MustStartFromNumberBlock => mustStartFromNumberBlock;

    public int SchemaVersion => schemaVersion;

    public bool Contains(long clothingId) => dataDict != null && dataDict.ContainsKey(clothingId);

    /// <summary>服装 Id → 预览图；未配置返回 null。</summary>
    public string GetPreviewSpritePath(long clothingId)
        => dataDict != null && dataDict.TryGetValue(clothingId, out DressMakingEmbroideryLevelData level) && level != null
            ? level.PreviewSpritePath
            : null;

    /// <summary>获取服装对应关卡；未配置时抛出 KeyNotFoundException，便于尽早发现资源问题。</summary>
    public DressMakingEmbroideryLevelData GetLevel(long clothingId) => dataDict[clothingId];

    /// <summary>仅在需要兼容“Get”命名的调用点使用。</summary>
    public DressMakingEmbroideryLevelData Get(long clothingId) => GetLevel(clothingId);
    public void SetLevel(long clothingId, DressMakingEmbroideryLevelData level)
    {
        if (level == null)
            throw new ArgumentNullException(nameof(level));
        dataDict ??= new Dictionary<long, DressMakingEmbroideryLevelData>();
        level.clothingId = clothingId;
        dataDict[clothingId] = level;
    }

    public bool RemoveLevel(long clothingId) => dataDict != null && dataDict.Remove(clothingId);

    /// <summary>
    /// 保证示例服装 1001、1002 存在。编辑器窗口首次创建资产时调用。
    /// 已存在的关卡不会被覆盖，避免误伤用户已经编辑的内容。
    /// </summary>
    public void EnsureSampleData()
    {
        dataDict ??= new Dictionary<long, DressMakingEmbroideryLevelData>();
        if (!dataDict.ContainsKey(1001))
            dataDict.Add(1001, DressMakingEmbroiderySample.CreateLevel1001());
        if (!dataDict.ContainsKey(1002))
            dataDict.Add(1002, DressMakingEmbroiderySample.CreateLevel1002());
    }

    /// <summary>校验所有关卡，返回可直接显示在编辑器中的问题文本。</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (dataDict == null || dataDict.Count == 0)
        {
            errors.Add("关卡字典为空。");
            return errors;
        }

        foreach (var pair in dataDict)
        {
            if (pair.Value == null)
            {
                errors.Add($"服装 {pair.Key} 的关卡数据为空。");
                continue;
            }

            pair.Value.Validate(pair.Key, errors);
        }

        return errors;
    }

    public void Normalize()
    {
        dataDict ??= new Dictionary<long, DressMakingEmbroideryLevelData>();
        foreach (var pair in dataDict)
        {
            if (pair.Value == null)
                continue;
            pair.Value.clothingId = pair.Key;
            pair.Value.Normalize();
        }
    }

    /// <summary>升级旧配置并返回是否需要重新保存资产。</summary>
    public bool UpgradeSchema()
    {
        if (schemaVersion >= CurrentSchemaVersion)
            return false;

        schemaVersion = CurrentSchemaVersion;
        Normalize();
        return true;
    }
}

/// <summary>
/// 刺绣棋盘的蜿蜒网格显示参数。
/// <para>
/// 网格只负责视觉表现，不参与区域命中或路径规则。spacing、amplitude 和 wavelength
/// 使用棋盘画布单位，运行时与 UIToolkit 工作台共享同一套波形。
/// </para>
/// </summary>
[Serializable]
public sealed class DressMakingEmbroideryWavyGridSettings
{
    [LabelText("启用蜿蜒网格")]
    public bool enabled = true;

    [LabelText("叠加在区域上方")]
    public bool overlay = true;

    [LabelText("网格颜色")]
    public Color color = new(0.78f, 0.96f, 0.94f, 0.2f);

    [LabelText("列间距")]
    [MinValue(8f)]
    public float columnSpacing = 96f;

    [LabelText("行间距")]
    [MinValue(8f)]
    public float rowSpacing = 96f;

    [LabelText("列曲折幅度")]
    [MinValue(0f)]
    public float columnAmplitude = 10f;

    [LabelText("行曲折幅度")]
    [MinValue(0f)]
    public float rowAmplitude = 10f;

    [LabelText("列波长")]
    [MinValue(16f)]
    public float columnWavelength = 260f;

    [LabelText("行波长")]
    [MinValue(16f)]
    public float rowWavelength = 260f;

    [LabelText("线宽")]
    [MinValue(0.25f)]
    public float lineWidth = 1.5f;

    [LabelText("每条线采样段数")]
    [Range(4, 128)]
    public int segmentsPerLine = 36;

    [LabelText("绘制虚线")]
    public bool dashed;

    [LabelText("虚线长度")]
    [MinValue(1f)]
    public float dashLength = 34f;

    [LabelText("虚线间隔")]
    [MinValue(1f)]
    public float gapLength = 18f;

    [LabelText("波形相位")]
    public float phase;

    public float ColumnSpacing => Mathf.Max(8f, columnSpacing);
    public float RowSpacing => Mathf.Max(8f, rowSpacing);
    public float ColumnAmplitude => Mathf.Max(0f, columnAmplitude);
    public float RowAmplitude => Mathf.Max(0f, rowAmplitude);
    public float ColumnWavelength => Mathf.Max(16f, columnWavelength);
    public float RowWavelength => Mathf.Max(16f, rowWavelength);
    public float LineWidth => Mathf.Max(0.25f, lineWidth);
    public int SegmentsPerLine => Mathf.Clamp(segmentsPerLine, 4, 128);
    public float DashLength => Mathf.Max(1f, dashLength);
    public float GapLength => Mathf.Max(1f, gapLength);

    public void Normalize()
    {
        color.a = Mathf.Clamp01(color.a);
        columnSpacing = ColumnSpacing;
        rowSpacing = RowSpacing;
        columnAmplitude = ColumnAmplitude;
        rowAmplitude = RowAmplitude;
        columnWavelength = ColumnWavelength;
        rowWavelength = RowWavelength;
        lineWidth = LineWidth;
        segmentsPerLine = SegmentsPerLine;
        dashLength = DashLength;
        gapLength = GapLength;
    }
}

/// <summary>由网格单元边界生成的棕色分段线与外轮廓。</summary>
[Serializable]
public sealed class DressMakingEmbroideryGridDividerSettings
{
    [LabelText("启用单元分割线")]
    public bool enabled = true;

    [LabelText("分割线颜色")]
    public Color dividerColor = new(0.47f, 0.30f, 0.30f, 0.72f);

    [LabelText("外轮廓颜色")]
    public Color borderColor = new(0.22f, 0.21f, 0.23f, 1f);

    [LabelText("分割线宽度")]
    [MinValue(0.25f)]
    public float dividerWidth = 3f;

    [LabelText("外轮廓宽度")]
    [MinValue(0.25f)]
    public float borderWidth = 5f;

    [LabelText("虚线长度")]
    [MinValue(1f)]
    public float dashLength = 14f;

    [LabelText("虚线间隔")]
    [MinValue(1f)]
    public float gapLength = 9f;

    [LabelText("绘制外轮廓")]
    public bool drawOuterBorder = true;

    public float DividerWidth => Mathf.Max(0.25f, dividerWidth);
    public float BorderWidth => Mathf.Max(0.25f, borderWidth);
    public float DashLength => Mathf.Max(1f, dashLength);
    public float GapLength => Mathf.Max(1f, gapLength);

    public void Normalize()
    {
        dividerColor.a = Mathf.Clamp01(dividerColor.a);
        borderColor.a = Mathf.Clamp01(borderColor.a);
        dividerWidth = DividerWidth;
        borderWidth = BorderWidth;
        dashLength = DashLength;
        gapLength = GapLength;
    }
}

/// <summary>工作台中用于生成网格单元的一条折线或二次贝塞尔曲线；交点由编辑器自动计算。</summary>
[Serializable]
public sealed class DressMakingEmbroideryGridLineData
{
    public long id;
    public bool isBoundary;
    public bool isClosed = false;
    public bool useBezier;
    public int curveSegments = 12;
    public List<Vector2> points = new();
    public List<Vector2> bezierControls = new();

    public long Id => id;
    public bool IsBoundary => isBoundary;
    public bool IsClosed => isClosed;
    public bool UseBezier => useBezier;
    public int CurveSegments => Mathf.Clamp(curveSegments, 4, 48);
    public List<Vector2> Points => points;
    public List<Vector2> BezierControls => bezierControls ??= new List<Vector2>();
    public int SegmentCount => Mathf.Max(0, points.Count - 1 + (isClosed ? 1 : 0));

    public void Normalize(int fallbackIndex)
    {
        id = id == 0 ? fallbackIndex + 1 : id;
        points ??= new List<Vector2>();
        bezierControls ??= new List<Vector2>();
        curveSegments = CurveSegments;
        for (int i = 0; i < points.Count; i++)
            points[i] = Clamp01(points[i]);
        EnsureBezierControls();
        for (int i = 0; i < bezierControls.Count; i++)
            bezierControls[i] = Clamp01(bezierControls[i]);
    }

    public void EnsureBezierControls()
    {
        bezierControls ??= new List<Vector2>();
        int segmentCount = SegmentCount;
        while (bezierControls.Count > segmentCount)
            bezierControls.RemoveAt(bezierControls.Count - 1);
        while (bezierControls.Count < segmentCount)
        {
            int index = bezierControls.Count;
            Vector2 start = points[index];
            Vector2 end = points[(index + 1) % points.Count];
            bezierControls.Add((start + end) * 0.5f);
        }
    }

    public Vector2 EvaluateSegment(int segmentIndex, float t)
    {
        Vector2 start = points[segmentIndex];
        Vector2 end = points[(segmentIndex + 1) % points.Count];
        if (!useBezier || segmentIndex >= BezierControls.Count)
            return Vector2.LerpUnclamped(start, end, t);
        Vector2 control = bezierControls[segmentIndex];
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static Vector2 Clamp01(Vector2 value)
        => new(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
}
/// <summary>单个服装的刺绣棋盘。</summary>
[Serializable]
public class DressMakingEmbroideryLevelData
{
    [LabelText("服装Id")]
    public long clothingId;

    [LabelText("关卡名称")]
    public string displayName = "刺绣关卡";

    [LabelText("画布尺寸")]
    public Vector2 canvasSize = new(1000f, 640f);

    [LabelText("背景颜色")]
    public Color backgroundColor = new(0.18f, 0.65f, 0.62f, 1f);

    [LabelText("蜿蜒网格")]
    public DressMakingEmbroideryWavyGridSettings wavyGrid = new();

    [LabelText("网格单元分割线")]
    public DressMakingEmbroideryGridDividerSettings gridDivider = new();

    [LabelText("工作台线网")]
    public List<DressMakingEmbroideryGridLineData> gridLines = new();

    [LabelText("编辑器默认选中区域 Id")]
    public long startRegionId;

    [LabelText("数字块本身计入数量")]
    public bool includeNumberBlockInCount = true;

    [LabelText("按区域 Quantity 累加数量")]
    public bool countByQuantity;

    [LabelText("服装预览图路径")]
    public string previewSpritePath = string.Empty;

    [LabelText("生成的棋盘预制体")]
    public GameObject levelPrefab;

    [LabelText("预制体资源路径（只读缓存）")]
    [ReadOnly]
    public string levelPrefabPath = string.Empty;

    [LabelText("网格单元")]
    [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, ShowIndexLabels = true)]
    public List<DressMakingEmbroideryRegionData> regions = new();

    public long ClothingId => clothingId;
    public string DisplayName => displayName;
    public Vector2 CanvasSize => canvasSize;
    public Color BackgroundColor => backgroundColor;
    public DressMakingEmbroideryWavyGridSettings WavyGrid =>
        wavyGrid ??= new DressMakingEmbroideryWavyGridSettings();
    public DressMakingEmbroideryGridDividerSettings GridDivider =>
        gridDivider ??= new DressMakingEmbroideryGridDividerSettings();
    public List<DressMakingEmbroideryGridLineData> GridLines =>
        gridLines ??= new List<DressMakingEmbroideryGridLineData>();
    public long StartRegionId => startRegionId;
    public bool IncludeNumberBlockInCount => includeNumberBlockInCount;
    public bool CountByQuantity => countByQuantity;
    public string PreviewSpritePath => previewSpritePath;
    public GameObject LevelPrefab => levelPrefab;
    public string LevelPrefabPath => levelPrefabPath;
    public List<DressMakingEmbroideryRegionData> Regions => regions;

    public DressMakingEmbroideryRegionData GetRegion(long regionId)
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i] != null && regions[i].id == regionId)
                return regions[i];
        }

        return null;
    }

    public DressMakingEmbroideryRegionData GetStartRegion()
    {
        DressMakingEmbroideryRegionData start = GetRegion(startRegionId);
        if (start != null)
            return start;

        for (int i = 0; i < regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = regions[i];
            if (region != null && region.isNumberBlock)
                return region;
        }

        return regions.Count > 0 ? regions[0] : null;
    }

    public void Normalize()
    {
        canvasSize.x = Mathf.Max(1f, canvasSize.x);
        canvasSize.y = Mathf.Max(1f, canvasSize.y);
        levelPrefabPath ??= string.Empty;
        previewSpritePath ??= string.Empty;
        wavyGrid ??= new DressMakingEmbroideryWavyGridSettings();
        wavyGrid.Normalize();
        gridDivider ??= new DressMakingEmbroideryGridDividerSettings();
        gridDivider.Normalize();
        gridLines ??= new List<DressMakingEmbroideryGridLineData>();
        for (int i = 0; i < gridLines.Count; i++)
            gridLines[i]?.Normalize(i);
        regions ??= new List<DressMakingEmbroideryRegionData>();

        for (int i = 0; i < regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = regions[i];
            if (region == null)
                continue;
            region.Normalize(i);
        }

    }

    public void Validate(long dictionaryId, List<string> errors)
    {
        if (regions == null || regions.Count == 0)
        {
            errors.Add($"服装 {dictionaryId} 没有区域。");
            return;
        }

        var ids = new HashSet<long>();
        int numberBlockCount = 0;
        int requiredCountTotal = 0;
        for (int i = 0; i < regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = regions[i];
            if (region == null)
            {
                errors.Add($"服装 {dictionaryId} 的区域 {i} 为空。");
                continue;
            }

            if (!ids.Add(region.id))
                errors.Add($"服装 {dictionaryId} 存在重复区域 Id：{region.id}。");
            if (region.boundaryPoints == null || region.boundaryPoints.Count < 3)
                errors.Add($"服装 {dictionaryId} 区域 {region.id} 至少需要 3 个边界点。");
            if (region.isNumberBlock && region.RequiredCount <= 0)
                errors.Add($"服装 {dictionaryId} 区域 {region.id} 的数字块数量必须大于 0。");
            if (region.isNumberBlock)
            {
                numberBlockCount++;
                requiredCountTotal += region.RequiredCount;
            }
        }

        if (numberBlockCount == 0)
            errors.Add($"服装 {dictionaryId} 至少需要一个数字块。");
        else if (requiredCountTotal != regions.Count)
            errors.Add(
                $"服装 {dictionaryId} 的数字总数为 {requiredCountTotal}，必须等于网格数 {regions.Count}。");

        for (int i = 0; i < regions.Count; i++)
        {
            DressMakingEmbroideryRegionData region = regions[i];
            if (region?.neighbourIds == null)
                continue;
            for (int j = 0; j < region.neighbourIds.Count; j++)
            {
                long neighbourId = region.neighbourIds[j];
                if (neighbourId == region.id)
                    errors.Add($"服装 {dictionaryId} 区域 {region.id} 不能与自身邻接。");
                else if (!ids.Contains(neighbourId))
                    errors.Add($"服装 {dictionaryId} 区域 {region.id} 的邻接 Id {neighbourId} 不存在。");
            }
        }
    }
}

/// <summary>
/// 一个可逐格经过、可即时填色的任意形状网格单元。
/// <para>
/// boundaryPoints 使用归一化坐标（左下为 (0,0)，右上为 (1,1)）。
/// bezierPoints 可选：按每条边依次存放出控制点、入控制点，数量应为 boundaryPoints.Count × 2；
/// 为空时按直线多边形处理。
/// </para>
/// </summary>
[Serializable]
public class DressMakingEmbroideryRegionData
{
    public long id;
    public int requiredCount;
    public int quantity = 1;
    public Color fillColor = Color.white;
    public Color completedColor = Color.white;
    [LabelText("刺绣纹理路径")]
    public string fillTexturePath = string.Empty;
    [MinValue(4f)]
    public float stitchTileSize = 32f;
    public string label = string.Empty;
    public Vector2 labelPosition = new(0.5f, 0.5f);
    [MinValue(8f)]
    public float labelFontSize = 48f;
    public bool isNumberBlock;
    public bool isClosed = true;
    public List<Vector2> boundaryPoints = new();
    public List<Vector2> bezierPoints = new();
    [Tooltip("可选的逻辑邻接区域 Id；为空时运行时可按几何边界自动推导。")]
    public List<long> neighbourIds = new();
    public string fillSpritePath = string.Empty;
    public string numberSpritePath = string.Empty;
    public string remark = string.Empty;

    public long Id => id;
    public int RequiredCount => Mathf.Max(0, requiredCount);
    public int Quantity => Mathf.Max(1, quantity);
    public Color FillColor => fillColor;
    public Color CompletedColor => completedColor;
    public string FillTexturePath => fillTexturePath;
    public float StitchTileSize => Mathf.Max(4f, stitchTileSize);
    public string Label => label;
    public Vector2 LabelPosition => labelPosition;
    public float LabelFontSize => Mathf.Max(8f, labelFontSize);
    public bool IsNumberBlock => isNumberBlock;
    public bool IsClosed => isClosed;
    public List<Vector2> BoundaryPoints => boundaryPoints;
    public List<Vector2> BezierPoints => bezierPoints;
    public List<long> NeighbourIds => neighbourIds;
    public string FillSpritePath => fillSpritePath;
    public string NumberSpritePath => numberSpritePath;
    public string Remark => remark;

    // Runtime/editor compatibility aliases requested by the gameplay implementation.
    public Vector2 labelPos
    {
        get => labelPosition;
        set => labelPosition = value;
    }

    public int PassCount => RequiredCount;
    public int BlockCount => Quantity;
    public int Number => RequiredCount;
    public bool HasBezier => boundaryPoints != null
        && bezierPoints != null
        && bezierPoints.Count >= boundaryPoints.Count * 2;

    /// <summary>可选邻接列表的语义别名。空列表表示由运行时按几何边界自动推导。</summary>
    public List<long> Neighbors => neighbourIds;

    /// <summary>
    /// 该区域在规则中一次可消耗的步数。数字块优先使用 requiredCount，普通块使用 quantity。
    /// </summary>
    public int RequiredTraversalCount =>
        isNumberBlock && requiredCount > 0 ? requiredCount : Quantity;

    public int GetTraversalCount() => RequiredTraversalCount;

    public void Normalize(int fallbackIndex = 0)
    {
        id = id == 0 ? fallbackIndex + 1 : id;
        requiredCount = Mathf.Max(0, requiredCount);
        quantity = Mathf.Max(1, quantity);
        isNumberBlock |= requiredCount > 0;
        stitchTileSize = Mathf.Max(4f, stitchTileSize);
        label ??= string.Empty;
        fillTexturePath ??= string.Empty;
        fillSpritePath ??= string.Empty;
        numberSpritePath ??= string.Empty;
        remark ??= string.Empty;
        boundaryPoints ??= new List<Vector2>();
        bezierPoints ??= new List<Vector2>();
        neighbourIds ??= new List<long>();
        labelPosition = Clamp01(labelPosition);
        labelFontSize = LabelFontSize;

        for (int i = 0; i < boundaryPoints.Count; i++)
            boundaryPoints[i] = Clamp01(boundaryPoints[i]);
        for (int i = 0; i < bezierPoints.Count; i++)
            bezierPoints[i] = Clamp01(bezierPoints[i]);
    }

    /// <summary>
    /// 取得用于碰撞/绘制的边界采样点。直线区域直接返回边界副本，曲线区域按 cubic Bézier 采样。
    /// </summary>
    public List<Vector2> GetSampledBoundary(int segmentsPerEdge = 8)
    {
        var result = new List<Vector2>();
        if (boundaryPoints == null || boundaryPoints.Count == 0)
            return result;

        segmentsPerEdge = Mathf.Max(1, segmentsPerEdge);
        if (!HasBezier)
        {
            result.AddRange(boundaryPoints);
            return result;
        }

        int count = boundaryPoints.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 p0 = boundaryPoints[i];
            Vector2 p1 = bezierPoints[i * 2];
            Vector2 p2 = bezierPoints[i * 2 + 1];
            int nextIndex = i + 1;
            if (nextIndex >= count)
            {
                if (!isClosed)
                    break;
                nextIndex = 0;
            }

            Vector2 p3 = boundaryPoints[nextIndex];
            for (int step = 0; step < segmentsPerEdge; step++)
            {
                float t = step / (float)segmentsPerEdge;
                result.Add(EvaluateCubic(p0, p1, p2, p3, t));
            }
        }

        if (!isClosed)
            result.Add(boundaryPoints[count - 1]);

        return result;
    }

    public void SetAutomaticBezierHandles()
    {
        bezierPoints ??= new List<Vector2>();
        bezierPoints.Clear();
        if (boundaryPoints == null || boundaryPoints.Count == 0)
            return;

        int count = boundaryPoints.Count;
        if (!isClosed && count == 1)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2 start = boundaryPoints[i];
            int nextIndex = i + 1;
            if (nextIndex >= count)
            {
                if (!isClosed)
                    break;
                nextIndex = 0;
            }

            Vector2 end = boundaryPoints[nextIndex];
            bezierPoints.Add(Vector2.Lerp(start, end, 1f / 3f));
            bezierPoints.Add(Vector2.Lerp(start, end, 2f / 3f));
        }
    }

    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0
            + 3f * u * u * t * p1
            + 3f * u * t * t * p2
            + t * t * t * p3;
    }

    private static Vector2 Clamp01(Vector2 value)
    {
        return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }
}

/// <summary>内置示例数据，供编辑器一键创建 1001/1002。</summary>
public static class DressMakingEmbroiderySample
{
    public static DressMakingEmbroideryLevelData CreateLevel1001()
    {
        var level = new DressMakingEmbroideryLevelData
        {
            clothingId = 1001,
            displayName = "1001 · 金色花瓣",
            canvasSize = new Vector2(1000f, 640f),
            backgroundColor = new Color(0.17f, 0.62f, 0.60f, 1f),
            startRegionId = 1,
        };

        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 1,
            requiredCount = 2,
            quantity = 1,
            isNumberBlock = true,
            label = "2",
            labelPosition = new Vector2(0.23f, 0.58f),
            fillColor = new Color(0.95f, 0.76f, 0.28f, 1f),
            completedColor = new Color(1f, 0.92f, 0.47f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.08f, 0.15f), new(0.33f, 0.12f), new(0.36f, 0.53f),
                new(0.25f, 0.78f), new(0.07f, 0.65f),
            },
            neighbourIds = new List<long> { 4 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 2,
            requiredCount = 2,
            quantity = 1,
            isNumberBlock = true,
            label = "2",
            labelPosition = new Vector2(0.55f, 0.72f),
            fillColor = new Color(0.96f, 0.79f, 0.34f, 1f),
            completedColor = new Color(1f, 0.93f, 0.53f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.36f, 0.53f), new(0.62f, 0.57f), new(0.66f, 0.89f),
                new(0.44f, 0.96f), new(0.25f, 0.78f),
            },
            neighbourIds = new List<long> { 3 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 3,
            quantity = 1,
            label = string.Empty,
            labelPosition = new Vector2(0.75f, 0.38f),
            fillColor = new Color(0.16f, 0.16f, 0.18f, 1f),
            completedColor = new Color(0.27f, 0.27f, 0.30f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.62f, 0.57f), new(0.66f, 0.89f), new(0.92f, 0.61f), new(0.88f, 0.14f),
                new(0.66f, 0.08f),
            },
            neighbourIds = new List<long> { 2 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 4,
            quantity = 1,
            label = string.Empty,
            labelPosition = new Vector2(0.50f, 0.30f),
            fillColor = new Color(0.16f, 0.16f, 0.18f, 1f),
            completedColor = new Color(0.27f, 0.27f, 0.30f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.33f, 0.12f), new(0.66f, 0.08f), new(0.62f, 0.57f),
                new(0.36f, 0.53f),
            },
            neighbourIds = new List<long> { 1 },
        });

        return level;
    }

    public static DressMakingEmbroideryLevelData CreateLevel1002()
    {
        var level = new DressMakingEmbroideryLevelData
        {
            clothingId = 1002,
            displayName = "1002 · 靛青叶片",
            canvasSize = new Vector2(1000f, 640f),
            backgroundColor = new Color(0.20f, 0.60f, 0.66f, 1f),
            startRegionId = 11,
        };

        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 11,
            requiredCount = 2,
            quantity = 1,
            isNumberBlock = true,
            label = "2",
            labelPosition = new Vector2(0.26f, 0.72f),
            fillColor = new Color(0.20f, 0.38f, 0.40f, 1f),
            completedColor = new Color(0.33f, 0.55f, 0.55f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.08f, 0.52f), new(0.45f, 0.52f),
                new(0.45f, 0.92f), new(0.12f, 0.88f),
            },
            neighbourIds = new List<long> { 14 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 12,
            requiredCount = 2,
            quantity = 1,
            isNumberBlock = true,
            label = "2",
            labelPosition = new Vector2(0.68f, 0.74f),
            fillColor = new Color(0.94f, 0.70f, 0.32f, 1f),
            completedColor = new Color(1f, 0.88f, 0.48f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.45f, 0.52f), new(0.90f, 0.58f),
                new(0.88f, 0.92f), new(0.45f, 0.92f),
            },
            neighbourIds = new List<long> { 13 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 13,
            quantity = 1,
            label = string.Empty,
            labelPosition = new Vector2(0.68f, 0.32f),
            fillColor = new Color(0.80f, 0.35f, 0.40f, 1f),
            completedColor = new Color(0.96f, 0.53f, 0.55f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.45f, 0.12f), new(0.88f, 0.16f),
                new(0.90f, 0.58f), new(0.45f, 0.52f),
            },
            neighbourIds = new List<long> { 12 },
        });
        level.regions.Add(new DressMakingEmbroideryRegionData
        {
            id = 14,
            quantity = 1,
            label = string.Empty,
            labelPosition = new Vector2(0.26f, 0.30f),
            fillColor = new Color(0.44f, 0.28f, 0.63f, 1f),
            completedColor = new Color(0.64f, 0.47f, 0.82f, 1f),
            boundaryPoints = new List<Vector2>
            {
                new(0.08f, 0.12f), new(0.45f, 0.12f),
                new(0.45f, 0.52f), new(0.08f, 0.52f),
            },
            neighbourIds = new List<long> { 11 },
        });

        return level;
    }
}
