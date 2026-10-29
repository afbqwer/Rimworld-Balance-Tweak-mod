using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private static readonly FieldInfo? wildAnimalsField = Reflect.Field(typeof(BiomeDef), "wildAnimals");
    private static readonly FieldInfo? coastalWildAnimalsField = Reflect.Field(typeof(BiomeDef), "coastalWildAnimals");
    private static readonly FieldInfo? pollutionWildAnimalsField = Reflect.Field(typeof(BiomeDef), "pollutionWildAnimals");
    private static readonly FieldInfo? diseasesField = Reflect.Field(typeof(BiomeDef), "diseases");
    private static readonly FieldInfo? allowedPackAnimalsField = Reflect.Field(typeof(BiomeDef), "allowedPackAnimals");
    private static readonly FieldInfo?[] biomeCacheFields =
    [
        Reflect.Field(typeof(BiomeDef), "cachedAnimalCommonalities"),
        Reflect.Field(typeof(BiomeDef), "cachedCoastalAnimalCommonalities"),
        Reflect.Field(typeof(BiomeDef), "cachedPollutionAnimalCommonalities"),
        Reflect.Field(typeof(BiomeDef), "cachedPlantCommonalities"),
        Reflect.Field(typeof(BiomeDef), "cachedWildPlants"),
        Reflect.Field(typeof(BiomeDef), "cachedLowestWildPlantOrder"),
        Reflect.Field(typeof(BiomeDef), "cachedMaxWildPlantsClusterRadius"),
        Reflect.Field(typeof(BiomeDef), "cachedDiseaseCommonalities"),
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
            if (!IsFieldModified(nameof(wildPlants))) wildPlants ??= d.wildPlants;
            if (!IsFieldModified(nameof(wildAnimals))) wildAnimals ??= wildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
            if (!IsFieldModified(nameof(coastalWildAnimals))) coastalWildAnimals ??= coastalWildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
            if (!IsFieldModified(nameof(pollutionWildAnimals))) pollutionWildAnimals ??= pollutionWildAnimalsField?.GetValue(d) as List<BiomeAnimalRecord>;
            if (!IsFieldModified(nameof(allowedPackAnimals))) allowedPackAnimals ??= allowedPackAnimalsField?.GetValue(d) as List<ThingDef>;
            if (!IsFieldModified(nameof(diseases))) diseases ??= diseasesField?.GetValue(d) as List<BiomeDiseaseRecord>;
            if (!IsFieldModified(nameof(baseWeatherCommonalities))) baseWeatherCommonalities ??= d.baseWeatherCommonalities;
            if (!IsFieldModified(nameof(freshwaterCommon))) freshwaterCommon ??= d.fishTypes?.freshwater_Common;
            if (!IsFieldModified(nameof(freshwaterUncommon))) freshwaterUncommon ??= d.fishTypes?.freshwater_Uncommon;
            if (!IsFieldModified(nameof(saltwaterCommon))) saltwaterCommon ??= d.fishTypes?.saltwater_Common;
            if (!IsFieldModified(nameof(saltwaterUncommon))) saltwaterUncommon ??= d.fishTypes?.saltwater_Uncommon;
            rareCatchesSetMaker ??= d.fishTypes?.rareCatchesSetMaker;
            if (!IsFieldModified(nameof(terrainsByFertility))) terrainsByFertility ??= d.terrainsByFertility;
            gravelTerrain ??= d.gravelTerrain;
            noGravel ??= d.noGravel;
            if (!IsFieldModified(nameof(extraRockTypes))) extraRockTypes ??= d.extraRockTypes;
            if (!IsFieldModified(nameof(forceRockTypes))) forceRockTypes ??= d.forceRockTypes;
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
        ApplyV<float>(nameof(animalDensity), v => def.animalDensity = v);
        ApplyV<float>(nameof(plantDensity), v => def.plantDensity = v);
        ApplyV<float>(nameof(wildPlantRegrowDays), v => def.wildPlantRegrowDays = v);
        ApplyV<float>(nameof(diseaseMtbDays), v => def.diseaseMtbDays = v);
        ApplyV<float>(nameof(movementDifficulty), v => def.movementDifficulty = v);
        ApplyV<float>(nameof(forageability), v => def.forageability = v);
        ApplyV<float>(nameof(settlementSelectionWeight), v => def.settlementSelectionWeight = v);
        ApplyV<float>(nameof(campSelectionWeight), v => def.campSelectionWeight = v);
        ApplyV<float>(nameof(pollutionOffset), v => def.pollutionOffset = v);
        ApplyV<float>(nameof(wildAnimalScariaChance), v => def.wildAnimalScariaChance = v);
        ApplyV<float>(nameof(geyserCountFactor), v => def.geyserCountFactor = v);
        ApplyV<float>(nameof(maxFishPopulation), v => def.maxFishPopulation = v);
        ApplyV<bool>(nameof(canBuildBase), v => def.canBuildBase = v);
        ApplyV<bool>(nameof(canAutoChoose), v => def.canAutoChoose = v);
        ApplyV<bool>(nameof(allowRoads), v => def.allowRoads = v);
        ApplyV<bool>(nameof(allowRivers), v => def.allowRivers = v);
        ApplyV<bool>(nameof(allowPollution), v => def.allowPollution = v);
        ApplyV<bool>(nameof(allowFarmingCamps), v => def.allowFarmingCamps = v);
        ApplyV<bool>(nameof(wildAnimalsCanWanderInto), v => def.wildAnimalsCanWanderInto = v);
        ApplyV<bool>(nameof(wildPlantsCareAboutLocalFertility), v => def.wildPlantsCareAboutLocalFertility = v);
        ApplyV<bool>(nameof(impassable), v => def.impassable = v);
        ApplyD<ThingDef>(nameof(foragedFood), v => def.foragedFood = v);

        ApplyL<BiomePlantRecord>(nameof(wildPlants), v => def.wildPlants = v);
        ApplyL<BiomeAnimalRecord>(nameof(wildAnimals), v => { if (wildAnimalsField != null) wildAnimalsField.SetValue(def, v); });
        ApplyL<BiomeAnimalRecord>(nameof(coastalWildAnimals), v => { if (coastalWildAnimalsField != null) coastalWildAnimalsField.SetValue(def, v); });
        ApplyL<BiomeAnimalRecord>(nameof(pollutionWildAnimals), v => { if (pollutionWildAnimalsField != null) pollutionWildAnimalsField.SetValue(def, v); });
        ApplyL<ThingDef>(nameof(allowedPackAnimals), v => { if (allowedPackAnimalsField != null) allowedPackAnimalsField.SetValue(def, v); });
        ApplyL<BiomeDiseaseRecord>(nameof(diseases), v => { if (diseasesField != null) diseasesField.SetValue(def, v); });
        ApplyL<WeatherCommonalityRecord>(nameof(baseWeatherCommonalities), v => def.baseWeatherCommonalities = v);

        bool fishTouched = StateOf(nameof(freshwaterCommon)) != FieldState.Unset
            || StateOf(nameof(freshwaterUncommon)) != FieldState.Unset
            || StateOf(nameof(saltwaterCommon)) != FieldState.Unset
            || StateOf(nameof(saltwaterUncommon)) != FieldState.Unset
            || StateOf(nameof(rareCatchesSetMaker)) != FieldState.Unset;
        if (fishTouched)
        {
            def.fishTypes ??= new BiomeFishTypes();
            ApplyL<FishChance>(nameof(freshwaterCommon), v => def.fishTypes.freshwater_Common = v);
            ApplyL<FishChance>(nameof(freshwaterUncommon), v => def.fishTypes.freshwater_Uncommon = v);
            ApplyL<FishChance>(nameof(saltwaterCommon), v => def.fishTypes.saltwater_Common = v);
            ApplyL<FishChance>(nameof(saltwaterUncommon), v => def.fishTypes.saltwater_Uncommon = v);
            ApplyD<ThingSetMakerDef>(nameof(rareCatchesSetMaker), v => def.fishTypes.rareCatchesSetMaker = v);
        }

        ApplyL<TerrainThreshold>(nameof(terrainsByFertility), v => def.terrainsByFertility = v);
        ApplyD<TerrainDef>(nameof(gravelTerrain), v => def.gravelTerrain = v);
        ApplyV<bool>(nameof(noGravel), v => def.noGravel = v);
        ApplyL<ThingDef>(nameof(extraRockTypes), v => def.extraRockTypes = v);
        ApplyL<ThingDef>(nameof(forceRockTypes), v => def.forceRockTypes = v);
        ApplyD<TerrainDef>(nameof(waterShallowTerrain), v => def.waterShallowTerrain = v);
        ApplyD<TerrainDef>(nameof(waterDeepTerrain), v => def.waterDeepTerrain = v);
        ApplyD<TerrainDef>(nameof(waterMovingShallowTerrain), v => def.waterMovingShallowTerrain = v);
        ApplyD<TerrainDef>(nameof(waterMovingChestDeepTerrain), v => def.waterMovingChestDeepTerrain = v);
        ApplyD<TerrainDef>(nameof(oceanShallowTerrain), v => def.oceanShallowTerrain = v);
        ApplyD<TerrainDef>(nameof(oceanDeepTerrain), v => def.oceanDeepTerrain = v);
        ApplyD<TerrainDef>(nameof(riverbankTerrain), v => def.riverbankTerrain = v);
        ApplyV<IntRange>(nameof(riverbankSizeRange), v => def.riverbankSizeRange = v);
        ApplyD<TerrainDef>(nameof(mudTerrain), v => def.mudTerrain = v);
        ApplyD<TerrainDef>(nameof(coastalBeachTerrain), v => def.coastalBeachTerrain = v);
        ApplyD<TerrainDef>(nameof(lakeBeachTerrain), v => def.lakeBeachTerrain = v);

        if (StateOf(nameof(wildPlants)) != FieldState.Unset
            || StateOf(nameof(wildAnimals)) != FieldState.Unset
            || StateOf(nameof(coastalWildAnimals)) != FieldState.Unset
            || StateOf(nameof(pollutionWildAnimals)) != FieldState.Unset
            || StateOf(nameof(diseases)) != FieldState.Unset
            || fishTouched)
        {
            InvalidateBiomeCaches(def);
        }

        ApplyDefLabel(nameof(defLabel), def);
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
