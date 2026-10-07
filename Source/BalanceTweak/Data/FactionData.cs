using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(FactionDef), SettingType.Faction)]
class FactionData : TweakData<FactionData>
{
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? raidCommonalityFromPointsCurve = null;

    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? maxPawnCostPerTotalPointsCurve = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? settlementGenerationWeight = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? naturalEnemy = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? permanentEnemy = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is FactionDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description;
            uiIcon = d.FactionIcon;
            uiIconColor = d.DefaultColor;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            raidCommonalityFromPointsCurve ??= d.raidCommonalityFromPointsCurve;
            maxPawnCostPerTotalPointsCurve ??= d.maxPawnCostPerTotalPointsCurve;
            techLevel ??= d.techLevel;
            settlementGenerationWeight ??= d.settlementGenerationWeight;
            naturalEnemy ??= d.naturalEnemy;
            permanentEnemy ??= d.permanentEnemy;
        }
    }

    public override void Apply()
    {
        if (this.def is not FactionDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (raidCommonalityFromPointsCurve != null) { def.raidCommonalityFromPointsCurve = raidCommonalityFromPointsCurve; }
        if (maxPawnCostPerTotalPointsCurve != null) { def.maxPawnCostPerTotalPointsCurve = maxPawnCostPerTotalPointsCurve; }
        if (techLevel.HasValue) { def.techLevel = techLevel.Value; }
        if (settlementGenerationWeight.HasValue) { def.settlementGenerationWeight = settlementGenerationWeight.Value; }
        if (naturalEnemy.HasValue) { def.naturalEnemy = naturalEnemy.Value; }
        if (permanentEnemy.HasValue) { def.permanentEnemy = permanentEnemy.Value; }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(FactionType).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (this.def is FactionDef def)
        {
            if (def.hidden)
            {
                return (int)FactionType.Hidden;
            }
        }
        return (int)FactionType.Normal;
    }

    private enum FactionType
    {
        Normal,
        Hidden
    }
}
