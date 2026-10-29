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
    /// <summary>
    /// 炮塔瞄准（预热）时长区间，单位秒。原版 <c>Building_TurretGun</c> 每次开火前取 <c>RandomInRange</c>；
    /// 枪自己的 <c>verb.warmupTime</c> 对炮塔**不生效**（<c>Verb.TryStartCastOn</c> 的 warmup 只在
    /// <c>CasterIsPawn</c> 时应用，炮塔不是 pawn）。武器页签里炮塔枪的 DPS 就按这里的平均值算。
    /// </summary>
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Range)]
    public FloatRange? turretBurstWarmupTime = null;
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Prec)]
    public float? turretBurstCooldownTime = null;
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Prec)]
    public float? turretInitialCooldownTime = null;
    [TweakField(Available = nameof(AvailableIfTurret), Style = ColumnStyle.Int)]
    public float? combatPower = null;

    /// <summary>
    /// 燃料性价比：每消耗 1 份燃料物资能打出的名义伤害 = 单次齐射总伤害 ÷ 单次齐射烧掉的物资份数。
    /// <para>
    /// 只读派生列。燃料按发（<c>VerbProperties.consumeFuelPerShot</c>）与按齐射（<c>consumeFuelPerBurst</c>）
    /// 两种扣法都计入分母，两者的和才是"打完一轮"的真实开销。都不为 0 才轮得到这个比值 ——
    /// 原版燃料炮塔全是"每发 1 燃料"或"每轮 1 燃料"，不吃燃料的炮塔（含按天连续烧的）显示为空。
    /// </para>
    /// <para>
    /// 分子与武器页的伤害列同源（弹丸的 <c>damageAmountBase</c>），<b>不含命中率、不含爆炸衰减</b>；
    /// 迫击炮这类伤害取决于装填炮弹的炮塔在 def 上没有 <c>defaultProjectile</c>，同样留空。
    /// </para>
    /// <para>
    /// 分母换算成<b>物资份数</b>：开火扣的是燃料槽点数，换料时 1 份物资折算
    /// <c>CompProperties_Refuelable.fuelMultiplier</c> 点。所以调燃料倍率这一列会跟着动 ——
    /// 它回答的是"这场炮火每烧掉一份枪管 / 弹片 / 火箭，换来多少伤害"。
    /// </para>
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfTurret))]
    public float? damagePerFuel = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? replaceTags = null;
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? thingSetMakerTags = null;
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
    // 四个字段都只对带 CompProperties_Refuelable 的建筑有效；Apply 在 `cp is CompProperties_Refuelable`
    // 分支里才写回，其它建筑改这几列不会有任何效果（值也留空）。
    // 燃料有两个量纲，别混：
    //   · 点（fuel）：燃料槽的计量单位，开火时按 consumeFuelPerShot / consumeFuelPerBurst 直接扣，不乘任何倍率；
    //   · 份（item）：换料时搬的物资个数，1 份折算 fuelMultiplier 点（CompRefuelable.Refuel）。
    // 下面 fuelCapacity 是点，fuelConsumptionRate 是点/天，fuelMultiplier 是点/份 ——
    // 「每燃料伤害」按"份"算，所以它衡量的正是每份物资换多少伤害，即性价比。

    /// <summary>换料消耗的物资（<c>fuelFilter.Summary</c>）。只读；也是「每燃料伤害」里"1 份"的单位。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    public string? fuelName = null;
    /// <summary>
    /// 燃料槽容量（<b>点</b>）。满槽能打几轮 = 容量 ÷ 一轮扣的点数（每发燃料 × 发数 + 每轮燃料）。
    /// 只影响"多久加一次料"，不影响「每燃料伤害」。
    /// </summary>
    [TweakField()]
    public float? fuelCapacity = null;
    /// <summary>
    /// 每 1 份燃料物资折算多少<b>点</b>进燃料槽（点/份）。<c>CompRefuelable.Refuel</c> 里
    /// <c>fuel += amount * this</c>，只作用于装填，不影响开火扣点 —— 所以它是物价换算：
    /// 调大它，同一份物资换更多轮开火，「每燃料伤害」随之线性变大。
    /// 原版该字段是 private，走反射读写。
    /// </summary>
    [TweakField()]
    public float? fuelMultiplier = null;
    /// <summary>
    /// 闲置时每天连续消耗的<b>点</b>数（点/天，不是倍率）。只在 <c>consumeFuelOnlyWhenUsed = false</c>
    /// 的建筑（发电机等）上有意义；炮塔按发扣点，这一项对它们无效。
    /// </summary>
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
            if (!IsFieldModified(nameof(thingSetMakerTags))) thingSetMakerTags ??= d.thingSetMakerTags;
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
                turretBurstWarmupTime ??= d.building.turretBurstWarmupTime;
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

    public override void EnsureDisplayValues()
    {
        if (def == null) return;
        if (!displayValuesEnsured)
        {
            displayValuesEnsured = true;
            normalMarketValue = (float?)GetNormalMarketValue(this);
        }
        // damagePerFuel 不能跟上面那个一次性开关走：它随武器页的伤害 / 连发数与燃料设置变化，
        // `??=` 会把它冻在第一次算出的值上（改完枪回来，炮塔页还显示旧比值）。
        // 重算成本只是几次 def 读取，跟着列表刷新算一遍无所谓。
        damagePerFuel = def is ThingDef d ? ComputeDamagePerFuel(d) : null;
    }

    /// <summary>
    /// 每消耗 1 <b>份燃料物资</b>能打出的名义伤害。
    /// <para>
    /// <b>一律读 def，不读 TweakData</b>：<see cref="Apply"/> 已经把用户的改动写进 def 了，def 就是
    /// "游戏下一次开火会用的值"；而 <see cref="WeaponData"/> 里的伤害 / 连发数是带原始-活实例双层的镜像，
    /// 在 <see cref="EnsureDisplayValues"/> 的调用时机（可能早于 Apply）会读到半新半旧的组合。
    /// </para>
    /// <para>
    /// 分子是<b>裸伤害</b>、不含命中率 —— 与武器页 MaxDPS 同口径。命中率是另一个维度，
    /// 乘进来会把"省不省燃料"和"打不打得中"混成一个数，反而不利于判断。
    /// </para>
    /// <para>
    /// 分母换算成<b>物资份数</b>：开火扣的是燃料槽点数（<c>consumeFuelPerShot</c> / <c>consumeFuelPerBurst</c>），
    /// 而换料时 1 份物资折算 <c>CompProperties_Refuelable.fuelMultiplier</c> 点（<c>CompRefuelable.Refuel</c>）。
    /// 所以份数 = 点数 ÷ 倍率 —— 调倍率必须让这一列跟着动，否则它就答不了"换个枪管划不划算"。
    /// </para>
    /// <para>
    /// 不含 <c>fuelMultiplier / difficulty.maintenanceCostFactor</c> 那层难度系数：那是全局的运行时系数，
    /// 掺进来会让同一件东西在不同难度下显示不同的数，不适合当平衡指标。
    /// </para>
    /// <para>取不到（非炮塔 / 无枪 / 不吃燃料 / 伤害为 0）时返回 null，单元格留空。</para>
    /// </summary>
    private static float? ComputeDamagePerFuel(ThingDef def)
    {
        if (def.building?.turretGunDef is not ThingDef gunDef) return null;
        // 必须真的有燃料槽组件：开火扣燃料那一步是
        // <c>caster.TryGetComp&lt;CompRefuelable&gt;()?.ConsumeFuel(...)</c>，
        // 没有这个组件时枪上的 consumeFuelPerShot 是死值 —— 原版战术炮塔（Anomaly）的
        // Gun_TacticalTurret 就写着每发 1 燃料，但建筑靠 CompProperties_MechPowerCell 电池供能，
        // 从不扣燃料。不看组件就会给它算出一个根本不存在的"每燃料伤害"。
        if (def.GetCompProperties<CompProperties_Refuelable>() is not CompProperties_Refuelable refuelable) return null;
        // 与 WeaponData.SetDef / GenValues 取 verb 的规则保持一致（Building_TurretGun.AttackVerb
        // = GunCompEq.PrimaryVerb，原版炮塔枪的第一个非近战 verb 就是它）。
        var verb = gunDef.Verbs?.FirstOrDefault(v => !v.IsMeleeAttack);
        if (verb == null) return null;

        float fuelPerShot = verb.consumeFuelPerShot;
        float fuelPerBurst = verb.consumeFuelPerBurst;
        if (fuelPerShot <= 0f && fuelPerBurst <= 0f) return null;

        int shots = verb.burstShotCount < 1 ? 1 : verb.burstShotCount;
        float damage = ShotDamage(verb);
        if (damage <= 0f) return null;

        // 点火一轮扣的点数 ÷ 每份物资折算的点数 = 这一轮烧掉的物资份数。
        return shots * damage / (fuelPerShot * shots + fuelPerBurst) * FuelPerItem(refuelable);
    }

    /// <summary>
    /// 1 份燃料物资折算成多少点燃料槽（<c>CompProperties_Refuelable.fuelMultiplier</c>）。
    /// 字段是 private 的（原版没暴露），走反射；值非正（坏 XML）时按 1 处理，别把整列算成 0。
    /// </summary>
    private static float FuelPerItem(CompProperties_Refuelable refuelable)
    {
        if (CompPropertiesfuelMultiplier.GetValue(refuelable) is float m && m > 0f) return m;
        return 1f;
    }

    /// <summary>
    /// 单发伤害：优先弹丸（原版炮塔全走这条），其次光束。
    /// <para>
    /// 与 <see cref="WeaponData.GenRangeDPS"/> 同一条判据：既不打生命也不造成眩晕的"伤害"
    /// （泡沫炮的 <c>Extinguish</c> 写着 9999 伤害，实际只是灭火）拿去当分子只会得到一个荒谬的大数，
    /// 这类炮塔按"无伤害"处理，整列留空。
    /// </para>
    /// </summary>
    private static float ShotDamage(VerbProperties verb)
    {
        if (verb.defaultProjectile?.projectile is ProjectileProperties p && p.damageDef != null)
        {
            if (!p.damageDef.causeStun && !p.damageDef.harmsHealth) return 0f;
            return p.GetDamageAmount(null, null, null);
        }
        if (verb.beamDamageDef != null)
            return verb.beamTotalDamage > 0f ? verb.beamTotalDamage : verb.beamDamageDef.defaultDamage;
        return 0f;
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyBuildingProp(def);
        ApplyDefStats(def);
        // 炮塔的射击节奏（冷却 / 预热）写在建筑上，挂载的那把枪只是据它算 DPS。
        // 改完必须主动刷那一行，否则武器页签的 DPS / 强度停在旧值 —— 与弹丸改动后
        // 刷新共用武器（ProjectileData.RefreshSharedWeapons）是同一条道理。
        if (TurretRateTouched) RefreshTurretGun();
        // 参考价值按"当前 def 现算"：改完造价后，下一次读就该是新值，不能冻在改之前那次计算上。
        displayValuesEnsured = false;
    }

    /// <summary>
    /// 本次 Apply 是否动过"决定炮塔射速"的字段（预热 / 冷却）。
    /// <para>
    /// 比三态多判两项，是因为"重置"（<see cref="StatColumnConfig.ResetTo"/> 的单格还原、
    /// 整行重置 <see cref="TweakData.forceWriteAll"/>）都是先把字段写回原值、再清掉标记，
    /// 那一刻三态已经是 Unset —— 只看 <c>IsFieldModified</c> 会把重置漏掉。
    /// </para>
    /// </summary>
    private bool TurretRateTouched
        => RateFieldTouched(nameof(turretBurstWarmupTime)) || RateFieldTouched(nameof(turretBurstCooldownTime));

    private bool RateFieldTouched(string field)
        => IsFieldModified(field) || forceWriteAll || forceWriteFields?.Contains(field) == true;

    /// <summary>
    /// 刷新挂载武器（<see cref="TurretGun"/>）的派生值：DPS / 强度。
    /// <para>
    /// <b>原始快照跳过</b>：<see cref="WeaponData.GenValues"/> 会连镜像列（伤害/穿透）一起重写，
    /// 而快照是 <c>Revert</c> / <c>ResetTo</c> 的原值基准。活实例（UI 缓存里的克隆、已存修改实例）照常刷。
    /// </para>
    /// <para>未 SetDef 的枪行也跳过：那时候算出来的是野值（同一类坑见 WeaponData.GenValues 里 isCompQuality 的注释）。</para>
    /// </summary>
    private void RefreshTurretGun()
    {
        if (TurretGun == null) return;
        if (GetLiveData(TurretGun) is not WeaponData gun) return;
        if (gun.def == null) return;
        if (ReferenceEquals(TweakDatabase.GetOriginal(gun.id), gun)) return;
        gun.GenValues();
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
        ApplyL<string>(nameof(thingSetMakerTags), v => def.thingSetMakerTags = v);
        if (IsFieldModified(nameof(thingSetMakerTags))) DataUtility.RefreshThingSetMakerFilters();
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
            // 区间：Cleared（清空）走 ApplyV 的 Revert —— 还原原区间，而不是写成 null，
            // 因为 def 上的这个字段是不可空的 FloatRange，null 没有对应的游戏状态。
            ApplyV<FloatRange>(nameof(turretBurstWarmupTime), v => def.building.turretBurstWarmupTime = v);
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


