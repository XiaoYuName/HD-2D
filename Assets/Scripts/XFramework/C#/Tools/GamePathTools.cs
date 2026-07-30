using UnityEngine;
using XFramework;

public static class GamePathTools
{
    #region Scene组合
    
    /// <summary>
    /// 场景路径组合
    /// </summary>
    /// <param name="scenePath">场景名</param>
    /// <returns></returns>
    public static string CombinationScenePath(string scenePath)
    {
        return $"{AssetsPaths.GameScenePath}{scenePath}.unity";
    }

    /// <summary>
    /// 场景图片组合
    /// </summary>
    /// <param name="scenePath">场景路径</param>
    /// <returns></returns>
    public static string CombinationSceneImagePath(string scenePath)
    {
        return $"{AssetsPaths.GameSceneTexturePath}{scenePath}";
    }
    

    #endregion

    #region Item组合

    /// <summary>
    /// 场景ItemIcon组合，请附带后缀
    /// </summary>
    /// <param name="iconName"></param>
    /// <returns></returns>
    public static string CombinationItemIconPath(string iconName)
    {
        return $"{AssetsPaths.ItemImagePath}{iconName}";
    }

    public static string CombinationDollImagePath(string iconName)
    {
        return $"{AssetsPaths.DollTexturePath}{iconName}";
    }

    public static string CombinationClothingImagePath(string iconName)
    {
        return $"{AssetsPaths.ClothingTexturePath}{iconName}";
    }

    public static string CombinationClothingGemIconPath(string iconName)
    {
        return $"{AssetsPaths.ClothingGemTexturePath}{iconName}";
    }

    public static string CombinationSuperMaketIconPath(string iconName)
    {
        return $"{AssetsPaths.SuperMaketTexturePath}{iconName}";
    }

    public static string CombinationExhibitionIconPath(string iconName)
    {
        return $"{AssetsPaths.ExhibitionTexturePath}{iconName}";
    }

    public static string CombinationAccessoriesIconPath(string iconName)
    {
        return $"{AssetsPaths.AccessoriesTexturePath}{iconName}";
    }

    public static string CombinationPcbIconPath(string iconName)
    {
        return $"{AssetsPaths.PcbTexturePath}{iconName}.png";
    }

    #endregion
    
    
}
