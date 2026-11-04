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

    // ── Fortified 中继指挥（可选 Mod，见 RelayConfigWindow）──
    // 建筑可能挂 SubRelay / Overseer / OverseerFacility 三种 Comp；只留一个只读触发列，
    // 其余字段 NotAColumn，由窗口按实际存在的 Comp 分节编辑。

    /// <summary>
    /// 只读触发列：打开中继配置窗口。即使没有任何中继 Comp 也要能打开窗口来新增 Comp，
    /// 所以不做行级留白。
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(RelayConfigWindow), MayRequire = TweakDatabase.FFF)]
    public TweakID? relayConfig = null;

    // 中继 Comp 的新增 / 移除开关（与 ApparelData.isCompQuality 同一模式）。
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasSubRelay = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasOverseer = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasOverseerFacility = null;

    // 宿主 stat：带宽 / 控制组 / 指挥范围由 ApplyDefStats 唯一写入；
    // NotAColumn = 不进表格，改到 RelayConfigWindow 里编辑。
    [TweakField(NotAColumn = true, StatDef = "MechBandwidth", Style = ColumnStyle.Int,
        MayRequire = "ludeon.rimworld.biotech")]
    public float? mechBandwidth = null;
    [TweakField(NotAColumn = true, StatDef = "MechControlGroups", Style = ColumnStyle.Int,
        MayRequire = "ludeon.rimworld.biotech")]
    public float? mechControlGroups = null;
    [TweakField(NotAColumn = true, StatDef = "FFF_MechCommandRange",
        MayRequire = TweakDatabase.FFF)]
    public float? mechCommandRange = null;

    // 以下字段由 RelayConfigWindow 编辑，只在存档里存在，不进表格。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? subRelayRange = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? subRelayCoverWholeMap = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? overseerCanRepair = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? overseerTicksPerHeal = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? overseerInstantControl = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? facilityMaxSimultaneous = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? facilityMaxDistance = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? facilityRequiresLOS = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? facilityMustBePlacedAdjacent = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.StatModList, MayRequire = TweakDatabase.FFF)]
    public List<StatModifier>? facilityStatOffsets = null;

    /// <summary>中继 Comp 的引用（SetDef 时解析一次；闭包里只捕获 object）。</summary>
    private object? subRelayProps = null;
    private object? overseerProps = null;
    private object? overseerFacilityProps = null;

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

    // ── Atmosphere Overhaul：建筑（可选 Mod，见 AtmosphereConfigWindow）──
    // AO 给建筑挂了 9 个 CompProperties + 1 个 BuildingAtmosphereExtension，一共 57 个数值，
    // 但每个建筑只命中其中 2~10 个：全部做成列会让建筑页签多 57 列恒空白，列宽与列序被永久占住。
    // 做法与 P2 一致 —— 只留一个只读触发列，字段全部 NotAColumn，窗口按"实际挂了哪些 Comp"分节。

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(AtmosphereConfigWindow), MayRequire = TweakDatabase.AO,
        Hidden = nameof(HiddenIfNoAoBuildingData))]
    public TweakID? aoBuildingConfig = null;

    public static bool HiddenIfNoAoBuildingData(TweakData data) => !AoBuilding.HasAny(data.def);

    // 风扇（CompProperties_SingleFieldFan）：4 个风扇共用这一个 Comp，靠字段值区分。
    /// <summary>供能方式。<c>Electricity</c> = 接电网；<c>StoredEnergy</c> = 发条储能；<c>Unlimited</c> = 不耗能。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.Enum, OptionsList = nameof(FanPowerSourceOptions),
        MayRequire = TweakDatabase.AO)]
    public string? fanPowerSource = null;
    /// <summary>下拉数据源。存字符串而不是枚举：类型来自 AO 的程序集，字段类型不能用它。</summary>
    public static readonly List<string> FanPowerSourceOptions = ["Electricity", "StoredEnergy", "Unlimited"];

    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanMaxPowerWatts = null;
    /// <summary>默认输出功率。<c>allowOutputAdjustment = true</c> 时只影响新建筑（老建筑用 gizmo 那份值）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanDefaultPowerWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanRatedFreeAirFlow = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanOutletAreaM2 = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanWindResponsePerTick = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? fanAllowEasyRotation = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? fanAllowOutputAdjustment = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanStoredEnergyCapacityJoules = null;
    /// <summary>初始储能：只在生成时给一次，改它不影响已建成的建筑。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fanInitialStoredEnergyJoules = null;

    // 涡轮喷气（CompProperties_PairedJet）。几何（inletSize / outletSize / offset）与四个灯光字段不做。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetFuelPerSecond = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetWaterKgPerFuel = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetMaxSpeed = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetHalfSpeed = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetMaxTemperature = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetMaxPressureRatio = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetHalfPressureRatio = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetInletPressureRatio = null;
    /// <summary>排气氧含量，**按百分比数值**填（13 = 13%），不是 0~1。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetOxygenPercent = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetColumnHeight = null;
    /// <summary>每秒烧掉进气里氧气的百分比数值（8 = 8%/秒）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetOxygenConsumptionPercent = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetFlameoutOxygenKPa = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? jetRelightOxygenKPa = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? jetAllowEasyRotation = null;

    // 空压机 / PSA（CompProperties_AirCompressor）。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? compressorIntakeM3PerTick = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? compressorRunningWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? compressorIdleWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? compressorMaxGoalKPa = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? compressorSeparateOxygen = null;
    /// <summary>产出的气瓶 Def；留空 = AO 的 <c>AO_AirTank</c>。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.DefSelector, MayRequire = TweakDatabase.AO)]
    public ThingDef? compressorProductDef = null;
    /// <summary>分离出的氧气瓶 Def；留空 = AO 的 <c>AO_OxygenTank</c>。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.DefSelector, MayRequire = TweakDatabase.AO)]
    public ThingDef? compressorOxygenProductDef = null;

    // 气瓶（CompProperties_GasTank）。
    /// <summary>瓶内氧含量百分比数值（21 = 空气）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? gasTankOxygenPercent = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? gasTankExplodeAboveCelsius = null;

    // 加湿器（CompProperties_Humidifier）。目标湿度是 CompHumidifier 上的存档值，不是 Def 字段。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? humidifierMaxKgPerHour = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? humidifierRunningWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? humidifierIdleWatts = null;

    // 除湿机（CompProperties_Dehumidifier）。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? dehumidifierSweptVolumes = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? dehumidifierRunningWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? dehumidifierIdleWatts = null;

    // 发条（CompProperties_Mainspring）。windingTicks 每 tick 现读；autoRewind 在 Initialize 时抄进实例并存档。
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? mainspringWindingTicks = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? mainspringAutoRewind = null;

    // 集雨器（CompProperties_RainCollector）。容量单位是"水单位"，1 单位 = 0.25 kg。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? rainCollectorCatchmentM2 = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? rainCollectorCapacityUnits = null;

    // 灌溉器（CompProperties_Irrigator）。
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? irrigatorSprayRadius = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? irrigatorPumpRadius = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? irrigatorSprayTicks = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? irrigatorSessionMm = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? irrigatorCheckIntervalTicks = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? irrigatorRestTicks = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? irrigatorSprayWatts = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? irrigatorIdleWatts = null;

    // 建筑大气属性（BuildingAtmosphereExtension）：原版建筑里**唯一**能改的一组。
    /// <summary>压力耐受倍率（× HP 推导值）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? buildingPressureTolerance = null;
    /// <summary>抗风倍率（× HP 推导值）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? buildingWindTolerance = null;
    /// <summary>漏气倍率，0 = 完全气密。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? buildingAirLeak = null;
    /// <summary>换热倍率，0 = 完全隔热。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? buildingHeatExchange = null;
    /// <summary>是否允许空气穿过（与小人能否通过无关）。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.AO)]
    public bool? buildingAirPassable = null;
    /// <summary>空气阻力用的填充率；-1 = 沿用 def 的 <c>fillPercent</c>。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? buildingAirFillPercent = null;

    // 上面这些 Comp / 扩展的引用（SetDef 时解析一次；闭包里只捕获 object）。
    private object? fanProps = null;
    private object? jetProps = null;
    private object? compressorProps = null;
    private object? gasTankProps = null;
    private object? humidifierProps = null;
    private object? dehumidifierProps = null;
    private object? mainspringProps = null;
    private object? rainCollectorProps = null;
    private object? irrigatorProps = null;
    private object? buildingAtmosphereExt = null;

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

            LoadRelay(d);
            LoadAoBuilding(d);
            relayHasSubRelay ??= subRelayProps != null;
            relayHasOverseer ??= overseerProps != null;
            relayHasOverseerFacility ??= overseerFacilityProps != null;
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
        ApplyRelay(def);
        ApplyAoBuilding();
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

    /// <summary>
    /// 回填 AO 的 9 个 Comp + 建筑大气扩展。没有的 Comp 整块跳过（字段保持 null）。
    /// <para>
    /// 只在 SetDef 里调一次，不做 <c>syncUnmodified</c>：这些 Comp 不会被本 Mod 增删，
    /// 不像中继那样需要"Comp 可能在 Apply 里刚被加进来"再同步一遍。
    /// </para>
    /// </summary>
    private void LoadAoBuilding(ThingDef d)
    {
        buildingAtmosphereExt = AoHelper.Ext(d, AoHelper.BuildingExt);
        if (buildingAtmosphereExt != null)
        {
            var ext = buildingAtmosphereExt;
            buildingPressureTolerance ??= AoHelper.F(ext, "pressureToleranceFactor");
            buildingWindTolerance ??= AoHelper.F(ext, "windToleranceFactor");
            buildingAirLeak ??= AoHelper.F(ext, "airLeakFactor");
            buildingHeatExchange ??= AoHelper.F(ext, "heatExchangeFactor");
            buildingAirPassable ??= AoHelper.B(ext, "airPassable");
            buildingAirFillPercent ??= AoHelper.F(ext, "airFillPercent");
        }

        fanProps = AoBuilding.FindComp(d, AoBuilding.FanProps);
        if (fanProps != null)
        {
            var p = fanProps;
            fanPowerSource ??= AoHelper.G(p, "powerSource")?.ToString();
            fanMaxPowerWatts ??= AoHelper.F(p, "maxPowerWatts");
            fanDefaultPowerWatts ??= AoHelper.F(p, "defaultPowerWatts");
            fanRatedFreeAirFlow ??= AoHelper.F(p, "ratedFreeAirFlowM3PerSecond");
            fanOutletAreaM2 ??= AoHelper.F(p, "outletAreaM2");
            fanWindResponsePerTick ??= AoHelper.F(p, "windResponsePerTick");
            fanAllowEasyRotation ??= AoHelper.B(p, "allowEasyRotation");
            fanAllowOutputAdjustment ??= AoHelper.B(p, "allowOutputAdjustment");
            fanStoredEnergyCapacityJoules ??= AoHelper.F(p, "storedEnergyCapacityJoules");
            fanInitialStoredEnergyJoules ??= AoHelper.F(p, "initialStoredEnergyJoules");
        }

        jetProps = AoBuilding.FindComp(d, AoBuilding.JetProps);
        if (jetProps != null)
        {
            var p = jetProps;
            jetFuelPerSecond ??= AoHelper.F(p, "fuelPerSecond");
            jetWaterKgPerFuel ??= AoHelper.F(p, "waterKgPerFuel");
            jetMaxSpeed ??= AoHelper.F(p, "maxSpeed");
            jetHalfSpeed ??= AoHelper.F(p, "halfSpeed");
            jetMaxTemperature ??= AoHelper.F(p, "maxTemperature");
            jetMaxPressureRatio ??= AoHelper.F(p, "maxPressureRatio");
            jetHalfPressureRatio ??= AoHelper.F(p, "halfPressureRatio");
            jetInletPressureRatio ??= AoHelper.F(p, "inletPressureRatio");
            jetOxygenPercent ??= AoHelper.F(p, "oxygenPercent");
            jetColumnHeight ??= AoHelper.F(p, "columnHeight");
            jetOxygenConsumptionPercent ??= AoHelper.F(p, "oxygenConsumptionPercent");
            jetFlameoutOxygenKPa ??= AoHelper.F(p, "flameoutOxygenKPa");
            jetRelightOxygenKPa ??= AoHelper.F(p, "relightOxygenKPa");
            jetAllowEasyRotation ??= AoHelper.B(p, "allowEasyRotation");
        }

        compressorProps = AoBuilding.FindComp(d, AoBuilding.CompressorProps);
        if (compressorProps != null)
        {
            var p = compressorProps;
            compressorIntakeM3PerTick ??= AoHelper.F(p, "intakeCubicMetresPerTick");
            compressorRunningWatts ??= AoHelper.F(p, "runningWatts");
            compressorIdleWatts ??= AoHelper.F(p, "idleWatts");
            compressorMaxGoalKPa ??= AoHelper.F(p, "maxGoalKPa");
            compressorSeparateOxygen ??= AoHelper.B(p, "separateOxygen");
            compressorProductDef ??= AoHelper.G(p, "productDef") as ThingDef;
            compressorOxygenProductDef ??= AoHelper.G(p, "oxygenProductDef") as ThingDef;
        }

        gasTankProps = AoBuilding.FindComp(d, AoBuilding.GasTankProps);
        if (gasTankProps != null)
        {
            var p = gasTankProps;
            gasTankOxygenPercent ??= AoHelper.F(p, "oxygenPercent");
            gasTankExplodeAboveCelsius ??= AoHelper.F(p, "explodeAboveCelsius");
        }

        humidifierProps = AoBuilding.FindComp(d, AoBuilding.HumidifierProps);
        if (humidifierProps != null)
        {
            var p = humidifierProps;
            humidifierMaxKgPerHour ??= AoHelper.F(p, "maxKgPerHour");
            humidifierRunningWatts ??= AoHelper.F(p, "runningWatts");
            humidifierIdleWatts ??= AoHelper.F(p, "idleWatts");
        }

        dehumidifierProps = AoBuilding.FindComp(d, AoBuilding.DehumidifierProps);
        if (dehumidifierProps != null)
        {
            var p = dehumidifierProps;
            dehumidifierSweptVolumes ??= AoHelper.F(p, "sweptCellVolumesPerInterval");
            dehumidifierRunningWatts ??= AoHelper.F(p, "runningWatts");
            dehumidifierIdleWatts ??= AoHelper.F(p, "idleWatts");
        }

        mainspringProps = AoBuilding.FindComp(d, AoBuilding.MainspringProps);
        if (mainspringProps != null)
        {
            var p = mainspringProps;
            mainspringWindingTicks ??= AoHelper.I(p, "windingTicks");
            mainspringAutoRewind ??= AoHelper.B(p, "autoRewind");
        }

        rainCollectorProps = AoBuilding.FindComp(d, AoBuilding.RainCollectorProps);
        if (rainCollectorProps != null)
        {
            var p = rainCollectorProps;
            rainCollectorCatchmentM2 ??= AoHelper.F(p, "catchmentM2");
            rainCollectorCapacityUnits ??= AoHelper.F(p, "capacityUnits");
        }

        irrigatorProps = AoBuilding.FindComp(d, AoBuilding.IrrigatorProps);
        if (irrigatorProps != null)
        {
            var p = irrigatorProps;
            irrigatorSprayRadius ??= AoHelper.F(p, "sprayRadius");
            irrigatorPumpRadius ??= AoHelper.F(p, "pumpRadius");
            irrigatorSprayTicks ??= AoHelper.I(p, "sprayTicks");
            irrigatorSessionMm ??= AoHelper.F(p, "sessionMm");
            irrigatorCheckIntervalTicks ??= AoHelper.I(p, "checkIntervalTicks");
            irrigatorRestTicks ??= AoHelper.I(p, "restTicks");
            irrigatorSprayWatts ??= AoHelper.F(p, "sprayWatts");
            irrigatorIdleWatts ??= AoHelper.F(p, "idleWatts");
        }
    }

    /// <summary>
    /// 把窗口里的值写回 AO 的 Comp / 扩展。三态门控，没动过的字段不写；没有对应 Comp 的建筑直接跳过。
    /// <para>
    /// <b>不用清任何缓存</b>：这些 Comp 每 tick 现读 <c>Props.xxx</c>；<c>BuildingAtmosphereExtension</c>
    /// 的静态 <c>Cache</c> 只缓存**实例引用**、字段是活的（见 §4.0）。只有"增删扩展"才需要清，
    /// 而本 Mod 不做增删。
    /// </para>
    /// </summary>
    private void ApplyAoBuilding()
    {
        if (!DataUtility.MayRequire(TweakDatabase.AO)) return;

        if (buildingAtmosphereExt != null)
        {
            var ext = buildingAtmosphereExt;
            AoHelper.ApplyF<float>(this, ext, "pressureToleranceFactor", nameof(buildingPressureTolerance));
            AoHelper.ApplyF<float>(this, ext, "windToleranceFactor", nameof(buildingWindTolerance));
            AoHelper.ApplyF<float>(this, ext, "airLeakFactor", nameof(buildingAirLeak));
            AoHelper.ApplyF<float>(this, ext, "heatExchangeFactor", nameof(buildingHeatExchange));
            AoHelper.ApplyF<bool>(this, ext, "airPassable", nameof(buildingAirPassable));
            AoHelper.ApplyF<float>(this, ext, "airFillPercent", nameof(buildingAirFillPercent));
        }

        if (fanProps != null)
        {
            var p = fanProps;
            ApplyS(nameof(fanPowerSource), name =>
            {
                // 跨 Mod 枚举：写入前按 AO 的枚举类型解析。脏值（手输 / 旧存档）直接跳过，
                // 不猜"最接近的那个"—— 猜错等于把风扇的供能方式静默改掉。
                if (name.NullOrEmpty()) return;
                var enumType = AoHelper.T(AoHelper.FanPowerSource);
                if (enumType != null && System.Enum.TryParse(enumType, name, out var parsed)
                    && System.Enum.IsDefined(enumType, parsed))
                    AoHelper.S(p, "powerSource", parsed);
            });
            AoHelper.ApplyF<float>(this, p, "maxPowerWatts", nameof(fanMaxPowerWatts));
            AoHelper.ApplyF<float>(this, p, "defaultPowerWatts", nameof(fanDefaultPowerWatts));
            AoHelper.ApplyF<float>(this, p, "ratedFreeAirFlowM3PerSecond", nameof(fanRatedFreeAirFlow));
            AoHelper.ApplyF<float>(this, p, "outletAreaM2", nameof(fanOutletAreaM2));
            AoHelper.ApplyF<float>(this, p, "windResponsePerTick", nameof(fanWindResponsePerTick));
            AoHelper.ApplyF<bool>(this, p, "allowEasyRotation", nameof(fanAllowEasyRotation));
            AoHelper.ApplyF<bool>(this, p, "allowOutputAdjustment", nameof(fanAllowOutputAdjustment));
            AoHelper.ApplyF<float>(this, p, "storedEnergyCapacityJoules", nameof(fanStoredEnergyCapacityJoules));
            AoHelper.ApplyF<float>(this, p, "initialStoredEnergyJoules", nameof(fanInitialStoredEnergyJoules));
        }

        if (jetProps != null)
        {
            var p = jetProps;
            AoHelper.ApplyF<float>(this, p, "fuelPerSecond", nameof(jetFuelPerSecond));
            AoHelper.ApplyF<float>(this, p, "waterKgPerFuel", nameof(jetWaterKgPerFuel));
            AoHelper.ApplyF<float>(this, p, "maxSpeed", nameof(jetMaxSpeed));
            AoHelper.ApplyF<float>(this, p, "halfSpeed", nameof(jetHalfSpeed));
            AoHelper.ApplyF<float>(this, p, "maxTemperature", nameof(jetMaxTemperature));
            AoHelper.ApplyF<float>(this, p, "maxPressureRatio", nameof(jetMaxPressureRatio));
            AoHelper.ApplyF<float>(this, p, "halfPressureRatio", nameof(jetHalfPressureRatio));
            AoHelper.ApplyF<float>(this, p, "inletPressureRatio", nameof(jetInletPressureRatio));
            AoHelper.ApplyF<float>(this, p, "oxygenPercent", nameof(jetOxygenPercent));
            AoHelper.ApplyF<float>(this, p, "columnHeight", nameof(jetColumnHeight));
            AoHelper.ApplyF<float>(this, p, "oxygenConsumptionPercent", nameof(jetOxygenConsumptionPercent));
            AoHelper.ApplyF<float>(this, p, "flameoutOxygenKPa", nameof(jetFlameoutOxygenKPa));
            AoHelper.ApplyF<float>(this, p, "relightOxygenKPa", nameof(jetRelightOxygenKPa));
            AoHelper.ApplyF<bool>(this, p, "allowEasyRotation", nameof(jetAllowEasyRotation));
        }

        if (compressorProps != null)
        {
            var p = compressorProps;
            AoHelper.ApplyF<float>(this, p, "intakeCubicMetresPerTick", nameof(compressorIntakeM3PerTick));
            AoHelper.ApplyF<float>(this, p, "runningWatts", nameof(compressorRunningWatts));
            AoHelper.ApplyF<float>(this, p, "idleWatts", nameof(compressorIdleWatts));
            AoHelper.ApplyF<float>(this, p, "maxGoalKPa", nameof(compressorMaxGoalKPa));
            AoHelper.ApplyF<bool>(this, p, "separateOxygen", nameof(compressorSeparateOxygen));
            AoHelper.ApplyDef<ThingDef>(this, p, "productDef", nameof(compressorProductDef));
            AoHelper.ApplyDef<ThingDef>(this, p, "oxygenProductDef", nameof(compressorOxygenProductDef));
        }

        if (gasTankProps != null)
        {
            var p = gasTankProps;
            AoHelper.ApplyF<float>(this, p, "oxygenPercent", nameof(gasTankOxygenPercent));
            AoHelper.ApplyF<float>(this, p, "explodeAboveCelsius", nameof(gasTankExplodeAboveCelsius));
        }

        if (humidifierProps != null)
        {
            var p = humidifierProps;
            AoHelper.ApplyF<float>(this, p, "maxKgPerHour", nameof(humidifierMaxKgPerHour));
            AoHelper.ApplyF<float>(this, p, "runningWatts", nameof(humidifierRunningWatts));
            AoHelper.ApplyF<float>(this, p, "idleWatts", nameof(humidifierIdleWatts));
        }

        if (dehumidifierProps != null)
        {
            var p = dehumidifierProps;
            AoHelper.ApplyF<float>(this, p, "sweptCellVolumesPerInterval", nameof(dehumidifierSweptVolumes));
            AoHelper.ApplyF<float>(this, p, "runningWatts", nameof(dehumidifierRunningWatts));
            AoHelper.ApplyF<float>(this, p, "idleWatts", nameof(dehumidifierIdleWatts));
        }

        if (mainspringProps != null)
        {
            var p = mainspringProps;
            AoHelper.ApplyF<int>(this, p, "windingTicks", nameof(mainspringWindingTicks));
            AoHelper.ApplyF<bool>(this, p, "autoRewind", nameof(mainspringAutoRewind));
        }

        if (rainCollectorProps != null)
        {
            var p = rainCollectorProps;
            AoHelper.ApplyF<float>(this, p, "catchmentM2", nameof(rainCollectorCatchmentM2));
            AoHelper.ApplyF<float>(this, p, "capacityUnits", nameof(rainCollectorCapacityUnits));
        }

        if (irrigatorProps != null)
        {
            var p = irrigatorProps;
            AoHelper.ApplyF<float>(this, p, "sprayRadius", nameof(irrigatorSprayRadius));
            AoHelper.ApplyF<float>(this, p, "pumpRadius", nameof(irrigatorPumpRadius));
            AoHelper.ApplyF<int>(this, p, "sprayTicks", nameof(irrigatorSprayTicks));
            AoHelper.ApplyF<float>(this, p, "sessionMm", nameof(irrigatorSessionMm));
            AoHelper.ApplyF<int>(this, p, "checkIntervalTicks", nameof(irrigatorCheckIntervalTicks));
            AoHelper.ApplyF<int>(this, p, "restTicks", nameof(irrigatorRestTicks));
            AoHelper.ApplyF<float>(this, p, "sprayWatts", nameof(irrigatorSprayWatts));
            AoHelper.ApplyF<float>(this, p, "idleWatts", nameof(irrigatorIdleWatts));
        }
    }

    /// <summary>
    /// 从三种中继 Comp 回填窗口字段。<paramref name="syncUnmodified"/> = true 时未修改字段跟随
    /// Comp 当前值（外部改动 / 重新解析后界面保持一致）；列表始终带 <c>!IsFieldModified</c> 守卫。
    /// </summary>
    private void LoadRelay(ThingDef d, bool syncUnmodified = false)
    {
        subRelayProps = FortifiedRelay.FindSubRelay(d);
        if (subRelayProps != null)
        {
            var props = subRelayProps;
            Sync(ref subRelayRange, nameof(subRelayRange),
                DataUtility.AsFloat(DataUtility.GetByName(props, "relayRange")), syncUnmodified);
            Sync(ref subRelayCoverWholeMap, nameof(subRelayCoverWholeMap),
                DataUtility.GetByName(props, "coverWholeMap") as bool?, syncUnmodified);
        }

        overseerProps = FortifiedRelay.FindOverseer(d);
        if (overseerProps != null)
        {
            var props = overseerProps;
            Sync(ref overseerCanRepair, nameof(overseerCanRepair),
                DataUtility.GetByName(props, "canRepair") as bool?, syncUnmodified);
            Sync(ref overseerTicksPerHeal, nameof(overseerTicksPerHeal),
                DataUtility.AsInt(DataUtility.GetByName(props, "ticksPerHeal")), syncUnmodified);
            Sync(ref overseerInstantControl, nameof(overseerInstantControl),
                DataUtility.GetByName(props, "instantControl") as bool?, syncUnmodified);
        }

        overseerFacilityProps = FortifiedRelay.FindOverseerFacility(d);
        if (overseerFacilityProps != null)
        {
            var props = overseerFacilityProps;
            Sync(ref facilityMaxSimultaneous, nameof(facilityMaxSimultaneous),
                DataUtility.AsInt(DataUtility.GetByName(props, "maxSimultaneous")), syncUnmodified);
            Sync(ref facilityMaxDistance, nameof(facilityMaxDistance),
                DataUtility.AsFloat(DataUtility.GetByName(props, "maxDistance")), syncUnmodified);
            Sync(ref facilityRequiresLOS, nameof(facilityRequiresLOS),
                DataUtility.GetByName(props, "requiresLOS") as bool?, syncUnmodified);
            Sync(ref facilityMustBePlacedAdjacent, nameof(facilityMustBePlacedAdjacent),
                DataUtility.GetByName(props, "mustBePlacedAdjacent") as bool?, syncUnmodified);
            if (!IsFieldModified(nameof(facilityStatOffsets)) && (syncUnmodified || facilityStatOffsets == null))
                facilityStatOffsets = DataUtility.GetByName(props, "statOffsets") is List<StatModifier> list
                    ? new List<StatModifier>(list)
                    : null;
        }
    }

    private void Sync<T>(ref T? field, string name, T? value, bool syncUnmodified) where T : struct
    {
        if (IsFieldModified(name)) return;
        if (syncUnmodified || field == null) field = value;
    }

    /// <summary>
    /// 把窗口里的值写回中继 Comp。三态门控，未动过的字段不写；没有对应 Comp 的建筑直接跳过。
    /// 宿主 stat 由 <see cref="ApplyDefStats"/> 负责，这里只登记缓存刷新。
    /// </summary>
    private void ApplyRelay(ThingDef def)
    {
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;

        // Comp 增删：与 isCompQuality 同一模式，只改 def.comps。已存在的单位保留出生时的 Comp。
        ApplyV<bool>(nameof(relayHasSubRelay),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.SubRelayPropsType, v));
        ApplyV<bool>(nameof(relayHasOverseer),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.OverseerPropsType, v));
        ApplyV<bool>(nameof(relayHasOverseerFacility),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.OverseerFacilityPropsType, v));

        // 重新解析（Comp 可能刚被增删），并让未修改字段跟随当前值。
        // 权威重置时不覆盖：那时字段里是要写回的原值。
        LoadRelay(def, syncUnmodified: !forceWriteAll);

        if (subRelayProps != null)
        {
            var props = subRelayProps; // 闭包只捕获 object
            ApplyV<float>(nameof(subRelayRange), v => DataUtility.SetByName(props, "relayRange", v));
            ApplyV<bool>(nameof(subRelayCoverWholeMap), v => DataUtility.SetByName(props, "coverWholeMap", v));
        }

        if (overseerProps != null)
        {
            var props = overseerProps;
            ApplyV<bool>(nameof(overseerCanRepair), v => DataUtility.SetByName(props, "canRepair", v));
            ApplyV<int>(nameof(overseerTicksPerHeal), v => DataUtility.SetByName(props, "ticksPerHeal", v));
            ApplyV<bool>(nameof(overseerInstantControl), v => DataUtility.SetByName(props, "instantControl", v));
        }

        if (overseerFacilityProps != null)
        {
            var props = overseerFacilityProps;
            ApplyV<int>(nameof(facilityMaxSimultaneous), v => DataUtility.SetByName(props, "maxSimultaneous", v));
            ApplyV<float>(nameof(facilityMaxDistance), v => DataUtility.SetByName(props, "maxDistance", v));
            ApplyV<bool>(nameof(facilityRequiresLOS), v => DataUtility.SetByName(props, "requiresLOS", v));
            ApplyV<bool>(nameof(facilityMustBePlacedAdjacent), v => DataUtility.SetByName(props, "mustBePlacedAdjacent", v));
            ApplyL<StatModifier>(nameof(facilityStatOffsets), v => DataUtility.SetByName(props, "statOffsets", v));
        }

        if (RelayTouched) FortifiedRelay.RequestRefresh(def);
    }

    /// <summary>
    /// 本次 Apply 是否动过会进入中继缓存的字段。比三态多判 <c>forceWriteAll</c> / <c>forceWriteFields</c>：
    /// 重置是先写回原值再清标记，那一刻三态已是 Unset。
    /// </summary>
    private bool RelayTouched
        => Touched(nameof(statBases))
        || Touched(nameof(mechBandwidth))
        || Touched(nameof(mechControlGroups))
        || Touched(nameof(mechCommandRange))
        || Touched(nameof(subRelayRange))
        || Touched(nameof(subRelayCoverWholeMap))
        || Touched(nameof(overseerCanRepair))
        || Touched(nameof(overseerTicksPerHeal))
        || Touched(nameof(overseerInstantControl))
        || Touched(nameof(facilityMaxSimultaneous))
        || Touched(nameof(facilityMaxDistance))
        || Touched(nameof(facilityRequiresLOS))
        || Touched(nameof(facilityMustBePlacedAdjacent))
        || Touched(nameof(facilityStatOffsets))
        || Touched(nameof(relayHasSubRelay))
        || Touched(nameof(relayHasOverseer))
        || Touched(nameof(relayHasOverseerFacility));

    private bool Touched(string field)
        => IsFieldModified(field) || forceWriteAll || forceWriteFields?.Contains(field) == true;

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
            if (def.building.turretGunDef != null)
            {
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


