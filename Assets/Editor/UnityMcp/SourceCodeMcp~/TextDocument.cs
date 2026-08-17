using System.Security.Cryptography;
using System.Text;

namespace SourceCodeMcp;

sealed class TextDocument
{
    const int MaximumFileBytes = 4 * 1024 * 1024;

    TextDocument(
        byte[] bytes,
        Encoding encoding,
        bool hasBom,
        string eol,
        bool hasFinalNewline,
        List<string> lines)
    {
        Bytes = bytes;
        Encoding = encoding;
        HasBom = hasBom;
        Eol = eol;
        HasFinalNewline = hasFinalNewline;
        Lines = lines;
        Sha256 = Hash(bytes);
    }

    public byte[] Bytes { get; }
    public Encoding Encoding { get; }
    public bool HasBom { get; }
    public string Eol { get; }
    public bool HasFinalNewline { get; }
    public List<string> Lines { get; }
    public string Sha256 { get; }
    public string EncodingName => Encoding.CodePage switch
    {
        65001 => HasBom ? "utf-8-bom" : "utf-8",
        1200 => "utf-16-le",
        1201 => "utf-16-be",
        _ => Encoding.WebName,
    };

    public static TextDocument Read(string path)
    {
        FileInfo file = new(path);
        if (file.Length > MaximumFileBytes)
            throw new ToolException("FILE_TOO_LARGE", $"Text files larger than {MaximumFileBytes} bytes are not supported.");
        byte[] bytes = File.ReadAllBytes(path);
        return FromBytes(bytes);
    }

    public static TextDocument Empty() =>
        new(Array.Empty<byte>(), new UTF8Encoding(false, true), false, "\n", false, []);

    public byte[] Encode(IReadOnlyList<string> lines, bool hasFinalNewline)
    {
        string text = string.Join(Eol, lines);
        if (hasFinalNewline && lines.Count > 0)
            text += Eol;
        byte[] body = Encoding.GetBytes(text);
        if (!HasBom)
            return body;
        byte[] preamble = Encoding.GetPreamble();
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    public static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static TextDocument FromBytes(byte[] bytes)
    {
        if (bytes.AsSpan().IndexOf((byte)0) >= 0 &&
            !HasUtf16Bom(bytes))
            throw new ToolException("BINARY_FILE", "The requested file appears to be binary.");

        Encoding encoding;
        bool hasBom;
        int offset;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            encoding = new UTF8Encoding(true, true);
            hasBom = true;
            offset = 3;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            encoding = new UnicodeEncoding(false, true, true);
            hasBom = true;
            offset = 2;
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            encoding = new UnicodeEncoding(true, true, true);
            hasBom = true;
            offset = 2;
        }
        else
        {
            encoding = new UTF8Encoding(false, true);
            hasBom = false;
            offset = 0;
        }

        string text;
        try
        {
            text = encoding.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw new ToolException("UNSUPPORTED_ENCODING", "Only valid UTF-8 and BOM-marked UTF-16 text files are supported.");
        }

        string eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool hasFinalNewline = text.EndsWith('\n') || text.EndsWith('\r');
        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Length == 0
            ? []
            : normalized.Split('\n').ToList();
        if (hasFinalNewline && lines.Count > 0)
            lines.RemoveAt(lines.Count - 1);
        return new(bytes, encoding, hasBom, eol, hasFinalNewline, lines);
    }

    static bool HasUtf16Bom(byte[] bytes) =>
        bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }) ||
        bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF });
}
