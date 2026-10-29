using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Food, Priority = 100, MatchMethod = nameof(DataUtility.MatchFood))]
class FoodData : TweakData<FoodData>
{
    private static Dictionary<ThingDef, TweakID>? _harvestedByMap;


    [TweakField(StatDef = "Nutrition", Style = ColumnStyle.Float)]
    public float? nutrition = null;
    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;
    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Int)]
    public float? maxHitPoints = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec)]
    public float? flammability = null;
    [TweakField(StatDef = "MedicalPotency", Style = ColumnStyle.Prec)]
    public float? medicalPotency = null;
    [TweakField(StatDef = "MedicalQualityMax", Style = ColumnStyle.Prec)]
    public float? medicalQualityMax = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? baseIngestTicks = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? joy = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(FoodPreferability))]
    public FoodPreferability? preferability = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfPlant))]
    public TweakID? harvestedThingData = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfRaw))]
    public TweakID? harvestedByData = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonPlant))]
    public bool? canRot = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonPlant))]
    public bool? rotDestroys = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfNonPlant))]
    public float? daysToRotStart = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfNonPlant))]
    public float? rotDamagePerDay = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfNonPlant))]
    public float? daysToDessicated = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfNonPlant))]
    public float? dessicatedDamagePerDay = null;

    // ——— Plant fields ———
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? growDays = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? harvestYield = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? sowWork = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? harvestWork = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfPlant))]
    public int? sowMinSkill = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPlant))]
    public float? harvestMinGrowth = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? harvestAfterGrowth = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPlant))]
    public float? fertilityMin = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPlant))]
    public float? fertilitySensitivity = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? minGrowthTemperature = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? maxGrowthTemperature = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPlant))]
    public float? growMinGlow = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPlant))]
    public float? growOptimalGlow = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? lifespanDaysPerGrowDays = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? completelyIgnoreFertility = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? blockAdjacentSow = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? harvestFailable = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? autoHarvestable = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? neverBlightable = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfPlant))]
    public bool? dieIfNoSunlight = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfPlant))]
    public float? minSpacingBetweenSamePlant = null;
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfPlant))]
    public List<string>? sowTags = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfPlant))]
    public List<ResearchProjectDef>? sowResearchPrerequisites = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(Pollution), Available = nameof(AvailableIfPlant))]
    public Pollution? pollution = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? thingSetMakerTags = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

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
            defLabel ??= def?.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            if (d.ingestible!= null)
            {
            baseIngestTicks ??= d.ingestible.baseIngestTicks;
            joy ??= d.ingestible.joy;
            preferability ??= d.ingestible.preferability;
            }
            canRot = false;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(thingSetMakerTags))) thingSetMakerTags ??= d.thingSetMakerTags;
            foreach (var cp in d.comps)
            {
                if (cp is CompProperties_Rottable cr)
                {
                    canRot = true;
                    daysToRotStart ??= cr.daysToRotStart;
                    rotDestroys ??= cr.rotDestroys;
                    rotDamagePerDay ??= cr.rotDamagePerDay;
                    daysToDessicated ??= cr.daysToDessicated;
                    dessicatedDamagePerDay ??= cr.dessicatedDamagePerDay;
                }
            }
            if (d.plant != null)
            {
                growDays ??= d.plant.growDays;
                harvestYield ??= d.plant.harvestYield;
                sowWork ??= d.plant.sowWork;
                harvestWork ??= d.plant.harvestWork;
                sowMinSkill ??= d.plant.sowMinSkill;
                harvestMinGrowth ??= d.plant.harvestMinGrowth;
                harvestAfterGrowth ??= d.plant.harvestAfterGrowth;
                fertilityMin ??= d.plant.fertilityMin;
                fertilitySensitivity ??= d.plant.fertilitySensitivity;
                minGrowthTemperature ??= d.plant.minGrowthTemperature;
                maxGrowthTemperature ??= d.plant.maxGrowthTemperature;
                growMinGlow ??= d.plant.growMinGlow;
                growOptimalGlow ??= d.plant.growOptimalGlow;
                lifespanDaysPerGrowDays ??= d.plant.lifespanDaysPerGrowDays;
                completelyIgnoreFertility ??= d.plant.completelyIgnoreFertility;
                blockAdjacentSow ??= d.plant.blockAdjacentSow;
                harvestFailable ??= d.plant.harvestFailable;
                autoHarvestable ??= d.plant.autoHarvestable;
                neverBlightable ??= d.plant.neverBlightable;
                dieIfNoSunlight ??= d.plant.dieIfNoSunlight;
                minSpacingBetweenSamePlant ??= d.plant.minSpacingBetweenSamePlant;
                if (harvestedThingData == null && d.plant.harvestedThingDef != null)
                {
                    var harvestedData = TweakData.GetData(d.plant.harvestedThingDef, SettingType.Food);
                    if (harvestedData != null)
                        harvestedThingData = harvestedData.id;
                }
                if (!IsFieldModified(nameof(sowTags))) sowTags ??= d.plant.sowTags;
                if (!IsFieldModified(nameof(sowResearchPrerequisites))) sowResearchPrerequisites ??= d.plant.sowResearchPrerequisites;
                pollution ??= d.plant.pollution;
            }
            else if (harvestedByData == null)
            {
                EnsureHarvestedByMap();
                if (_harvestedByMap!.TryGetValue(d, out var plantId))
                    harvestedByData = plantId;
            }
        }
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyDefStats(def);
        ApplyL<string>(nameof(thingSetMakerTags), v => def.thingSetMakerTags = v);
        if (IsFieldModified(nameof(thingSetMakerTags))) DataUtility.RefreshThingSetMakerFilters();
        if (def.ingestible != null)
        {
            ApplyV<int>(nameof(baseIngestTicks), v => def.ingestible.baseIngestTicks = v);
            ApplyV<float>(nameof(joy), v => def.ingestible.joy = v);
            ApplyV<FoodPreferability>(nameof(preferability), v => def.ingestible.preferability = v);
        }
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_Rottable cr)
            {
                ApplyV<float>(nameof(daysToRotStart), v => cr.daysToRotStart = v);
                ApplyV<bool>(nameof(rotDestroys), v => cr.rotDestroys = v);
                ApplyV<float>(nameof(rotDamagePerDay), v => cr.rotDamagePerDay = v);
                ApplyV<float>(nameof(daysToDessicated), v => cr.daysToDessicated = v);
                ApplyV<float>(nameof(dessicatedDamagePerDay), v => cr.dessicatedDamagePerDay = v);
            }
        }
        if (def.plant != null)
        {
            ApplyV<float>(nameof(growDays), v => def.plant.growDays = v);
            ApplyV<float>(nameof(harvestYield), v => def.plant.harvestYield = v);
            ApplyV<float>(nameof(sowWork), v => def.plant.sowWork = v);
            ApplyV<float>(nameof(harvestWork), v => def.plant.harvestWork = v);
            ApplyV<int>(nameof(sowMinSkill), v => def.plant.sowMinSkill = v);
            ApplyV<float>(nameof(harvestMinGrowth), v => def.plant.harvestMinGrowth = v);
            ApplyV<float>(nameof(harvestAfterGrowth), v => def.plant.harvestAfterGrowth = v);
            ApplyV<float>(nameof(fertilityMin), v => def.plant.fertilityMin = v);
            ApplyV<float>(nameof(fertilitySensitivity), v => def.plant.fertilitySensitivity = v);
            ApplyV<float>(nameof(minGrowthTemperature), v => def.plant.minGrowthTemperature = v);
            ApplyV<float>(nameof(maxGrowthTemperature), v => def.plant.maxGrowthTemperature = v);
            ApplyV<float>(nameof(growMinGlow), v => def.plant.growMinGlow = v);
            ApplyV<float>(nameof(growOptimalGlow), v => def.plant.growOptimalGlow = v);
            ApplyV<float>(nameof(lifespanDaysPerGrowDays), v => def.plant.lifespanDaysPerGrowDays = v);
            ApplyV<bool>(nameof(completelyIgnoreFertility), v => def.plant.completelyIgnoreFertility = v);
            ApplyV<bool>(nameof(blockAdjacentSow), v => def.plant.blockAdjacentSow = v);
            ApplyV<bool>(nameof(harvestFailable), v => def.plant.harvestFailable = v);
            ApplyV<bool>(nameof(autoHarvestable), v => def.plant.autoHarvestable = v);
            ApplyV<bool>(nameof(neverBlightable), v => def.plant.neverBlightable = v);
            ApplyV<bool>(nameof(dieIfNoSunlight), v => def.plant.dieIfNoSunlight = v);
            ApplyV<float>(nameof(minSpacingBetweenSamePlant), v => def.plant.minSpacingBetweenSamePlant = v);
            ApplyL<string>(nameof(sowTags), v => def.plant.sowTags = v);
            ApplyL<ResearchProjectDef>(nameof(sowResearchPrerequisites), v => def.plant.sowResearchPrerequisites = v);
            ApplyV<Pollution>(nameof(pollution), v => def.plant.pollution = v);
        }
    }
    private static void EnsureHarvestedByMap()
    {
        if (_harvestedByMap != null) return;
        _harvestedByMap = new Dictionary<ThingDef, TweakID>();
        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            if (def.plant?.harvestedThingDef == null) continue;
            var plantId = new TweakID(def.defName, SettingType.Food);
            _harvestedByMap.TryAdd(def.plant.harvestedThingDef, plantId);
        }
    }
    public override List<string> TypeStrings => typeStrings;
    public static bool AvailableIfPlant(TweakData data) => data.propType switch
    {
        (int)FoodCategory.PlantCrop => true,
        (int)FoodCategory.PlantTree => true,
        (int)FoodCategory.PlantWild => true,
        _ => false,
    };
    public static bool AvailableIfNonPlant(TweakData data) => !AvailableIfPlant(data);
    public static bool AvailableIfRaw(TweakData data) => data.propType switch
    {
        (int)FoodCategory.Raw => true,
        _ => false,
    };

    public static bool AvailableIfDrag(TweakData data) => data.propType switch
    {
        (int)FoodCategory.Drug => true,
        _ => false,
    };
    private static readonly List<string> typeStrings = typeof(FoodCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum FoodCategory
    {
        Corpse,
        PlantCrop,
        PlantTree,
        PlantWild,
        MeatAndEgg,
        Meal,
        Raw,
        Drug,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.ingestible != null)
            {
                if (def.race != null)
                {
                    return (int)FoodCategory.Corpse;
                }
                if (def.plant != null)
                {
                    if (def.plant.IsTree) return (int)FoodCategory.PlantTree;
                    if (!def.plant.sowTags.NullOrEmpty()) return (int)FoodCategory.PlantCrop;
                    return (int)FoodCategory.PlantWild;
                }
                if (def.IsMeat || def.IsEgg)
                {
                    return (int)FoodCategory.MeatAndEgg;
                }
                if (def.ingestible.drugCategory != DrugCategory.None)
                {
                    return (int)FoodCategory.Drug;
                }
                var p = def.ingestible.preferability;
                if (p == FoodPreferability.MealTerrible || p == FoodPreferability.MealAwful || p == FoodPreferability.MealSimple || p == FoodPreferability.MealFine || p == FoodPreferability.MealLavish)
                {
                    return (int)FoodCategory.Meal;
                }
                if (p == FoodPreferability.RawBad || p == FoodPreferability.RawTasty || p == FoodPreferability.DesperateOnly || p == FoodPreferability.DesperateOnlyForHumanlikes)
                {
                    return (int)FoodCategory.Raw;
                }
            }
            if (def.IsMedicine)
            {
                return (int)FoodCategory.Drug;
            }
            return (int)FoodCategory.Other;
        }
        return (int)FoodCategory.Other;
    }
}
