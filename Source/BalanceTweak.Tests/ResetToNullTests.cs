using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// 单字段重置（<c>StatColumnConfig.ResetTo</c>，被 <c>TweakUtility.ResetSingleItem</c> /
/// <c>ResetSelectedColumn</c> 调用）的两条不变量：
///
/// <list type="number">
/// <item><b>原值为 null 的 stat 字段，重置后必须真的回到 null</b> —— 也就是 def 上那条 stat 被删掉。</item>
/// <item><b>重置别的列时，不能顺手删掉别的 stat</b> —— 那些 null 是 Phase 3 同步出来的，
/// 不代表"要删"。</item>
/// </list>
///
/// <para>
/// 这两条互为反面：想修好第一条，最省事的做法是让 <c>forceResetStats</c> 越过 Phase 2 的 null 守卫，
/// 但那样会同时破坏第二条（重置列表列时 Phase 1 刚恢复的条目被一并删掉）。
/// 所以修法是"只让被重置的那一个字段越权"（<c>TweakData.forceResetField</c>）。
/// </para>
/// </summary>
public class ResetToNullTests
{
    private const string ProbeStatName = "__BT_ProbeNullPct";

    private class NullResetProbeData : TweakData<NullResetProbeData>
    {
        // 与生产里的百分比 stat 字段同构：StatDef 关联 + Prec 样式
        [TweakField(StatDef = ProbeStatName, Style = ColumnStyle.Prec)]
        public float? pct = null;

        // 生产数据类都带这一列；ApplyDefStats 的 Phase 1 / Phase 3 需要它
        [TweakField(Style = ColumnStyle.StatModList)]
        public List<StatModifier> statBases = new();

        public override void Apply()
        {
            if (def is ThingDef td) ApplyDefStats(td);
        }

        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    private sealed class Probe
    {
        public required NullResetProbeData Data { get; init; }
        public required StatColumnConfig Pct { get; init; }
        public required StatColumnConfig StatBases { get; init; }
        public required ThingDef Thing { get; init; }
    }

    /// <summary>StatDef 只能进一次 DefDatabase（<c>Fields</c> 首次访问时按名解析）。</summary>
    private static StatDef EnsureProbeStat()
    {
        var existing = DefDatabase<StatDef>.GetNamedSilentFail(ProbeStatName);
        if (existing != null) return existing;
        var stat = new StatDef { defName = ProbeStatName, label = "probe pct" };
        DefDatabase<StatDef>.Add(stat);
        return stat;
    }

    /// <param name="initialPct">def 上那条 stat 的初始值；null 表示 def 里本来没有这一项。</param>
    private static Probe Arrange(float? initialPct)
    {
        var stat = EnsureProbeStat();
        var thing = new ThingDef
        {
            defName = "__BT_ProbeNullThing",
            label = "probe thing",
            statBases = initialPct.HasValue
                ? new List<StatModifier> { new() { stat = stat, value = initialPct.Value } }
                : new List<StatModifier>(),
        };

        var data = new NullResetProbeData
        {
            id = new TweakData.TweakID("__BT_ProbeNullThing", SettingType.Apparel),
            def = thing,
            pct = initialPct,
            statBases = thing.statBases!.ToList(),
        };
        var cols = data.AllColumn;
        return new Probe
        {
            Data = data,
            Pct = cols.Single(c => c.fieldName == nameof(NullResetProbeData.pct)),
            StatBases = cols.Single(c => c.fieldName == nameof(NullResetProbeData.statBases)),
            Thing = thing,
        };
    }

    private static void Cleanup(NullResetProbeData data) => tweakDatas.Remove(data.id);

    private static bool HasProbeStat(ThingDef thing)
        => thing.statBases?.Any(s => s.stat?.defName == ProbeStatName) == true;

    private static float? ProbeStatValue(ThingDef thing)
        => thing.statBases?.Find(s => s.stat?.defName == ProbeStatName)?.value;

    [Fact]
    public void 原值为null的stat字段_重置后应回到null()
    {
        var p = Arrange(initialPct: null);
        try
        {
            Assert.Equal("Null", p.Pct.GetString(p.Data));

            // 改成 150% ⇒ 字段 1.5，def 上新增一条 stat
            p.Pct.CheckAndApply(p.Data, "150");
            Assert.Equal(1.5f, p.Data.pct!.Value, 5);
            Assert.Equal(1.5f, ProbeStatValue(p.Thing)!.Value, 5);

            // 单元格重置
            p.Pct.ResetTo(p.Data, originalValue: null);

            Assert.False(HasProbeStat(p.Thing), "重置后 def.statBases 不应残留该 stat");
            Assert.Null(p.Data.pct);
            Assert.Equal("Null", p.Pct.GetString(p.Data));
        }
        finally { Cleanup(p.Data); }
    }

    /// <summary>
    /// 反面守卫：重置 <c>statBases</c> 列（原值里有这条 stat）时，标量字段因 Phase 3 同步而成的 null
    /// 不能被当成"要删掉"。否则 Phase 1 刚恢复的条目会被 Phase 2 删掉，重置同样失效。
    /// </summary>
    [Fact]
    public void 重置stat列表列_不得删掉刚恢复的条目()
    {
        var p = Arrange(initialPct: 0.5f);
        try
        {
            var originalList = p.Thing.statBases!.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList();

            // 1) 用户把列表清空 ⇒ 条目从 def 消失，标量字段被 Phase 3 同步成 null
            p.StatBases.ApplyValue(p.Data, new List<StatModifier>());
            Assert.False(HasProbeStat(p.Thing));
            Assert.Null(p.Data.pct);

            // 2) 重置列表列 ⇒ Phase 1 把原列表写回 def
            p.StatBases.ResetTo(p.Data, originalList);

            Assert.True(HasProbeStat(p.Thing), "重置列表列后该条目应恢复");
            Assert.Equal(0.5f, ProbeStatValue(p.Thing)!.Value, 5);
            Assert.Equal(0.5f, p.Data.pct!.Value, 5);
        }
        finally { Cleanup(p.Data); }
    }
}
