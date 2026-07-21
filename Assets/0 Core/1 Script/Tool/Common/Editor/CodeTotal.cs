using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

public static class CodeTotal
{
    private const string ComparePath = "Assets/0 Core/1 Script";
    private const string MenuItemPath = "Tools/Code/统计代码总行数";

    [MenuItem(MenuItemPath)]
    private static void Run()
    {
        if (!TryGetCompareFullPath(out string fullPath))
        {
            return;
        }

        BenchmarkResult result = Measure(() => CountByEnumerateFilesAndBuffer(fullPath));
        LogBenchmarkResult(result);
    }

    private static BenchmarkResult Measure(System.Func<CodeCountResult> counter)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CodeCountResult countResult = counter();
        stopwatch.Stop();

        BenchmarkResult result = new BenchmarkResult();
        result.TotalLineCount = countResult.TotalLineCount;
        result.FileCount = countResult.FileCount;
        result.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        return result;
    }

    private static CodeCountResult CountByEnumerateFilesAndBuffer(string fullPath)
    {
        int totalLineCount = 0;
        int fileCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories))
        {
            totalLineCount += CountLinesWithBuffer(filePath);
            fileCount++;
        }

        CodeCountResult result = new CodeCountResult();
        result.TotalLineCount = totalLineCount;
        result.FileCount = fileCount;
        return result;
    }

    private static int CountLinesWithBuffer(string path)
    {
        const int BufferSize = 8192;
        char[] buffer = new char[BufferSize];
        int lineCount = 0;
        bool hasContent = false;
        bool previousWasCarriageReturn = false;
        char lastChar = '\0';

        using (StreamReader reader = new StreamReader(path))
        {
            int readCount;
            while ((readCount = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < readCount; i++)
                {
                    char currentChar = buffer[i];
                    hasContent = true;

                    if (currentChar == '\n')
                    {
                        if (!previousWasCarriageReturn)
                        {
                            lineCount++;
                        }

                        previousWasCarriageReturn = false;
                    }
                    else if (currentChar == '\r')
                    {
                        lineCount++;
                        previousWasCarriageReturn = true;
                    }
                    else
                    {
                        previousWasCarriageReturn = false;
                    }

                    lastChar = currentChar;
                }
            }
        }

        if (!hasContent)
        {
            return 0;
        }

        return lastChar == '\n' || lastChar == '\r' ? lineCount : lineCount + 1;
    }

    private static void LogBenchmarkResult(BenchmarkResult result)
    {
        Debug.Log(
            $"[代码统计] Path: {ComparePath} | Files: {result.FileCount} | Lines: {result.TotalLineCount} | Time: {result.ElapsedMilliseconds:F3} ms");
    }

    private static bool TryGetCompareFullPath(out string fullPath)
    {
        if (TryGetDirectoryFullPath(ComparePath, out fullPath))
        {
            return true;
        }

        Debug.LogError($"Path Not Exist: \"{ComparePath}\"");
        return false;
    }

    private static bool TryGetDirectoryFullPath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        if (normalizedPath == "Assets" || normalizedPath.StartsWith("Assets/"))
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string relativePath = normalizedPath.Substring("Assets".Length).TrimStart('/');
            fullPath = string.IsNullOrEmpty(relativePath)
                ? Application.dataPath
                : Path.Combine(projectPath, "Assets", relativePath);
        }
        else
        {
            fullPath = Path.GetFullPath(path);
        }

        return Directory.Exists(fullPath);
    }

    private struct CodeCountResult
    {
        public int TotalLineCount;
        public int FileCount;
    }

    private struct BenchmarkResult
    {
        public int TotalLineCount;
        public int FileCount;
        public double ElapsedMilliseconds;
    }
}
