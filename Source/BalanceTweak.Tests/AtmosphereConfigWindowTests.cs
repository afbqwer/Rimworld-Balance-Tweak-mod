using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using Xunit;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="AtmosphereConfigWindow"/> 的接线守卫。
///
/// <para>
/// 这个窗口靠 <c>nameof(XxxData.字段)</c> 手写"哪一格属于哪个宿主、哪个控件"。写错的三种方式
/// 编译期全是绿的，运行期的表现却各不相同：
/// </para>
/// <list type="bullet">
/// <item>把 <c>DamageData</c> 的字段放进地形节 → 那一行永远不出现（<c>ConfigWindow</c> 按字段名取不到就整行跳过）。</item>
/// <item>漏了一格 → 那个字段再也无法从界面编辑（它已 <c>NotAColumn</c>，表格里也没有）。</item>
/// <item>行类型写成 <c>Float</c> 却挂在 <c>int?</c> 字段上 → 玩家一提交就 <c>InvalidCastException</c>（窗口构造时只 <c>Log.Error</c>）。</item>
/// </list>
/// <para>
/// 三条都只读元数据，不需要 <c>DefDatabase</c>，因此能在没有游戏数据的测试进程里跑。
///
/// <para>
/// P3 把建筑页也接了进来（9 个 Comp + 1 个扩展、57 格、十节），字段数从 18 涨到 75，
/// 手抄对应关系的出错面随之变大 —— 这三条断言的价值主要在建筑这半边。
/// </para>
/// </para>
/// </summary>
public class AtmosphereConfigWindowTests
{
    private static readonly Type[] Hosts =
    {
        typeof(TerrainData), typeof(DamageData), typeof(ApparelData), typeof(HediffData), typeof(RaceData),
        typeof(BuildingData),
    };

    private static IEnumerable<(ConfigSection Section, ConfigRow Row)> Rows()
        => AtmosphereConfigWindow.Sections.SelectMany(s => s.rows.Select(r => (s, r)));

    /// <summary>五个宿主上所有"只落盘、不出列"的 AO 字段（触发列本身不在此列）。</summary>
    private static Dictionary<string, Type> AoFields()
    {
        var map = new Dictionary<string, Type>();
        foreach (var host in Hosts)
            foreach (var field in host.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<TweakFieldAttribute>();
                if (attr == null || !attr.NotAColumn) continue;
                if (attr.MayRequire != TweakDatabase.AO) continue;
                map[field.Name] = host;
            }
        return map;
    }

    /// <summary>
    /// 一格不多、一格不少：窗口的行集合必须与五个宿主上的 AO 字段集合完全相等。
    /// 加了字段忘加行 → 这里挂；写了行但字段名打错 → 这里也挂（<c>nameof</c> 打错会编译失败，
    /// 但"抄了别的宿主的字段名"能过编译）。
    /// </summary>
    [Fact]
    public void 窗口覆盖全部AO字段且不重复()
    {
        var expected = AoFields();
        var actual = Rows().Select(r => r.Row.field).ToList();

        var duplicated = actual.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var missing = expected.Keys.Except(actual).ToList();
        var extra = actual.Except(expected.Keys).ToList();

        Assert.True(duplicated.Count == 0, "同一格在窗口里出现了多次：" + string.Join(", ", duplicated));
        Assert.True(missing.Count == 0, "这些 AO 字段没有任何窗口行（永远无法从界面编辑）：" + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "这些窗口行找不到对应的 AO 字段（整行不会显示）：" + string.Join(", ", extra));
    }

    /// <summary>
    /// 每一节的行必须全属于同一个宿主。判据就是字段的 <c>DeclaringType</c> ——
    /// 而"这一节到底给谁看"由 <c>visible: d =&gt; d is XxxData</c> 决定，两者对不上就等于整行消失。
    /// </summary>
    [Fact]
    public void 每一节的行都来自同一个宿主()
    {
        var problems = new List<string>();

        foreach (var section in AtmosphereConfigWindow.Sections)
        {
            var owners = section.rows
                .Select(r => AoFields().TryGetValue(r.field, out var t) ? t.Name : "?" + r.field)
                .Distinct()
                .ToList();

            if (owners.Count != 1)
                problems.Add($"节 {section.titleKey ?? "(无标题)"} 的字段来自多个宿主：{string.Join(" / ", owners)}");
        }

        Assert.True(problems.Count == 0, "配置窗口的分节与宿主对不上（那些行永远不会显示）：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// 行类型必须与字段真实类型相容，否则 <c>ApplyValue(float)</c> 会往 <c>int?</c> 里塞。
    /// 窗口构造时也校验这一条，但那条只写日志；这里让它在 CI 里直接失败。
    /// </summary>
    [Fact]
    public void 行类型与字段类型相容()
    {
        var fields = AoFields();
        var problems = new List<string>();

        foreach (var (_, row) in Rows())
        {
            if (!fields.TryGetValue(row.field, out var host)) continue;
            var field = host.GetField(row.field);
            if (field == null) continue;
            if (!ConfigWindow.KindMatches(row.kind, field.FieldType))
                problems.Add($"{host.Name}.{row.field}：行是 {row.kind}，字段是 {field.FieldType.Name}");
        }

        Assert.True(problems.Count == 0, "配置窗口的行类型与字段类型不符：\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// 每个宿主**恰好**一个指向本窗口的触发列 —— 多一个就成了两列，
    /// 少一个则那一页的 AO 数据彻底不可编辑（字段已经全是 <c>NotAColumn</c>）。
    /// <para>
    /// 建筑页另有 <c>relayConfig</c> / <c>turretConfig</c> 两个 Fortified 触发列，按 <c>Editor</c>
    /// 过滤后不影响这条断言（它们指向别的窗口）。
    /// </para>
    /// </summary>
    [Fact]
    public void 每个宿主各一个触发列()
    {
        foreach (var host in Hosts)
        {
            var triggers = host.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => (Field: f, Attr: f.GetCustomAttribute<TweakFieldAttribute>()))
                .Where(x => x.Attr != null && x.Attr.Style == ColumnStyle.Config)
                .Where(x => x.Attr!.Editor == typeof(AtmosphereConfigWindow))
                .ToList();

            Assert.True(triggers.Count == 1,
                $"{host.Name} 应当恰好有一个 AtmosphereConfigWindow 触发列，实际 {triggers.Count} 个：{string.Join(", ", triggers.Select(t => t.Field.Name))}");
        }
    }
}
