using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(PawnsArrivalModeDef), SettingType.ArrivalMode)]
class PawnsArrivalModeData : TweakData<PawnsArrivalModeData>
{
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? selectionWeightCurve = null;
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? pointsFactorCurve = null;

    // 复合列表：每个元素 = 派系 + 该派系按点数取该抵达方式的选择权重曲线。
    // FactionCurve.selectionWeightPerPointsCurve 是 private 字段，编解码由 PocoCodec 的 Kind.Curve 处理。
    [TweakField(Style = ColumnStyle.FactionCurveList)]
    public List<FactionCurve>? selectionWeightCurvesPerFaction = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? minTechLevel = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? forQuickMilitaryAid = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? walkIn = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canBeBackup = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? minSpaceSelectionWeight = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<BiomeDef>? biomeWhitelist = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<BiomeDef>? biomeBlacklist = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<PlanetLayerDef>? layerWhitelist = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<PlanetLayerDef>? layerBlacklist = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? textEnemy = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? textFriendly = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? textWillArrive = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (def is PawnsArrivalModeDef d)
        {
            selectionWeightCurve ??= d.selectionWeightCurve;
            pointsFactorCurve ??= d.pointsFactorCurve;
            if (!IsFieldModified(nameof(selectionWeightCurvesPerFaction)))
                selectionWeightCurvesPerFaction ??= d.selectionWeightCurvesPerFaction?
                    .Select(CloneFactionCurve)
                    .OfType<FactionCurve>()
                    .ToList();
            minTechLevel ??= d.minTechLevel;
            forQuickMilitaryAid ??= d.forQuickMilitaryAid;
            walkIn ??= d.walkIn;
            canBeBackup ??= d.canBeBackup;
            minSpaceSelectionWeight ??= d.minSpaceSelectionWeight;
            if (!IsFieldModified(nameof(biomeWhitelist))) biomeWhitelist ??= d.biomeWhitelist;
            if (!IsFieldModified(nameof(biomeBlacklist))) biomeBlacklist ??= d.biomeBlacklist;
            if (!IsFieldModified(nameof(layerWhitelist))) layerWhitelist ??= d.layerWhitelist;
            if (!IsFieldModified(nameof(layerBlacklist))) layerBlacklist ??= d.layerBlacklist;
            textEnemy ??= d.textEnemy;
            textFriendly ??= d.textFriendly;
            textWillArrive ??= d.textWillArrive;
        }
    }

    public override void Apply()
    {
        if (this.def is not PawnsArrivalModeDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyV<SimpleCurve>(nameof(selectionWeightCurve), v => def.selectionWeightCurve = v);
        ApplyV<SimpleCurve>(nameof(pointsFactorCurve), v => def.pointsFactorCurve = v);
        ApplyL<FactionCurve>(nameof(selectionWeightCurvesPerFaction), v => def.selectionWeightCurvesPerFaction = v);
        ApplyV<TechLevel>(nameof(minTechLevel), v => def.minTechLevel = v);
        ApplyV<bool>(nameof(forQuickMilitaryAid), v => def.forQuickMilitaryAid = v);
        ApplyV<bool>(nameof(walkIn), v => def.walkIn = v);
        ApplyV<bool>(nameof(canBeBackup), v => def.canBeBackup = v);
        ApplyV<float>(nameof(minSpaceSelectionWeight), v => def.minSpaceSelectionWeight = v);
        ApplyL<BiomeDef>(nameof(biomeWhitelist), v => def.biomeWhitelist = v);
        ApplyL<BiomeDef>(nameof(biomeBlacklist), v => def.biomeBlacklist = v);
        ApplyL<PlanetLayerDef>(nameof(layerWhitelist), v => def.layerWhitelist = v);
        ApplyL<PlanetLayerDef>(nameof(layerBlacklist), v => def.layerBlacklist = v);
        ApplyS(nameof(textEnemy), v => def.textEnemy = v);
        ApplyS(nameof(textFriendly), v => def.textFriendly = v);
        ApplyS(nameof(textWillArrive), v => def.textWillArrive = v);
    }

    /// <summary>
    /// 深拷贝一条 FactionCurve（faction 单例共享、曲线换成新实例）。
    /// FactionCurve.selectionWeightPerPointsCurve 是 private 字段，克隆按 PocoCodec 字段表走
    /// （faction / selectionWeightPerPointsCurve，Kind.Curve 的深拷贝由 PocoCodec 负责），
    /// 编辑器窗口的条目克隆复用这里，保证"字段与 Def 的活对象永不共享实例"。
    /// </summary>
    internal static FactionCurve? CloneFactionCurve(FactionCurve source)
    {
        TweakData.FieldCodecs.EnsureBuilt();
        return PocoCodec.CloneElement(source, typeof(FactionCurve)) as FactionCurve;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ArrivalCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    // 分类键：MST.Other 复用 Core.xml 的共享定义（与 BiomeCategory 同款），此处只新增 MST.WalkIn
    private enum ArrivalCategory
    {
        Other,
        WalkIn,
    }

    public override int GetPropType()
    {
        if (this.def is PawnsArrivalModeDef def && def.walkIn)
            return (int)ArrivalCategory.WalkIn;
        return (int)ArrivalCategory.Other;
    }
}
