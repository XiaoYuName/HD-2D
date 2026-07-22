using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Project 视图中将选中的 CSV 统一转换为 UTF-8 BOM 编码。</summary>
public static class CsvEncodingConverter
{
    const string MenuPath = "Assets/CSV/转换为 UTF-8（带 BOM）";
    static readonly byte[] Utf8Bom = new UTF8Encoding(true).GetPreamble();
    static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    [MenuItem(MenuPath, true)]
    static bool Validate() => Selection.objects.Any(IsCsvAsset);

    [MenuItem(MenuPath)]
    static void ConvertSelected()
    {
        string[] assetPaths = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(IsCsvPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        int converted = 0;
        int unchanged = 0;
        int failed = 0;
        foreach(string assetPath in assetPaths)
        {
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                byte[] source = ReadAllBytesShared(fullPath);
                if(HasUtf8Bom(source))
                {
                    unchanged++;
                    continue;
                }

                string text = Decode(source);
                byte[] content = StrictUtf8.GetBytes(text);
                byte[] output = new byte[Utf8Bom.Length + content.Length];
                Buffer.BlockCopy(Utf8Bom, 0, output, 0, Utf8Bom.Length);
                Buffer.BlockCopy(content, 0, output, Utf8Bom.Length, content.Length);
                File.WriteAllBytes(fullPath, output);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                converted++;
            }
            catch(Exception e)
            {
                failed++;
                Debug.LogError($"[CSV 编码转换] {assetPath} 转换失败：\n{e.Message}");
            }
        }

        Debug.Log($"[CSV 编码转换] 已转换 {converted} 个，原已是 UTF-8 BOM {unchanged} 个，失败 {failed} 个。");
    }

    static bool IsCsvAsset(UnityEngine.Object asset)
        => asset && IsCsvPath(AssetDatabase.GetAssetPath(asset));

    static bool IsCsvPath(string path)
        => !string.IsNullOrEmpty(path)
           && path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
           && File.Exists(Path.GetFullPath(path));

    static bool HasUtf8Bom(byte[] bytes)
        => bytes.Length >= Utf8Bom.Length
           && bytes[0] == Utf8Bom[0]
           && bytes[1] == Utf8Bom[1]
           && bytes[2] == Utf8Bom[2];

    static string Decode(byte[] bytes)
    {
        if(bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            return Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4);
        if(bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return new UTF32Encoding(true, true).GetString(bytes, 4, bytes.Length - 4);
        if(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if(bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch(DecoderFallbackException)
        {
            // 兼容 Windows 上 Excel 导出的本地代码页 CSV。
            int codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            return Encoding.GetEncoding(codePage).GetString(bytes);
        }
    }

    static byte[] ReadAllBytesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
