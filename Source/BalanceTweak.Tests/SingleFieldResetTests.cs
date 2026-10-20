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
/// 单元格重置（<c>StatColumnConfig.ResetTo</c>）的第三条不变量：
/// <b>同行还有别的字段处于"已修改"状态时，被重置的那个字段依然要落盘</b>。
///
/// <para>
/// 重置做的事是"把字段写回原值 + 清掉修改标记"，于是该字段变成 <see cref="FieldState.Unset"/>。
/// 非 stat 字段的写入完全依赖三态（<c>ApplyV/ApplyN/ApplyL/ApplyD</c>），Unset ⇒ 不写；
/// 只有整行恰好等于原始数据时才会由 <c>RemoveData</c>（整行重置）兜回来。
/// 同行只要还有一个字段是 Set，这条路就走不到，重置就"只改了界面"。
/// </para>
///
/// <para>
/// stat 字段不受影响：<c>forceResetStats</c> + Phase 2 的 <c>isResetTarget</c> 会把它写下去。
/// 所以第二组用例反过来钉住"重置 stat 时，同行的非 stat 修改不能被顺手抹掉"。
/// </para>
/// </summary>
[Collection(DefDatabaseProbeCollection.Name)]
public class SingleFieldResetTests
{
    private const string ProbeStatName = "__BT_ProbeSinglePct";

    private class SingleResetProbeData : TweakData<SingleResetProbeData>
    {
        [TweakField(StatDef = ProbeStatName, Style = ColumnStyle.Prec)]
        public float? pct = null;

        /// <summary>非 stat 字段，写法与生产数据类一致（<c>TerrainData.Apply</c> 的 <c>pathCost</c>）。</summary>
        [TweakField(Style = ColumnStyle.Int)]
        public int? pathCost = null;

        [TweakField(Style = ColumnStyle.StatModList)]
        public List<StatModifier>? statBases = null;

        public override void Apply()
        {
            if (def is ThingDef td)
            {
                ApplyDefStats(td);
                ApplyV(nameof(pathCost), (int v) => { td.pathCost = v; });
            }
        }

        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    private sealed class Probe
    {
        public required SingleResetProbeData Data { get; init; }
        public required StatColumnConfig Pct { get; init; }
        public required StatColumnConfig PathCost { get; init; }
        public required ThingDef Thing { get; init; }
    }

    private static StatDef EnsureStat()
    {
        var existing = DefDatabase<StatDef>.GetNamedSilentFail(ProbeStatName);
        if (existing != null) return existing;
        var stat = new StatDef { defName = ProbeStatName, label = "probe single pct" };
        DefDatabase<StatDef>.Add(stat);
        return stat;
    }

    /// <param name="initialPct">def 上那条 stat 的初始值；null 表示 def 里本来没有这一项。</param>
    private static Probe Arrange(float? initialPct)
    {
        var stat = EnsureStat();
        var bases = new List<StatModifier>();
        if (initialPct.HasValue) bases.Add(new StatModifier { stat = stat, value = initialPct.Value });

        var thing = new ThingDef
        {
            defName = "__BT_ProbeSingleThing",
            label = "probe thing",
            statBases = bases,
            pathCost = 0,
        };

        var data = new SingleResetProbeData
        {
            id = new TweakData.TweakID("__BT_ProbeSingleThing", SettingType.Apparel),
            def = thing,
            pct = initialPct,
            pathCost = 0,
            statBases = bases.ToList(),
        };
        var cols = data.AllColumn;
        return new Probe
        {
            Data = data,
            Pct = cols.Single(c => c.fieldName == nameof(SingleResetProbeData.pct)),
            PathCost = cols.Single(c => c.fieldName == nameof(SingleResetProbeData.pathCost)),
            Thing = thing,
        };
    }

    private static void Cleanup(SingleResetProbeData data) => tweakDatas.Remove(data.id);

    private static float? ProbeStatValue(ThingDef thing)
        => thing.statBases?.Find(s => s.stat?.defName == ProbeStatName)?.value;

    [Fact]
    public void 重置非stat字段_同行还有别的字段被改时也要落盘()
    {
        var p = Arrange(initialPct: null);
        try
        {
            // 两个字段都改：pct 是 stat 字段，pathCost 是非 stat 字段
            p.Pct.CheckAndApply(p.Data, "150");
            p.PathCost.ApplyValue(p.Data, 42);
            Assert.Equal(42, p.Thing.pathCost);
            Assert.Equal(1.5f, ProbeStatValue(p.Thing)!.Value, 5);

            // 只重置 pathCost —— 因为 pct 仍是"已修改"，走不到 RemoveData 那条兜底
            p.PathCost.ResetTo(p.Data, 0);

            Assert.Equal(0, p.Data.pathCost);
            Assert.Equal(0, p.Thing.pathCost); // ← 修复前停在 42：界面重置了，Def 没动
            Assert.Equal(FieldState.Unset, p.Data.StateOfField(nameof(SingleResetProbeData.pathCost)));

            // 别的字段不受牵连
            Assert.Equal(1.5f, ProbeStatValue(p.Thing)!.Value, 5);
            Assert.Equal(FieldState.Set, p.Data.StateOfField(nameof(SingleResetProbeData.pct)));
        }
        finally { Cleanup(p.Data); }
    }

    [Fact]
    public void 重置stat字段_同行非stat字段的修改不能被抹掉()
    {
        var p = Arrange(initialPct: null);
        try
        {
            p.Pct.CheckAndApply(p.Data, "150");
            p.PathCost.ApplyValue(p.Data, 42);

            p.Pct.ResetTo(p.Data, null);

            Assert.Null(p.Data.pct);
            Assert.False(p.Thing.statBases?.Any(s => s.stat?.defName == ProbeStatName) == true,
                "重置后 def.statBases 不应残留该 stat");

            // 未重置的字段必须保持"已修改"并留在 def 上
            Assert.Equal(42, p.Thing.pathCost);
            Assert.Equal(42, p.Data.pathCost);
            Assert.Equal(FieldState.Set, p.Data.StateOfField(nameof(SingleResetProbeData.pathCost)));
        }
        finally { Cleanup(p.Data); }
    }
}
