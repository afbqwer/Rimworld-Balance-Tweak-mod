using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(BiomeDef), SettingType.Biome)]
class BiomeData : TweakData<BiomeData>
{
    [TweakField()]
    public float? animalDensity = null;
    [TweakField()]
    public float? plantDensity = null;
    [TweakField()]
    public float? wildPlantRegrowDays = null;
    [TweakField()]
    public float? diseaseMtbDays = null;
    [TweakField()]
    public float? movementDifficulty = null;
    [TweakField()]
    public float? forageability = null;
    [TweakField()]
    public float? settlementSelectionWeight = null;
    [TweakField()]
    public float? campSelectionWeight = null;
    [TweakField()]
    public float? pollutionOffset = null;
    [TweakField()]
    public float? wildAnimalScariaChance = null;
    [TweakField()]
    public float? geyserCountFactor = null;
    [TweakField()]
    public float? maxFishPopulation = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canBuildBase = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canAutoChoose = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? allowRoads = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? allowRivers = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? allowPollution = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? allowFarmingCamps = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? wildAnimalsCanWanderInto = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? wildPlantsCareAboutLocalFertility = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? impassable = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThingDef? foragedFood = null;

    [TweakField(Style = ColumnStyle.BiomePlantList)]
    public List<BiomePlantRecord>? wildPlants = null;
    [TweakField(Style = ColumnStyle.BiomeAnimalList)]
    public List<BiomeAnimalRecord>? wildAnimals = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // BiomeDef.wildAnimals 是 private 字段，且动植物共性值缓存在私有字段里（懒加载、无失效机制），
    // 只能反射读写；写完列表后把缓存置 null 强制下次访问重建，否则运行中修改不生效。
    private static readonly FieldInfo? wildAnimalsField = AccessTools.Field(typeof(BiomeDef), "wildAnimals");
    private static readonly FieldInfo?[] biomeCacheFields =
    [
        AccessTools.Field(typeof(BiomeDef), "cachedAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedCoastalAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedPollutionAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedPlantCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedWildPlants"),
        AccessTools.Field(typeof(BiomeDef), "cachedLowestWildPlantOrder"),
        AccessTools.Field(typeof(BiomeDef), "cachedMaxWildPlantsClusterRadius"),
    ];

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is BiomeDef d)
        {
            animalDensity ??= d.animalDensity;
            plantDensity ??= d.plantDensity;
            wildPlantRegrowDays ??= d.wildPlantRegrowDays;
            diseaseMtbDays ??= d.diseaseMtbDays;
            movementDifficulty ??= d.movementDifficulty;
            forageability ??= d.forageability;
            settlementSelectionWeight ??= d.settlementSelectionWeight;
            campSelectionWeight ??= d.campSelectionWeight;
            pollutionOffset ??= d.pollutionOffset;
            wildAnimalScariaChance ??= d.wildAnimalScariaChance;
            geyserCountFactor ??= d.geyserCountFactor;
            maxFishPopulation ??= d.maxFishPopulation;
            canBuildBase ??= d.canBuildBase;
            canAutoChoose ??= d.canAutoChoose;
            allowRoads ??= d.allowRoads;
            allowRivers ??= d.allowRivers;
            allowPollution ??= d.allowPollution;
            allowFarmingCamps ??= d.allowFarmingCamps;
            wildAnimalsCanWanderInto ??= d.wildAnimalsCanWanderInto;
            wildPlantsCareAboutLocalFertility ??= d.wildPlantsCareAboutLocalFertility;
            impassable ??= d.impassable;
            foragedFood ??= d.foragedFood;
            wildPlants ??= d.wildPlants;
            wildAnimals ??= wildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
        }
    }

    public override void Apply()
    {
        if (this.def is not BiomeDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (animalDensity.HasValue) { def.animalDensity = animalDensity.Value; }
        if (plantDensity.HasValue) { def.plantDensity = plantDensity.Value; }
        if (wildPlantRegrowDays.HasValue) { def.wildPlantRegrowDays = wildPlantRegrowDays.Value; }
        if (diseaseMtbDays.HasValue) { def.diseaseMtbDays = diseaseMtbDays.Value; }
        if (movementDifficulty.HasValue) { def.movementDifficulty = movementDifficulty.Value; }
        if (forageability.HasValue) { def.forageability = forageability.Value; }
        if (settlementSelectionWeight.HasValue) { def.settlementSelectionWeight = settlementSelectionWeight.Value; }
        if (campSelectionWeight.HasValue) { def.campSelectionWeight = campSelectionWeight.Value; }
        if (pollutionOffset.HasValue) { def.pollutionOffset = pollutionOffset.Value; }
        if (wildAnimalScariaChance.HasValue) { def.wildAnimalScariaChance = wildAnimalScariaChance.Value; }
        if (geyserCountFactor.HasValue) { def.geyserCountFactor = geyserCountFactor.Value; }
        if (maxFishPopulation.HasValue) { def.maxFishPopulation = maxFishPopulation.Value; }
        if (canBuildBase.HasValue) { def.canBuildBase = canBuildBase.Value; }
        if (canAutoChoose.HasValue) { def.canAutoChoose = canAutoChoose.Value; }
        if (allowRoads.HasValue) { def.allowRoads = allowRoads.Value; }
        if (allowRivers.HasValue) { def.allowRivers = allowRivers.Value; }
        if (allowPollution.HasValue) { def.allowPollution = allowPollution.Value; }
        if (allowFarmingCamps.HasValue) { def.allowFarmingCamps = allowFarmingCamps.Value; }
        if (wildAnimalsCanWanderInto.HasValue) { def.wildAnimalsCanWanderInto = wildAnimalsCanWanderInto.Value; }
        if (wildPlantsCareAboutLocalFertility.HasValue) { def.wildPlantsCareAboutLocalFertility = wildPlantsCareAboutLocalFertility.Value; }
        if (impassable.HasValue) { def.impassable = impassable.Value; }
        if (foragedFood != null) { def.foragedFood = foragedFood; }

        if (wildPlants != null) { def.wildPlants = wildPlants; }
        if (wildAnimals != null && wildAnimalsField != null) { wildAnimalsField.SetValue(def, wildAnimals); }
        if (wildPlants != null || wildAnimals != null) { InvalidateBiomeCaches(def); }

        if (defLabel != null) this.def.label = defLabel;
    }

    private static void InvalidateBiomeCaches(BiomeDef def)
    {
        foreach (var field in biomeCacheFields)
        {
            field?.SetValue(def, null);
        }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(BiomeCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum BiomeCategory
    {
        Normal,
        Water,
        Extreme,
        Space,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is BiomeDef def)
        {
            if (def.isWaterBiome) return (int)BiomeCategory.Water;
            if (def.inVacuum) return (int)BiomeCategory.Space;
            if (def.isExtremeBiome) return (int)BiomeCategory.Extreme;
            if (def.implemented) return (int)BiomeCategory.Normal;
        }
        return (int)BiomeCategory.Other;
    }
}
