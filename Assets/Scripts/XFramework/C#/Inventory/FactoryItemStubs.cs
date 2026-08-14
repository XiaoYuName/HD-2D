using System;
using XFramework;

/// <summary>
/// 工厂周边物品信息 - 框架存根版本
/// 如果项目不需要工厂系统，可以删除此文件及相关引用
/// </summary>
[Serializable]
public class FactoryMerchandiseItemInfo : RuntimeItemInfo
{
    public long FrameId;
    public long PaintingId;

    public FactoryMerchandiseItemInfo()
    {
    }

    public FactoryMerchandiseItemInfo(long merchandiseId, int count, long frameId, long paintingId) : base(count)
    {
        ID = merchandiseId;
        FrameId = frameId;
        PaintingId = paintingId;
    }
}

/// <summary>
/// 工厂组合物品扩展方法 - 框架存根版本
/// </summary>
public static class FactoryComposedItemInfoEt
{
    /// <summary>
    /// 组合周边物品ID: 框架 + 贴纸
    /// </summary>
    public static long ComposeMerchandiseId(long frameId, long paintingId)
    {
        // 简单的组合逻辑：frameId * 10000 + paintingId
        return frameId * 10000 + paintingId;
    }
}
