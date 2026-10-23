using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.DataUtility;
using static BalanceTweak.BalanceTweakSettings;
namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Building, Priority = 500, MatchMethod = nameof(DataUtility.MatchBuilding))]
class BuildingData : TweakData<BuildingData>
{
    // ===== 字段定义 =====

    // --- 通用建筑字段（所有分类可见）---
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfTurret))]
    public TweakID? TurretGun = null;
    [TweakField(StatDef = "ShootingAccuracyTurret", Style = ColumnStyle.Prec, Available = nameof(AvailableIfTurret))]
    public float? shootingAccuracyTurret = null;

    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Int)]
    public float? maxHitPoints = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? pathCost = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? fillPercent = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec)]
    public float? flammability = null;
    [TweakField(StatDef = "WorkToBuild", Style = ColumnStyle.Int)]
    public float? workToBuild = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? costStuffCount = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList)]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<StuffCategoryDef>? stuffCategories = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectDef>? researchPrerequisites = null;
    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;
    [TweakField(StatDef = "WorkTableWorkSpeedFactor", Style = ColumnStyle.Prec, Available = nameof(AvailableIfProduction))]
    public float? workTableWorkSpeedFactor = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalMarketValue = null;
    [TweakField(StatDef = "Beauty")]
    public float? beauty = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isAirtight = null;

    // --- Turret 字段 ---
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Prec)]
    public float? turretBurstCooldownTime = null;
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Prec)]
    public float? turretInitialCooldownTime = null;
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Int)]
    public float? combatPower = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? replaceTags = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;

    // --- 新增通用 ThingDef 字段（无 Available 限制）---
    //[TweakField(Style = ColumnStyle.Enum, EnumType = typeof(Traversability))]
    //public Traversability? passability = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? constructionSkillPrerequisite = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? holdsRoof = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blockLight = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blockWind = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? destroyable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? selectable = null;

    // --- 通用 Comp 字段 ---
    [TweakField()]
    public float? explosionRadius = null;


    // --- Anomaly 字段 ---
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly", Style = ColumnStyle.Int)]
    public int? studiableFrequencyTicks = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? anomalyKnowledge = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? activityStartingRangeMin = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? activityStartingRangeMax = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? changePerDayBase = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? changePerDamage = null;


    // --- PowerBuilding 字段 ---
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfPower))]
    public float? basePowerConsumption = null;
    [TweakField(Available = nameof(AvailableIfPower), Style = ColumnStyle.Bool)]
    public bool? shortCircuitInRain = null;
    [TweakField(Available = nameof(AvailableIfPower), Style = ColumnStyle.Bool)]
    public bool? transmitsPower = null;
    [TweakField(Available = nameof(AvailableIfPower), Style = ColumnStyle.Bool)]
    public bool? allowWireConnection = null;

    // --- Door 字段 ---
    [TweakField(Available = nameof(AvailableIfDoor), Style = ColumnStyle.Bool)]
    public bool? canExchangeVacuum = null;
    [TweakField(Available = nameof(AvailableIfDoor), Style = ColumnStyle.Prec)]
    public float? poweredDoorOpenSpeedFactor = null;
    [TweakField(Available = nameof(AvailableIfDoor), Style = ColumnStyle.Prec)]
    public float? poweredDoorCloseSpeedFactor = null;
    [TweakField(Available = nameof(AvailableIfDoor), Style = ColumnStyle.Prec)]
    public float? unpoweredDoorOpenSpeedFactor = null;
    [TweakField(Available = nameof(AvailableIfDoor), Style = ColumnStyle.Prec)]
    public float? unpoweredDoorCloseSpeedFactor = null;

    // --- Bed 字段 ---
    [TweakField(Available = nameof(AvailableIfBed), Style = ColumnStyle.Prec)]
    public float? bed_healPerDay = null;
    [TweakField(Available = nameof(AvailableIfBed))]
    public float? bed_maxBodySize = null;
    [TweakField(Available = nameof(AvailableIfBed), Style = ColumnStyle.Bool)]
    public bool? bed_caravansCanUse = null;

    // --- LightSource 字段 ---
    [TweakField(Available = nameof(AvailableIfLightSource))]
    public float? glowRadius = null;
    [TweakField(Available = nameof(AvailableIfLightSource))]
    public float? overlightRadius = null;
    [TweakField(Available = nameof(AvailableIfLightSource), Style = ColumnStyle.Prec)]
    public float? scheduleStartTime = null;
    [TweakField(Available = nameof(AvailableIfLightSource), Style = ColumnStyle.Prec)]
    public float? scheduleEndTime = null;

    // --- TemperatureControl 字段 ---
    [TweakField(Available = nameof(AvailableIfTemperatureControl))]
    public float? energyPerSecond = null;
    [TweakField(Available = nameof(AvailableIfTemperatureControl))]
    public float? defaultTargetTemperature = null;
    [TweakField(Available = nameof(AvailableIfTemperatureControl))]
    public float? minTargetTemperature = null;
    [TweakField(Available = nameof(AvailableIfTemperatureControl))]
    public float? maxTargetTemperature = null;

    // --- Production 字段 ---
    [TweakField(Available = nameof(AvailableIfProduction), Style = ColumnStyle.DefSelector)]
    public RoomRoleDef? workTableRoomRole = null;
    [TweakField(Available = nameof(AvailableIfProduction), Style = ColumnStyle.Prec)]
    public float? workTableNotInRoomRoleFactor = null;
    [TweakField(Available = nameof(AvailableIfProduction), Style = ColumnStyle.Prec)]
    public float? unpoweredWorkTableWorkSpeedFactor = null;
    [TweakField(Available = nameof(AvailableIfProduction), Style = ColumnStyle.DefList)]
    public List<RecipeDef>? recipes = null;

    //[TweakField(Available = nameof(AvailableIfPower))]
    //public float? heatPerSecond = null;
    //[TweakField(Available = nameof(AvailableIfPower))]
    //public float? heatPushMaxTemperature = null;
    //[TweakField(Available = nameof(AvailableIfPower))]
    //public float? heatPushMinTemperature = null;

    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfPowerBuilding))]
    public float? fertility = null;

    // --- Mineable 字段 ---
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfMineable))]
    public ThingDef? mineableThing = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMineable))]
    public int? mineableYield = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? veinMineable = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfMineable))]
    public float? mineableNonMinedEfficiency = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfMineable))]
    public float? mineableDropChance = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? mineableYieldWasteable = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfMineable))]
    public float? mineableScatterCommonality = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? mineablePreventMeteorite = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? mineablePreventNaturalRockOnSurface = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? isNaturalRock = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMineable))]
    public bool? isResourceRock = null;

    // --- Refuelable 字段 ---
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    public string? fuelName = null;
    [TweakField()]
    public float? fuelCapacity = null;
    [TweakField()]
    public float? fuelMultiplier = null;
    [TweakField()]
    public float? fuelConsumptionRate = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isTargetable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? alwaysDeconstructible = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? alwaysUninstallable = null;
    [TweakField(Style = ColumnStyle.Int)]
    public float? uninstallWork = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? claimable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? repairable = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // ===== FieldInfo 反射 =====
    static FieldInfo CompPropertiesBasePowerConsumption = Reflect.Field(typeof(CompProperties_Power), "basePowerConsumption");
    static FieldInfo CompPropertiesfuelMultiplier = Reflect.Field(typeof(CompProperties_Refuelable), "fuelMultiplier");
    static FieldInfo CompPropertiesStudiableAnomalyKnowledge = Reflect.Field(typeof(CompProperties_Studiable), "anomalyKnowledge");

    public override void SetDef(Def def, BalanceTweakSettings.SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            fillPercent = d.fillPercent;
            pathCost ??= d.pathCost;
            costStuffCount ??= d.costStuffCount;
            if (!IsFieldModified(nameof(costList))) costList ??= d.costList;
            if (!IsFieldModified(nameof(stuffCategories))) stuffCategories ??= d.stuffCategories;
            if (!IsFieldModified(nameof(researchPrerequisites))) researchPrerequisites ??= d.researchPrerequisites;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(replaceTags))) replaceTags ??= d.replaceTags;
            // 新增 ThingDef 字段
            //passability ??= d.passability;
            constructionSkillPrerequisite ??= d.constructionSkillPrerequisite;
            holdsRoof ??= d.holdsRoof;
            blockLight ??= d.blockLight;
            blockWind ??= d.blockWind;
            destroyable ??= d.destroyable;
            selectable ??= d.selectable;
            fertility ??= d.fertility;
            if (d.building != null)
            {
                isTargetable ??= d.building.isTargetable;
                alwaysDeconstructible ??= d.building.alwaysDeconstructible;
                alwaysUninstallable ??= d.building.alwaysUninstallable;
                uninstallWork ??= d.building.uninstallWork;
                claimable ??= d.building.claimable;
                repairable ??= d.building.repairable;
                isAirtight ??= d.building.isAirtight;
                // 门字段
                canExchangeVacuum ??= d.building.canExchangeVacuum;
                poweredDoorOpenSpeedFactor ??= d.building.poweredDoorOpenSpeedFactor;
                poweredDoorCloseSpeedFactor ??= d.building.poweredDoorCloseSpeedFactor;
                unpoweredDoorOpenSpeedFactor ??= d.building.unpoweredDoorOpenSpeedFactor;
                unpoweredDoorCloseSpeedFactor ??= d.building.unpoweredDoorCloseSpeedFactor;
                // 床字段
                bed_healPerDay ??= d.building.bed_healPerDay;
                bed_maxBodySize ??= d.building.bed_maxBodySize;
                bed_caravansCanUse ??= d.building.bed_caravansCanUse;
                // 炮塔字段
                turretBurstCooldownTime ??= d.building.turretBurstCooldownTime;
                turretInitialCooldownTime ??= d.building.turretInitialCooldownTime;
                combatPower ??= d.building.combatPower;
                // 生产字段
                workTableRoomRole ??= d.building.workTableRoomRole;
                workTableNotInRoomRoleFactor ??= d.building.workTableNotInRoomRoleFactor;
                unpoweredWorkTableWorkSpeedFactor ??= d.building.unpoweredWorkTableWorkSpeedFactor;
                // 电力字段
                allowWireConnection ??= d.building.allowWireConnection;
            }
            // ThingDef 直接列表字段
            if (!IsFieldModified(nameof(recipes))) recipes ??= d.recipes;

            foreach (var cp in d.comps)
            {
                if (cp is CompProperties_Power cpp)
                {
                    basePowerConsumption ??= (float?)CompPropertiesBasePowerConsumption.GetValue(cpp);
                    shortCircuitInRain ??= cpp.shortCircuitInRain;
                    transmitsPower ??= cpp.transmitsPower;
                }
                else if (cp is CompProperties_Explosive ce)
                {
                    explosionRadius ??= ce.explosiveRadius;
                }
                else if (cp is CompProperties_Refuelable cr)
                {
                    fuelName ??= cr.fuelFilter.Summary;
                    fuelCapacity ??= cr.fuelCapacity;
                    fuelConsumptionRate ??= cr.fuelConsumptionRate;
                    fuelMultiplier ??= (float?)CompPropertiesfuelMultiplier.GetValue(cr);
                }
                else if (cp is CompProperties_Studiable studiable)
                {
                    studiableFrequencyTicks ??= studiable.frequencyTicks;
                    anomalyKnowledge ??= (float?)CompPropertiesStudiableAnomalyKnowledge.GetValue(studiable);
                }
                else if (cp is CompProperties_Activity activity)
                {
                    activityStartingRangeMin ??= activity.startingRange.min;
                    activityStartingRangeMax ??= activity.startingRange.max;
                    changePerDayBase ??= activity.changePerDayBase;
                    changePerDamage ??= activity.changePerDamage;
                }
                //else if (cp is CompProperties_HeatPusher chp)
                //{
                //    heatPerSecond ??= chp.heatPerSecond;
                //    heatPushMaxTemperature ??= chp.heatPushMaxTemperature;
                //    heatPushMinTemperature ??= chp.heatPushMinTemperature;
                //}
                else if (cp is CompProperties_Glower glower)
                {
                    glowRadius ??= glower.glowRadius;
                    overlightRadius ??= glower.overlightRadius;
                }
                else if (cp is CompProperties_TempControl temp)
                {
                    energyPerSecond ??= temp.energyPerSecond;
                    defaultTargetTemperature ??= temp.defaultTargetTemperature;
                    minTargetTemperature ??= temp.minTargetTemperature;
                    maxTargetTemperature ??= temp.maxTargetTemperature;
                }
                else if (cp is CompProperties_Schedule schedule)
                {
                    scheduleStartTime ??= schedule.startTime;
                    scheduleEndTime ??= schedule.endTime;
                }
            }
            // --- Mineable 字段读取 ---
            if (d.building != null)
            {
                mineableThing ??= d.building.mineableThing;
                mineableYield ??= d.building.mineableYield;
                veinMineable ??= d.building.veinMineable;
                mineableNonMinedEfficiency ??= d.building.mineableNonMinedEfficiency;
                mineableDropChance ??= d.building.mineableDropChance;
                mineableYieldWasteable ??= d.building.mineableYieldWasteable;
                mineableScatterCommonality ??= d.building.mineableScatterCommonality;
                mineablePreventMeteorite ??= d.building.mineablePreventMeteorite;
                mineablePreventNaturalRockOnSurface ??= d.building.mineablePreventNaturalRockOnSurface;
                isNaturalRock ??= d.building.isNaturalRock;
                isResourceRock ??= d.building.isResourceRock;
            }

        }
    }

    private bool _displayValuesEnsured;
    public override void EnsureDisplayValues()
    {
        if (_displayValuesEnsured) return;
        _displayValuesEnsured = true;
        normalMarketValue ??= (float?)GetNormalMarketValue(this);
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyBuildingProp(def);
        ApplyDefStats(def);
    }

    private void ApplyBuildingProp(ThingDef def)
    {
        ApplyV<float>(nameof(fillPercent), v => def.fillPercent = v);
        ApplyV<int>(nameof(costStuffCount), v => def.costStuffCount = v);
        ApplyL<ThingDefCountClass>(nameof(costList), v => def.costList = v);
        ApplyL<StuffCategoryDef>(nameof(stuffCategories), v => def.stuffCategories = v);

        ApplyL<ResearchProjectDef>(nameof(researchPrerequisites), v => def.researchPrerequisites = v);
        ApplyV<int>(nameof(pathCost), v => def.pathCost = v);
        // ThingDef 直接字段写回
        //if (passability.HasValue) def.passability = passability.Value;
        ApplyV<int>(nameof(constructionSkillPrerequisite), v => def.constructionSkillPrerequisite = v);
        ApplyV<bool>(nameof(holdsRoof), v => def.holdsRoof = v);
        ApplyV<bool>(nameof(blockLight), v => def.blockLight = v);
        ApplyV<bool>(nameof(blockWind), v => def.blockWind = v);
        ApplyV<bool>(nameof(destroyable), v => def.destroyable = v);
        ApplyV<bool>(nameof(selectable), v => def.selectable = v);
        ApplyV<float>(nameof(fertility), v => def.fertility = v);
        ApplyL<string>(nameof(replaceTags), v => def.replaceTags = v);
        if (def.building != null)
        {
            ApplyV<bool>(nameof(isTargetable), v => def.building.isTargetable = v);
            ApplyV<bool>(nameof(alwaysDeconstructible), v => def.building.alwaysDeconstructible = v);
            ApplyV<bool>(nameof(alwaysUninstallable), v => def.building.alwaysUninstallable = v);
            ApplyV<float>(nameof(uninstallWork), v => def.building.uninstallWork = v);
            ApplyV<bool>(nameof(claimable), v => def.building.claimable = v);
            ApplyV<bool>(nameof(repairable), v => def.building.repairable = v);
            ApplyV<bool>(nameof(isAirtight), v => def.building.isAirtight = v);
            ApplyD<RoomRoleDef>(nameof(workTableRoomRole), v => def.building.workTableRoomRole = v);
            ApplyV<float>(nameof(workTableNotInRoomRoleFactor), v => def.building.workTableNotInRoomRoleFactor = v);
            ApplyV<float>(nameof(unpoweredWorkTableWorkSpeedFactor), v => def.building.unpoweredWorkTableWorkSpeedFactor = v);
            // Door
            ApplyV<bool>(nameof(canExchangeVacuum), v => def.building.canExchangeVacuum = v);
            ApplyV<float>(nameof(poweredDoorOpenSpeedFactor), v => def.building.poweredDoorOpenSpeedFactor = v);
            ApplyV<float>(nameof(poweredDoorCloseSpeedFactor), v => def.building.poweredDoorCloseSpeedFactor = v);
            ApplyV<float>(nameof(unpoweredDoorOpenSpeedFactor), v => def.building.unpoweredDoorOpenSpeedFactor = v);
            ApplyV<float>(nameof(unpoweredDoorCloseSpeedFactor), v => def.building.unpoweredDoorCloseSpeedFactor = v);
            // Bed
            ApplyV<float>(nameof(bed_healPerDay), v => def.building.bed_healPerDay = v);
            ApplyV<float>(nameof(bed_maxBodySize), v => def.building.bed_maxBodySize = v);
            ApplyV<bool>(nameof(bed_caravansCanUse), v => def.building.bed_caravansCanUse = v);
            // Turret
            ApplyV<float>(nameof(turretBurstCooldownTime), v => def.building.turretBurstCooldownTime = v);
            ApplyV<float>(nameof(turretInitialCooldownTime), v => def.building.turretInitialCooldownTime = v);
            ApplyV<float>(nameof(combatPower), v => def.building.combatPower = v);

            // Production / Refuelable / Power
            ApplyV<bool>(nameof(allowWireConnection), v => def.building.allowWireConnection = v);

            // --- Mineable 字段写入 ---
            ApplyD<ThingDef>(nameof(mineableThing), v => def.building.mineableThing = v);
            ApplyV<int>(nameof(mineableYield), v => def.building.mineableYield = v);
            ApplyV<bool>(nameof(veinMineable), v => def.building.veinMineable = v);
            ApplyV<float>(nameof(mineableNonMinedEfficiency), v => def.building.mineableNonMinedEfficiency = v);
            ApplyV<float>(nameof(mineableDropChance), v => def.building.mineableDropChance = v);
            ApplyV<bool>(nameof(mineableYieldWasteable), v => def.building.mineableYieldWasteable = v);
            ApplyV<float>(nameof(mineableScatterCommonality), v => def.building.mineableScatterCommonality = v);
            ApplyV<bool>(nameof(mineablePreventMeteorite), v => def.building.mineablePreventMeteorite = v);
            ApplyV<bool>(nameof(mineablePreventNaturalRockOnSurface), v => def.building.mineablePreventNaturalRockOnSurface = v);
            ApplyV<bool>(nameof(isNaturalRock), v => def.building.isNaturalRock = v);
            ApplyV<bool>(nameof(isResourceRock), v => def.building.isResourceRock = v);
        }
        // ThingDef 非 building 字段写回

        ApplyL<RecipeDef>(nameof(recipes), v => def.recipes = v);
        if (def is ThingDef d)
        {
            foreach (var cp in d.comps)
            {
                if (cp is CompProperties_Power cpp)
                {
                    ApplyV<float>(nameof(basePowerConsumption), v => CompPropertiesBasePowerConsumption.SetValue(cpp, v));
                    ApplyV<bool>(nameof(shortCircuitInRain), v => cpp.shortCircuitInRain = v);
                    ApplyV<bool>(nameof(transmitsPower), v => cpp.transmitsPower = v);
                }
                else if (cp is CompProperties_Explosive ce)
                {
                    ApplyV<float>(nameof(explosionRadius), v => ce.explosiveRadius = v);
                }
                else if (cp is CompProperties_Refuelable cr)
                {
                    ApplyV<float>(nameof(fuelCapacity), v => cr.fuelCapacity = v);
                    ApplyV<float>(nameof(fuelConsumptionRate), v => cr.fuelConsumptionRate = v);
                    ApplyV<float>(nameof(fuelMultiplier), v => CompPropertiesfuelMultiplier.SetValue(cr, v));
                }
                else if (cp is CompProperties_Studiable studiable)
                {
                    ApplyV<int>(nameof(studiableFrequencyTicks), v => studiable.frequencyTicks = v);
                    ApplyN<float>(nameof(anomalyKnowledge), v => CompPropertiesStudiableAnomalyKnowledge.SetValue(studiable, v));
                }
                else if (cp is CompProperties_Activity activity)
                {
                    ApplyV<float>(nameof(activityStartingRangeMin),
                        v => activity.startingRange = new FloatRange(v, activityStartingRangeMax ?? activity.startingRange.max));
                    ApplyV<float>(nameof(activityStartingRangeMax),
                        v => activity.startingRange = new FloatRange(activityStartingRangeMin ?? activity.startingRange.min, v));
                    ApplyV<float>(nameof(changePerDayBase), v => activity.changePerDayBase = v);
                    ApplyV<float>(nameof(changePerDamage), v => activity.changePerDamage = v);
                }
                //else if (cp is CompProperties_HeatPusher chp)
                //{
                //    if (heatPerSecond.HasValue) chp.heatPerSecond = heatPerSecond.Value;
                //    if (heatPushMaxTemperature.HasValue) chp.heatPushMaxTemperature = heatPushMaxTemperature.Value;
                //    if (heatPushMinTemperature.HasValue) chp.heatPushMinTemperature = heatPushMinTemperature.Value;
                //}
                else if (cp is CompProperties_Glower glower)
                {
                    ApplyV<float>(nameof(glowRadius), v => glower.glowRadius = v);
                    ApplyV<float>(nameof(overlightRadius), v => glower.overlightRadius = v);
                }
                else if (cp is CompProperties_TempControl temp)
                {
                    ApplyV<float>(nameof(energyPerSecond), v => temp.energyPerSecond = v);
                    ApplyV<float>(nameof(defaultTargetTemperature), v => temp.defaultTargetTemperature = v);
                    ApplyV<float>(nameof(minTargetTemperature), v => temp.minTargetTemperature = v);
                    ApplyV<float>(nameof(maxTargetTemperature), v => temp.maxTargetTemperature = v);
                }
                else if (cp is CompProperties_Schedule schedule)
                {
                    ApplyV<float>(nameof(scheduleStartTime), v => schedule.startTime = v);
                    ApplyV<float>(nameof(scheduleEndTime), v => schedule.endTime = v);
                }
            }
        }

    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(BuildingType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum BuildingType
    {
        Nature,
        Mineable,
        Building,
        PowerBuilding,
        Turret,
        Production,
        Door,
        Bed,
        LightSource,
        TemperatureControl,
        Anomaly,
        Frame,
        Prop,
    }
    public static bool AvailableIfPower(TweakData data) => data.propType switch
    {
        (int)BuildingType.PowerBuilding => true,
        (int)BuildingType.Turret => true,
        (int)BuildingType.Door => true,
        (int)BuildingType.LightSource => true,
        (int)BuildingType.TemperatureControl => true,
        (int)BuildingType.Production => true,
        _ => false,
    };

    public static bool AvailableIfPowerBuilding(TweakData data) => data.propType switch
    {
        (int)BuildingType.PowerBuilding => true,
        _ => false,
    };

    public static bool AvailableIfTurret(TweakData data) => data.propType switch
    {
        (int)BuildingType.Turret => true,
        _ => false,
    };

    public static bool AvailableIfAnomaly(TweakData data) => data.propType switch
    {
        (int)BuildingType.Anomaly => true,
        _ => false,
    };

    public static bool AvailableIfDoor(TweakData data) => data.propType switch
    {
        (int)BuildingType.Door => true,
        _ => false,
    };

    public static bool AvailableIfBed(TweakData data) => data.propType switch
    {
        (int)BuildingType.Bed => true,
        _ => false,
    };

    public static bool AvailableIfLightSource(TweakData data) => data.propType switch
    {
        (int)BuildingType.LightSource => true,
        _ => false,
    };

    public static bool AvailableIfTemperatureControl(TweakData data) => data.propType switch
    {
        (int)BuildingType.TemperatureControl => true,
        _ => false,
    };

    public static bool AvailableIfProduction(TweakData data) => data.propType switch
    {
        (int)BuildingType.Production => true,
        _ => false,
    };

    public static bool AvailableIfMineable(TweakData data) => data.propType switch
    {
        (int)BuildingType.Mineable => true,
        _ => false,
    };

    public override int GetPropType()
    {
        if (this.def is ThingDef def && def.building != null)
        {
            if (def.IsFrame)
            {
                return (int)BuildingType.Frame;
            }
            if (def.thingClass.Namespace == "VFEProps")
            {
                return (int)BuildingType.Prop;
            }
            if (def.GetCompProperties<CompProperties_Studiable>()?.frequencyTicks > 0)
            {
                return (int)BuildingType.Anomaly;
            }
            if (def.mineable || def.building.isNaturalRock || def.building.isResourceRock)
            {
                return (int)BuildingType.Mineable;
            }
            if (def.building.turretGunDef is ThingDef g)
            {
                TweakDatabase.turretBuildingDatas.TryAdd(g.defName, id);
                return (int)BuildingType.Turret;
            }
            if (def.IsDoor)
            {
                return (int)BuildingType.Door;
            }
            if (def.IsBed)
            {
                return (int)BuildingType.Bed;
            }
            if (def.GetCompProperties<CompProperties_TempControl>() != null)
            {
                return (int)BuildingType.TemperatureControl;
            }
            if (def.IsWorkTable)
            {
                return (int)BuildingType.Production;
            }
            var p = def.GetCompProperties<CompProperties_Power>();
            if (def.GetCompProperties<CompProperties_Glower>() != null && ((p?.PowerConsumption ?? 1f) > 0f))
            {
                return (int)BuildingType.LightSource;
            }
            if (p != null)
            {
                return (int)BuildingType.PowerBuilding;
            }
            if (def.designationCategory == null)
            {
                return (int)BuildingType.Nature;
            }
        }
        return (int)BuildingType.Building;
    }
}


