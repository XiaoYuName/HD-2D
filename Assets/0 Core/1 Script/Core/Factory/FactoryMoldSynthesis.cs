/// <summary>
/// 「框架 + 贴纸」合成结果物品 Id 的编码/解码规则，供运行时（<see cref="FactoryMoldMgPanel"/>/<see cref="FactoryMainPanel"/>）取用。
/// 结果Id = 贴纸Id × 1000000 + 框架Id（拼接式，如贴纸 200000 + 框架 210000 → 200000210000，与合成图文件名 200000+210000 天然对应）。
/// 规则须与 ItemConfigSupplement.FactoryProductionMaterialsSupplementStrategy(导入 ItemConfig 时实际生成生产资料物品的地方，编辑器专用)
/// 保持一致，改这里也要同步改那边，否则查不到合成物品。
/// </summary>
public static class FactoryMoldSynthesis
{
    public const long FrameIdBase = 210000L;
    public const long PaintingIdBase = 200000L;
    public const long PaintingStride = 1000000L;   // 贴纸Id 左移的位数：低 6 位留给框架Id

    public static long GetResultId(long frameId, long paintingId)
        => paintingId * PaintingStride + frameId;

    /// <summary>由合成结果物品 Id 反推其来源的框架Id、贴纸Id。</summary>
    public static void DecodeResultId(long resultId, out long frameId, out long paintingId)
    {
        paintingId = resultId / PaintingStride;
        frameId = resultId % PaintingStride;
    }
}
