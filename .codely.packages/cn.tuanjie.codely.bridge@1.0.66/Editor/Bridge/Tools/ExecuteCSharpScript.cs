using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Codely.Newtonsoft.Json.Linq;
using Codely.Microsoft.CodeAnalysis;
using Codely.Microsoft.CodeAnalysis.CSharp;
using Codely.Microsoft.CodeAnalysis.CSharp.Scripting;
using Codely.Microsoft.CodeAnalysis.Scripting;
using UnityEngine;
using UnityTcp.Editor.Helpers;

namespace UnityTcp.Editor.Tools
{
    public static class ExecuteCSharpScript
    {
        static readonly List<string> s_CapturedLogs = new List<string>();
        static bool s_IsCapturingLogs;

        static readonly string s_ShadowCopyDir = Path.Combine(
            Application.temporaryCachePath,
            "CodelyScriptRefs"
        );

        static readonly string[] s_ShadowCopyAssemblyNames =
        {
            "Assembly-CSharp",
            "Assembly-CSharp-Editor"
        };

        static readonly List<ScriptFixProvider> s_FixProviders = new List<ScriptFixProvider>
        {
            new FixMissingImports(),
            new FixMissingAssemblyReference(),
            new FixUnqualifiedUnityStaticMethod(),
            new FixMissingParenthesis(),
            new FixMissingBrace(),
            new FixMissingSquareBracket(),
            new FixMissingSemicolon(),
            new FixAmbiguousReference()
        };

        const int k_MaxFixIterations = 50;
        const string k_TextMeshProUGUIKeyword = "TextMeshProUGUI";
        const string k_TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static object HandleCommand(JObject @params)
        {
            string script = @params["script"]?.ToString();
            string scriptPath = @params["script_path"]?.ToString();
            string description = @params["description"]?.ToString();

            // At least one of script or script_path must be provided
            if (string.IsNullOrEmpty(script) && string.IsNullOrEmpty(scriptPath))
                return Response.Success("'script' parameter is required.");

            if (!string.IsNullOrEmpty(description))
                CodelyLogger.Log($"[ExecuteCSharpScript] Description: {description}");

            // If script_path is provided (legacy support), read the file content
            if (!string.IsNullOrEmpty(scriptPath))
            {
                try
                {
                    if (!File.Exists(scriptPath))
                    {
                        // A U+FFFD in the path means the bytes were already decoded as UTF-8 with
                        // replacement at the TCP layer — the client sent a non-UTF-8 path and the
                        // original bytes are unrecoverable here. Diagnose it instead of a vague 404.
                        if (scriptPath.IndexOf((char)0xFFFD) >= 0)
                            return Response.Success(
                                $"Script file not found, and the path contains replacement characters (U+FFFD): '{scriptPath}'. " +
                                "The path was likely sent in a non-UTF-8 encoding (JSON must be UTF-8). " +
                                "Fix the client to send the path as UTF-8 — the original path cannot be recovered on this side.");
                        return Response.Success($"Script file not found: {scriptPath}");
                    }

                    script = ReadScriptFileSmart(scriptPath);
                    CodelyLogger.Log($"[ExecuteCSharpScript] Loaded script from file: {scriptPath} ({script.Length} chars)");

                    if (string.IsNullOrWhiteSpace(script))
                        return Response.Success($"Script file is empty: {scriptPath}");
                }
                catch (IOException ioEx)
                {
                    return Response.Success($"Failed to read script file: {ioEx.Message}");
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    return Response.Success($"Access denied to script file: {uaEx.Message}");
                }
            }
            // Auto-detect if script parameter is a file path
            // Heuristic: single line, ends with .cs, and file exists
            else if (!string.IsNullOrEmpty(script))
            {
                var trimmedScript = script.Trim();
                bool looksLikePath = !trimmedScript.Contains("\n") &&
                                     trimmedScript.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

                if (looksLikePath && File.Exists(trimmedScript))
                {
                    try
                    {
                        script = ReadScriptFileSmart(trimmedScript);
                        CodelyLogger.Log($"[ExecuteCSharpScript] Auto-detected and loaded script from path: {trimmedScript} ({script.Length} chars)");

                        if (string.IsNullOrWhiteSpace(script))
                            return Response.Success($"Script file is empty: {trimmedScript}");
                    }
                    catch (IOException ioEx)
                    {
                        return Response.Success($"Failed to read script file: {ioEx.Message}");
                    }
                    catch (UnauthorizedAccessException uaEx)
                    {
                        return Response.Success($"Access denied to script file: {uaEx.Message}");
                    }
                }
            }

            ScheduleTmpEssentialsImportIfNeeded(script);

            bool captureLogs = @params["capture_logs"]?.ToObject<bool>() ?? true;
            string[] imports = @params["imports"]?.ToObject<string[]>() ?? new[]
            {
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "UnityEngine",
                "UnityEditor",
                "UnityEditor.SceneManagement",
                "UnityEngine.SceneManagement"
            };

            try
            {
                CodelyLogger.Log($"[ExecuteCSharpScript] Executing script ({script.Length} chars, {imports.Length} imports)");
                StartLogCapture(captureLogs);

                object result;
                try
                {
                    result = ExecuteScriptInternal(script, imports);
                }
                finally
                {
                    // Always stop log capture, even on error
                }

                var logs = captureLogs ? StopLogCapture() : new List<string>();
                var response = Response.Success(
                    "C# script executed successfully.",
                    new { result = result?.ToString(), logs, log_count = logs.Count }
                );

                CodelyLogger.Log($"[ExecuteCSharpScript] Response: {Codely.Newtonsoft.Json.JsonConvert.SerializeObject(response)}");
                return response;
            }
            catch (Exception e)
            {
                var logs = captureLogs ? StopLogCapture() : new List<string>();
                var errorResponse = Response.Success(
                    $"C# script execution failed: {e?.Message}"
                );
                CodelyLogger.Log($"[ExecuteCSharpScript] Error Response: {Codely.Newtonsoft.Json.JsonConvert.SerializeObject(errorResponse)}");
                return errorResponse;
            }
        }

