using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.BalanceTweakSettings;
namespace BalanceTweak;

[TweakFor(typeof(ThingDef), SettingType.Race, Priority = 700, MatchMethod = nameof(DataUtility.MatchRace))]
class RaceData : TweakData<RaceData>, ISubItemHost
{
    /// <summary>武器招式子项（原 Init() 里 "data is RaceData or WeaponData" 那段硬编码）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not ThingDef t || t.tools.NullOrEmpty()) yield break;
        for (int i = 0; i < t.tools.Count; i++)
        {
            var td = new ToolData { index = i };
            td.SetParentTweak(parent, SettingType.MeleeTool, tweaked);
            yield return td;
        }
    }

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? PawnKind = null;

    /// <summary>该种族使用的整套身体（<c>race.body</c>）对应的 BodyData 链接。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? bodyDef = null;

    /// <summary>
    /// 该种族抽得到的手持武器（只读触发列）。
    /// <para>
    /// race 上同样<b>没有</b>武器这个字段 —— 装备按 <c>weaponTags</c> + <c>weaponMoney</c> 挂在 pawnkind 上，
    /// 所以这里反查的是"它名下所有兵种的并集"。窗口打开时由 <see cref="FindWeaponLinks"/> 实时计算，
    /// 字段本身不存也不读任何值。
    /// </para>
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.PawnWeaponLinks,
        Hidden = nameof(HiddenIfNoKindWeaponTags))]
    public TweakID? weaponLinks = null;

    /// <summary>
    /// 名下兵种全都没有武器标签（动物、昆虫、无枪机械）→ 该格留白。
    /// <para>
    /// 这个谓词每帧每格都会被求值，所以走 <see cref="CombatPower.HasArmedKind"/> 的缓存，
    /// 不在绘制路径上遍历兵种列表。
    /// </para>
    /// </summary>
    public static bool HiddenIfNoKindWeaponTags(TweakData data)
        => data.def is ThingDef race && !CombatPower.HasArmedKind(race);

    // === 战斗力参考值（只读派生列，见 CombatPower.FillRace） ===
    // race 自己只有身体与基础数值，装备 / 技能 / 预算都在 pawnkind 上，所以这三列 = 名下兵种的平均值。
    // 人形不适用（一个 race 挂上百个兵种，平均值没有意义）—— 走 Available，和 Wildness 之于 Animal 同一个模式：
    // 种族页本来就按 propType 分组显示（filterPropType），选到 Humanlike 时这三列整列不出现。
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNotHumanlike))]
    public float? attackPower = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNotHumanlike))]
    public float? defensePower = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int, Available = nameof(AvailableIfNotHumanlike))]
    public float? overallPower = null;

    /// <summary>战斗力三列的可用性：人形不适用（种族页按 propType 分组，选到 Humanlike 时整列隐藏）。</summary>
    public static bool AvailableIfNotHumanlike(TweakData data) => data.propType switch
    {
        (int)RaceType.Humanlike => false,
        _ => true,
    };

    [TweakField(StatDef = "ArmorRating_Sharp")]
    public float? sharpArmor = null;
    [TweakField(StatDef = "ArmorRating_Blunt")]
    public float? bluntArmor = null;
    [TweakField(StatDef = "ArmorRating_Heat")]
    public float? heatArmor = null;
    [TweakField(StatDef = "BandwidthCost", Style = ColumnStyle.Int, Available = nameof(AvailableIfMech), MayRequire = "ludeon.rimworld.biotech")]
    public float? bandwidthCost = null;
    [TweakField(StatDef = "MoveSpeed")]
    public float? moveSpeed = null;
    [TweakField(StatDef = "WorkSpeedGlobal", Style = ColumnStyle.Prec)]
    public float? workSpeedGlobal = null;
    [TweakField(StatDef = "RangedCooldownFactor", Style = ColumnStyle.Prec)]
    public float? rangedCooldownFactor = null;
    [TweakField(StatDef = "ShootingAccuracyPawn", Style = ColumnStyle.Prec)]
    public float? shootingAccuracyPawn = null;
    [TweakField(StatDef = "AimingDelayFactor", Style = ColumnStyle.Prec)]
    public float? aimingDelayFactor = null;
    [TweakField(StatDef = "MeleeDamageFactor", Style = ColumnStyle.Prec)]
    public float? meleeDamageFactor = null;
    [TweakField(StatDef = "MeleeCooldownFactor", Style = ColumnStyle.Prec)]
    public float? meleeCooldownFactor = null;
    [TweakField(StatDef = "MeleeDodgeChance", Style = ColumnStyle.Prec)]
    public float? meleeDodgeChance = null;
    [TweakField(StatDef = "Wildness", Available = nameof(AvailableIfBeast))]
    public float? wildness = null;

    [TweakField(StatDef = "InjuryHealingFactor", Available = nameof(AvailableIfLive))]
    public float? injuryHealingFactor = null;
    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;
    [TweakField(StatDef = "IncomingDamageFactor", Style = ColumnStyle.Prec)]
    public float? incomingDamageFactor = null;
    [TweakField(StatDef = "ComfyTemperatureMin", Available = nameof(AvailableIfLive))]
    public float? insulationCold = null;
    [TweakField(StatDef = "ComfyTemperatureMax", Available = nameof(AvailableIfLive))]
    public float? insulationHeat = null;
    [TweakField(StatDef = "EnergyShieldEnergyMax", Style = ColumnStyle.Prec, Available = nameof(AvailableIfMech))]
    public float? energyShieldEnergyMax = null;
    [TweakField(StatDef = "EnergyShieldRechargeRate", Style = ColumnStyle.Prec, Available = nameof(AvailableIfMech))]
    public float? energyShieldRechargeRate = null;
    [TweakField(StatDef = "PsychicSensitivity")]
    public float? psychicSensitivity = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec, Available = nameof(AvailableIfLive))]
    public float? flammability = null;
    [TweakField(StatDef = "StaggerDurationFactor", Style = ColumnStyle.Prec)]
    public float? staggerDurationFactor = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? RaceTool = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? MeleeDPS = null;
    [TweakField(StatDef = "CarryingCapacity", Style = ColumnStyle.Int, Available = nameof(AvailableIfLive))]
    public float? carryingCapacity = null;
    [TweakField(StatDef = "MinimumContainmentStrength", Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? minimumContainmentStrength = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? bioferriteDensity = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly", Style = ColumnStyle.Int)]
    public int? studiableFrequencyTicks = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? anomalyKnowledge = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? activityStartingRangeMin = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? activityStartingRangeMax = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? changePerDayBase = null;
    [TweakField(Available = nameof(AvailableIfEntity), MayRequire = "ludeon.rimworld.anomaly")]
    public float? changePerDamage = null;
    [TweakField(StatDef = "VEF_MassCarryCapacity", MayRequire = "oskarpotocki.vanillafactionsexpanded.core")]
    public float? massCarryCapacity = null;
    [TweakField(StatDef = "VQE_LifespanYears", MayRequire = "vanillaquestsexpanded.dronefactory", Available = nameof(AvailableIfOther))]
    public float? lifespanYears = null;
    [TweakField(StatDef = "VacuumResistance", MayRequire = "ludeon.rimworld.odyssey", Style = ColumnStyle.Prec)]
    public float? vacuumResistance = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMech))]
    public int? mechFixedSkillLevel = null;
    [TweakField(StatDef = "EMPResistance", MayRequire = "ludeon.rimworld.biotech",Style = ColumnStyle.Prec, Available = nameof(AvailableIfMech))]
    public float? EMPResistance = null;
    [TweakField(StatDef = "MechEnergyUsageFactor", MayRequire = "ludeon.rimworld.biotech",Style = ColumnStyle.Prec, Available = nameof(AvailableIfMech))]
    public float? MechEnergyUsageFactor = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? baseHealthScale = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? baseBodySize = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfLive))]
    public float? baseHungerRate = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfLive))]
    public float? lifeExpectancy = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int)]
    public float? corepartHP = null;

    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfMech))]
    public float? projectileInterceptorRadius = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMech))]
    public int? projectileInterceptorHitPoints = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMech))]
    public int? projectileInterceptorChargeDurationTicks = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMech))]
    public int? projectileInterceptorRechargeHitPointsIntervalTicks = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMech))]
    public int? projectileInterceptorDisarmedByEmpForTicks = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // ── 机械专属：屠宰产物 / 炮塔武器 ──
    // butcherProducts 在 ThingDef 上；炮塔武器在 CompProperties_TurretGun.turretDef 上。
    // 机械的自带武器（weaponTags / weaponMoney）不在这里 —— 那是 PawnKind 侧的字段。

    /// <summary>屠宰（拆解）该机械时产出的物品（<c>ThingDef.butcherProducts</c>）。</summary>
    [TweakField(Style = ColumnStyle.ThingDefCountList, Available = nameof(AvailableIfMech))]
    public List<ThingDefCountClass>? butcherProducts = null;

    /// <summary>炮塔武器（<c>CompProperties_TurretGun.turretDef</c>）。</summary>
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfMech))]
    public ThingDef? turretDef = null;

    // ── Fortified 机械武器过滤（可选 Mod，见 MechWeaponFilterWindow）──
    // 一组字段共同决定"这台机兵能用哪些武器"，逐列展示没有价值：表格里只留一个触发列，
    // 其余字段只落盘、不出现在列里（NotAColumn），由窗口统一编辑。

    /// <summary>只读触发列：打开机械武器过滤窗口。没有 <c>MechWeaponExtension</c> 的行整格留白。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.MechWeaponFilter,
        MayRequire = TweakDatabase.FFF, Available = nameof(AvailableIfMech),
        Hidden = nameof(HiddenIfNoMechWeaponExtension))]
    public TweakID? mechWeaponFilter = null;

    /// <summary>没有 Fortified 机械武器扩展的种族不适用这一格。</summary>
    public static bool HiddenIfNoMechWeaponExtension(TweakData data)
        => FortifiedMech.FindExtension(data.def) == null;

    // 以下字段由窗口编辑，只在存档里存在，不进表格。
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? enableWeaponFilter = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.StringList, MayRequire = TweakDatabase.FFF)]
    public List<string>? usableWeaponTags = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? enableTechLevelFilter = null;
    /// <summary>存 <see cref="TechLevel"/> 的整数值，避开"List&lt;enum&gt; 没有现成列样式"的问题。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.IntList, MayRequire = TweakDatabase.FFF)]
    public List<int>? usableTechLevels = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? enableClassFilter = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.DefList, MayRequire = TweakDatabase.FFF)]
    public List<WeaponClassDef>? usableWeaponClasses = null;
    /// <summary>按武器 <b>defName</b> 豁免（不是标签）。窗口里用武器选择器编辑。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.StringList, MayRequire = TweakDatabase.FFF)]
    public List<string>? bypassUsableWeapons = null;

    /// <summary>机械武器扩展的引用（SetDef 时解析一次）。</summary>
    private object? mechWeaponExtension = null;

    // ── Fortified 中继指挥（可选 Mod，见 RelayConfigWindow）──
    // 中继配置全部挂在 CompProperties 上。这里只留一个只读触发列，其余字段 NotAColumn：
    // 照常落盘 / 走三态 / 参与重置，由窗口统一编辑。

    /// <summary>
    /// 只读触发列：打开中继配置窗口。种族页只在机械分组出现（AvailableIfMech）；
    /// 没有中继 Comp 的机械也要能打开窗口来新增 Comp，所以不做行级留白。
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.RelayConfig,
        MayRequire = TweakDatabase.FFF, Available = nameof(AvailableIfMech))]
    public TweakID? relayConfig = null;

    // 宿主 stat：带宽 / 控制组 / 指挥范围写在 def.statBases 上，仍由 ApplyDefStats 唯一写入；
    // NotAColumn = 不进表格，改到 RelayConfigWindow 里编辑（一个组件不值得占三列）。
    [TweakField(NotAColumn = true, StatDef = "MechBandwidth", Style = ColumnStyle.Int,
        MayRequire = "ludeon.rimworld.biotech")]
    public float? mechBandwidth = null;
    [TweakField(NotAColumn = true, StatDef = "MechControlGroups", Style = ColumnStyle.Int,
        MayRequire = "ludeon.rimworld.biotech")]
    public float? mechControlGroups = null;
    [TweakField(NotAColumn = true, StatDef = "FFF_MechCommandRange",
        MayRequire = TweakDatabase.FFF)]
    public float? mechCommandRange = null;

    // 中继 Comp 的新增 / 移除开关：与 ApparelData.isCompQuality 同一模式（Apply 时增删 def.comps）。
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasCommandRelay = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasOverseer = null;

    // 以下字段由 RelayConfigWindow 编辑，只在存档里存在，不进表格。
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? commandRelayMaxWorldMapRadius = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? commandRelayMaxRelayRadius = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? commandRelayMinRelayRadius = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? commandRelayCoverWholeMap = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? overseerCanRepair = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? overseerTicksPerHeal = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? overseerInstantControl = null;

    /// <summary>中继 Comp 的引用（SetDef 时解析一次；闭包里只捕获 object）。</summary>
    private object? commandRelayProps = null;
    private object? overseerProps = null;

    // ── Fortified 多炮塔（可选 Mod，见 TurretConfigWindow）──
    // 副炮槽是列表，但槽位不多，用一个只读触发列 + 单窗口按槽分节编辑；
    // 槽位数据以 TurretSlotRecord 列表落盘（字段表驱动），渲染配置始终留在 Def 上。

    /// <summary>只读触发列：打开炮塔配置窗口。既没有副炮槽也没有主炮的行整格留白。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.TurretConfig,
        MayRequire = TweakDatabase.FFF, Available = nameof(AvailableIfMech),
        Hidden = nameof(HiddenIfNoTurretComp))]
    public TweakID? turretConfig = null;

    public static bool HiddenIfNoTurretComp(TweakData data) => !FortifiedTurret.HasAny(data.def);

    [TweakField(NotAColumn = true, Style = ColumnStyle.TurretSlotList, MayRequire = TweakDatabase.FFF)]
    public List<TurretSlotRecord>? turretSlots = null;

    // 主炮（CompProperties_VehicleWeapon）也放在窗口里编辑，避免在种族页多出四列空格子。
    [TweakField(NotAColumn = true, Style = ColumnStyle.DefSelector, MayRequire = TweakDatabase.FFF)]
    public ThingDef? vehicleDefaultWeapon = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? vehicleTurretRotationFollowPawn = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? vehicleRotationSmoothTime = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? vehicleDrawSize = null;

    private object? multipleTurretProps = null;
    private object? vehicleWeaponProps = null;

    // EggLayer comp
    [TweakField(Available = nameof(AvailableIfAnimal))]
    public float? eggLayIntervalDays = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? eggCountRangeMin = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? eggCountRangeMax = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? eggFertilizationCountMax = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAnimal))]
    public bool? eggLayFemaleOnly = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfAnimal))]
    public float? eggProgressUnfertilizedMax = null;

    // Milkable comp
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? milkIntervalDays = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? milkAmount = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAnimal))]
    public bool? milkFemaleOnly = null;

    // Shearable comp
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? shearIntervalDays = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfAnimal))]
    public int? woolAmount = null;

    static System.Reflection.FieldInfo CompPropertiesStudiableAnomalyKnowledge = Reflect.Field(typeof(CompProperties_Studiable), "anomalyKnowledge");

    private static object? GetCorepartHP(TweakData data) =>
    (data.def is ThingDef d ? (d.race?.body?.corePart?.def?.hitPoints * d.race?.baseHealthScale) ?? null : null);

    public static object? GetDPS(TweakData data) =>
    (data.def is ThingDef d ? DataUtility.CalculateExpectedDPS(d.tools) : null);

    /// <summary>
    /// 派生显示列按需计算。这两列都读当前 def（<c>race.body</c> / <c>tools</c>），
    /// 而 def 在 Init 阶段会被 Apply 改写，所以推迟到 UI 第一次要它们时再算 —— 那时 def 已是最终值。
    /// </summary>
    public override void EnsureDisplayValues()
    {
        if (displayValuesEnsured) return;
        // def 还没绑定就先别算，也别置标志：那一刻算出来的 null 会被永久冻住。
        if (def == null) return;
        displayValuesEnsured = true;
        MeleeDPS = (float?)GetDPS(this);
        corepartHP = (float?)GetCorepartHP(this);
        // 攻防三列走兵种的平均值（CombatPower 内部有装备画像与防御缓存，多行共享时几乎不花钱）。
        CombatPower.FillRace(this);
    }

    public override void SetDef(Def def, BalanceTweakSettings.SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            bodyDef ??= d.race?.body != null ? new TweakID(d.race.body.defName, SettingType.BodyDef) : null;
            mechFixedSkillLevel ??= d.race?.mechFixedSkillLevel;
            baseHealthScale ??= d.race?.baseHealthScale;
            baseBodySize ??= d.race?.baseBodySize;
            baseHungerRate ??= d.race?.baseHungerRate;
            lifeExpectancy ??= d.race?.lifeExpectancy;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (d.race?.IsMechanoid == true)
            {
                if (!IsFieldModified(nameof(butcherProducts))) butcherProducts ??= d.butcherProducts;
                turretDef ??= d.GetCompProperties<CompProperties_TurretGun>()?.turretDef;
            }
            foreach (var cp in d.comps)
            {
                if (cp is CompProperties_ProducesBioferrite bioferrite)
                {
                    bioferriteDensity ??= bioferrite.bioferriteDensity;
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
                else if (cp is CompProperties_ProjectileInterceptor interceptor)
                {
                    projectileInterceptorRadius ??= interceptor.radius;
                    projectileInterceptorHitPoints ??= interceptor.hitPoints;
                    projectileInterceptorChargeDurationTicks ??= interceptor.chargeDurationTicks;
                    projectileInterceptorRechargeHitPointsIntervalTicks ??= interceptor.rechargeHitPointsIntervalTicks;
                    projectileInterceptorDisarmedByEmpForTicks ??= interceptor.disarmedByEmpForTicks;
                }
                else if (cp is CompProperties_EggLayer eggLayer)
                {
                    eggLayIntervalDays ??= eggLayer.eggLayIntervalDays;
                    eggCountRangeMin ??= eggLayer.eggCountRange.min;
                    eggCountRangeMax ??= eggLayer.eggCountRange.max;
                    eggFertilizationCountMax ??= eggLayer.eggFertilizationCountMax;
                    eggLayFemaleOnly ??= eggLayer.eggLayFemaleOnly;
                    eggProgressUnfertilizedMax ??= eggLayer.eggProgressUnfertilizedMax;
                }
                else if (cp is CompProperties_Milkable milkable)
                {
                    milkIntervalDays ??= milkable.milkIntervalDays;
                    milkAmount ??= milkable.milkAmount;
                    milkFemaleOnly ??= milkable.milkFemaleOnly;
                }
                else if (cp is CompProperties_Shearable shearable)
                {
                    shearIntervalDays ??= shearable.shearIntervalDays;
                    woolAmount ??= shearable.woolAmount;
                }
            }

            // Fortified：机械武器过滤（可选 Mod，只按全名反射）。
            mechWeaponExtension = FortifiedMech.FindExtension(d);
            if (mechWeaponExtension != null) LoadMechWeaponFilter(mechWeaponExtension);
            LoadRelay(d);
            relayHasCommandRelay ??= commandRelayProps != null;
            relayHasOverseer ??= overseerProps != null;
            LoadTurret(d);
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
        ApplyRaceProp(def);
        ApplyMechProps(def);
        ApplyMechWeaponFilter();
        ApplyRelay(def);
        ApplyTurret(def);
        ApplyDefStats(def);
        ApplyEntityCompProps(def);
        ApplyAnimalCompProps(def);
        // 参考列按"当前 def 现算"：改完体型/血量/招式后，下一次读就该是新值。
        displayValuesEnsured = false;
    }

    /// <summary>
    /// 机械专属两项：屠宰产物（ThingDef）、炮塔武器（CompProperties_TurretGun）。
    /// 全部走三态门控；未设置（Unset）的字段不写，所以非机械行调用也无副作用。
    /// </summary>
    private void ApplyMechProps(ThingDef def)
    {
        ApplyL<ThingDefCountClass>(nameof(butcherProducts), v => def.butcherProducts = v);
        ApplyD<ThingDef>(nameof(turretDef), v =>
        {
            var props = def.GetCompProperties<CompProperties_TurretGun>();
            if (props != null) props.turretDef = v;
        });
    }

    /// <summary>
    /// 从 <c>Fortified.MechWeaponExtension</c> 回填窗口字段。
    /// 列表一律带 <c>!IsFieldModified</c> 守卫：已清空的字段读档后不得被扩展回填"复活"。
    /// </summary>
    private void LoadMechWeaponFilter(object extension)
    {
        enableWeaponFilter ??= DataUtility.GetByName(extension, "EnableWeaponFilter") as bool?;
        enableTechLevelFilter ??= DataUtility.GetByName(extension, "EnableTechLevelFilter") as bool?;
        enableClassFilter ??= DataUtility.GetByName(extension, "EnableClassFilter") as bool?;

        if (!IsFieldModified(nameof(usableWeaponTags)))
            usableWeaponTags ??= DataUtility.GetByName(extension, "UsableWeaponTags") as List<string>;
        if (!IsFieldModified(nameof(bypassUsableWeapons)))
            bypassUsableWeapons ??= DataUtility.GetByName(extension, "BypassUsableWeapons") as List<string>;
        if (!IsFieldModified(nameof(usableWeaponClasses)))
            usableWeaponClasses ??= DataUtility.GetByName(extension, "UsableWeaponClasses") as List<WeaponClassDef>;

        if (!IsFieldModified(nameof(usableTechLevels)) && usableTechLevels == null
            && DataUtility.GetByName(extension, "UsableTechLevels") is IEnumerable levels)
        {
            usableTechLevels = new List<int>();
            foreach (var level in levels)
                if (level is TechLevel t) usableTechLevels.Add((int)t);
        }
    }

    /// <summary>
    /// 把窗口里的值写回 <c>MechWeaponExtension</c>。三态门控，未动过的字段不写；
    /// 没有扩展的种族直接跳过（窗口本来也打不开）。
    /// </summary>
    private void ApplyMechWeaponFilter()
    {
        if (mechWeaponExtension == null) return;
        var extension = mechWeaponExtension; // 闭包只捕获 object，绝不捕获可选 Mod 类型

        // 目标是 bool（不可空），但字段是 bool?：用 ApplyN 避免"字段为 null 却处于 Set"这种
        // 异常存档在拆箱时抛异常；null 视为"没有可写的值"，保持扩展现状。
        ApplyN<bool>(nameof(enableWeaponFilter),
            v => { if (v.HasValue) DataUtility.SetByName(extension, "EnableWeaponFilter", v.Value); });
        ApplyN<bool>(nameof(enableTechLevelFilter),
            v => { if (v.HasValue) DataUtility.SetByName(extension, "EnableTechLevelFilter", v.Value); });
        ApplyN<bool>(nameof(enableClassFilter),
            v => { if (v.HasValue) DataUtility.SetByName(extension, "EnableClassFilter", v.Value); });

        ApplyL<string>(nameof(usableWeaponTags),
            v => DataUtility.SetByName(extension, "UsableWeaponTags", v));
        ApplyL<string>(nameof(bypassUsableWeapons),
            v => DataUtility.SetByName(extension, "BypassUsableWeapons", v));
        ApplyL<WeaponClassDef>(nameof(usableWeaponClasses),
            v => DataUtility.SetByName(extension, "UsableWeaponClasses", v));

        // 字段是 List<int>（避开 List<enum> 的列样式问题），扩展要 List<TechLevel>。
        ApplyL<int>(nameof(usableTechLevels), v =>
        {
            var levels = new List<TechLevel>();
            if (v != null)
                foreach (var raw in v)
                    if (raw >= 0 && raw <= byte.MaxValue) levels.Add((TechLevel)raw);
            DataUtility.SetByName(extension, "UsableTechLevels", levels);
        });
    }

    /// <summary>
    /// 从 <c>CompProperties_CommandRelay</c> / <c>CompProperties_Overseer</c> 回填窗口字段。
    /// <paramref name="syncUnmodified"/> = true 时，未修改的字段也跟随 Comp 的当前值
    /// （用于 Comp 新增 / 移除后让界面与实际一致）；已修改 / 已清空的字段永远不动。
    /// </summary>
    private void LoadRelay(ThingDef d, bool syncUnmodified = false)
    {
        commandRelayProps = FortifiedRelay.FindCommandRelay(d);
        if (commandRelayProps != null)
        {
            var props = commandRelayProps;
            Sync(ref commandRelayMaxWorldMapRadius, nameof(commandRelayMaxWorldMapRadius),
                DataUtility.AsInt(DataUtility.GetByName(props, "maxWorldMapRadius")), syncUnmodified);
            Sync(ref commandRelayMaxRelayRadius, nameof(commandRelayMaxRelayRadius),
                DataUtility.AsFloat(DataUtility.GetByName(props, "maxRelayRadius")), syncUnmodified);
            Sync(ref commandRelayMinRelayRadius, nameof(commandRelayMinRelayRadius),
                DataUtility.AsFloat(DataUtility.GetByName(props, "minRelayRadius")), syncUnmodified);
            Sync(ref commandRelayCoverWholeMap, nameof(commandRelayCoverWholeMap),
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
    }

    private void Sync<T>(ref T? field, string name, T? value, bool syncUnmodified) where T : struct
    {
        if (IsFieldModified(name)) return;
        if (syncUnmodified || field == null) field = value;
    }

    /// <summary>
    /// 把窗口里的值写回中继 Comp。先处理 Comp 增删，再原地写字段；没有对应 Comp 的种族直接跳过。
    /// 宿主 stat（带宽 / 控制组 / 指挥范围）由 <see cref="ApplyDefStats"/> 负责，这里只登记缓存刷新。
    /// </summary>
    private void ApplyRelay(ThingDef def)
    {
        // FFF 缺席时中继字段全部不在 Fields 里，任何 ApplyV 都可能撞到不存在的字段；直接跳过。
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;

        // Comp 增删：与 isCompQuality 同一模式，只改 def.comps。已存在的单位保留出生时的 Comp。
        ApplyV<bool>(nameof(relayHasCommandRelay),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.CommandRelayPropsType, v));
        ApplyV<bool>(nameof(relayHasOverseer),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.OverseerPropsType, v));

        // Comp 集合可能刚被增删：重新解析引用，并让未修改字段跟随 Comp 的当前值（新增后显示默认值）。
        // 权威重置（forceWriteAll）时不能同步：那一刻字段里放的正是"要写回的原值"，
        // 让 Comp 的新默认值盖掉它就等于重置失败。
        LoadRelay(def, syncUnmodified: !forceWriteAll);

        if (commandRelayProps != null)
        {
            var props = commandRelayProps; // 闭包只捕获 object
            ApplyV<int>(nameof(commandRelayMaxWorldMapRadius), v => DataUtility.SetByName(props, "maxWorldMapRadius", v));
            ApplyV<float>(nameof(commandRelayMaxRelayRadius), v => DataUtility.SetByName(props, "maxRelayRadius", v));
            ApplyV<float>(nameof(commandRelayMinRelayRadius), v => DataUtility.SetByName(props, "minRelayRadius", v));
            ApplyV<bool>(nameof(commandRelayCoverWholeMap), v => DataUtility.SetByName(props, "coverWholeMap", v));
        }

        if (overseerProps != null)
        {
            var props = overseerProps;
            ApplyV<bool>(nameof(overseerCanRepair), v => DataUtility.SetByName(props, "canRepair", v));
            ApplyV<int>(nameof(overseerTicksPerHeal), v => DataUtility.SetByName(props, "ticksPerHeal", v));
            ApplyV<bool>(nameof(overseerInstantControl), v => DataUtility.SetByName(props, "instantControl", v));
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
        || Touched(nameof(commandRelayMaxWorldMapRadius))
        || Touched(nameof(commandRelayMaxRelayRadius))
        || Touched(nameof(commandRelayMinRelayRadius))
        || Touched(nameof(commandRelayCoverWholeMap))
        || Touched(nameof(overseerCanRepair))
        || Touched(nameof(overseerTicksPerHeal))
        || Touched(nameof(overseerInstantControl))
        || Touched(nameof(relayHasCommandRelay))
        || Touched(nameof(relayHasOverseer));

    private bool Touched(string field)
        => IsFieldModified(field) || forceWriteAll || forceWriteFields?.Contains(field) == true;

    /// <summary>
    /// 从 <c>CompPropertiesMultipleTurretGun</c> / <c>CompProperties_VehicleWeapon</c> 回填窗口字段。
    /// 槽位列表带 <c>!IsFieldModified</c> 守卫：已清空 / 已编辑的字段读档后不得被 Comp 回填"复活"。
    /// </summary>
    private void LoadTurret(ThingDef d)
    {
        multipleTurretProps = FortifiedTurret.FindMultipleTurret(d);
        if (!IsFieldModified(nameof(turretSlots)))
            turretSlots ??= FortifiedTurret.ReadSlots(multipleTurretProps);

        vehicleWeaponProps = FortifiedTurret.FindVehicleWeapon(d);
        if (vehicleWeaponProps == null) return;
        var props = vehicleWeaponProps;
        vehicleDefaultWeapon ??= DataUtility.GetByName(props, "defaultWeapon") as ThingDef;
        vehicleTurretRotationFollowPawn ??= DataUtility.GetByName(props, "turretRotationFollowPawn") as bool?;
        vehicleRotationSmoothTime ??= DataUtility.AsFloat(DataUtility.GetByName(props, "rotationSmoothTime"));
        vehicleDrawSize ??= DataUtility.AsFloat(DataUtility.GetByName(props, "drawSize"));
    }

    /// <summary>
    /// 把窗口里的值写回炮塔 Comp。副炮按槽位 ID 原地合并（保留渲染配置），主炮直接写字段。
    /// 这些字段都是运行时实时读取，没有缓存需要刷新。
    /// </summary>
    private void ApplyTurret(ThingDef def)
    {
        // FFF 缺席时炮塔字段全部不在 Fields 里，避免撞到不存在的字段。
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;

        var multiple = multipleTurretProps ??= FortifiedTurret.FindMultipleTurret(def);
        ApplyL<TurretSlotRecord>(nameof(turretSlots), v => FortifiedTurret.WriteSlots(def, multiple, v));

        var vehicle = vehicleWeaponProps ??= FortifiedTurret.FindVehicleWeapon(def);
        if (vehicle == null) return;
        var props = vehicle;
        ApplyD<ThingDef>(nameof(vehicleDefaultWeapon), v => DataUtility.SetByName(props, "defaultWeapon", v));
        ApplyV<bool>(nameof(vehicleTurretRotationFollowPawn), v => DataUtility.SetByName(props, "turretRotationFollowPawn", v));
        ApplyV<float>(nameof(vehicleRotationSmoothTime), v => DataUtility.SetByName(props, "rotationSmoothTime", v));
        ApplyV<float>(nameof(vehicleDrawSize), v => DataUtility.SetByName(props, "drawSize", v));
    }

    private void ApplyRaceProp(ThingDef def)
    {
        ApplyV<int>(nameof(mechFixedSkillLevel), v => def.race.mechFixedSkillLevel = v);
        ApplyV<float>(nameof(baseHealthScale), v =>
        {
            def.race.baseHealthScale = v;
            corepartHP = (float?)GetCorepartHP(this);
        });
        ApplyV<float>(nameof(baseBodySize), v => def.race.baseBodySize = v);
        ApplyV<float>(nameof(baseHungerRate), v => def.race.baseHungerRate = v);
        ApplyV<float>(nameof(lifeExpectancy), v => def.race.lifeExpectancy = v);
    }

    private void ApplyEntityCompProps(ThingDef def)
    {
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_ProducesBioferrite bioferrite)
            {
                ApplyV<float>(nameof(bioferriteDensity), v => bioferrite.bioferriteDensity = v);
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
            else if (cp is CompProperties_ProjectileInterceptor interceptor)
            {
                ApplyV<float>(nameof(projectileInterceptorRadius), v => interceptor.radius = v);
                ApplyV<int>(nameof(projectileInterceptorHitPoints), v => interceptor.hitPoints = v);
                ApplyV<int>(nameof(projectileInterceptorChargeDurationTicks), v => interceptor.chargeDurationTicks = v);
                ApplyV<int>(nameof(projectileInterceptorRechargeHitPointsIntervalTicks), v => interceptor.rechargeHitPointsIntervalTicks = v);
                ApplyV<int>(nameof(projectileInterceptorDisarmedByEmpForTicks), v => interceptor.disarmedByEmpForTicks = v);
            }
        }
    }

    private void ApplyAnimalCompProps(ThingDef def)
    {
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_EggLayer eggLayer)
            {
                ApplyV<float>(nameof(eggLayIntervalDays), v => eggLayer.eggLayIntervalDays = v);
                ApplyV<int>(nameof(eggCountRangeMin),
                    v => eggLayer.eggCountRange = new IntRange(v, eggCountRangeMax ?? eggLayer.eggCountRange.max));
                ApplyV<int>(nameof(eggCountRangeMax),
                    v => eggLayer.eggCountRange = new IntRange(eggCountRangeMin ?? eggLayer.eggCountRange.min, v));
                ApplyV<int>(nameof(eggFertilizationCountMax), v => eggLayer.eggFertilizationCountMax = v);
                ApplyV<bool>(nameof(eggLayFemaleOnly), v => eggLayer.eggLayFemaleOnly = v);
                ApplyV<float>(nameof(eggProgressUnfertilizedMax), v => eggLayer.eggProgressUnfertilizedMax = v);
            }
            else if (cp is CompProperties_Milkable milkable)
            {
                ApplyV<int>(nameof(milkIntervalDays), v => milkable.milkIntervalDays = v);
                ApplyV<int>(nameof(milkAmount), v => milkable.milkAmount = v);
                ApplyV<bool>(nameof(milkFemaleOnly), v => milkable.milkFemaleOnly = v);
            }
            else if (cp is CompProperties_Shearable shearable)
            {
                ApplyV<int>(nameof(shearIntervalDays), v => shearable.shearIntervalDays = v);
                ApplyV<int>(nameof(woolAmount), v => shearable.woolAmount = v);
            }
        }
    }

    public static bool AvailableIfEntity(TweakData data) => data.propType switch
    {
        (int)RaceType.Entity => true,
        _ => false,
    };
    public static bool AvailableIfMech(TweakData data) => data.propType switch
    {
        (int)RaceType.Mechanoid => true,
        _ => false,
    };

    public static bool AvailableIfLive(TweakData data) => data.propType switch
    {
        (int)RaceType.Mechanoid => false,
        _ => true,
    };

    public static bool AvailableIfBeast(TweakData data) => data.propType switch
    {
        (int)RaceType.Animal => true,
        (int)RaceType.Insect => true,
        _ => false,
    };

    public static bool AvailableIfAnimal(TweakData data) => data.propType switch
    {
        (int)RaceType.Animal => true,
        _ => false,
    };

    public static bool AvailableIfOther(TweakData data) => data.propType switch
    {
        (int)RaceType.Other => true,
        _ => false,
    };

    public enum RaceType
    {
        Animal,
        Humanlike,
        Insect,
        Mechanoid,
        Entity,
        Other,
    }
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(RaceType).GetEnumNames().Select(s => "MST." + s).ToList();

    /// <summary>
    /// 该种族抽得到的手持武器（供 <see cref="TweakLinksWindow"/> 使用）：名下所有兵种候选的并集。
    /// <para>
    /// 判定逻辑与兵种页的 <see cref="PawnKindData.FindWeaponLinks"/> 是同一份实现
    /// （<see cref="PawnKindData.CollectWeaponLinks"/>）—— 两边各写一套，迟早会给出不同的列表。
    /// 只在窗口打开时调用一次，故不缓存。
    /// </para>
    /// </summary>
    public static List<TweakID> FindWeaponLinks(ThingDef race)
        => race?.race == null ? [] : PawnKindData.CollectWeaponLinks(CombatPower.KindsOf(race));

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            TweakDatabase.raceDatas.SetOrAdd(def.defName, id);
            if (def.race.IsAnomalyEntity) return (int)RaceType.Entity;
            if (def.race.Humanlike) return (int)RaceType.Humanlike;
            if (def.race.Insect) return (int)RaceType.Insect;
            if (def.race.IsMechanoid) return (int)RaceType.Mechanoid;
            if (def.race.Animal) return (int)RaceType.Animal;
        }
        return (int)RaceType.Other;
    }




}