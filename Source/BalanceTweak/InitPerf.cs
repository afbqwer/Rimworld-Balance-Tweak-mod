using System.Collections.Generic;
using System.Diagnostics;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak;

/// <summary>
/// 初始化期的细分计量（只测量，不改变任何行为）。
///
/// <para>
/// <see cref="TweakDatabase.Init"/> 那层 <c>Mark</c> 只能给到"阶段"粒度，而两个大桶
/// （<c>originalData 创建</c>、<c>直接修改项 Apply</c>）各自包着好几种截然不同的工作，
/// 光看总数没法判断该改哪里。这里补的是**桶内部**的账：
/// </para>
/// <list type="bullet">
/// <item>Apply 按 <see cref="SettingType"/> 拆开：条数、二次 SetDef、Apply 本体、其中 stat 写回；</item>
/// <item>Apply 期间到底跑了几次 <c>GenValues</c>、清了几次 <c>Def.ClearCachedData</c>
/// —— 这两个计数是"跨行重复刷新"的直接证据（次数远大于行数就是重复算）；</item>
/// <item>Apply 结束后还有多少行的展示值被置脏（= 进页签时还要再算一遍）。</item>
/// </list>
///
/// <para>
/// 全部计数只在 <see cref="Start"/> 与 <see cref="Finish"/> 之间累加，运行期零成本：
/// 每个入口先看 <see cref="Active"/>，为 false 直接返回，连时间戳都不取。
/// </para>
/// </summary>
internal static class InitPerf
{
    internal struct Row
    {
        public int Count;
        public double SetDefMs;
        public double ApplyMs;
        public double StatMs;
        public int GenValues;
        public int LabelClears;
    }

    private static readonly Dictionary<SettingType, Row> rows = [];
    private static bool active;

    internal static bool Active => active;

    // Prepare 阶段（逐行 EnsureDisplayValues）
    internal static int PrepareRows;
    internal static int PrepareComputed;
    internal static double PrepareMs;

    // Apply 期间被触发的池构建（CombatGear 上报）
    internal static double PoolMs;
    internal static int PoolBuilds;

    // Apply 结束后仍为脏的展示值
    internal static int StaleAfterApply;
    internal static int StaleTotal;

    /// <summary>
    /// 全表级刷新的次数。这两件事本身是"重建整张索引"，一次就该覆盖本轮所有改动；
    /// 若在逐条 Apply 里被反复调用（次数跟着改动行数走），那就是纯粹的重复劳动。
    /// </summary>
    internal static int ThingSetRefreshes;
    internal static int RecipeRegens;

    internal static void Start()
    {
        rows.Clear();
        PrepareRows = PrepareComputed = PoolBuilds = 0;
        PrepareMs = PoolMs = 0;
        StaleAfterApply = StaleTotal = 0;
        ThingSetRefreshes = RecipeRegens = 0;
        active = true;
    }

    internal static void Finish() => active = false;

    internal static long T() => Stopwatch.GetTimestamp();

    internal static double MsSince(long t0)
        => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

    // 全是"取-改-写"：Row 是值类型，Dictionary 索引器返回副本，直接改副本不会落回表里。
    internal static void AddApply(SettingType type, double setDefMs, double applyMs)
    {
        if (!active) return;
        var r = rows.TryGetValue(type, out var cur) ? cur : default;
        r.Count++;
        r.SetDefMs += setDefMs;
        r.ApplyMs += applyMs;
        rows[type] = r;
    }

    internal static void AddStat(SettingType type, double ms)
    {
        if (!active) return;
        var r = rows.TryGetValue(type, out var cur) ? cur : default;
        r.StatMs += ms;
        rows[type] = r;
    }

    internal static void AddGenValues(SettingType type)
    {
        if (!active) return;
        var r = rows.TryGetValue(type, out var cur) ? cur : default;
        r.GenValues++;
        rows[type] = r;
    }

    internal static void AddLabelClear(SettingType type)
    {
        if (!active) return;
        var r = rows.TryGetValue(type, out var cur) ? cur : default;
        r.LabelClears++;
        rows[type] = r;
    }

    internal static void AddPool(double ms)
    {
        if (!active) return;
        PoolMs += ms;
        PoolBuilds++;
    }

    internal static void AddThingSetRefresh()
    {
        if (active) ThingSetRefreshes++;
    }

    internal static void AddRecipeRegen(int count)
    {
        if (active) RecipeRegens += count;
    }

    /// <summary>把细分账追加到 <see cref="TweakDatabase.Init"/> 的耗时报告里。</summary>
    internal static void AppendReport(List<string> lines)
    {
        int totalCount = 0, genValues = 0, labelClears = 0;
        double totalApply = 0, totalStat = 0, totalSet = 0;
        foreach (var kv in rows)
        {
            totalCount += kv.Value.Count;
            totalApply += kv.Value.ApplyMs;
            totalStat += kv.Value.StatMs;
            totalSet += kv.Value.SetDefMs;
            genValues += kv.Value.GenValues;
            labelClears += kv.Value.LabelClears;
        }

        if (totalCount == 0) return;

        lines.Add($"  Apply 细分（{totalCount} 条 / {totalApply:F1}ms；二次 SetDef {totalSet:F1}ms，其中 stat 写回 {totalStat:F1}ms）:");
        var ordered = new List<KeyValuePair<SettingType, Row>>(rows);
        ordered.Sort((a, b) => b.Value.ApplyMs.CompareTo(a.Value.ApplyMs));

        for (int i = 0; i < ordered.Count && i < 12; i++)
        {
            var type = ordered[i].Key;
            var r = ordered[i].Value;
            lines.Add($"    {type}: {r.Count} 条 {r.ApplyMs:F1}ms"
                      + $"（均 {r.ApplyMs * 1000.0 / r.Count:F0}µs，stat {r.StatMs:F1}ms"
                      + (r.GenValues > 0 ? $"，GenValues {r.GenValues} 次" : "")
                      + (r.LabelClears > 0 ? $"，清 label 缓存 {r.LabelClears} 次" : "")
                      + "）");
        }
        if (ordered.Count > 12)
        {
            double restMs = 0;
            int restCount = 0;
            for (int i = 12; i < ordered.Count; i++)
            {
                restMs += ordered[i].Value.ApplyMs;
                restCount += ordered[i].Value.Count;
            }
            lines.Add($"    其余 {ordered.Count - 12} 类: {restCount} 条 {restMs:F1}ms");
        }

        // 一条汇总比逐类数字更能回答"有没有重复刷新"：次数明显大于行数就是重复。
        lines.Add($"  Apply 期间缓存刷新合计：GenValues {genValues} 次，Def.ClearCachedData {labelClears} 次");
        if (ThingSetRefreshes > 0 || RecipeRegens > 0)
            lines.Add($"  全表级刷新：ThingSetMaker 索引重建 {ThingSetRefreshes} 次，配方重算 {RecipeRegens} 条");

        if (StaleTotal > 0)
            lines.Add($"  Apply 后展示值仍为脏：{StaleAfterApply} / {StaleTotal} 行（进对应页签时会再算一遍）");
    }
}
