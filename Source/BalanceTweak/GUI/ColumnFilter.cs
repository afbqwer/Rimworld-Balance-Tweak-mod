using System.Collections;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>列过滤的比较方式。文本过滤不看它（原版搜索只有"包含"一种语义）。</summary>
public enum ColumnFilterMode
{
    Greater,
    Less,
    Equal,
    /// <summary>区间 <c>[下限, 上限]</c> 或 <c>(下限, 上限)</c>，端点算不算看 <see cref="ColumnFilter.inclusive"/>。</summary>
    Range,
}

/// <summary>一列用哪种值来比 —— 由列的字段类型推出（见 <see cref="StatColumnConfig.filterKind"/>），不给用户选。</summary>
public enum ColumnFilterKind
{
    /// <summary>这一列没有可比的值（触发列 / Def 引用 / 过滤器 / 曲线…）。</summary>
    None,
    /// <summary>数字：大于 / 小于 / 等于 / 区间。</summary>
    Number,
    /// <summary>文本：走原版 <see cref="QuickSearchFilter"/> 的 match。</summary>
    Text,
    /// <summary>列表：项数 大于 / 小于 / 等于 / 区间。</summary>
    ListCount,
}

/// <summary>
/// 一条列过滤条件。
///
/// <para>
/// <b>它属于"视图"而不是"数据"。</b>和顶栏的搜索词、排序方式同级 —— 只活在本次会话内存里，
/// 既不写 ModSettings 也不进存档。理由是过滤会把行**藏起来**：一旦落盘，下次开游戏"数据怎么少了"
/// 就只能靠用户自己想起这回事；而搜索词 / 排序本来就不落盘，多一种落盘、少一种不落盘反而更难解释。
/// </para>
/// <para>
/// <b>建好之后不再改</b>（改条件 = 新建一条，见 <see cref="BalanceTweakSettings.ColumnFilterWindow"/>）。因此快照
/// （<c>BalanceTweakSettings.NavState.filters</c>）里存对象引用就够，不必深拷贝。
/// </para>
/// </summary>
public sealed class ColumnFilter
{
    public readonly StatColumnConfig column;
    public readonly ColumnFilterKind kind;
    public readonly ColumnFilterMode mode;

    /// <summary><see cref="number"/> 的单位：<c>Prec</c> 列屏幕上显示的是 ×100 的百分数，这个值就是 <c>true</c>。</summary>
    public readonly bool isPercent;

    /// <summary>
    /// 数字 / 项数的比较值，<b>按屏幕上看到的单位</b>。区间模式下它是<b>下限</b>（见 <see cref="numberMax"/>）。
    /// <para>
    /// <c>Prec</c> 列（ flammability / 各种 factor）的单元格显示 <c>value × 100</c> + <c>%</c>，输入框也按百分数收；
    /// 过滤值必须同口径，否则"看着 95.00% 填了 95"会一行都筛不出来。落回字段值时再乘 <see cref="isPercent"/> 的系数。
    /// </para>
    /// </summary>
    public readonly float number;

    /// <summary>区间模式下限的上界（闭区间，两端都算）。非区间模式等于 <see cref="number"/>，不参与比较。</summary>
    public readonly float numberMax;

    /// <summary>
    /// 端点算不算命中。一个开关同时管三种比较方式，而不是给每种各留一个：
    /// <list type="bullet">
    /// <item>大于：<c>true</c> → <c>≥</c>，<c>false</c> → <c>&gt;</c></item>
    /// <item>小于：<c>true</c> → <c>≤</c>，<c>false</c> → <c>&lt;</c></item>
    /// <item>范围：<c>true</c> → 闭区间 <c>[a, b]</c>，<c>false</c> → 开区间 <c>(a, b)</c></item>
    /// </list>
    /// 默认 <c>true</c>：填 5~10 的人想的是"5 和 10 都留着"，而界面上这个开关一直看得见，不会变成暗中生效的语义。
    /// </summary>
    public readonly bool inclusive;

    /// <summary>
    /// 文本过滤用的匹配器 —— <b>就是原版搜索组件本身</b>，与顶栏搜索同一套语义：
    /// <c>Trim</c> 后不区分大小写的子串匹配，命中结果进 5000 条 LRU 缓存。
    /// 自己写 <c>Contains</c> 会悄悄多出大小写 / 前后空格两种差异，所以这里绝不复刻一遍。
    /// </summary>
    public readonly QuickSearchFilter search = new();

    public ColumnFilter(StatColumnConfig column, ColumnFilterMode mode, float number, float numberMax,
        bool inclusive, string? text = null)
    {
        this.column = column;
        kind = column.filterKind;
        this.mode = mode;
        this.number = number;
        this.numberMax = mode == ColumnFilterMode.Range ? numberMax : number;
        this.inclusive = inclusive;
        isPercent = column.columnType == StatColumnConfig.ColumnStyle.Prec;
        search.Text = text ?? "";
    }

