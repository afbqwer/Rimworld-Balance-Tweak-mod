namespace CheckLoc;

/// <summary>
/// 检测代码引用但 XML 翻译文件中缺失的键。
/// </summary>
public static class MissingKeyDetector
{
    /// <summary>
    /// 运行缺失键检测。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要检查的语言列表。为空则检查所有。</param>
    /// <param name="showComments">是否显示 Comment 后缀键（低优先级）</param>
    /// <param name="totalMissing">输出: 发现的缺失键总数</param>
    /// <returns>按语言分组的缺失键报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, bool showComments, out int totalMissing)
    {
        totalMissing = 0;
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
            .ToDictionary(g => g.Key, g => g.Select(k => k.Key).ToHashSet());

        // 获取所有实际存在的语言
        var availableLanguages = targetLanguages ?? xmlKeysByLang.Keys.ToArray();

        // 3. 对每种语言，计算差集
        foreach (var lang in availableLanguages.OrderBy(l => l))
        {
            if (!xmlKeysByLang.TryGetValue(lang, out var langXmlKeys))
            {
                reportLines.Add($"[WARN] 语言 '{lang}' 的翻译文件不存在，跳过。");
                continue;
            }

            var missingKeys = codeKeySet
                .Except(langXmlKeys)
                .OrderBy(k => k)
                .ToList();

            if (missingKeys.Count == 0) continue;

            // 分离 Comment 后缀键（可选 tooltip）和常规键（必须）
            var commentKeys = missingKeys.Where(k => k.EndsWith("Comment")).ToList();
            var regularKeys = missingKeys.Where(k => !k.EndsWith("Comment")).ToList();

            if (regularKeys.Count > 0)
            {
                totalMissing += regularKeys.Count;
                reportLines.Add("");
                reportLines.Add($"[WARN] {lang} 缺少以下翻译键 ({regularKeys.Count} 个):");

                foreach (var missingKey in regularKeys)
                {
                    var sourceInfo = codeKeys
                        .Where(c => c.Key == missingKey)
                        .Select(c => $"{c.Source} ({c.OriginFile})")
                        .FirstOrDefault() ?? "未知来源";

                    reportLines.Add($"  {missingKey,-40} <- {sourceInfo}");
                }
            }

            if (commentKeys.Count > 0)
            {
                if (showComments)
                {
                    totalMissing += commentKeys.Count;
                    reportLines.Add("");
                    reportLines.Add($"[INFO] {lang} 缺少以下 Comment 键（可选 tooltip，{commentKeys.Count} 个）:");

                    foreach (var missingKey in commentKeys.Take(10))
                    {
                        var sourceInfo = codeKeys
                            .Where(c => c.Key == missingKey)
                            .Select(c => c.Source?.Replace(" Comment", "") ?? "")
                            .FirstOrDefault() ?? "";

                        reportLines.Add($"  {missingKey,-40} <- {sourceInfo} (StatDef 字段可忽略)");
                    }

                    if (commentKeys.Count > 10)
                    {
                        reportLines.Add($"  ... 以及其他 {commentKeys.Count - 10} 个 Comment 键");
                    }
                }
                else
                {
                    reportLines.Add("");
                    reportLines.Add($"[INFO] {lang} 缺失 {commentKeys.Count} 个 Comment 键（可选 tooltip）。使用 --show-comments 查看详情。");
                }
            }

            if (regularKeys.Count == 0 && commentKeys.Count == 0)
            {
                reportLines.Add("  (无缺失翻译键)");
            }
        }

        if (totalMissing == 0)
        {
            reportLines.Add("  (无缺失翻译键)");
        }

        return reportLines;
    }
}