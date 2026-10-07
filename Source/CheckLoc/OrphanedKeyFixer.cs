namespace CheckLoc;

/// <summary>
/// 清除多余翻译键。
/// 找出 XML 翻译文件中存在但 C# 代码中未引用的键，将其移除。
/// </summary>
public static class OrphanedKeyFixer
{
    /// <summary>
    /// 运行多余键修复。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要修复的语言列表。为空则修复所有。</param>
    /// <param name="showComments">是否详细报告 Comment 后缀键</param>
    /// <param name="totalFixed">输出: 修复的多余键总数</param>
    /// <returns>修复报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, bool showComments, out int totalFixed)
    {
        totalFixed = 0;
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
        if (xmlKeys.Count == 0)
        {
            reportLines.Add("  (无翻译键)");
            return reportLines;
        }

        // 按语言分组 XML 键
        var xmlKeysByLang = xmlKeys
            .GroupBy(k => k.Language)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 获取所有实际存在的语言
        var availableLanguages = targetLanguages ?? xmlKeysByLang.Keys.ToArray();

        // 收集所有待删除的行
        var allToRemove = new List<(string Language, string FileName, int LineNumber, string Key)>();

        // 3. 对每种语言，计算差集
        foreach (var lang in availableLanguages.OrderBy(l => l))
        {
            if (!xmlKeysByLang.TryGetValue(lang, out var langXmlKeys))
            {
                reportLines.Add($"[WARN] 语言 '{lang}' 的翻译文件不存在，跳过。");
                continue;
            }

            var orphanedKeys = langXmlKeys
                .Where(k => !codeKeySet.Contains(k.Key))
                .ToList();

            if (orphanedKeys.Count == 0) continue;

            foreach (var keyInfo in orphanedKeys)
            {
                allToRemove.Add((keyInfo.Language, keyInfo.FileName, keyInfo.LineNumber, keyInfo.Key));
            }
        }

        if (allToRemove.Count == 0)
        {
            reportLines.Add("  (无需修复)");
            return reportLines;
        }

        // 4. 按 (Language, FileName) 分组执行修改
        var filesToFix = allToRemove
            .GroupBy(r => (r.Language, r.FileName))
            .OrderBy(g => g.Key.Language)
            .ThenBy(g => g.Key.FileName);

        foreach (var fileGroup in filesToFix)
        {
            var lang = fileGroup.Key.Language;
            var fileName = fileGroup.Key.FileName;
            var filePath = Path.Combine(languagesRoot, lang, "Keyed", fileName);

            if (!File.Exists(filePath))
            {
                reportLines.Add($"[ERROR] 文件不存在: {lang}/{fileName}");
                continue;
            }

            // 按行号降序排列（从后往前删，避免行号偏移）
            var linesToRemove = fileGroup
                .OrderByDescending(r => r.LineNumber)
                .ToList();

            try
            {
                var allLines = File.ReadAllLines(filePath).ToList();
                var fileFixedCount = 0;

                foreach (var item in linesToRemove)
                {
                    var idx = item.LineNumber - 1;
                    if (idx >= 0 && idx < allLines.Count)
                    {
                        allLines.RemoveAt(idx);
                        fileFixedCount++;
                        totalFixed++;

                        var isComment = item.Key.EndsWith("Comment");
                        if (showComments || !isComment)
                        {
                            reportLines.Add($"[FIX] {lang}/{fileName}: 移除多余键 {item.Key} (第{item.LineNumber}行)");
                        }
                    }
                }

                // 如果未详细报告 Comment 键，输出一条摘要
                var commentCount = linesToRemove.Count(r => r.Key.EndsWith("Comment"));
                var regularCount = fileFixedCount - commentCount;
                if (!showComments && commentCount > 0 && regularCount == fileFixedCount)
                {
                    // 全是 Comment 键的情况
                }
                else if (!showComments && commentCount > 0)
                {
                    reportLines.Add($"[INFO] {lang}/{fileName}: 同时移除了 {commentCount} 个 Comment 键（使用 --show-comments 查看详情）");
                }

                File.WriteAllLines(filePath, allLines);
            }
            catch (Exception ex)
            {
                reportLines.Add($"[ERROR] {lang}/{fileName}: 写入失败 - {ex.Message}");
            }
        }

        if (totalFixed == 0)
        {
            reportLines.Add("  (无需修复)");
        }

        return reportLines;
    }
}