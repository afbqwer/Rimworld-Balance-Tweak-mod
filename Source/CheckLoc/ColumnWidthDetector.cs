namespace CheckLoc;

/// <summary>
/// 检测字段列标题（MST.{TweakField 字段名}）的显示宽度是否超出表格列头可容纳的范围。
/// 列头空间有限：上限约 5 个中文字符（或 10 个英文字符）宽。
/// Comment 后缀键（tooltip）、弹窗按钮、枚举/页签标签等不显示在列头的键不受此限制。
/// </summary>
public static class ColumnWidthDetector
{
    /// <summary>
    /// 列头最大显示宽度：半角字符按 1、全角（CJK）字符按 2 计，
    /// 上限 10 即 5 个中文字符（或 10 个英文字符）的宽度。
    /// </summary>
    private const int MaxWidth = 10;

    /// <summary>
    /// 运行列标题宽度检查。
    /// </summary>
    /// <param name="projectRoot">项目根目录</param>
    /// <param name="targetLanguages">要检查的语言列表。为空则检查所有。</param>
    /// <param name="totalIssues">输出: 超宽列标题总数</param>
    /// <returns>检查报告行</returns>
    public static List<string> Run(string projectRoot, string[]? targetLanguages, out int totalIssues)
    {
        totalIssues = 0;
        var reportLines = new List<string>();

        // 1. 从 C# 源码提取 [TweakField] 字段名，确定列标题键的范围
        var sourceDir = Path.Combine(projectRoot, "Source", "BalanceTweak");
        var fieldNames = CodeKeyExtractor.ExtractTweakFieldNames(sourceDir);
        if (fieldNames.Count == 0)
        {
            reportLines.Add("[ERROR] 未从 C# 源码解析到任何 [TweakField] 字段，无法确定列标题键范围: " + sourceDir);
            return reportLines;
        }

        var columnTitleKeys = fieldNames.Select(n => "MST." + n).ToHashSet();

        var languagesRoot = Path.Combine(projectRoot, "Languages");
        if (!Directory.Exists(languagesRoot))
        {
            reportLines.Add("[ERROR] 语言目录不存在: " + languagesRoot);
            return reportLines;
        }

        // 2. 只取列标题键的翻译条目；同键重复定义只检查一次（重复问题由 duplicate 检查负责）
        var entries = XmlKeyExtractor.ExtractEntries(languagesRoot, targetLanguages)
            .Where(e => columnTitleKeys.Contains(e.Key))
            .GroupBy(e => (e.Language, e.FileName, e.Key))
            .Select(g => g.OrderBy(e => e.LineNumber).First())
            .OrderBy(e => e.Language).ThenBy(e => e.FileName).ThenBy(e => e.LineNumber)
            .ToList();

        if (entries.Count == 0)
        {
            reportLines.Add("  (未找到任何字段列标题翻译条目)");
            return reportLines;
        }

        // 3. 逐条测量显示宽度
        var violations = entries
            .Select(e => (Entry: e, Width: MeasureDisplayWidth(e.Value)))
            .Where(x => x.Width > MaxWidth)
            .ToList();

        if (violations.Count == 0)
        {
            reportLines.Add("  (所有字段列标题宽度均未超限)");
            return reportLines;
        }

        totalIssues = violations.Count;
        foreach (var v in violations)
        {
            reportLines.Add(
                $"[WARN] {v.Entry.Language}/{v.Entry.FileName}: {v.Entry.Key} (第{v.Entry.LineNumber}行) " +
                $"\"{v.Entry.Value}\" 显示宽度 {v.Width} > {MaxWidth}（≈5 中文 / 10 英文）");
        }

        return reportLines;
    }

    /// <summary>
    /// 计算显示宽度：全角字符（CJK 统一表意文字、假名、谚文、全角标点与符号）计 2，其余计 1。
    /// </summary>
    private static int MeasureDisplayWidth(string text)
    {
        var width = 0;
        foreach (var ch in text)
        {
            width += IsFullWidth(ch) ? 2 : 1;
        }
        return width;
    }

    private static bool IsFullWidth(char ch) => ((int)ch) switch
    {
        >= 0x1100 and <= 0x115F => true,   // 谚文字母
        >= 0x2E80 and <= 0xA4CF => true,   // CJK 部首、标点、假名、注音、统一表意文字
        >= 0xAC00 and <= 0xD7A3 => true,   // 谚文音节
        >= 0xF900 and <= 0xFAFF => true,   // CJK 兼容表意文字
        >= 0xFE30 and <= 0xFE6F => true,   // CJK 兼容形式
        >= 0xFF00 and <= 0xFF60 => true,   // 全角 ASCII 与标点
        >= 0xFFE0 and <= 0xFFE6 => true,   // 全角符号
        _ => false
    };
}
