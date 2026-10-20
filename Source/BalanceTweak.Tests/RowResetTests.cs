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
/// 行尾 <c>R</c> 按钮（<c>TweakUtility.RemoveData</c>）的语义：整行回滚到原始数据。
///
/// <para>
/// 它与单元格重置（<c>StatColumnConfig.ResetTo</c>）走的是完全不同的代码路径：
/// 不调用 <c>ResetTo</c>，而是拿原始数据的一份克隆替换掉当前行，然后
/// <c>forceResetStats = true</c> + <c>Apply()</c>。字段值是原值（stat 缺席时就是 null），
/// 但没有 <c>forceResetField</c>。
/// </para>
///
/// <para>
/// 因此它会撞上 <c>ApplyDefStats</c> Phase 2 的 null 守卫 —— 除非 Phase 1 已经把整个
/// stat 列表按原值重写回 def。这里把三种情形都钉住：原值缺席（stat 不在列表里）、
/// 原值存在、以及装备加成（<c>equippedStatOffsets</c>）那一支。
/// </para>
/// </summary>
[Collection(DefDatabaseProbeCollection.Name)]
public class RowResetTests
{
    private const string ProbeStatName = "__BT_ProbeRowPct";
    private const string ProbeEqStatName = "__BT_ProbeRowEq";

    private class RowResetProbeData : TweakData<RowResetProbeData>
    {
        [TweakField(StatDef = ProbeStatName, Style = ColumnStyle.Prec)]
        public float? pct = null;

        [TweakField(StatDef = ProbeEqStatName, IsEquippedStat = true, Style = ColumnStyle.Prec)]
        public float? equippedPct = null;

        [TweakField(Style = ColumnStyle.StatModList)]
        public List<StatModifier>? statBases = null;

        [TweakField(Style = ColumnStyle.StatModList)]
        public List<StatModifier>? equippedStatOffsets = null;

        /// <summary>非 stat 字段，写法与生产数据类一致（<c>TerrainData.Apply</c> 的 <c>pathCost</c>）。</summary>
        [TweakField(Style = ColumnStyle.Int)]
        public int? pathCost = null;

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
        public required RowResetProbeData Data { get; init; }
        public required StatColumnConfig Pct { get; init; }
        public required StatColumnConfig EquippedPct { get; init; }
        public required StatColumnConfig PathCost { get; init; }
        public required ThingDef Thing { get; init; }
    }

    private static StatDef EnsureStat(string name)
    {
        var existing = DefDatabase<StatDef>.GetNamedSilentFail(name);
        if (existing != null) return existing;
        var stat = new StatDef { defName = name, label = name };
        DefDatabase<StatDef>.Add(stat);
        return stat;
    }

    private static Probe Arrange(float? initialPct, float? initialEquippedPct)
    {
        var stat = EnsureStat(ProbeStatName);
        var eqStat = EnsureStat(ProbeEqStatName);

        var bases = new List<StatModifier>();
        if (initialPct.HasValue) bases.Add(new StatModifier { stat = stat, value = initialPct.Value });
        var equipped = new List<StatModifier>();
        if (initialEquippedPct.HasValue) equipped.Add(new StatModifier { stat = eqStat, value = initialEquippedPct.Value });

        var thing = new ThingDef
        {
            defName = "__BT_ProbeRowThing",
            label = "probe thing",
            statBases = bases,
            equippedStatOffsets = equipped,
            pathCost = 0,
        };

        var data = new RowResetProbeData
        {
            id = new TweakData.TweakID("__BT_ProbeRowThing", SettingType.Apparel),
            def = thing,
            pct = initialPct,
            equippedPct = initialEquippedPct,
            statBases = bases.ToList(),
            equippedStatOffsets = equipped.ToList(),
            pathCost = 0,
        };
        var cols = data.AllColumn;
        return new Probe
        {
            Data = data,
            Pct = cols.Single(c => c.fieldName == nameof(RowResetProbeData.pct)),
            EquippedPct = cols.Single(c => c.fieldName == nameof(RowResetProbeData.equippedPct)),
            PathCost = cols.Single(c => c.fieldName == nameof(RowResetProbeData.pathCost)),
            Thing = thing,
        };
    }

