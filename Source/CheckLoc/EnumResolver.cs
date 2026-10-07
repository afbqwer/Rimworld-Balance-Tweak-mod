using System.Text.RegularExpressions;

namespace CheckLoc;

/// <summary>
/// 在 C# 源码中查找 enum 定义并提取成员名称。
/// 支持嵌套类型（如 HediffData.HediffCategory）。
/// </summary>
public static partial class EnumResolver
{
    /// <summary>
    /// 匹配 "enum TypeName { ... }" 定义，提取花括号内容。
    /// </summary>
    [GeneratedRegex(@"enum\s+\w+\s*\{([^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex EnumBodyRegex();

    /// <summary>
    /// 匹配 "class TypeName" 定义，用于定位嵌套枚举的父类。
    /// </summary>
    [GeneratedRegex(@"\bclass\s+(\w+)")]
    private static partial Regex ClassDefRegex();

    /// <summary>
    /// 根据类型名解析枚举成员名称。
    /// 类型名可以是简单名 "SettingType" 或嵌套名 "HediffData.HediffCategory"。
    /// </summary>
    /// <param name="sourceDir">C# 源码根目录（Source/BalanceTweak/）</param>
    /// <param name="typeName">枚举类型名称，如 "SettingType" 或 "HediffData.HediffCategory"</param>
    /// <returns>枚举成员名称列表</returns>
    public static List<string> Resolve(string sourceDir, string typeName)
    {
        // 尝试在 TypeName.cs 中查找
        var directFile = Path.Combine(sourceDir, typeName + ".cs");
        if (File.Exists(directFile))
        {
            var result = ExtractFromFile(directFile, typeName);
            if (result.Count > 0) return result;
        }

        // 处理嵌套类型: OuterClass.InnerEnum
        if (typeName.Contains('.'))
        {
            var parts = typeName.Split('.');
            var outerClass = parts[0];
            var innerEnum = parts[1];

            // 在 OuterClass.cs 或以 OuterClass 开头的文件中查找
            foreach (var csFile in Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(csFile);
                if (ClassDefRegex().IsMatch(content) && content.Contains($"class {outerClass}") && content.Contains($"enum {innerEnum}"))
                {
                    var result = ExtractEnumMembersFromContent(content, innerEnum);
                    if (result.Count > 0) return result;
                }
            }
        }

        // 遍历所有 .cs 文件搜索
        foreach (var csFile in Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories))
        {
            var result = ExtractFromFile(csFile, typeName);
            if (result.Count > 0) return result;
        }

        return new List<string>();
    }

    private static List<string> ExtractFromFile(string filePath, string enumName)
    {
        var content = File.ReadAllText(filePath);
        return ExtractEnumMembersFromContent(content, enumName);
    }

    internal static List<string> ExtractEnumMembersFromContent(string content, string enumName)
    {
        var regex = new Regex($@"enum\s+{Regex.Escape(enumName)}\s*\{{([^}}]*)\}}", RegexOptions.Singleline);
        var match = regex.Match(content);
        if (!match.Success) return new List<string>();

        var body = match.Groups[1].Value;
        return ParseEnumBody(body);
    }

    /// <summary>
    /// 从枚举体字符串中提取成员名称。
    /// 跳过注释、空行和数值赋值部分。
    /// </summary>
    public static List<string> ParseEnumBody(string body)
    {
        var members = new List<string>();
        // 移除单行注释
        body = Regex.Replace(body, @"//[^\n]*", "");
        // 移除多行注释
        body = Regex.Replace(body, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // 按逗号分割
        var parts = body.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 取等号前的部分（如果有赋值）
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx >= 0)
                trimmed = trimmed[..eqIdx].Trim();

            if (!string.IsNullOrEmpty(trimmed) && IsValidIdentifier(trimmed))
            {
                members.Add(trimmed);
            }
        }

        return members;
    }

    private static bool IsValidIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (!char.IsLetter(s[0]) && s[0] != '_') return false;
        return s.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}