        static void ScheduleTmpEssentialsImportIfNeeded(string script)
        {
            if (string.IsNullOrEmpty(script))
                return;

            if (script.IndexOf(k_TextMeshProUGUIKeyword, StringComparison.Ordinal) < 0)
                return;

            if (File.Exists(k_TmpSettingsAssetPath))
                return;

            TmpEssentialsAutoImporter.ScheduleImport();
            CodelyLogger.Log("[ExecuteCSharpScript] Scheduled TMP essential resources import because script references TextMeshProUGUI.");
        }

        static object ExecuteScriptInternal(string script, string[] imports)
        {
            SaveScriptToTemp(script);

            try
            {
                // Build minimal base references — no pre-loaded optional modules
                var references = BuildBaseReferences();
                var fixedImports = new List<string>(imports);
                var fixedScript = script;

                // Compile and auto-fix before execution
                CompileAndAutoFix(ref fixedScript, fixedImports, references);

                var options = ScriptOptions.Default
                    .WithReferences(references)
                    .WithImports(fixedImports);

                var scriptTask = CSharpScript.EvaluateAsync(fixedScript, options);
                scriptTask.Wait();
                return scriptTask.Result;
            }
            catch (AggregateException ae)
            {
                if (ae.InnerException != null)
                    throw ae.InnerException;
                throw;
            }
        }

        static List<MetadataReference> BuildBaseReferences()
        {
            var references = new List<MetadataReference>();
            var addedLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Core + Unity assemblies
            AddCoreAssemblyReferences(references);
            references.Add(MetadataReference.CreateFromFile(typeof(UnityEngine.Debug).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(UnityEditor.EditorApplication).Assembly.Location));

            foreach (var r in references.OfType<PortableExecutableReference>())
                if (r.FilePath != null) addedLocations.Add(r.FilePath);

            // 2. Reference all loaded non-dynamic assemblies so scripts can use any
            //    runtime type (package assemblies, third-party DLLs, etc.)
            //    This fixes CS0246/CS0311 when referencing types from packages like Codely.Utilities.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                var loc = asm.Location;
                if (string.IsNullOrEmpty(loc)) continue;
                if (!addedLocations.Add(loc)) continue;

                // Assembly-CSharp / -Editor are handled via shadow copy below
                if (s_ShadowCopyAssemblyNames.Contains(asm.GetName().Name)) continue;

                try { references.Add(MetadataReference.CreateFromFile(loc)); }
                catch { /* skip unreadable assemblies */ }
            }

            // 3. Assembly-CSharp / -Editor via shadow copy (avoids domain reload file locks)
            foreach (var assemblyName in s_ShadowCopyAssemblyNames)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);
                if (asm == null || string.IsNullOrEmpty(asm.Location))
                    continue;

