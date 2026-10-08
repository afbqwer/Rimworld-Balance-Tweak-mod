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
    [TweakField(Style = ColumnStyle.BiomeAnimalList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<BiomeAnimalRecord>? coastalWildAnimals = null;
    [TweakField(Style = ColumnStyle.BiomeAnimalList, MayRequire = "ludeon.rimworld.biotech")]
    public List<BiomeAnimalRecord>? pollutionWildAnimals = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? allowedPackAnimals = null;

    [TweakField(Style = ColumnStyle.DiseaseList)]
    public List<BiomeDiseaseRecord>? diseases = null;

    [TweakField(Style = ColumnStyle.WeatherCommonalityList)]
    public List<WeatherCommonalityRecord>? baseWeatherCommonalities = null;

    // fishTypes 是复合对象，四个鱼列表 + 稀有渔获子字段都是它下面的内容；
    // Apply 中任一非空时先确保 fishTypes 实例存在。
    [TweakField(Style = ColumnStyle.FishChanceList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<FishChance>? freshwaterCommon = null;
    [TweakField(Style = ColumnStyle.FishChanceList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<FishChance>? freshwaterUncommon = null;
    [TweakField(Style = ColumnStyle.FishChanceList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<FishChance>? saltwaterCommon = null;
    [TweakField(Style = ColumnStyle.FishChanceList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<FishChance>? saltwaterUncommon = null;
    [TweakField(Style = ColumnStyle.DefSelector, MayRequire = "ludeon.rimworld.odyssey")]
    public ThingSetMakerDef? rareCatchesSetMaker = null;

    // 地形生成（只影响新生成的地图）
    [TweakField(Style = ColumnStyle.TerrainThresholdList)]
    public List<TerrainThreshold>? terrainsByFertility = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? gravelTerrain = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? noGravel = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? extraRockTypes = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? forceRockTypes = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? waterShallowTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? waterDeepTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? waterMovingShallowTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? waterMovingChestDeepTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? oceanShallowTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? oceanDeepTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? riverbankTerrain = null;
    [TweakField(Style = ColumnStyle.Range)]
    public IntRange? riverbankSizeRange = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? mudTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? coastalBeachTerrain = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TerrainDef? lakeBeachTerrain = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // BiomeDef 的 wildAnimals/coastalWildAnimals/pollutionWildAnimals/diseases/allowedPackAnimals
    // 是 private 字段，且动植物与疾病共性值缓存在私有字段里（懒加载、无失效机制），只能反射读写；
    // 写完列表后把缓存置 null 强制下次访问重建，否则运行中修改不生效。
    private static readonly FieldInfo? wildAnimalsField = AccessTools.Field(typeof(BiomeDef), "wildAnimals");
    private static readonly FieldInfo? coastalWildAnimalsField = AccessTools.Field(typeof(BiomeDef), "coastalWildAnimals");
    private static readonly FieldInfo? pollutionWildAnimalsField = AccessTools.Field(typeof(BiomeDef), "pollutionWildAnimals");
    private static readonly FieldInfo? diseasesField = AccessTools.Field(typeof(BiomeDef), "diseases");
    private static readonly FieldInfo? allowedPackAnimalsField = AccessTools.Field(typeof(BiomeDef), "allowedPackAnimals");
    private static readonly FieldInfo?[] biomeCacheFields =
    [
        AccessTools.Field(typeof(BiomeDef), "cachedAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedCoastalAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedPollutionAnimalCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedPlantCommonalities"),
        AccessTools.Field(typeof(BiomeDef), "cachedWildPlants"),
        AccessTools.Field(typeof(BiomeDef), "cachedLowestWildPlantOrder"),
        AccessTools.Field(typeof(BiomeDef), "cachedMaxWildPlantsClusterRadius"),
        AccessTools.Field(typeof(BiomeDef), "cachedDiseaseCommonalities"),
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
            coastalWildAnimals ??= coastalWildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
            pollutionWildAnimals ??= pollutionWildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
            allowedPackAnimals ??= allowedPackAnimalsField?.GetValue(d) as List<ThingDef>;
            diseases ??= diseasesField?.GetValue(d) as List<BiomeDiseaseRecord>;
            baseWeatherCommonalities ??= d.baseWeatherCommonalities;
            freshwaterCommon ??= d.fishTypes?.freshwater_Common;
            freshwaterUncommon ??= d.fishTypes?.freshwater_Uncommon;
            saltwaterCommon ??= d.fishTypes?.saltwater_Common;
            saltwaterUncommon ??= d.fishTypes?.saltwater_Uncommon;
            rareCatchesSetMaker ??= d.fishTypes?.rareCatchesSetMaker;
            terrainsByFertility ??= d.terrainsByFertility;
            gravelTerrain ??= d.gravelTerrain;
            noGravel ??= d.noGravel;
            extraRockTypes ??= d.extraRockTypes;
            forceRockTypes ??= d.forceRockTypes;
            waterShallowTerrain ??= d.waterShallowTerrain;
            waterDeepTerrain ??= d.waterDeepTerrain;
            waterMovingShallowTerrain ??= d.waterMovingShallowTerrain;
            waterMovingChestDeepTerrain ??= d.waterMovingChestDeepTerrain;
            oceanShallowTerrain ??= d.oceanShallowTerrain;
            oceanDeepTerrain ??= d.oceanDeepTerrain;
            riverbankTerrain ??= d.riverbankTerrain;
            riverbankSizeRange ??= d.riverbankSizeRange;
            mudTerrain ??= d.mudTerrain;
            coastalBeachTerrain ??= d.coastalBeachTerrain;
            lakeBeachTerrain ??= d.lakeBeachTerrain;
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
        if (coastalWildAnimals != null && coastalWildAnimalsField != null) { coastalWildAnimalsField.SetValue(def, coastalWildAnimals); }
        if (pollutionWildAnimals != null && pollutionWildAnimalsField != null) { pollutionWildAnimalsField.SetValue(def, pollutionWildAnimals); }
        if (allowedPackAnimals != null && allowedPackAnimalsField != null) { allowedPackAnimalsField.SetValue(def, allowedPackAnimals); }
        if (diseases != null && diseasesField != null) { diseasesField.SetValue(def, diseases); }
        if (baseWeatherCommonalities != null) { def.baseWeatherCommonalities = baseWeatherCommonalities; }

        bool fishTouched = freshwaterCommon != null || freshwaterUncommon != null
            || saltwaterCommon != null || saltwaterUncommon != null || rareCatchesSetMaker != null;
        if (fishTouched)
        {
            def.fishTypes ??= new BiomeFishTypes();
            if (freshwaterCommon != null) { def.fishTypes.freshwater_Common = freshwaterCommon; }
            if (freshwaterUncommon != null) { def.fishTypes.freshwater_Uncommon = freshwaterUncommon; }
            if (saltwaterCommon != null) { def.fishTypes.saltwater_Common = saltwaterCommon; }
            if (saltwaterUncommon != null) { def.fishTypes.saltwater_Uncommon = saltwaterUncommon; }
            if (rareCatchesSetMaker != null) { def.fishTypes.rareCatchesSetMaker = rareCatchesSetMaker; }
        }

        if (terrainsByFertility != null) { def.terrainsByFertility = terrainsByFertility; }
        if (gravelTerrain != null) { def.gravelTerrain = gravelTerrain; }
        if (noGravel.HasValue) { def.noGravel = noGravel.Value; }
        if (extraRockTypes != null) { def.extraRockTypes = extraRockTypes; }
        if (forceRockTypes != null) { def.forceRockTypes = forceRockTypes; }
        if (waterShallowTerrain != null) { def.waterShallowTerrain = waterShallowTerrain; }
        if (waterDeepTerrain != null) { def.waterDeepTerrain = waterDeepTerrain; }
        if (waterMovingShallowTerrain != null) { def.waterMovingShallowTerrain = waterMovingShallowTerrain; }
        if (waterMovingChestDeepTerrain != null) { def.waterMovingChestDeepTerrain = waterMovingChestDeepTerrain; }
        if (oceanShallowTerrain != null) { def.oceanShallowTerrain = oceanShallowTerrain; }
        if (oceanDeepTerrain != null) { def.oceanDeepTerrain = oceanDeepTerrain; }
        if (riverbankTerrain != null) { def.riverbankTerrain = riverbankTerrain; }
        if (riverbankSizeRange.HasValue) { def.riverbankSizeRange = riverbankSizeRange.Value; }
        if (mudTerrain != null) { def.mudTerrain = mudTerrain; }
        if (coastalBeachTerrain != null) { def.coastalBeachTerrain = coastalBeachTerrain; }
        if (lakeBeachTerrain != null) { def.lakeBeachTerrain = lakeBeachTerrain; }

        if (wildPlants != null || wildAnimals != null || coastalWildAnimals != null || pollutionWildAnimals != null
            || diseases != null || fishTouched)
        {
            InvalidateBiomeCaches(def);
        }

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
        Space,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is BiomeDef def)
        {
            if (def.isWaterBiome) return (int)BiomeCategory.Water;
            if (def.inVacuum) return (int)BiomeCategory.Space;
            if (def.implemented) return (int)BiomeCategory.Normal;
        }
        return (int)BiomeCategory.Other;
    }
}
