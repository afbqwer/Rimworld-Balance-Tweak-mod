using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

/// <summary>
/// 袭击策略（<see cref="RaidStrategyDef"/>）。
///
/// <para>
/// 这一层决定"袭击以什么形式打过来"，以及点数进入 <see cref="PawnGroupMakerUtility"/> 之前被乘哪些系数：
/// <c>pointsFactorCurve</c> 直接缩放总点数，<c>minPawns</c> 通过 <c>totalPoints / minPawns</c>
/// 压低单个兵种的单价上限，<c>selectionWeightPerPointsCurve</c> 决定该策略在各点数档位的被选概率。
/// </para>
///
/// <para>
/// <b>未纳入 <c>workerClass</c></b>：它是 <see cref="System.Type"/>，没有对应的 <see cref="ColumnStyle"/>，
/// 且改 worker 等于换掉整段生成逻辑（<c>SpawnThreats</c> / <c>CanUseWith</c> 全部来自 worker），
/// 改错只会让袭击生成期抛异常，不属于"平衡调整"。同理 <c>letterLabel*</c> / <c>arrivalText*</c>
/// 是 <c>[MustTranslate]</c> 文案，也不在本页签开放。
/// </para>
/// </summary>
[TweakFor(typeof(RaidStrategyDef), SettingType.RaidStrategy)]
class RaidStrategyData : TweakData<RaidStrategyData>
{
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? selectionWeightPerPointsCurve = null;

    [TweakField()]
    public float? minPawns = null;

    // 复合列表：每个元素 = 派系 + 该派系按点数取该策略的选择权重曲线。
    // FactionCurve.selectionWeightPerPointsCurve 是 private 字段，编解码由 PocoCodec 的 Kind.Curve 处理；
    // 克隆复用 PawnsArrivalModeData.CloneFactionCurve，两处是同一个类型。
    [TweakField(Style = ColumnStyle.FactionCurveList)]
    public List<FactionCurve>? selectionWeightCurvesPerFaction = null;

    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? pointsFactorCurve = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? pawnsCanBringFood = null;

    [TweakField()]
    public float? raidLootValueFactor = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<PawnsArrivalModeDef>? arriveModes = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<PlanetLayerDef>? layerWhitelist = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<PlanetLayerDef>? layerBlacklist = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (def is RaidStrategyDef d)
        {
            selectionWeightPerPointsCurve ??= d.selectionWeightPerPointsCurve;
            minPawns ??= d.minPawns;
            if (!IsFieldModified(nameof(selectionWeightCurvesPerFaction)))
                selectionWeightCurvesPerFaction ??= d.selectionWeightCurvesPerFaction?
                    .Select(PawnsArrivalModeData.CloneFactionCurve)
                    .OfType<FactionCurve>()
                    .ToList();
            pointsFactorCurve ??= d.pointsFactorCurve;
            pawnsCanBringFood ??= d.pawnsCanBringFood;
            raidLootValueFactor ??= d.raidLootValueFactor;
            if (!IsFieldModified(nameof(arriveModes))) arriveModes ??= d.arriveModes;
            if (!IsFieldModified(nameof(layerWhitelist))) layerWhitelist ??= d.layerWhitelist;
            if (!IsFieldModified(nameof(layerBlacklist))) layerBlacklist ??= d.layerBlacklist;
        }
    }

    public override void Apply()
    {
        if (this.def is not RaidStrategyDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        // 这几个字段只被 raid 生成链路在"每次生成时现读"，worker 侧没有对它们做缓存，
        // 所以写完不需要额外刷新（对比 BiomeData 的懒加载缓存）。
        ApplyV<SimpleCurve>(nameof(selectionWeightPerPointsCurve), v => def.selectionWeightPerPointsCurve = v);
        ApplyV<float>(nameof(minPawns), v => def.minPawns = v);
        ApplyL<FactionCurve>(nameof(selectionWeightCurvesPerFaction), v => def.selectionWeightCurvesPerFaction = v);
        ApplyV<SimpleCurve>(nameof(pointsFactorCurve), v => def.pointsFactorCurve = v);
        ApplyV<bool>(nameof(pawnsCanBringFood), v => def.pawnsCanBringFood = v);
        ApplyV<float>(nameof(raidLootValueFactor), v => def.raidLootValueFactor = v);
        ApplyL<PawnsArrivalModeDef>(nameof(arriveModes), v => def.arriveModes = v);
        ApplyL<PlanetLayerDef>(nameof(layerWhitelist), v => def.layerWhitelist = v);
        ApplyL<PlanetLayerDef>(nameof(layerBlacklist), v => def.layerBlacklist = v);
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(RaidStrategyCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    // 分组按 worker 的运行时类型判断（全名比较，不走 typeof —— 子类可能来自其它 Mod）。
    // 顺序即判定优先级：Siege 与 Breaching 都不是 ImmediateAttack 的子类，但 PsychicRitualSiege
    // 与 Sappers 直接继承 WithRequiredPawnKinds，所以必须显式列出。
    // MST.Other 复用 Core.xml 的共享定义，此处只新增另外三个。
    private enum RaidStrategyCategory
    {
        Immediate,
        Breaching,
        Siege,
        Other,
    }

    private const string WorkerImmediate = "RimWorld.RaidStrategyWorker_ImmediateAttack";
    private const string WorkerImmediateSmart = "RimWorld.RaidStrategyWorker_ImmediateAttackSmart";
    private const string WorkerBreaching = "RimWorld.RaidStrategyWorker_ImmediateAttackBreaching";
    private const string WorkerSappers = "RimWorld.RaidStrategyWorker_ImmediateAttackSappers";
    private const string WorkerSiege = "RimWorld.RaidStrategyWorker_Siege";
    private const string WorkerPsychicSiege = "RimWorld.RaidStrategyWorker_PsychicRitualSiege";

    public override int GetPropType()
    {
        if (this.def is not RaidStrategyDef def) return (int)RaidStrategyCategory.Other;
        var worker = def.workerClass;
        if (DataUtility.IsTypeOrSubclassOfName(worker, WorkerSiege) ||
            DataUtility.IsTypeOrSubclassOfName(worker, WorkerPsychicSiege))
            return (int)RaidStrategyCategory.Siege;
        if (DataUtility.IsTypeOrSubclassOfName(worker, WorkerBreaching) ||
            DataUtility.IsTypeOrSubclassOfName(worker, WorkerSappers))
            return (int)RaidStrategyCategory.Breaching;
        if (DataUtility.IsTypeOrSubclassOfName(worker, WorkerImmediate) ||
            DataUtility.IsTypeOrSubclassOfName(worker, WorkerImmediateSmart))
            return (int)RaidStrategyCategory.Immediate;
        return (int)RaidStrategyCategory.Other;
    }
}
