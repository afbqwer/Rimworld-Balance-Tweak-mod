using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(ThingDef), SettingType.Stuff, Priority = 300, MatchMethod = nameof(DataUtility.MatchStuff))]
class StuffData : TweakData<StuffData>
{
    // 字段定义
    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;

    [TweakField(StatDef = "StuffPower_Armor_Sharp", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorSharp = null;
    [TweakField(StatDef = "StuffPower_Armor_Blunt", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorBlunt = null;
    [TweakField(StatDef = "StuffPower_Armor_Heat", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorHeat = null;
    [TweakField(StatDef = "StuffPower_Insulation_Cold", Style = ColumnStyle.Float)]
    public float? stuffPowerInsulationCold = null;
    [TweakField(StatDef = "StuffPower_Insulation_Heat", Style = ColumnStyle.Float)]
    public float? stuffPowerInsulationHeat = null;
    [TweakField(StatDef = "SharpDamageMultiplier", Style = ColumnStyle.Prec)]
    public float? sharpDamageMultiplier = null;
    [TweakField(StatDef = "BluntDamageMultiplier", Style = ColumnStyle.Prec)]
    public float? bluntDamageMultiplier = null;
    [TweakField(StatDef = "MeleeWeapon_CooldownMultiplier", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffMeleeWeaponCooldownMultiplier = null;
    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffMaxHitPoints = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffFlammability = null;
    [TweakField(StatDef = "Beauty", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffBeauty = null;
    [TweakField(StatDef = "WorkToMake", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffWorkToMake = null;
    [TweakField(StatDef = "WorkToBuild", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffWorkToBuild = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? deepCommonality = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? deepCountPerPortion = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? deepCountPerCell = null;
    [TweakField(Style = ColumnStyle.Range)]
    public IntRange? deepLumpSizeRange = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 100;



    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            deepCommonality ??= d.deepCommonality;
            deepCountPerPortion ??= d.deepCountPerPortion;
            deepCountPerCell ??= d.deepCountPerCell;
            deepLumpSizeRange ??= d.deepLumpSizeRange;
            statBases ??= d.statBases;
            statOffsets ??= d.stuffProps?.statOffsets;
            statFactors ??= d.stuffProps?.statFactors;
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
        if (deepCommonality.HasValue) { def.deepCommonality = deepCommonality.Value; }
        if (deepCountPerPortion.HasValue) { def.deepCountPerPortion = deepCountPerPortion.Value; }
        if (deepCountPerCell.HasValue) { def.deepCountPerCell = deepCountPerCell.Value; }
        if (deepLumpSizeRange.HasValue) { def.deepLumpSizeRange = deepLumpSizeRange.Value; }
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(StuffType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum StuffType
    {
        Metallic,
        Woody,
        Stony,
        Fabric,
        Leathery,
        Other
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.stuffProps == null)
            {
                return (int)StuffType.Other;
            }
            if (!def.stuffProps.categories.NullOrEmpty())
            {
                var f = def.stuffProps.categories.FirstOrDefault();
                if (f == StuffCategoryDefOf.Metallic) { return (int)StuffType.Metallic; }
                if (f == StuffCategoryDefOf.Woody) { return (int)StuffType.Woody; }
                if (f == StuffCategoryDefOf.Stony) { return (int)StuffType.Stony; }
                if (f == StuffCategoryDefOf.Fabric) { return (int)StuffType.Fabric; }
                if (f == StuffCategoryDefOf.Leathery) { return (int)StuffType.Leathery; }
            }
        }
        return (int)StuffType.Other;
    }
}