    /// <summary>
    /// 复刻 <c>TweakUtility.RemoveData</c> 的核心：拿原始数据克隆、整套写回。
    /// （RemoveData 里的缓存替换与本测试无关，略去。）
    /// </summary>
    private static RowResetProbeData EmulateRowReset(RowResetProbeData original)
    {
        var newdata = (RowResetProbeData)original.Clone();
        newdata.ApplyAuthoritative();
        return newdata;
    }

    private static void Cleanup(RowResetProbeData data) => tweakDatas.Remove(data.id);

    private static bool HasStat(IEnumerable<StatModifier>? list, string name)
        => list?.Any(s => s.stat?.defName == name) == true;

    private static float? ValueOf(IEnumerable<StatModifier>? list, string name)
        => list?.FirstOrDefault(s => s.stat?.defName == name)?.value;

    [Fact]
    public void 原值缺席的stat_整行重置后应从def移除()
    {
        var p = Arrange(initialPct: null, initialEquippedPct: null);
        try
        {
            var original = (RowResetProbeData)p.Data.Clone();

            p.Pct.CheckAndApply(p.Data, "150");
            Assert.Equal(1.5f, ValueOf(p.Thing.statBases, ProbeStatName)!.Value, 5);

            var restored = EmulateRowReset(original);

            Assert.False(HasStat(p.Thing.statBases, ProbeStatName), "整行重置后 def.statBases 不应残留该 stat");
            Assert.Null(restored.pct);
        }
        finally { Cleanup(p.Data); }
    }

    [Fact]
    public void 原值存在的stat_整行重置后应恢复原值()
    {
        var p = Arrange(initialPct: 0.5f, initialEquippedPct: null);
        try
        {
            var original = (RowResetProbeData)p.Data.Clone();

            p.Pct.CheckAndApply(p.Data, "150");
            Assert.Equal(1.5f, ValueOf(p.Thing.statBases, ProbeStatName)!.Value, 5);

            var restored = EmulateRowReset(original);

            Assert.Equal(0.5f, ValueOf(p.Thing.statBases, ProbeStatName)!.Value, 5);
            Assert.Equal(0.5f, restored.pct!.Value, 5);
        }
        finally { Cleanup(p.Data); }
    }

    [Fact]
    public void 原值缺席的装备stat_整行重置后应从def移除()
    {
        var p = Arrange(initialPct: null, initialEquippedPct: null);
        try
        {
            var original = (RowResetProbeData)p.Data.Clone();

            p.EquippedPct.CheckAndApply(p.Data, "120");
            Assert.Equal(1.2f, ValueOf(p.Thing.equippedStatOffsets, ProbeEqStatName)!.Value, 5);

            var restored = EmulateRowReset(original);

            Assert.False(HasStat(p.Thing.equippedStatOffsets, ProbeEqStatName),
                "整行重置后 def.equippedStatOffsets 不应残留该 stat");
            Assert.Null(restored.equippedPct);
        }
        finally { Cleanup(p.Data); }
    }

    /// <summary>
    /// 非 stat 字段由 <c>ApplyV</c>/<c>ApplyN</c>/<c>ApplyL</c> 按三态写入，而整行重置换上的克隆体
    /// 一个修改标记都没有（全 Unset）⇒ 这些字段<b>一个都不会写回 def</b>。
    /// 界面上因为整行数据被换成了原始克隆，看起来像"重置成功"，实际 Def 还是改过的值。
    /// </summary>
    [Fact]
    public void 非stat字段_整行重置后应写回def()
    {
        var p = Arrange(initialPct: null, initialEquippedPct: null);
        try
        {
            var original = (RowResetProbeData)p.Data.Clone();

            p.PathCost.ApplyValue(p.Data, 42);
            Assert.Equal(42, p.Thing.pathCost);
            Assert.Equal(42, p.Data.pathCost);

            var restored = EmulateRowReset(original);

            // 行数据显示已回滚（这就是"看起来重置了"的来源）
            Assert.Equal(0, restored.pathCost);
            // 但 Def 必须同步回滚，否则游戏里数值没变
            Assert.Equal(0, p.Thing.pathCost);
        }
        finally { Cleanup(p.Data); }
    }
}
