using System.Text.RegularExpressions;

namespace CheckLoc;

/// <summary>
/// 从 Languages/ 目录下的 XML 翻译文件中提取已定义的 MST.xxx 键，
/// 同时检测同文件内的重复键。
/// </summary>
public static partial class XmlKeyExtractor
{
    /// <summary>
    /// 匹配 &lt;MST.KeyName&gt;value&lt;/MST.KeyName&gt; 格式。
    /// 提取 KeyName 部分。
    /// </summary>
    [GeneratedRegex(@"<MST\.([^>]+)>")]
    private static partial Regex KeyRegex();

    /// <summary>
    /// 提取指定语言目录下的所有翻译键。
    /// </summary>
    /// <param name="languagesRoot">Languages/ 目录的绝对路径</param>
    /// <param name="targetLanguages">要扫描的语言列表。为空则扫描所有。</param>
    /// <returns>所有翻译键及其位置信息</returns>
    public static List<XmlKeyInfo> ExtractKeys(string languagesRoot, string[]? targetLanguages = null)
    {
        var keys = new List<XmlKeyInfo>();
        var regex = KeyRegex();

        if (!Directory.Exists(languagesRoot))
        {
            Console.Error.WriteLine($"[WARN] 语言目录不存在: {languagesRoot}");
            return keys;
        }

        foreach (var langDir in Directory.GetDirectories(languagesRoot))
        {
            var langName = Path.GetFileName(langDir);

            if (targetLanguages is { Length: > 0 } &&
                !targetLanguages.Contains(langName, StringComparer.OrdinalIgnoreCase))
                continue;

            var keyedDir = Path.Combine(langDir, "Keyed");
            if (!Directory.Exists(keyedDir)) continue;

            foreach (var xmlFile in Directory.GetFiles(keyedDir, "*.xml"))
            {
                var fileName = Path.GetFileName(xmlFile);
                var lineNumber = 0;

                foreach (var line in File.ReadLines(xmlFile))
                {
                    lineNumber++;
                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        var key = "MST." + match.Groups[1].Value;
                        keys.Add(new XmlKeyInfo(key, fileName, langName, lineNumber));
                    }
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// 检测同文件内的重复键。
    /// </summary>
    /// <returns>重复项列表: (语言, 文件名, 键名, 出现的所有行号)</returns>
    public static List<(string Language, string FileName, string Key, int[] Lines)>
        FindSameFileDuplicates(List<XmlKeyInfo> allKeys)
    {
        return allKeys
            .GroupBy(k => (k.Language, k.FileName, k.Key))
            .Where(g => g.Count() > 1)
            .Select(g => (
                Language: g.Key.Language,
                FileName: g.Key.FileName,
                Key: g.Key.Key,
                Lines: g.Select(k => k.LineNumber).OrderBy(l => l).ToArray()
            ))
            .ToList();
    }
}