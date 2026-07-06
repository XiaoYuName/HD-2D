using System;

public static class AssetPathSet
{
    public const string BaseSprtePath = "Assets/AddressableAssets/Remote/Texture2D/";
    public const string ItemSpritePath = BaseSprtePath + "Item/";   // ItemConfig 图标基路径(与 ItemConfigImporter.ItemIconPath 一致)
    public const string FactoryMoldMgSpritePath = ItemSpritePath + "FactoryMoldMgSprite/";
    public const string MoldFrameSpritePath = FactoryMoldMgSpritePath + "MoldFrame/";
    public const string PaintingSpritePath = FactoryMoldMgSpritePath + "Drawing/";
    public const string MoldComposedSpritePath = FactoryMoldMgSpritePath + "Mold/";   // 框架×贴纸 合成成品图(256×256)，作合成物品 Icon

    /// <summary>把 CSV 里的裸文件名拼成完整 AA Key：basePath + 文件名(+ 缺省扩展名)。已是"Assets/"开头或已带扩展名的原样/只补前缀。</summary>
    public static string BuildAssetPath(string basePath, string rawName, string defaultExt = ".png")
    {
        if(string.IsNullOrWhiteSpace(rawName))
            return "";
        string n = rawName.Trim().Replace("\\", "/");
        if(n.StartsWith("Assets/"))
            return n;

        if(!n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            n += defaultExt;
        return basePath + n;
    }
}
