using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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
            // FactionIcon 首次访问会 ContentFinder 加载纹理，延迟到真正显示时再取。
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
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<SimpleCurve>(nameof(raidCommonalityFromPointsCurve), v => def.raidCommonalityFromPointsCurve = v);
        ApplyV<SimpleCurve>(nameof(maxPawnCostPerTotalPointsCurve), v => def.maxPawnCostPerTotalPointsCurve = v);
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);
        ApplyV<float>(nameof(settlementGenerationWeight), v => def.settlementGenerationWeight = v);
        ApplyV<bool>(nameof(naturalEnemy), v => def.naturalEnemy = v);
        ApplyV<bool>(nameof(permanentEnemy), v => def.permanentEnemy = v);
    }

    protected override Texture2D? ResolveUiIcon() => (def as FactionDef)?.FactionIcon;

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
