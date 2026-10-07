using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
    static FieldInfo CompPropertiesBasePowerConsumption = AccessTools.Field(typeof(CompProperties_Power), "basePowerConsumption");
    static FieldInfo CompPropertiesfuelMultiplier = AccessTools.Field(typeof(CompProperties_Refuelable), "fuelMultiplier");
    static FieldInfo CompPropertiesStudiableAnomalyKnowledge = AccessTools.Field(typeof(CompProperties_Studiable), "anomalyKnowledge");

    public override void SetDef(Def def, BalanceTweakSettings.SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            fillPercent = d.fillPercent;
            pathCost ??= d.pathCost;
            costStuffCount ??= d.costStuffCount;
            costList ??= d.costList;
            stuffCategories ??= d.stuffCategories;
            researchPrerequisites ??= d.researchPrerequisites;
            normalMarketValue ??= (float?)GetNormalMarketValue(this);
            statBases ??= d.statBases;
            replaceTags ??= d.replaceTags;
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
            recipes ??= d.recipes;

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

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        ApplyBuildingProp(def);
        ApplyDefStats(def);
    }

    private void ApplyBuildingProp(ThingDef def)
    {
        if (fillPercent.HasValue) { def.fillPercent = fillPercent.Value; }
        if (costStuffCount.HasValue) { def.costStuffCount = costStuffCount.Value; }
        if (costList != null) { def.costList = costList; }
        if (stuffCategories != null) { def.stuffCategories = stuffCategories; }

        if (researchPrerequisites != null) def.researchPrerequisites = researchPrerequisites;
        if (pathCost.HasValue) def.pathCost = pathCost.Value;
        // ThingDef 直接字段写回
        //if (passability.HasValue) def.passability = passability.Value;
        if (constructionSkillPrerequisite.HasValue) def.constructionSkillPrerequisite = constructionSkillPrerequisite.Value;
        if (holdsRoof.HasValue) def.holdsRoof = holdsRoof.Value;
        if (blockLight.HasValue) def.blockLight = blockLight.Value;
        if (blockWind.HasValue) def.blockWind = blockWind.Value;
        if (destroyable.HasValue) def.destroyable = destroyable.Value;
        if (selectable.HasValue) def.selectable = selectable.Value;
        if (fertility.HasValue) def.fertility = fertility.Value;
        if (replaceTags != null) def.replaceTags = replaceTags;
        if (def.building != null)
        {
            if (isTargetable.HasValue) def.building.isTargetable = isTargetable.Value;
            if (alwaysDeconstructible.HasValue) def.building.alwaysDeconstructible = alwaysDeconstructible.Value;
            if (alwaysUninstallable.HasValue) def.building.alwaysUninstallable = alwaysUninstallable.Value;
            if (uninstallWork.HasValue) def.building.uninstallWork = uninstallWork.Value;
            if (claimable.HasValue) def.building.claimable = claimable.Value;
            if (repairable.HasValue) def.building.repairable = repairable.Value;
            if (isAirtight.HasValue) def.building.isAirtight = isAirtight.Value;
            if (workTableRoomRole != null) def.building.workTableRoomRole = workTableRoomRole;
            if (workTableNotInRoomRoleFactor.HasValue) def.building.workTableNotInRoomRoleFactor = workTableNotInRoomRoleFactor.Value;
            if (unpoweredWorkTableWorkSpeedFactor.HasValue) def.building.unpoweredWorkTableWorkSpeedFactor = unpoweredWorkTableWorkSpeedFactor.Value;
            // Door
            if (canExchangeVacuum.HasValue) def.building.canExchangeVacuum = canExchangeVacuum.Value;
            if (poweredDoorOpenSpeedFactor.HasValue) def.building.poweredDoorOpenSpeedFactor = poweredDoorOpenSpeedFactor.Value;
            if (poweredDoorCloseSpeedFactor.HasValue) def.building.poweredDoorCloseSpeedFactor = poweredDoorCloseSpeedFactor.Value;
            if (unpoweredDoorOpenSpeedFactor.HasValue) def.building.unpoweredDoorOpenSpeedFactor = unpoweredDoorOpenSpeedFactor.Value;
            if (unpoweredDoorCloseSpeedFactor.HasValue) def.building.unpoweredDoorCloseSpeedFactor = unpoweredDoorCloseSpeedFactor.Value;
            // Bed
            if (bed_healPerDay.HasValue) def.building.bed_healPerDay = bed_healPerDay.Value;
            if (bed_maxBodySize.HasValue) def.building.bed_maxBodySize = bed_maxBodySize.Value;
            if (bed_caravansCanUse.HasValue) def.building.bed_caravansCanUse = bed_caravansCanUse.Value;
            // Turret
            if (turretBurstCooldownTime.HasValue) def.building.turretBurstCooldownTime = turretBurstCooldownTime.Value;
            if (turretInitialCooldownTime.HasValue) def.building.turretInitialCooldownTime = turretInitialCooldownTime.Value;
            if (combatPower.HasValue) def.building.combatPower = combatPower.Value;

            // Production / Refuelable / Power
            if (allowWireConnection.HasValue) def.building.allowWireConnection = allowWireConnection.Value;

            // --- Mineable 字段写入 ---
            if (mineableThing != null) def.building.mineableThing = mineableThing;
            if (mineableYield.HasValue) def.building.mineableYield = mineableYield.Value;
            if (veinMineable.HasValue) def.building.veinMineable = veinMineable.Value;
            if (mineableNonMinedEfficiency.HasValue) def.building.mineableNonMinedEfficiency = mineableNonMinedEfficiency.Value;
            if (mineableDropChance.HasValue) def.building.mineableDropChance = mineableDropChance.Value;
            if (mineableYieldWasteable.HasValue) def.building.mineableYieldWasteable = mineableYieldWasteable.Value;
            if (mineableScatterCommonality.HasValue) def.building.mineableScatterCommonality = mineableScatterCommonality.Value;
            if (mineablePreventMeteorite.HasValue) def.building.mineablePreventMeteorite = mineablePreventMeteorite.Value;
            if (mineablePreventNaturalRockOnSurface.HasValue) def.building.mineablePreventNaturalRockOnSurface = mineablePreventNaturalRockOnSurface.Value;
            if (isNaturalRock.HasValue) def.building.isNaturalRock = isNaturalRock.Value;
            if (isResourceRock.HasValue) def.building.isResourceRock = isResourceRock.Value;
        }
        // ThingDef 非 building 字段写回

        if (recipes != null) def.recipes = recipes;
        if (def is ThingDef d)
        {
            foreach (var cp in d.comps)
            {
                if (cp is CompProperties_Power cpp)
                {
                    if (basePowerConsumption.HasValue) CompPropertiesBasePowerConsumption.SetValue(cpp, basePowerConsumption.Value);
                    if (shortCircuitInRain.HasValue) cpp.shortCircuitInRain = shortCircuitInRain.Value;
                    if (transmitsPower.HasValue) cpp.transmitsPower = transmitsPower.Value;
                }
                else if (cp is CompProperties_Explosive ce)
                {
                    if (explosionRadius.HasValue) ce.explosiveRadius = explosionRadius.Value;
                }
                else if (cp is CompProperties_Refuelable cr)
                {
                    if (fuelCapacity.HasValue) cr.fuelCapacity = fuelCapacity.Value;
                    if (fuelConsumptionRate.HasValue) cr.fuelConsumptionRate = fuelConsumptionRate.Value;
                    if (fuelMultiplier.HasValue) CompPropertiesfuelMultiplier.SetValue(cr, fuelMultiplier.Value);
                }
                else if (cp is CompProperties_Studiable studiable)
                {
                    if (studiableFrequencyTicks.HasValue) studiable.frequencyTicks = studiableFrequencyTicks.Value;
                    if (anomalyKnowledge.HasValue) CompPropertiesStudiableAnomalyKnowledge.SetValue(studiable, anomalyKnowledge.Value);
                }
                else if (cp is CompProperties_Activity activity)
                {
                    if (activityStartingRangeMin.HasValue || activityStartingRangeMax.HasValue)
                    {
                        activity.startingRange = new FloatRange(
                            activityStartingRangeMin ?? activity.startingRange.min,
                            activityStartingRangeMax ?? activity.startingRange.max);
                    }
                    if (changePerDayBase.HasValue) activity.changePerDayBase = changePerDayBase.Value;
                    if (changePerDamage.HasValue) activity.changePerDamage = changePerDamage.Value;
                }
                //else if (cp is CompProperties_HeatPusher chp)
                //{
                //    if (heatPerSecond.HasValue) chp.heatPerSecond = heatPerSecond.Value;
                //    if (heatPushMaxTemperature.HasValue) chp.heatPushMaxTemperature = heatPushMaxTemperature.Value;
                //    if (heatPushMinTemperature.HasValue) chp.heatPushMinTemperature = heatPushMinTemperature.Value;
                //}
                else if (cp is CompProperties_Glower glower)
                {
                    if (glowRadius.HasValue) glower.glowRadius = glowRadius.Value;
                    if (overlightRadius.HasValue) glower.overlightRadius = overlightRadius.Value;
                }
                else if (cp is CompProperties_TempControl temp)
                {
                    if (energyPerSecond.HasValue) temp.energyPerSecond = energyPerSecond.Value;
                    if (defaultTargetTemperature.HasValue) temp.defaultTargetTemperature = defaultTargetTemperature.Value;
                    if (minTargetTemperature.HasValue) temp.minTargetTemperature = minTargetTemperature.Value;
                    if (maxTargetTemperature.HasValue) temp.maxTargetTemperature = maxTargetTemperature.Value;
                }
                else if (cp is CompProperties_Schedule schedule)
                {
                    if (scheduleStartTime.HasValue) schedule.startTime = scheduleStartTime.Value;
                    if (scheduleEndTime.HasValue) schedule.endTime = scheduleEndTime.Value;
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


