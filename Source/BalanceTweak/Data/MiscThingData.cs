using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


/// <summary>
/// ThingDef 的兜底类型。
///
/// 用于收容未被其它分类规则命中、且仍有编辑价值的 ThingDef（Item 与 Filth）。
/// 此前 TweakDatabase.Init() 的 if/else-if 链没有 else 兜底分支，未命中即被静默丢弃；
/// 改造后先做过全量兜底，实测绝大多数兜底对象（Gas、植物残骸等杂项）没有编辑价值，
/// 只会白白拖慢初始化，因此 2026-09 收窄为 MatchEligible：非 Item/Filth 的 ThingDef
/// 不再创建实例（在日志里计入 Uncovered，不进 UI）。
///
/// 字段只保留最通用的几项；更细的区分靠 GetPropType() 的子筛选，避免为每个小类
/// 新增一次 SettingType 枚举值。
/// </summary>
[TweakFor(typeof(ThingDef), SettingType.Misc, IsFallback = true, MatchMethod = nameof(MatchEligible))]
class MiscThingData : TweakData<MiscThingData>
{
    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? stackLimit = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? pathCost = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? useHitPoints = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? destroyable = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is ThingDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            stackLimit ??= d.stackLimit;
            statBases ??= d.statBases;
            pathCost ??= d.pathCost;
            useHitPoints ??= d.useHitPoints;
            destroyable ??= d.destroyable;
        }
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        ApplyDefStats(def);
        if (stackLimit.HasValue) { def.stackLimit = stackLimit.Value; }
        if (pathCost.HasValue) { def.pathCost = pathCost.Value; }
        if (useHitPoints.HasValue) { def.useHitPoints = useHitPoints.Value; }
        if (destroyable.HasValue) { def.destroyable = destroyable.Value; }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(MiscCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum MiscCategory
    {
        Item,
        Filth,
    }

    /// <summary>
    /// 兜底规则的收容范围：只有 Item 与 Filth 值得编辑。
    /// 其余 ThingDef（Gas、植物残骸等杂项）既没有编辑价值，又明显拖慢初始化，直接放弃。
    /// </summary>
    private static bool MatchEligible(Def def)
        => def is ThingDef t && (t.category == ThingCategory.Item || t.category == ThingCategory.Filth);

    public override int GetPropType()
    {
        // MatchEligible 已保证只有 Item/Filth 会被创建；末分支仅作防御，归入下拉框首项。
        return this.def is ThingDef d && d.category == ThingCategory.Filth
            ? (int)MiscCategory.Filth
            : (int)MiscCategory.Item;
    }
}
