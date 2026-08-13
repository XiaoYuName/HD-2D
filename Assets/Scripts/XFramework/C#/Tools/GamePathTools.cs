using UnityEngine;
using XFramework;

public static class GamePathTools
{
    #region Scene组合
    
    /// <summary>
    /// 组合游戏场景资源路径。
    /// </summary>
    /// <param name="scenePath">场景名，不需要包含 .unity 后缀。</param>
    /// <returns>可传给资源管理器加载的场景资源路径。</returns>
    public static string CombinationScenePath(string scenePath)
    {
        return $"{AssetsPaths.GameScenePath}{scenePath}.unity";
    }

    /// <summary>
    /// 组合场景预览图片资源路径。
    /// </summary>
    /// <param name="scenePath">场景图片资源名，按配置中的值原样拼接。</param>
    /// <returns>可传给资源管理器加载的场景图片资源路径。</returns>
    public static string CombinationSceneImagePath(string scenePath)
    {
        return $"{AssetsPaths.GameSceneTexturePath}{scenePath}";
    }
    

    #endregion

    #region Item组合

    /// <summary>
    /// 组合道具图标资源路径。
    /// </summary>
    /// <param name="iconName">道具图标资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的道具图标资源路径。</returns>
    public static string CombinationItemIconPath(string iconName)
    {
        return $"{AssetsPaths.ItemImagePath}{iconName}";
    }

    /// <summary>
    /// 组合娃娃图片资源路径。
    /// </summary>
    /// <param name="iconName">娃娃图片资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的娃娃图片资源路径。</returns>
    public static string CombinationDollImagePath(string iconName)
    {
        return $"{AssetsPaths.DollTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合服装图片资源路径。
    /// </summary>
    /// <param name="iconName">服装图片资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的服装图片资源路径。</returns>
    public static string CombinationClothingImagePath(string iconName)
    {
        return $"{AssetsPaths.ClothingTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合服装宝石图标资源路径。
    /// </summary>
    /// <param name="iconName">宝石图标资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的服装宝石图标资源路径。</returns>
    public static string CombinationClothingGemIconPath(string iconName)
    {
        return $"{AssetsPaths.ClothingGemTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合超市小游戏图标资源路径。
    /// </summary>
    /// <param name="iconName">超市小游戏图标资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的超市小游戏图标资源路径。</returns>
    public static string CombinationSuperMaketIconPath(string iconName)
    {
        return $"{AssetsPaths.SuperMaketTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合展览图标资源路径。
    /// </summary>
    /// <param name="iconName">展览图标资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的展览图标资源路径。</returns>
    public static string CombinationExhibitionIconPath(string iconName)
    {
        return $"{AssetsPaths.ExhibitionTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合饰品图标资源路径。
    /// </summary>
    /// <param name="iconName">饰品图标资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的饰品图标资源路径。</returns>
    public static string CombinationAccessoriesIconPath(string iconName)
    {
        return $"{AssetsPaths.AccessoriesTexturePath}{iconName}";
    }

    /// <summary>
    /// 组合 PCB 图标资源路径。
    /// </summary>
    /// <param name="iconName">PCB 图标资源名，不需要包含 .png 后缀。</param>
    /// <returns>可传给资源管理器加载的 PCB 图标资源路径。</returns>
    public static string CombinationPcbIconPath(string iconName)
    {
        return $"{AssetsPaths.PcbTexturePath}{iconName}.png";
    }

    #endregion

    #region Tutorial组合

    /// <summary>
    /// 组合新手引导图片资源路径。引导步骤表的 MaskSpriteName 填的就是这里的资源名。
    /// </summary>
    /// <param name="iconName">引导图片资源名，请包含资源后缀。</param>
    /// <returns>可传给资源管理器加载的引导图片资源路径。</returns>
    public static string CombinationTutorialImagePath(string iconName)
    {
        return $"{AssetsPaths.TutorialTexturePath}{iconName}";
    }

    #endregion
    
    
}
