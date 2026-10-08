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
            statBases ??= d.statBases;
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
                sowTags ??= d.plant.sowTags;
                sowResearchPrerequisites ??= d.plant.sowResearchPrerequisites;
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
        if (defLabel != null) this.def.label = defLabel;
        ApplyDefStats(def);
        if (def.ingestible != null)
        {
            if (baseIngestTicks.HasValue) { def.ingestible.baseIngestTicks = baseIngestTicks.Value; }
            if (joy.HasValue) { def.ingestible.joy = joy.Value; }
            if (preferability.HasValue) { def.ingestible.preferability = preferability.Value; }
        }
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_Rottable cr)
            {
                if (daysToRotStart.HasValue) { cr.daysToRotStart = daysToRotStart.Value; }
                if (rotDestroys.HasValue) { cr.rotDestroys = rotDestroys.Value; }
                if (rotDamagePerDay.HasValue) { cr.rotDamagePerDay = rotDamagePerDay.Value; }
                if (daysToDessicated.HasValue) { cr.daysToDessicated = daysToDessicated.Value; }
                if (dessicatedDamagePerDay.HasValue) { cr.dessicatedDamagePerDay = dessicatedDamagePerDay.Value; }
            }
        }
        if (def.plant != null)
        {
            if (growDays.HasValue) { def.plant.growDays = growDays.Value; }
            if (harvestYield.HasValue) { def.plant.harvestYield = harvestYield.Value; }
            if (sowWork.HasValue) { def.plant.sowWork = sowWork.Value; }
            if (harvestWork.HasValue) { def.plant.harvestWork = harvestWork.Value; }
            if (sowMinSkill.HasValue) { def.plant.sowMinSkill = sowMinSkill.Value; }
            if (harvestMinGrowth.HasValue) { def.plant.harvestMinGrowth = harvestMinGrowth.Value; }
            if (harvestAfterGrowth.HasValue) { def.plant.harvestAfterGrowth = harvestAfterGrowth.Value; }
            if (fertilityMin.HasValue) { def.plant.fertilityMin = fertilityMin.Value; }
            if (fertilitySensitivity.HasValue) { def.plant.fertilitySensitivity = fertilitySensitivity.Value; }
            if (minGrowthTemperature.HasValue) { def.plant.minGrowthTemperature = minGrowthTemperature.Value; }
            if (maxGrowthTemperature.HasValue) { def.plant.maxGrowthTemperature = maxGrowthTemperature.Value; }
            if (growMinGlow.HasValue) { def.plant.growMinGlow = growMinGlow.Value; }
            if (growOptimalGlow.HasValue) { def.plant.growOptimalGlow = growOptimalGlow.Value; }
            if (lifespanDaysPerGrowDays.HasValue) { def.plant.lifespanDaysPerGrowDays = lifespanDaysPerGrowDays.Value; }
            if (completelyIgnoreFertility.HasValue) { def.plant.completelyIgnoreFertility = completelyIgnoreFertility.Value; }
            if (blockAdjacentSow.HasValue) { def.plant.blockAdjacentSow = blockAdjacentSow.Value; }
            if (harvestFailable.HasValue) { def.plant.harvestFailable = harvestFailable.Value; }
            if (autoHarvestable.HasValue) { def.plant.autoHarvestable = autoHarvestable.Value; }
            if (neverBlightable.HasValue) { def.plant.neverBlightable = neverBlightable.Value; }
            if (dieIfNoSunlight.HasValue) { def.plant.dieIfNoSunlight = dieIfNoSunlight.Value; }
            if (minSpacingBetweenSamePlant.HasValue) { def.plant.minSpacingBetweenSamePlant = minSpacingBetweenSamePlant.Value; }
            if (sowTags != null) { def.plant.sowTags = sowTags; }
            if (sowResearchPrerequisites != null) { def.plant.sowResearchPrerequisites = sowResearchPrerequisites; }
            if (pollution.HasValue) { def.plant.pollution = pollution.Value; }
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
