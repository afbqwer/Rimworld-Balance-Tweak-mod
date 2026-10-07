using System.Xml;
using System.Xml.Linq;

namespace CheckLoc;

/// <summary>
/// 使用 System.Xml 对 Languages/{语言}/Keyed/*.xml 做语法检查。
/// 报告 XML 解析错误（格式非法、编码问题等，含行列号）与根元素不是 LanguageData 的问题。
/// 其它检查均为行级正则扫描，XML 本身损坏时其结果不可信，因此本检查放在默认流程最前。
/// </summary>
public static class XmlSyntaxDetector
{
    /// <summary>
    /// 运行 XML 语法检查。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要检查的语言列表。为空则检查所有。</param>
    /// <param name="totalIssues">输出: 发现的问题总数</param>
    /// <returns>检查报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, out int totalIssues)
    {
        totalIssues = 0;
        var reportLines = new List<string>();

        var languagesRoot = Path.Combine(projectRoot, "Languages");
        if (!Directory.Exists(languagesRoot))
        {
            reportLines.Add("[ERROR] 语言目录不存在: " + languagesRoot);
            return reportLines;
        }

        var files = EnumerateXmlFiles(languagesRoot, targetLanguages);
        if (files.Count == 0)
        {
            reportLines.Add("  (未找到任何翻译 XML 文件)");
            return reportLines;
        }

        foreach (var (path, language, fileName) in files)
        {
            try
            {
                var doc = XDocument.Load(path, LoadOptions.None);
                var rootName = doc.Root?.Name.LocalName;
                if (rootName != "LanguageData")
                {
                    totalIssues++;
                    reportLines.Add($"[WARN] {language}/{fileName}: 根元素应为 LanguageData (实际: {rootName ?? "无"})");
                }
            }
            catch (XmlException ex)
            {
                totalIssues++;
                reportLines.Add($"[ERROR] {language}/{fileName} (第{ex.LineNumber}行, 第{ex.LinePosition}列): {ex.Message}");
            }
        }

        if (totalIssues == 0)
        {
            reportLines.Add($"  (全部 {files.Count} 个翻译 XML 语法合法)");
        }

        return reportLines;
    }

    private static List<(string Path, string Language, string FileName)> EnumerateXmlFiles(
        string languagesRoot, string[]? targetLanguages)
    {
        var files = new List<(string, string, string)>();

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
                files.Add((xmlFile, langName, Path.GetFileName(xmlFile)));
            }
        }

        return files;
    }
}
