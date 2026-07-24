using System.IO;
using UnityEditor;

public class SpriteAutoImporter : AssetPostprocessor
{
    // 需要自动设为 Sprite 的目录（以 "Assets/" 开头，结尾带 "/"）
    static readonly string[] SpriteFolders =
    {
        "Assets/0 Core/3 Art/Scene/",
        "Assets/0 Core/3 Art/UI/",
    };

    void OnPreprocessTexture()
    {
        // 仅对首次导入的新图生效：首次导入时 .meta 尚未生成，重导入时已存在，
        // 以此避免覆盖已手动调整过的图
        if (File.Exists(assetPath + ".meta")) return;

        var importer = (TextureImporter)assetImporter;

        // 新图片默认使用 Single。该设置不会修改已有图片；
        // 确实需要切图的 Sprite Sheet 再手动切换为 Multiple。
        importer.spriteImportMode = SpriteImportMode.Single;

        foreach (string folder in SpriteFolders)
        {
            if (assetPath.StartsWith(folder))
            {
                importer.textureType = TextureImporterType.Sprite;
                break;
            }
        }
    }
}
