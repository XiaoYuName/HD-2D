using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeMcp;

sealed class UnityCodeInspectRequest
{
    public string Path { get; set; } = "";
    public string? FieldName { get; set; }
    public bool IncludeAssetReferences { get; set; } = true;
    public int MaxResults { get; set; } = 50;
}

sealed class UnityCodeInspectTool(ToolContext context)
{
    static readonly HashSet<string> ReferenceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab", ".unity", ".asset", ".controller", ".overrideController", ".playable",
    };

    public object Run(UnityCodeInspectRequest request)
    {
        string fullPath = context.Paths.Resolve(request.Path);
        if (!File.Exists(fullPath))
            throw new ToolException(ErrorCodeSet.NotAFile, $"Not a file: {request.Path}");
        string relativePath = context.Paths.Relative(fullPath);
        string metaPath = fullPath + ".meta";
        string? guid = File.Exists(metaPath) ? ReadMetaValue(metaPath, "guid") : null;
        int maxResults = Math.Clamp(request.MaxResults, 1, 500);

        if (!Path.GetExtension(fullPath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            return new
            {
                path = relativePath,
                guid,
                importer = File.Exists(metaPath) ? ReadImporter(metaPath) : null,
            };

        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath), path: fullPath);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        var fields = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(field => field.Declaration.Variables.Select(variable => CreateField(field, variable)))
            .Where(field => request.FieldName is null ||
                            string.Equals(field.Name, request.FieldName, StringComparison.Ordinal))
            .ToArray();
        string[] references = request.IncludeAssetReferences && guid is not null
            ? FindGuidReferences(guid, fullPath, maxResults)
            : [];
        return new
        {
            path = relativePath,
            guid,
            fields,
            assetReferences = references,
            returnedReferences = references.Length,
            referencesTruncated = references.Length == maxResults ? (bool?)true : null,
            guidance = request.FieldName is null
                ? null
                : fields.Length == 0
                    ? "Field not found. Properties are not serialized by Unity."
                    : fields[0].Serialized && fields[0].FormerNames.Length == 0
                        ? "Renaming this serialized field requires [FormerlySerializedAs(\"oldName\")]."
                        : null,
        };
    }

    static UnityFieldInfo CreateField(
        FieldDeclarationSyntax field,
        VariableDeclaratorSyntax variable)
    {
        string[] attributes = field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString().Split('.').Last())
            .ToArray();
        bool isPublic = field.Modifiers.Any(modifier => modifier.IsKind(
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword));
        bool isStatic = field.Modifiers.Any(modifier => modifier.IsKind(
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword));
        bool excluded = attributes.Any(attribute =>
            attribute is "NonSerialized" or "NonSerializedAttribute");
        bool explicitlySerialized = attributes.Any(attribute =>
            attribute is "SerializeField" or "SerializeFieldAttribute" or
                "SerializeReference" or "SerializeReferenceAttribute");
        string[] formerNames = field.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute =>
                attribute.Name.ToString().Split('.').Last() is
                    "FormerlySerializedAs" or "FormerlySerializedAsAttribute")
            .SelectMany(attribute => attribute.ArgumentList?.Arguments ?? default)
            .Select(argument => argument.Expression is LiteralExpressionSyntax literal
                ? literal.Token.ValueText
                : argument.Expression.ToString())
            .ToArray();
        FileLinePositionSpan span = variable.GetLocation().GetLineSpan();
        return new()
        {
            Name = variable.Identifier.Text,
            Type = field.Declaration.Type.ToString(),
            Line = span.StartLinePosition.Line + 1,
            Serialized = !isStatic && !excluded && (isPublic || explicitlySerialized),
            Public = isPublic,
            SerializeReference = attributes.Any(attribute =>
                attribute is "SerializeReference" or "SerializeReferenceAttribute"),
            FormerNames = formerNames,
            Attributes = attributes,
        };
    }

    string[] FindGuidReferences(string guid, string sourcePath, int maxResults)
    {
        string assets = Path.Combine(context.ProjectRoot, UnityPathSet.Assets);
        if (!Directory.Exists(assets))
            return [];
        List<string> result = [];
        foreach (string path in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
        {
            if (result.Count >= maxResults)
                break;
            if (!ReferenceExtensions.Contains(Path.GetExtension(path)) ||
                string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                using StreamReader reader = new(path);
                while (reader.ReadLine() is { } line)
                {
                    if (!line.Contains(guid, StringComparison.OrdinalIgnoreCase))
                        continue;
                    result.Add(context.Paths.Relative(path));
                    break;
                }
            }
            catch (IOException)
            {
                // Assets may be reimported while the read-only inspection is running.
            }
        }
        return result.ToArray();
    }

    static object ReadImporter(string metaPath)
    {
        string[] keys =
        {
            "guid", "TextureImporter", "textureType", "spriteMode", "wrapU", "wrapV", "wrapW",
            "filterMode", "isReadable", "enableMipMap",
        };
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(metaPath))
        {
            string trimmed = line.Trim();
            int separator = trimmed.IndexOf(':');
            if (separator < 0)
                continue;
            string key = trimmed[..separator];
            if (keys.Contains(key, StringComparer.Ordinal) && !values.ContainsKey(key))
                values[key] = trimmed[(separator + 1)..].Trim();
        }
        return values;
    }

    static string? ReadMetaValue(string path, string key)
    {
        string prefix = key + ":";
        foreach (string line in File.ReadLines(path))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                return trimmed[prefix.Length..].Trim();
        }
        return null;
    }
}

sealed class UnityFieldInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Line { get; set; }
    public bool Serialized { get; set; }
    public bool Public { get; set; }
    public bool SerializeReference { get; set; }
    public string[] FormerNames { get; set; } = [];
    public string[] Attributes { get; set; } = [];
}
