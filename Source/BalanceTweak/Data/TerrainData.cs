using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(TerrainDef), SettingType.Terrain)]
class TerrainData : TweakData<TerrainData>
{
    [TweakField(StatDef = "Beauty", Style = ColumnStyle.Int)]
    public float? beauty = null;
    [TweakField(StatDef = "WorkToBuild", Style = ColumnStyle.Int)]
    public float? workToBuild = null;
    [TweakField(StatDef = "CleaningTimeFactor", Style = ColumnStyle.Prec)]
    public float? cleaningTimeFactor = null;
    [TweakField(StatDef = "FilthMultiplier", Style = ColumnStyle.Prec)]
    public float? filthMultiplier = null;

    [TweakField(Style = ColumnStyle.Int)]
    public int? pathCost = null;
    [TweakField]
    public float? fertility = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? extraDeteriorationFactor = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? toxicBuildupFactor = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? changeable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? holdSnowOrSand = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? layerable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canEverTerraform = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? extinguishesFire = null;
    [TweakField]
    public float? destroyOnBombDamageThreshold = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? tags = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TerrainAffordanceDef>? affordances = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectDef>? researchPrerequisites = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList)]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<StuffCategoryDef>? stuffCategories = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is TerrainDef d)
        {
            pathCost ??= d.pathCost;
            destroyOnBombDamageThreshold ??= d.destroyOnBombDamageThreshold;
            fertility ??= d.fertility;
            extraDeteriorationFactor ??= d.extraDeteriorationFactor;
            toxicBuildupFactor ??= d.toxicBuildupFactor;
            changeable ??= d.changeable;
            holdSnowOrSand ??= d.holdSnowOrSand;
            layerable ??= d.layerable;
            canEverTerraform ??= d.canEverTerraform;
            extinguishesFire ??= d.extinguishesFire;
            statBases ??= d.statBases;
            tags ??= d.tags;
            affordances ??= d.affordances;
            researchPrerequisites ??= d.researchPrerequisites;
            costList ??= d.costList;
            stuffCategories ??= d.stuffCategories;
        }
    }

    public override void Apply()
    {
        if (this.def is not TerrainDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefStats(def);
        if (pathCost.HasValue) def.pathCost = pathCost.Value;
        if (destroyOnBombDamageThreshold.HasValue) def.destroyOnBombDamageThreshold = destroyOnBombDamageThreshold.Value;
        if (fertility.HasValue) def.fertility = fertility.Value;
        if (extraDeteriorationFactor.HasValue) def.extraDeteriorationFactor = extraDeteriorationFactor.Value;
        if (toxicBuildupFactor.HasValue) def.toxicBuildupFactor = toxicBuildupFactor.Value;
        if (changeable.HasValue) def.changeable = changeable.Value;
        if (holdSnowOrSand.HasValue) def.holdSnowOrSand = holdSnowOrSand.Value;
        if (layerable.HasValue) def.layerable = layerable.Value;
        if (canEverTerraform.HasValue) def.canEverTerraform = canEverTerraform.Value;
        if (extinguishesFire.HasValue) def.extinguishesFire = extinguishesFire.Value;

        if (tags != null) def.tags = tags;
        if (affordances != null) def.affordances = affordances;
        if (researchPrerequisites != null) def.researchPrerequisites = researchPrerequisites;
        if (costList != null) def.costList = costList;
        if (stuffCategories != null) def.stuffCategories = stuffCategories;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(TerrainCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum TerrainCategory
    {
        Foundation,
        Water,
        SandSoil,
        Stone,
        Special,
        Floor,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is TerrainDef def)
        {
            //if (def.bridge) return (int)TerrainCategory.Bridge;
            if (def.isFoundation || def.bridge) return (int)TerrainCategory.Foundation;
            if (def.HasTag("Water")) return (int)TerrainCategory.Water;
            if (def.categoryType == TerrainDef.TerrainCategoryType.Sand || def.categoryType == TerrainDef.TerrainCategoryType.Soil) return (int)TerrainCategory.SandSoil;
            if (def.categoryType == TerrainDef.TerrainCategoryType.Stone) return (int)TerrainCategory.Stone;
            if (def.natural) return (int)TerrainCategory.Special;
            if (def.layerable) return (int)TerrainCategory.Floor;
        }
        return (int)TerrainCategory.Other;
    }
}