using System.Text.RegularExpressions;

namespace CheckLoc;

/// <summary>
/// 从 C# 源码中提取所有被引用的 MST.xxx 翻译键。
/// 支持三种来源：硬编码字符串、Enum 动态生成、TweakField 字段名。
/// </summary>
public static partial class CodeKeyExtractor
{
    // 匹配 "MST.Xxx" 硬编码字符串
    [GeneratedRegex(@"""MST\.([^""]+)""")]
    private static partial Regex HardcodedKeyRegex();

    // 匹配 typeof(Xxx).GetEnumNames()... 模式，提取类型名
    // Singleline 让 . 匹配换行，确保跨行的 Select 链也能匹配
    [GeneratedRegex(@"typeof\(([\w.]+)\)\.GetEnumNames\(\).*?""MST\.""", RegexOptions.Singleline)]
    private static partial Regex EnumKeyRegex();

    // 匹配 Enum.GetNames(typeof(Xxx))... 模式，提取类型名
    [GeneratedRegex(@"Enum\.GetNames\(typeof\(([\w.]+)\)\).*?""MST\.""", RegexOptions.Singleline)]
    private static partial Regex EnumGetNamesRegex();

    // 匹配 [TweakField(...)] 属性及其下方紧跟的字段声明
    // 属性可能跨多行
    // 分组1: 属性参数内容（用于检测 StatDef）；分组2: 字段名
    [GeneratedRegex(@"\[TweakField([^\]]*)\]\s*public\s+[\w<>,.\s?]+\s+(\w+)\s*[=;]", RegexOptions.Singleline)]
    private static partial Regex TweakFieldRegex();

    /// <summary>
    /// 扫描源码目录，返回所有代码引用的翻译键。
    /// </summary>
    /// <param name="sourceDir">Source/BalanceTweak/ 目录的绝对路径</param>
    /// <returns>引用的键及其来源信息</returns>
    public static HashSet<CodeKeyRef> ExtractKeys(string sourceDir)
    {
        var keys = new Dictionary<string, CodeKeyRef>(); // Key -> ref, 去重用

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"[WARN] 源码目录不存在: {sourceDir}");
            return new HashSet<CodeKeyRef>();
        }

        var csFiles = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);

        foreach (var csFile in csFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDir, csFile);
            var content = File.ReadAllText(csFile);

            // 4a: 硬编码字符串
            ExtractHardcodedKeys(content, relativePath, keys);

            // 4b: Enum 动态生成的键
            ExtractEnumKeys(content, relativePath, sourceDir, keys);

            // 4c: TweakField 字段名生成的键
            ExtractTweakFieldKeys(content, sourceDir, relativePath, keys);
        }

        // 添加 ColumnDataType 和 ColumnStyle 枚举的键
        // 这两个枚举定义在 StatColumnConfig.cs 中
        AddColumnStyleEnumKeys(sourceDir, keys);

        return new HashSet<CodeKeyRef>(keys.Values);
    }

    private static void ExtractHardcodedKeys(string content, string relativePath,
        Dictionary<string, CodeKeyRef> keys)
    {
        foreach (Match match in HardcodedKeyRegex().Matches(content))
        {
            var rawKey = match.Groups[1].Value;
            // 跳过插值字符串中的变量引用，如 $"MST.{field.Name}Comment"
            if (rawKey.Contains('{') || rawKey.Contains('}')) continue;
            var key = "MST." + rawKey;
            keys.TryAdd(key, new CodeKeyRef(key, "硬编码字符串", relativePath));
        }
    }

    private static void ExtractEnumKeys(string content, string relativePath,
        string sourceDir, Dictionary<string, CodeKeyRef> keys)
    {
        var seenTypes = new HashSet<string>();

        foreach (var regex in new[] { EnumKeyRegex(), EnumGetNamesRegex() })
        {
            foreach (Match match in regex.Matches(content))
            {
                var typeName = match.Groups[1].Value;
                if (!seenTypes.Add(typeName)) continue; // 同类型只解析一次

                var members = EnumResolver.Resolve(sourceDir, typeName);
                foreach (var member in members)
                {
                    var key = "MST." + member;
                    keys.TryAdd(key, new CodeKeyRef(key, $"Enum: {typeName}", relativePath));
                }

                if (members.Count == 0)
                {
                    Console.Error.WriteLine($"[WARN] 无法解析枚举类型 '{typeName}' (引用自 {relativePath})");
                }
            }
        }
    }

    private static void ExtractTweakFieldKeys(string content, string sourceDir,
        string relativePath, Dictionary<string, CodeKeyRef> keys)
    {
        foreach (Match match in TweakFieldRegex().Matches(content))
        {
            var fieldName = match.Groups[2].Value;
            var attrParams = match.Groups[1].Value;

            var key = "MST." + fieldName;
            keys.TryAdd(key, new CodeKeyRef(key, "TweakField 字段", relativePath));

            // Comment 后缀键：仅当 TweakField 没有 StatDef 参数时才需要
            // 有 StatDef 时，tooltip 由游戏 StatDef.description 提供，无需单独翻译
            if (!attrParams.Contains("StatDef"))
            {
                var commentKey = "MST." + fieldName + "Comment";
                keys.TryAdd(commentKey, new CodeKeyRef(commentKey, "TweakField 字段 Comment", relativePath));
            }
        }
    }

    /// <summary>
    /// 添加 ColumnDataType 和 ColumnStyle 枚举的翻译键。
    /// 这些枚举定义在 StatColumnConfig.cs 中，StatColumnConfig 构造函数中会
    /// 调用 ("MST." + value.ToString()).Translate() 来获取列标题。
    /// </summary>
    private static void AddColumnStyleEnumKeys(string sourceDir,
        Dictionary<string, CodeKeyRef> keys)
    {
        // ColumnDataType 和 ColumnStyle 都定义在 StatColumnConfig.cs 中
        var statColConfigPath = Path.Combine(sourceDir, "StatColumnConfig.cs");
        if (!File.Exists(statColConfigPath)) return;

        var content = File.ReadAllText(statColConfigPath);

        foreach (var enumName in new[] { "ColumnDataType", "ColumnStyle" })
        {
            var members = EnumResolver.ExtractEnumMembersFromContent(content, enumName);
            foreach (var member in members)
            {
                var key = "MST." + member;
                keys.TryAdd(key, new CodeKeyRef(key, $"Enum: StatColumnConfig.{enumName}", "StatColumnConfig.cs"));
            }
        }

        // SortModeType — 在 GUI/BalanceTweakGUI.cs 中
        // 搜索 typeof(SortModeType).GetEnumNames()
        var sortModeMembers = EnumResolver.Resolve(sourceDir, "SortModeType");
        foreach (var member in sortModeMembers)
        {
            var key = "MST." + member;
            keys.TryAdd(key, new CodeKeyRef(key, "Enum: SortModeType", "BalanceTweakGUI.cs"));
        }
    }
}