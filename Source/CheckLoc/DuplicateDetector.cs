namespace CheckLoc;

/// <summary>
/// 检测 XML 翻译文件中的重复键（同文件内 + 跨文件）。
/// </summary>
public static class DuplicateDetector
{
    /// <summary>
    /// 运行重复键检测。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要检查的语言列表。为空则检查所有。</param>
    /// <param name="totalDuplicates">输出: 发现的重复键总数</param>
    /// <returns>重复键报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, out int totalDuplicates)
    {
        totalDuplicates = 0;
        var reportLines = new List<string>();

        var languagesRoot = Path.Combine(projectRoot, "Languages");
        if (!Directory.Exists(languagesRoot))
        {
            reportLines.Add("[ERROR] 语言目录不存在: " + languagesRoot);
            return reportLines;
        }

        // 1. 获取所有 XML 键
        var allKeys = XmlKeyExtractor.ExtractKeys(languagesRoot, targetLanguages);

        // 2. 同文件内重复
        var sameFileDups = XmlKeyExtractor.FindSameFileDuplicates(allKeys);
        if (sameFileDups.Count > 0)
        {
            totalDuplicates += sameFileDups.Count;
            reportLines.Add("");
            reportLines.Add($"--- 同文件内重复键 ({sameFileDups.Count} 处) ---");

            foreach (var dup in sameFileDups.OrderBy(d => d.Language).ThenBy(d => d.FileName))
            {
                var lines = string.Join(", ", dup.Lines.Select(l => $"第{l}行"));
                reportLines.Add($"[ERROR] {dup.Language}/{dup.FileName}: {dup.Key} ({lines})");
            }
        }
        else
        {
            reportLines.Add("  (无同文件内重复键)");
        }

        // 3. 跨文件重复（同语言内）
        var crossFileDups = allKeys
            .GroupBy(k => (k.Language, k.Key))
            .Where(g => g.Select(k => k.FileName).Distinct().Count() > 1)
            .Select(g => (
                Language: g.Key.Language,
                Key: g.Key.Key,
                Files: g.Select(k => k.FileName).Distinct().OrderBy(f => f).ToList()
            ))
            .ToList();

        if (crossFileDups.Count > 0)
        {
            totalDuplicates += crossFileDups.Count;
            reportLines.Add("");
            reportLines.Add($"--- 跨文件重复键 ({crossFileDups.Count} 处) ---");

            foreach (var dup in crossFileDups.OrderBy(d => d.Language).ThenBy(d => d.Key))
            {
                var files = string.Join(", ", dup.Files);
                reportLines.Add($"[ERROR] {dup.Language}: {dup.Key} (文件: {files})");
            }
        }
        else
        {
            reportLines.Add("  (无跨文件重复键)");
        }

        if (totalDuplicates == 0)
        {
            reportLines.Add("  (无重复翻译键)");
        }

        return reportLines;
    }
}