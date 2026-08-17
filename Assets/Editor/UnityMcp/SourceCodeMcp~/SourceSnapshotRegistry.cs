using System.Security.Cryptography;
using System.Text;

namespace SourceCodeMcp;

sealed class SourceSnapshotRegistry
{
    const int MaximumSnapshots = 128;

    readonly Dictionary<string, SourceSnapshot> snapshots = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, MatchSnapshot> matches = new(StringComparer.Ordinal);
    readonly Queue<string> snapshotOrder = new();

    public void RegisterDocument(string path, TextDocument document)
    {
        string key = GetSnapshotKey(path, document.Sha256);
        if (snapshots.ContainsKey(key))
            return;
        snapshots[key] = new(path, document.Sha256, document.Lines.ToArray());
        snapshotOrder.Enqueue(key);
        while (snapshotOrder.Count > MaximumSnapshots)
            snapshots.Remove(snapshotOrder.Dequeue());
    }

    public string RegisterMatch(
        string path,
        TextDocument document,
        int startLine,
        int endLine,
        int[] hitLines)
    {
        RegisterDocument(path, document);
        string source = $"{path}\n{document.Sha256}\n{startLine}\n{endLine}\n{string.Join(',', hitLines)}";
        string id = "match_" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..20].ToLowerInvariant();
        matches[id] = new(id, path, document.Sha256, startLine, endLine, hitLines);
        return id;
    }

    public MatchSnapshot GetMatch(string id)
    {
        if (!matches.TryGetValue(id, out MatchSnapshot? match))
            throw new ToolException("MATCH_NOT_FOUND", "matchId is unknown or expired; run search_code again.");
        return match;
    }

    public SourceSnapshot? GetSnapshot(string path, string sha256) =>
        snapshots.GetValueOrDefault(GetSnapshotKey(path, sha256));

    static string GetSnapshotKey(string path, string sha256) => $"{path}|{sha256}";
}

sealed record SourceSnapshot(string Path, string Sha256, string[] Lines);

sealed record MatchSnapshot(
    string Id,
    string Path,
    string Sha256,
    int StartLine,
    int EndLine,
    int[] HitLines);
