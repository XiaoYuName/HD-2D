/// <summary>
/// 「框架 + 贴纸」合成结果物品 Id 的编码/解码规则，供运行时（<see cref="FactoryMoldMgPanel"/>/<see cref="FactoryMainPanel"/>）取用。
/// 结果Id = 贴纸Id × 1000000 + 框架Id（拼接式，如贴纸 200000 + 框架 210000 → 200000210000，与合成图文件名 200000+210000 天然对应）。
/// <see cref="FactoryMerchandiseItemInfo"/> 的正品/次品 Id 在此结果 Id 基础上再加偏移，同一贴纸的 100 万号段内互不冲突。
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
