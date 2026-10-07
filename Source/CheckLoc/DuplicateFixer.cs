namespace CheckLoc;

/// <summary>
/// 清除重复翻译键。
/// 同文件重复：保留第一次出现的行，删除后续重复行。
/// 跨文件重复（含 Core.xml）：保留 Core.xml 中的行，删除其他文件中的行。
/// 跨文件重复（无 Core.xml）：不修复，仅报告。
/// </summary>
public static class DuplicateFixer
{
    /// <summary>
    /// 运行重复键修复。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要修复的语言列表。为空则修复所有。</param>
    /// <param name="totalFixed">输出: 修复的重复键总数</param>
    /// <returns>修复报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, out int totalFixed)
    {
        totalFixed = 0;
        var reportLines = new List<string>();

        var languagesRoot = Path.Combine(projectRoot, "Languages");
        if (!Directory.Exists(languagesRoot))
        {
            reportLines.Add("[ERROR] 语言目录不存在: " + languagesRoot);
            return reportLines;
        }

        // 1. 获取全量 XML 键
        var allKeys = XmlKeyExtractor.ExtractKeys(languagesRoot, targetLanguages);
        if (allKeys.Count == 0)
        {
            reportLines.Add("  (无翻译键)");
            return reportLines;
        }

        // 收集所有待删除的行: (Language, FileName, LineNumber, Key, Reason)
        var toRemove = new List<(string Language, string FileName, int LineNumber, string Key, string Reason)>();

        // 2. 处理同文件重复
        var sameFileDups = XmlKeyExtractor.FindSameFileDuplicates(allKeys);
        foreach (var dup in sameFileDups)
        {
            // 保留行号最小的，标记其余行
            var keepLine = dup.Lines.Min();
            foreach (var line in dup.Lines.Where(l => l != keepLine))
            {
                toRemove.Add((dup.Language, dup.FileName, line, dup.Key, "同文件重复"));
            }
        }

        // 3. 处理跨文件重复（Core.xml 优先）
        var crossFileDups = allKeys
            .GroupBy(k => (k.Language, k.Key))
            .Where(g => g.Select(k => k.FileName).Distinct().Count() > 1)
            .ToList();

        foreach (var group in crossFileDups)
        {
            var files = group.Select(k => k.FileName).Distinct().ToList();
            if (files.Contains("Core.xml"))
            {
                // 保留 Core.xml 中的行，标记其他文件中的行
                foreach (var keyInfo in group)
                {
                    if (!string.Equals(keyInfo.FileName, "Core.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add((keyInfo.Language, keyInfo.FileName, keyInfo.LineNumber, keyInfo.Key, "跨文件重复(保留 Core.xml)"));
                    }
                }
            }
            // 无 Core.xml 的跨文件重复 → 跳过，仅报告
        }

        if (toRemove.Count == 0)
        {
            reportLines.Add("  (无需修复)");
            return reportLines;
        }

        // 4. 按 (Language, FileName) 分组执行修改
        var filesToFix = toRemove
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

                foreach (var item in linesToRemove)
                {
                    // 行号从 1 开始，列表索引从 0 开始
                    var idx = item.LineNumber - 1;
                    if (idx >= 0 && idx < allLines.Count)
                    {
                        allLines.RemoveAt(idx);
                        totalFixed++;
                        reportLines.Add($"[FIX] {lang}/{fileName}: 移除{item.Reason}键 {item.Key} (第{item.LineNumber}行)");
                    }
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