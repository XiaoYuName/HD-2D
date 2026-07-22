using System;
using System.Collections.Generic;

/// <summary>
/// CSV 第二行的类型声明。兼容普通 C# 写法和 Luban 的容器写法：
/// List&lt;long&gt;#sep=+ / (list#sep=+),long / SomeBean#sep=+。
/// </summary>
public sealed class CsvTypeDeclaration
{
    readonly Dictionary<string, string> options;

    public string Source { get; }
    public string TypeExpression { get; }
    public string Separator => GetOption("sep");
    public string KeyValueSeparator => GetOption("kvsep") ?? GetOption("sep2");

    CsvTypeDeclaration(string source, string typeExpression, Dictionary<string, string> options)
    {
        Source = source;
        TypeExpression = typeExpression;
        this.options = options;
    }

    public static CsvTypeDeclaration Parse(string source)
    {
        source = (source ?? string.Empty).Trim();
        if(source.StartsWith("(", StringComparison.Ordinal))
        {
            int close = FindMatchingParenthesis(source);
            if(close > 0)
            {
                string container = source.Substring(1, close - 1);
                string elementType = source.Substring(close + 1).TrimStart().TrimStart(',').Trim();
                SplitOptions(container, out string containerName, out Dictionary<string, string> containerOptions);
                if(containerName.Equals("list", StringComparison.OrdinalIgnoreCase)
                   || containerName.Equals("array", StringComparison.OrdinalIgnoreCase))
                {
                    string mappedElement = MapTypeExpression(elementType);
                    string type = containerName.Equals("array", StringComparison.OrdinalIgnoreCase)
                        ? mappedElement + "[]"
                        : $"List<{mappedElement}>";
                    return new CsvTypeDeclaration(source, type, containerOptions);
                }
            }
        }

        SplitOptions(source, out string rawType, out Dictionary<string, string> options);
        return new CsvTypeDeclaration(source, MapTypeExpression(rawType), options);
    }

    public static CsvTypeDeclaration Parse(string source, string format)
    {
        format = (format ?? string.Empty).Trim();
        if(string.IsNullOrEmpty(format) || format == "-")
            return Parse(source);
        return Parse((source ?? string.Empty) + "#" + format.TrimStart('#'));
    }

    public string GetOption(string name)
        => options.TryGetValue(name, out string value) ? DecodeSeparator(value) : null;

    static int FindMatchingParenthesis(string text)
    {
        int depth = 0;
        for(int i = 0; i < text.Length; i++)
        {
            if(text[i] == '(') depth++;
            else if(text[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }

    static void SplitOptions(string text, out string type, out Dictionary<string, string> result)
    {
        result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int optionStart = text.IndexOf('#');
        if(optionStart < 0)
        {
            type = text.Trim();
            return;
        }

        type = text.Substring(0, optionStart).Trim();
        string optionText = text.Substring(optionStart + 1);
        foreach(string part in optionText.Split('#'))
        {
            int equals = part.IndexOf('=');
            if(equals <= 0)
                continue;
            result[part.Substring(0, equals).Trim()] = part.Substring(equals + 1).Trim();
        }
    }

    static string MapTypeExpression(string type)
    {
        type = (type ?? string.Empty).Trim();
        switch(type.ToLowerInvariant())
        {
            case "": case "str": case "string": return "string";
            case "int": return "int";
            case "float": return "float";
            case "bool": return "bool";
            case "long": return "long";
            case "double": return "double";
            case "color": return "Color";
        }

        if(TryMapGeneric(type, "List", out string listType))
            return listType;
        if(TryMapGeneric(type, "Dictionary", out string dictionaryType))
            return dictionaryType;
        if(type.EndsWith("[]", StringComparison.Ordinal))
            return MapTypeExpression(type.Substring(0, type.Length - 2)) + "[]";
        return type;
    }

    static bool TryMapGeneric(string type, string genericName, out string mapped)
    {
        mapped = null;
        if(!type.StartsWith(genericName + "<", StringComparison.OrdinalIgnoreCase) || !type.EndsWith(">", StringComparison.Ordinal))
            return false;

        string inner = type.Substring(genericName.Length + 1, type.Length - genericName.Length - 2);
        List<string> args = SplitGenericArguments(inner);
        for(int i = 0; i < args.Count; i++)
            args[i] = MapTypeExpression(args[i]);
        mapped = genericName + "<" + string.Join(", ", args) + ">";
        return true;
    }

    static List<string> SplitGenericArguments(string text)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for(int i = 0; i < text.Length; i++)
        {
            if(text[i] == '<') depth++;
            else if(text[i] == '>') depth--;
            else if(text[i] == ',' && depth == 0)
            {
                result.Add(text.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        result.Add(text.Substring(start).Trim());
        return result;
    }

    static string DecodeSeparator(string value)
    {
        switch(value)
        {
            case @"\t": return "\t";
            case @"\n": return "\n";
            case @"\r": return "\r";
            default: return value;
        }
    }
}
