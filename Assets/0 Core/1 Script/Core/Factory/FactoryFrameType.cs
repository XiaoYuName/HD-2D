/// <summary>
/// 框架(模具)分类：对应 <see cref="MoldFrameConfig"/> / Data/Factory/MoldFrameConfig.csv 的 Type 字段。
/// 数值与 CSV 保持一致，未在此声明的 Type(5 立牌、11 镭射票)暂不作为分类展示。
/// </summary>
public enum FactoryFrameType
{
    Scroll = 1,      // 挂轴
    Folder = 2,      // 文件夹
    CanvasBag = 3,   // 帆布包
    Postcard = 4,    // 明信片
    Blanket = 6,     // 毯子
    Pillow = 7,      // 抱枕
    Album = 8,       // 画册
    Badge = 9,       // 徽章
    Poster = 10,     // 海报
    PhoneCase = 12,  // 手机壳
}

public static class FactoryFrameTypeExtensions
{
    /// <summary>分类 → 多语言 Key(走 Factory 表)，供 <see cref="FrameTypeButton"/> 显示分类名。</summary>
    public static string LocKey(this FactoryFrameType type) => type switch
    {
        FactoryFrameType.Scroll => FactoryLocKeySet.Mold.FrameType.Scroll,
        FactoryFrameType.Folder => FactoryLocKeySet.Mold.FrameType.Folder,
        FactoryFrameType.CanvasBag => FactoryLocKeySet.Mold.FrameType.CanvasBag,
        FactoryFrameType.Postcard => FactoryLocKeySet.Mold.FrameType.Postcard,
        FactoryFrameType.Blanket => FactoryLocKeySet.Mold.FrameType.Blanket,
        FactoryFrameType.Pillow => FactoryLocKeySet.Mold.FrameType.Pillow,
        FactoryFrameType.Album => FactoryLocKeySet.Mold.FrameType.Album,
        FactoryFrameType.Badge => FactoryLocKeySet.Mold.FrameType.Badge,
        FactoryFrameType.Poster => FactoryLocKeySet.Mold.FrameType.Poster,
        FactoryFrameType.PhoneCase => FactoryLocKeySet.Mold.FrameType.PhoneCase,
        _ => FactoryLocKeySet.Mold.FrameType.Badge,
    };
}