                var shadowPath = CreateShadowCopy(asm.Location);
                if (addedLocations.Add(shadowPath))
                    references.Add(MetadataReference.CreateFromFile(shadowPath));
            }

            return references;
        }

        // Iteratively compiles the script using the same scripting engine as execution,
        // then applies auto-fixes until clean or exhausted.
        static void CompileAndAutoFix(ref string script, List<string> imports, List<MetadataReference> references)
        {
            HoistUsingDirectives(ref script, imports);

            var addedLocations = new HashSet<string>(references
                .OfType<PortableExecutableReference>()
                .Select(r => r.FilePath ?? ""));

            var context = new ScriptFixContext(imports, references, addedLocations);

            for (int iteration = 0; iteration < k_MaxFixIterations; iteration++)
            {
                var scriptOptions = ScriptOptions.Default
                    .WithReferences(references)
                    .WithImports(imports);
                var scriptObj = CSharpScript.Create(script, scriptOptions);
                var compilation = scriptObj.GetCompilation();
                var tree = compilation.SyntaxTrees.First();

                var errors = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (!errors.Any())
                {
                    CodelyLogger.Log($"[ExecuteCSharpScript] Compilation check passed (iteration {iteration})");
                    return;
                }

                int errorCountBefore = errors.Count;
                bool anyFixed = false;
                var updatedTree = tree;

                foreach (var diagnostic in errors)
                {
                    foreach (var fix in s_FixProviders)
                    {
                        if (!fix.CanFix(diagnostic))
                            continue;

                        var treeBeforeFix = updatedTree;
                        if (fix.ApplyFix(ref updatedTree, diagnostic, context))
                        {
                            anyFixed = true;
                            CodelyLogger.Log($"[ExecuteCSharpScript] {fix.GetType().Name} applied for {diagnostic.Id}");

                            // If the tree was modified, remaining diagnostic spans are stale.
                            // Break out and let the outer loop recompile with fresh diagnostics.
                            if (!ReferenceEquals(updatedTree, treeBeforeFix))
                                goto fixesApplied;
                        }
                    }
                }

                fixesApplied:
                if (!anyFixed)
                {
                    CodelyLogger.LogWarning("[ExecuteCSharpScript] Auto-fix could not resolve remaining errors:\n" +
                        string.Join("\n", errors.Select(e => $"  {e.Id}: {e.GetMessage()}")));
                    return;
                }

                if (!ReferenceEquals(updatedTree, tree))
                {
                    var candidate = updatedTree.GetText().ToString();

                    // Verify the fix reduced errors; if it made things worse, skip this fix
                    var checkOptions = ScriptOptions.Default
                        .WithReferences(references)
                        .WithImports(imports);
                    var checkErrors = CSharpScript.Create(candidate, checkOptions)
                        .GetCompilation().GetDiagnostics()
                        .Count(d => d.Severity == DiagnosticSeverity.Error);

                    if (checkErrors > errorCountBefore)
                    {
                        CodelyLogger.LogWarning($"[ExecuteCSharpScript] Auto-fix increased errors ({errorCountBefore} → {checkErrors}), reverting");
                        continue;
                    }

                    script = candidate;
                }
            }
        }

        // Parses top-level `using` directives out of the script, merges them into `imports`,
        // and returns the script body with those directives removed.
        static void HoistUsingDirectives(ref string script, List<string> imports)
        {
            var root = SyntaxFactory.ParseSyntaxTree(script).GetCompilationUnitRoot();
            if (root.Usings.Count == 0)
                return;

            foreach (var usingDirective in root.Usings)
            {
                var namespaceName = usingDirective.Name.ToString();
                if (!imports.Contains(namespaceName))
                    imports.Add(namespaceName);
            }

            // Remove the using directives from the script body
            var stripped = root.RemoveNodes(root.Usings, SyntaxRemoveOptions.KeepNoTrivia);
            script = stripped?.GetText().ToString().TrimStart() ?? script;
        }

        static void AddCoreAssemblyReferences(List<MetadataReference> references)
        {
            var coreTypes = new[]
            {
                typeof(object),
                typeof(System.Linq.Enumerable),
                typeof(System.Collections.Generic.List<>),
                typeof(System.Collections.ArrayList),
                typeof(System.Threading.Tasks.Task),
                typeof(System.Text.StringBuilder),
                typeof(System.IO.File),
                typeof(System.Text.RegularExpressions.Regex),
                typeof(System.Math),
            };

            var addedLocations = new HashSet<string>();
            foreach (var type in coreTypes)
            {
                var location = type.Assembly.Location;
                if (!string.IsNullOrEmpty(location) && addedLocations.Add(location))
                    references.Add(MetadataReference.CreateFromFile(location));
            }

            foreach (var name in new[] { "netstandard", "System.Runtime", "System.Core" })
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == name);
                if (asm != null && !string.IsNullOrEmpty(asm.Location) && addedLocations.Add(asm.Location))
                    references.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        static string CreateShadowCopy(string sourcePath)
        {
            var sourceTime = File.GetLastWriteTimeUtc(sourcePath).Ticks;
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var ext = Path.GetExtension(sourcePath);
            var versionedName = $"{fileName}_{sourceTime}{ext}";
            var destPath = Path.Combine(s_ShadowCopyDir, versionedName);

            Directory.CreateDirectory(s_ShadowCopyDir);

            if (File.Exists(destPath))
            {
                CodelyLogger.Log($"[ExecuteCSharpScript] Shadow copy exists: {versionedName}");
            }
            else
            {
                CleanupOldShadowCopies(fileName, sourceTime);
                File.Copy(sourcePath, destPath, overwrite: false);
                CodelyLogger.Log($"[ExecuteCSharpScript] Shadow copy created: {versionedName}");
            }

            var pdbSource = Path.ChangeExtension(sourcePath, ".pdb");
            var pdbDest = Path.ChangeExtension(destPath, ".pdb");
            if (File.Exists(pdbSource) && !File.Exists(pdbDest))
            {
                try { File.Copy(pdbSource, pdbDest, overwrite: false); }
                catch (IOException)
                {
                    CodelyLogger.LogWarning($"[ExecuteCSharpScript] Could not copy PDB for {fileName}");
                }
            }

            return destPath;
        }

        static void CleanupOldShadowCopies(string assemblyName, long currentTimestamp)
        {
            try
            {
                if (!Directory.Exists(s_ShadowCopyDir))
                    return;

                foreach (var file in Directory.GetFiles(s_ShadowCopyDir, $"{assemblyName}_*"))
                {
                    var nameNoExt = Path.GetFileNameWithoutExtension(file);
                    var lastUnderscore = nameNoExt.LastIndexOf('_');
                    if (lastUnderscore <= 0)
                        continue;

                    if (long.TryParse(nameNoExt.Substring(lastUnderscore + 1), out var fileTimestamp)
                        && fileTimestamp < currentTimestamp)
                    {
                        try { File.Delete(file); }
                        catch (IOException)
                        {
                            CodelyLogger.LogWarning($"[ExecuteCSharpScript] Could not delete old shadow copy: {Path.GetFileName(file)}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CodelyLogger.LogWarning($"[ExecuteCSharpScript] Shadow copy cleanup failed: {e.Message}");
            }
        }

        // Reads a script file with encoding auto-detection. File.ReadAllText assumes UTF-8 when
        // there is no BOM, which corrupts files saved in a local ANSI code page (e.g. GBK/936 on
        // zh-CN Windows) — Chinese identifiers/strings then arrive as '�' and Roslyn reports
        // "error CS1056: Unexpected character". We honor any BOM, validate the bytes as UTF-8
        // ourselves (Mono's UTF8Encoding.throwOnInvalidBytes is unreliable and silently substitutes
        // '�' instead of throwing), and only fall back to a local code page when they are not UTF-8.
        static string ReadScriptFileSmart(string path)
        {
            var bytes = File.ReadAllBytes(path);

            // 1) Honor an explicit BOM.
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            // 2) No BOM: if the bytes are valid UTF-8 (the common case), decode as UTF-8.
            if (IsValidUtf8(bytes))
                return new UTF8Encoding(false).GetString(bytes);

            // 3) Not UTF-8 — if the bytes are structurally valid GBK/936 (the dominant non-UTF-8
            //    case for these scripts: zh-CN Windows), decode as GBK.
            if (IsValidGbk(bytes))
            {
                CodelyLogger.LogWarning(
                    $"[ExecuteCSharpScript] '{Path.GetFileName(path)}' is not valid UTF-8; " +
                    "decoded as GBK/936. Save the file as UTF-8 to avoid encoding issues.");
                return Encoding.GetEncoding(936).GetString(bytes);
            }

            // 4) Neither UTF-8 nor GBK — encoding cannot be detected confidently (e.g. Shift-JIS,
            //    Big5, or a single-byte ANSI page). Decode with the system default as a last resort
            //    and warn LOUDLY so the result is not silently trusted.
            var fallback = Encoding.Default;
            if (fallback.CodePage == 65001) // system is itself UTF-8 — useless for non-UTF-8 bytes
                fallback = Encoding.GetEncoding(936);
            CodelyLogger.LogWarning(
                $"[ExecuteCSharpScript] Could not confidently detect the encoding of " +
                $"'{Path.GetFileName(path)}' (not UTF-8, not GBK). Decoding with {fallback.WebName} " +
                $"(cp {fallback.CodePage}) as a last resort — output may be garbled. " +
                "Save the file as UTF-8 to fix this.");
            return fallback.GetString(bytes);
        }

        // Manual UTF-8 validation — does not rely on UTF8Encoding throwing (Mono does not).
        static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0, n = bytes.Length;
            while (i < n)
            {
                byte b = bytes[i];
                if (b <= 0x7F) { i++; continue; }

                int extra;
                if ((b & 0xE0) == 0xC0) { extra = 1; if (b < 0xC2) return false; }      // 2-byte, reject overlong
                else if ((b & 0xF0) == 0xE0) { extra = 2; }                              // 3-byte
                else if ((b & 0xF8) == 0xF0) { extra = 3; if (b > 0xF4) return false; }  // 4-byte, reject > U+10FFFF
                else return false;                                                       // lone continuation / invalid lead

                if (i + extra >= n) return false;                                        // truncated sequence
                for (int j = 1; j <= extra; j++)
                    if ((bytes[i + j] & 0xC0) != 0x80) return false;                     // bad continuation byte

                i += extra + 1;
            }
            return true;
        }

        // Manual GBK/936 structural validation — does not rely on the decoder throwing (Mono does not).
        // GBK: single bytes 0x00-0x7F (ASCII) and 0x80 (euro in cp936); double bytes have a lead
        // byte 0x81-0xFE followed by a trailing byte 0x40-0x7E or 0x80-0xFE.
        static bool IsValidGbk(byte[] bytes)
        {
            int i = 0, n = bytes.Length;
            while (i < n)
            {
                byte b = bytes[i];
                if (b <= 0x7F || b == 0x80) { i++; continue; }  // ASCII / euro
                if (b == 0xFF) return false;                    // not a valid lead byte

                if (i + 1 >= n) return false;                   // dangling lead byte
                byte t = bytes[i + 1];
                bool validTrail = (t >= 0x40 && t <= 0x7E) || (t >= 0x80 && t <= 0xFE);
                if (!validTrail) return false;

                i += 2;
            }
            return true;
        }

        static void SaveScriptToTemp(string script)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HHmmss");
                var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ExecutedCSharpScripts");
                Directory.CreateDirectory(tempPath);
                var filePath = Path.Combine(tempPath, $"script_{timestamp}_{script.Length}.cs");
                File.WriteAllText(filePath, script);
                CodelyLogger.Log($"[ExecuteCSharpScript] Script saved: {filePath}");
            }
            catch (Exception e)
            {
                CodelyLogger.LogWarning($"[ExecuteCSharpScript] Failed to save script to temp: {e.Message}");
            }
        }

        static void StartLogCapture(bool enabled)
        {
            if (!enabled)
            {
                s_IsCapturingLogs = false;
                return;
            }
            s_CapturedLogs.Clear();
            s_IsCapturingLogs = true;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        static List<string> StopLogCapture()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            s_IsCapturingLogs = false;
            var logs = new List<string>(s_CapturedLogs);
            s_CapturedLogs.Clear();
            return logs;
        }

        static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (!s_IsCapturingLogs)
                return;

            // Suppress this tool's own internal trace logs from the captured output —
            // the caller wants their script's logs, not our scaffolding.
            if (!string.IsNullOrEmpty(logString) && logString.StartsWith("[ExecuteCSharpScript]"))
                return;

            var entry = new StringBuilder();
            entry.Append($"[{type}] {logString}");
            if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
                entry.Append($"\n{stackTrace}");

            s_CapturedLogs.Add(entry.ToString());
        }

    }
}