    /// <summary>
    /// 这条条件是否真的约束了行数。文本过滤的空串 = 不约束（等于没过滤），不能留下一条"什么都不筛"的条件。
    /// 判空要连空白一起算：<see cref="QuickSearchFilter"/> 的 <c>Text</c> 只在 match 时才 Trim，
    /// 一个纯空格的条件会留下"涂成橙色却一行都不筛"的列。
    /// </summary>
    public bool IsActive => kind switch
    {
        ColumnFilterKind.Text => !string.IsNullOrWhiteSpace(search.Text),
        ColumnFilterKind.None => false,
        _ => true,
    };

    /// <summary>这一行是否通过本条条件。</summary>
    public bool Matches(TweakData data)
    {
        switch (kind)
        {
            case ColumnFilterKind.Number:
                float? value = NumericValue(column, data);
                // 没有值的行不参与数字比较：既不满足 > 也不满足 <，更不满足 = / 区间。
                // 把 null 当成 0 会让"大于 0"冒出几百行本来没有值的行，那是另一种误导。
                return value.HasValue && Compare(value.Value);
            case ColumnFilterKind.Text:
                return search.Matches(column.GetString(data));
            case ColumnFilterKind.ListCount:
                int count = (column.GetRawValue(data) as IList)?.Count ?? 0;
                return Compare(count);
            default:
                return true;
        }
    }

    /// <summary>比较用的阈值 —— 换回<b>字段</b>单位（<see cref="number"/> 是屏幕单位，见其注释）。</summary>
    private float Target => isPercent ? number * 0.01f : number;

    /// <summary>区间上界，单位同 <see cref="Target"/>。</summary>
    private float TargetMax => isPercent ? numberMax * 0.01f : numberMax;

    private bool Compare(float value) => mode switch
    {
        ColumnFilterMode.Greater => inclusive ? value >= Target : value > Target,
        ColumnFilterMode.Less => inclusive ? value <= Target : value < Target,
        ColumnFilterMode.Equal => value == Target,
        // 区间用"一个比较"表达开闭，而不是让用户拿两条单边条件去凑：
        // 凑出来的 (> 5 ∧ < 10) 边界行会被漏掉，而填 5~10 的人想的就是"5 到 10 都留着"。
        _ => inclusive
            ? value >= Target && value <= TargetMax
            : value > Target && value < TargetMax,
    };

    /// <summary>
    /// 取列的数值，<b>不受界面状态影响</b>。
    /// <para>
    /// 刻意不用 <see cref="StatColumnConfig.GetNumericValue"/>：它对"当前选中列"有两条旁路
    /// （显示默认值 / 显示结果值预览），混进过滤会让同一份数据在"点了列头之后"筛出不同的行。
    /// 派生列（Display）先补一次 <c>EnsureDisplayValues</c> —— 进页签时已经全算过，这里是 O(1) 早退。
    /// </para>
    /// </summary>
    private static float? NumericValue(StatColumnConfig column, TweakData data)
    {
        if (column.columnDataType == StatColumnConfig.ColumnDataType.Display) data.EnsureDisplayValues();
        return column.GetRawValue(data) switch
        {
            float f => f,
            int i => i,
            _ => null,
        };
    }

    /// <summary>条件摘要，给列头 tooltip 与右键菜单看：<c>≥ 5</c> / <c>&gt; 5</c> / <c>[5 ~ 10]</c> / <c>(5 ~ 10)</c> / <c>包含 ab</c>。</summary>
    public string Describe()
    {
        if (kind == ColumnFilterKind.Text) return "MST.FilterTextDesc".Translate(search.Text).RawText;

        string unit = isPercent ? "%" : "";
        string expression;
        if (mode == ColumnFilterMode.Range)
        {
            // 方括号 / 圆括号一眼就能看出端点算不算，比再写一句"含端点"省地方（这一行要做菜单标签）。
            expression = "MST.FilterRangeDesc".Translate(
                (inclusive ? "[" : "(") + $"{number:0.##}{unit}",
                $"{numberMax:0.##}{unit}" + (inclusive ? "]" : ")")).RawText;
        }
        else
        {
            string symbol = mode switch
            {
                ColumnFilterMode.Greater => inclusive ? "≥" : ">",
                ColumnFilterMode.Less => inclusive ? "≤" : "<",
                _ => "=",
            };
            expression = $"{symbol} {number:0.##}{unit}";
        }

        return kind == ColumnFilterKind.ListCount
            ? "MST.FilterCountDesc".Translate(expression).RawText
            : expression;
    }
}
