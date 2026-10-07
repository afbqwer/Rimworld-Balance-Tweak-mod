using System.Reflection;

namespace CheckLoc;

/// <summary>
/// BalanceTweak 本地化辅助工具。
/// 检测翻译键的缺失、重复、多余和列标题宽度问题。
/// </summary>
public static class Program
{
    // ANSI 颜色代码
    private const string ColorReset = "\e[0m";
    private const string ColorRed = "\e[31m";
    private const string ColorGreen = "\e[32m";
    private const string ColorYellow = "\e[33m";
    private const string ColorCyan = "\e[36m";
    private const string ColorBold = "\e[1m";

    public static int Main(string[] args)
    {
        // 解析参数
        var check = "all";
        var lang = "all";
        var showComments = false;
        string? fix = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--check" when i + 1 < args.Length:
                    check = args[++i].ToLowerInvariant();
                    break;
                case "--lang" when i + 1 < args.Length:
                    lang = args[++i];
                    break;
                case "--show-comments":
                    showComments = true;
                    break;
                case "--fix" when i + 1 < args.Length:
                    fix = args[++i].ToLowerInvariant();
                    break;
                case "-h" or "--help":
                    PrintHelp();
                    return 0;
                default:
                    Console.Error.WriteLine($"未知参数: {args[i]}");
                    PrintHelp();
                    return 1;
            }
        }

        if (check is not ("all" or "missing" or "duplicate" or "orphaned" or "width"))
        {
            Console.Error.WriteLine($"无效的 --check 值: '{check}'，可选: all, missing, duplicate, orphaned, width");
            return 1;
        }

        if (fix is not null and not ("all" or "duplicate" or "orphaned"))
        {
            Console.Error.WriteLine($"无效的 --fix 值: '{fix}'，可选: all, duplicate, orphaned");
            return 1;
        }

        // --fix 模式隐式覆盖 --check
        if (fix != null)
        {
            check = fix switch
            {
                "all" => "all",
                "duplicate" => "duplicate",
                "orphaned" => "orphaned",
                _ => check
            };
        }

        if (lang is not "all" and not "English" and not "ChineseSimplified")
        {
            Console.Error.WriteLine($"无效的 --lang 值: '{lang}'，可选: all, English, ChineseSimplified");
            return 1;
        }

        // 定位项目根目录
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            Console.Error.WriteLine("[ERROR] 无法定位项目根目录。请确保从项目目录中运行此工具。");
            return 1;
        }

        // 确定目标语言
        string[]? targetLanguages = lang == "all" ? null : new[] { lang };

        // 检查语言目录是否存在
        var languagesRoot = Path.Combine(projectRoot, "Languages");
        if (!Directory.Exists(languagesRoot))
        {
            Console.Error.WriteLine($"[ERROR] 语言目录不存在: {languagesRoot}");
            return 1;
        }

        // 确定实际检查的语言
        var displayLanguages = targetLanguages ?? new[] { "English", "ChineseSimplified" };

        // 打印头部
        Console.WriteLine($"{ColorBold}{ColorCyan}=== BalanceTweak 本地化检查报告 ==={ColorReset}");
        Console.WriteLine($"语言: {string.Join(", ", displayLanguages)}");
        Console.WriteLine($"项目根目录: {projectRoot}");
        Console.WriteLine();

        var totalIssues = 0;
        var totalFixed = 0;

        // 运行缺失键检测
        if (check is "all" or "missing")
        {
            Console.WriteLine($"{ColorBold}--- 缺失翻译键 ---{ColorReset}");
            var lines = MissingKeyDetector.Run(projectRoot, targetLanguages, showComments, out var missingCount);
            foreach (var line in lines)
            {
                PrintColored(line);
            }
            totalIssues += missingCount;
        }

        // 运行重复键检测
        if (check is "all" or "duplicate")
        {
            Console.WriteLine($"{ColorBold}--- 重复翻译键 ---{ColorReset}");
            var lines = DuplicateDetector.Run(projectRoot, targetLanguages, out var dupCount);
            foreach (var line in lines)
            {
                PrintColored(line);
            }
            totalIssues += dupCount;
        }

        // 运行多余键检测
        if (check is "all" or "orphaned")
        {
            Console.WriteLine($"{ColorBold}--- 多余翻译键 ---{ColorReset}");
            var lines = OrphanedKeyDetector.Run(projectRoot, targetLanguages, showComments, out var orphanedCount);
            foreach (var line in lines)
            {
                PrintColored(line);
            }
            totalIssues += orphanedCount;
        }

        // 运行列标题宽度检查
        if (check is "all" or "width")
        {
            Console.WriteLine($"{ColorBold}--- 字段列标题宽度 ---{ColorReset}");
            var lines = ColumnWidthDetector.Run(projectRoot, targetLanguages, out var widthCount);
            foreach (var line in lines)
            {
                PrintColored(line);
            }
            totalIssues += widthCount;
        }

        // 运行修复
        if (fix is "duplicate" or "all")
        {
            Console.WriteLine();
            Console.WriteLine($"{ColorBold}--- 重复键修复 ---{ColorReset}");
            var fixLines = DuplicateFixer.Run(projectRoot, targetLanguages, out var fixDupCount);
            foreach (var line in fixLines)
            {
                PrintColored(line);
            }
            totalFixed += fixDupCount;
        }

        if (fix is "orphaned" or "all")
        {
            Console.WriteLine();
            Console.WriteLine($"{ColorBold}--- 多余键修复 ---{ColorReset}");
            var fixLines = OrphanedKeyFixer.Run(projectRoot, targetLanguages, showComments, out var fixOrphanedCount);
            foreach (var line in fixLines)
            {
                PrintColored(line);
            }
            totalFixed += fixOrphanedCount;
        }

        // 摘要
        Console.WriteLine();
        Console.WriteLine($"{ColorBold}{ColorCyan}=== 摘要 ==={ColorReset}");
        if (totalIssues == 0 && totalFixed == 0)
        {
            Console.WriteLine($"{ColorCyan}检查完成，未发现问题。{ColorReset}");
        }
        else
        {
            if (totalIssues > 0)
                Console.WriteLine($"{ColorYellow}检查完成，发现 {totalIssues} 个问题。{ColorReset}");
            if (totalFixed > 0)
                Console.WriteLine($"已自动修复 {totalFixed} 个问题。");
        }

        return (totalIssues - totalFixed) > 0 ? 1 : 0;
    }

    /// <summary>
    /// 根据程序集位置定位项目根目录。
    /// CheckLoc.dll 在 Source/CheckLoc/bin/Debug/net10.0/ 下，
    /// 项目根目录 = 向上 5 级。
    /// </summary>
    private static string? FindProjectRoot()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyDir == null) return null;

        // 尝试从 bin 目录向上查找
        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null)
        {
            // 检查是否存在 Languages/ 和 Source/ 目录（项目根目录的标志）
            if (Directory.Exists(Path.Combine(dir.FullName, "Languages")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Source")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }

    private static void PrintColored(string line)
    {
        if (line.StartsWith("[ERROR]"))
        {
            Console.WriteLine($"{ColorRed}{line}{ColorReset}");
        }
        else if (line.StartsWith("[WARN]"))
        {
            Console.WriteLine($"{ColorYellow}{line}{ColorReset}");
        }
        else if (line.StartsWith("[FIX]"))
        {
            Console.WriteLine($"{ColorGreen}{line}{ColorReset}");
        }
        else
        {
            Console.WriteLine(line);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine($@"
{ColorBold}BalanceTweak 本地化辅助工具{ColorReset}

用法: dotnet run --project Source/CheckLoc [--check <type>] [--lang <lang>] [--show-comments] [--fix <type>]

选项:
  --check <all|missing|duplicate|orphaned|width>  检查类型 (默认: all)
                                                  width: 字段列标题显示宽度（上限 ≈5 中文 / 10 英文）
  --fix <all|duplicate|orphaned>            自动修复模式（默认不修复）
                                    duplicate: 移除同文件/跨文件(Core.xml)重复键
                                    orphaned: 移除代码未引用的多余键
  --lang <all|English|ChineseSimplified>     目标语言 (默认: all)
  --show-comments                  显示 Comment 后缀键详情（默认不显示）
  -h, --help                        显示帮助

示例:
  dotnet run --project Source/CheckLoc
  dotnet run --project Source/CheckLoc -- --check missing
  dotnet run --project Source/CheckLoc -- --check orphaned
  dotnet run --project Source/CheckLoc -- --check width
  dotnet run --project Source/CheckLoc -- --check missing --show-comments
  dotnet run --project Source/CheckLoc -- --check duplicate --lang ChineseSimplified
  dotnet run --project Source/CheckLoc -- --fix duplicate
  dotnet run --project Source/CheckLoc -- --fix orphaned
  dotnet run --project Source/CheckLoc -- --fix all --lang ChineseSimplified
");
    }
}