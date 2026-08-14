using Sirenix.OdinInspector;
using UnityEngine;
using XFramework;

/// <summary>
/// 相册数据管理器 - 框架通用
/// </summary>
[CreateAssetMenu(fileName = "PhotoAlbumDataManager", menuName = "Configs/PhotoAlbumDataManager")]
public class PhotoAlbumDataManager : OdinScriptableManager<PhotoAlbumDataManager>
{
    [Title("相册配置")]
    [LabelText("相册容量上限")]
    public int MaxPhotoCount = 100;
}
