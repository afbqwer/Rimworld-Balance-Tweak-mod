namespace CheckLoc;

/// <summary>
/// 检测 XML 翻译文件中存在但代码中未引用的多余键（orphaned keys）。
/// 与 MissingKeyDetector 相反：XML 定义键 - 代码引用键 = 多余键。
/// </summary>
public static class OrphanedKeyDetector
{
    /// <summary>
    /// 运行多余键检测。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要检查的语言列表。为空则检查所有。</param>
    /// <param name="showComments">是否显示 Comment 后缀键（低优先级）</param>
    /// <param name="totalOrphaned">输出: 发现的多余键总数</param>
    /// <returns>按语言分组的报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, bool showComments, out int totalOrphaned)
    {
        totalOrphaned = 0;
        var reportLines = new List<string>();

        var sourceDir = Path.Combine(projectRoot, "Source", "BalanceTweak");
        var languagesRoot = Path.Combine(projectRoot, "Languages");

        if (!Directory.Exists(sourceDir))
        {
            reportLines.Add("[ERROR] 源码目录不存在: " + sourceDir);
            return reportLines;
        }

        // 1. 获取代码引用的所有键
        var codeKeys = CodeKeyExtractor.ExtractKeys(sourceDir);
        var codeKeySet = codeKeys.Select(k => k.Key).ToHashSet();

        // 2. 获取 XML 中已定义的所有键
        var xmlKeys = XmlKeyExtractor.ExtractKeys(languagesRoot, targetLanguages);

        // 按语言分组 XML 键
        var xmlKeysByLang = xmlKeys
            .GroupBy(k => k.Language)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 获取所有实际存在的语言
        var availableLanguages = targetLanguages ?? xmlKeysByLang.Keys.ToArray();

        // 3. 对每种语言，计算差集（XML 中有但代码中未引用的）
        foreach (var lang in availableLanguages.OrderBy(l => l))
        {
            if (!xmlKeysByLang.TryGetValue(lang, out var langXmlKeys))
            {
                reportLines.Add($"[WARN] 语言 '{lang}' 的翻译文件不存在，跳过。");
                continue;
            }

            var xmlKeySet = langXmlKeys.Select(k => k.Key).ToHashSet();
            var orphanedKeys = xmlKeySet
                .Except(codeKeySet)
                .OrderBy(k => k)
                .ToList();

            if (orphanedKeys.Count == 0) continue;

            // 分离 Comment 后缀键和常规键
            var commentKeys = orphanedKeys.Where(k => k.EndsWith("Comment")).ToList();
            var regularKeys = orphanedKeys.Where(k => !k.EndsWith("Comment")).ToList();

            if (regularKeys.Count > 0)
            {
                totalOrphaned += regularKeys.Count;
                reportLines.Add("");
                reportLines.Add($"[WARN] {lang} 有 {regularKeys.Count} 个多余翻译键（XML 中定义但代码未引用）:");

                foreach (var key in regularKeys)
                {
                    // 找到该键在哪些 XML 文件中
                    var files = langXmlKeys
                        .Where(k => k.Key == key)
                        .Select(k => k.FileName)
                        .Distinct()
                        .ToList();
                    var fileInfo = string.Join(", ", files);
                    reportLines.Add($"  {key,-40} <- {fileInfo}");
                }
            }

            if (commentKeys.Count > 0)
            {
                if (showComments)
                {
                    totalOrphaned += commentKeys.Count;
                    reportLines.Add("");
                    reportLines.Add($"[INFO] {lang} 有 {commentKeys.Count} 个多余 Comment 键（可选 tooltip）:");

                    foreach (var key in commentKeys.Take(10))
                    {
                        var files = langXmlKeys
                            .Where(k => k.Key == key)
                            .Select(k => k.FileName)
                            .Distinct()
                            .ToList();
                        var fileInfo = string.Join(", ", files);
                        reportLines.Add($"  {key,-40} <- {fileInfo}");
                    }

                    if (commentKeys.Count > 10)
                    {
                        reportLines.Add($"  ... 以及其他 {commentKeys.Count - 10} 个 Comment 键");
                    }
                }
                else
                {
                    reportLines.Add("");
                    reportLines.Add($"[INFO] {lang} 有 {commentKeys.Count} 个多余 Comment 键。使用 --show-comments 查看详情。");
                }
            }
        }

        if (totalOrphaned == 0)
        {
            reportLines.Add("  (无多余翻译键)");
        }

        return reportLines;
    }
}