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
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(tags))) tags ??= d.tags;
            if (!IsFieldModified(nameof(affordances))) affordances ??= d.affordances;
            if (!IsFieldModified(nameof(researchPrerequisites))) researchPrerequisites ??= d.researchPrerequisites;
            if (!IsFieldModified(nameof(costList))) costList ??= d.costList;
            if (!IsFieldModified(nameof(stuffCategories))) stuffCategories ??= d.stuffCategories;
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
        ApplyV<int>(nameof(pathCost), v => def.pathCost = v);
        ApplyV<float>(nameof(destroyOnBombDamageThreshold), v => def.destroyOnBombDamageThreshold = v);
        ApplyV<float>(nameof(fertility), v => def.fertility = v);
        ApplyV<float>(nameof(extraDeteriorationFactor), v => def.extraDeteriorationFactor = v);
        ApplyV<float>(nameof(toxicBuildupFactor), v => def.toxicBuildupFactor = v);
        ApplyV<bool>(nameof(changeable), v => def.changeable = v);
        ApplyV<bool>(nameof(holdSnowOrSand), v => def.holdSnowOrSand = v);
        ApplyV<bool>(nameof(layerable), v => def.layerable = v);
        ApplyV<bool>(nameof(canEverTerraform), v => def.canEverTerraform = v);
        ApplyV<bool>(nameof(extinguishesFire), v => def.extinguishesFire = v);

        ApplyL<string>(nameof(tags), v => def.tags = v);
        ApplyL<TerrainAffordanceDef>(nameof(affordances), v => def.affordances = v);
        ApplyL<ResearchProjectDef>(nameof(researchPrerequisites), v => def.researchPrerequisites = v);
        ApplyL<ThingDefCountClass>(nameof(costList), v => def.costList = v);
        ApplyL<StuffCategoryDef>(nameof(stuffCategories), v => def.stuffCategories = v);
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