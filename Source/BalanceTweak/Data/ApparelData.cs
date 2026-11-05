using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.DataUtility;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Apparel, Priority = 600, MatchMethod = nameof(DataUtility.MatchApparel))]
public class ApparelData : TweakData<ApparelData>
{
    // 字段定义

    [TweakField(StatDef = "ArmorRating_Sharp")]
    public float? sharpArmor = null;
    [TweakField(StatDef = "ArmorRating_Blunt")]
    public float? bluntArmor = null;
    [TweakField(StatDef = "ArmorRating_Heat")]
    public float? heatArmor = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalSharpArmor = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalBluntArmor = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalHeatArmor = null;
    [TweakField(StatDef = "StuffEffectMultiplierArmor", Style = ColumnStyle.Prec)]
    public float? stuffEffectMultiplierArmor = null;

    [TweakField(StatDef = "Insulation_Cold")]
    public float? insulationCold = null;
    [TweakField(StatDef = "Insulation_Heat")]
    public float? insulationHeat = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalInsulationCold = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalInsulationHeat = null;
    [TweakField(StatDef = "StuffEffectMultiplierInsulation_Cold", Style = ColumnStyle.Prec)]
    public float? stuffEffectMultiplierInsulationCold = null;
    [TweakField(StatDef = "StuffEffectMultiplierInsulation_Heat", Style = ColumnStyle.Prec)]
    public float? stuffEffectMultiplierInsulationHeat = null;

    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalMarketValue = null;
    //[TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec)]
    //public float? flammability = null;
    [TweakField(StatDef = "WorkToMake", Style = ColumnStyle.Int)]
    public float? workToMake = null;

    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Int)]
    public float? maxHitPoints = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? costStuffCount = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList)]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<StuffCategoryDef>? stuffCategories = null;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Default = nameof(GetIsMadeofStuff))]
    //public bool? isMadeofStuff = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isCompQuality = null;

    [TweakField(DataType = ColumnDataType.Display)]
    public float? equipPower = null;
    //[TweakField(DataType = ColumnDataType.Display)]
    //public float? equipCP = null;

    [TweakField(StatDef = "EnergyShieldEnergyMax", Style = ColumnStyle.Prec)]
    public float? energyShieldEnergyMax = null;
    [TweakField(StatDef = "EnergyShieldRechargeRate", Style = ColumnStyle.Prec)]
    public float? energyShieldRechargeRate = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksRangedWeapons = null;

    [TweakField(IsEquippedStat = true, StatDef = "MoveSpeed")]
    public float? moveSpeed = null;
    [TweakField(IsEquippedStat = true, StatDef = "WorkSpeedGlobal", Style = ColumnStyle.Prec)]
    public float? workSpeedGlobal = null;
    [TweakField(IsEquippedStat = true, StatDef = "MechBandwidth", MayRequire = "Ludeon.RimWorld.Biotech", Style = ColumnStyle.Int)]
    public float? mechBandwidth = null;
    [TweakField(IsEquippedStat = true, StatDef = "VacuumResistance", MayRequire = "ludeon.rimworld.odyssey", Style = ColumnStyle.Prec)]
    public float? vacuumResistance = null;
    [TweakField(IsEquippedStat = true, StatDef = "WorkSpeedGlobalOffsetMech", MayRequire = "Ludeon.RimWorld.Biotech", Style = ColumnStyle.Prec)]
    public float? workSpeedGlobalOffsetMech = null;
    [TweakField(IsEquippedStat = true, StatDef = "PsychicSensitivity")]
    public float? psychicSensitivity = null;
    [TweakField(IsEquippedStat = true, StatDef = "RangedCooldownFactor", Style = ColumnStyle.Prec)]
    public float? rangedCooldownFactor = null;
    [TweakField(IsEquippedStat = true, StatDef = "ShootingAccuracyPawn", Style = ColumnStyle.Int)]
    public float? shootingAccuracyPawn = null;
    [TweakField(IsEquippedStat = true, StatDef = "MeleeHitChance", Style = ColumnStyle.Int)]
    public float? MeleeHitChance = null;
    [TweakField(IsEquippedStat = true, StatDef = "AimingDelayFactor", Style = ColumnStyle.Prec)]
    public float? aimingDelayFactor = null;
    [TweakField(IsEquippedStat = true, StatDef = "MeleeDamageFactor", Style = ColumnStyle.Prec)]
    public float? meleeDamageFactor = null;
    [TweakField(IsEquippedStat = true, StatDef = "MeleeCooldownFactor", Style = ColumnStyle.Prec)]
    public float? meleeCooldownFactor = null;
    [TweakField(IsEquippedStat = true, StatDef = "MeleeDodgeChance", Style = ColumnStyle.Prec)]
    public float? meleeDodgeChance = null;
    [TweakField(IsEquippedStat = true, StatDef = "IncomingDamageFactor", Style = ColumnStyle.Prec)]
    public float? incomingDamageFactor = null;
    [TweakField(IsEquippedStat = true, StatDef = "CarryingCapacity", Style = ColumnStyle.Int)]
    public float? carryingCapacity = null;
    [TweakField(IsEquippedStat = true, StatDef = "VEF_MassCarryCapacity", MayRequire = "oskarpotocki.vanillafactionsexpanded.core")]
    public float? massCarryCapacity = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<BodyPartGroupDef>? bodyPartGroups = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ApparelLayerDef>? layers = null;
    /// <summary>
    /// 衣物自身的标签（<c>ApparelProperties.tags</c>，原版默认空列表、不允许为 null）。
    /// 角色生成时按 <c>PawnKindDef.apparelTags</c> 匹配这些标签挑衣服，派系与诉求筛选也按它归类。
    /// </summary>
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? apparelTagList = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? thingSetMakerTags = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? equippedStatOffsets = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? careIfWornByCorpse = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? careIfDamaged = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(Gender))]
    public Gender? gender = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(DevelopmentalStage))]
    public DevelopmentalStage? developmentalStageFilter = null;

    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfUtility))]
    public bool? removeReloadableComp = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfUtility))]
    public int? reloadableMaxCharges = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfUtility))]
    public ThingDef? reloadableAmmoDef = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfUtility))]
    public int? reloadableAmmoCountToRefill = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfUtility))]
    public int? reloadableAmmoCountPerCharge = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfUtility))]
    public int? reloadableBaseReloadTicks = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfUtility))]
    public bool? reloadableReplenishAfterCooldown = null;

    // ── Fortified 中继指挥（可选 Mod，见 RelayConfigWindow）──
    // 服装只支持 CompProperties_SubRelay（Fortified 把穿在身上的服装也当作副中继）。
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(RelayConfigWindow), MayRequire = TweakDatabase.FFF)]
    public TweakID? relayConfig = null;

    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? relayHasSubRelay = null;

    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.FFF)]
    public float? subRelayRange = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Bool, MayRequire = TweakDatabase.FFF)]
    public bool? subRelayCoverWholeMap = null;

    private object? subRelayProps = null;

    // ── Fortified 多炮塔（可选 Mod，见 TurretConfigWindow）──
    // 服装只支持副炮槽（CompMultipleTurretGun 挂在护甲上时由 CompDrawWornExtras 绘制）。
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(TurretConfigWindow),
        MayRequire = TweakDatabase.FFF, Hidden = nameof(HiddenIfNoTurretComp))]
    public TweakID? turretConfig = null;

    public static bool HiddenIfNoTurretComp(TweakData data) => !FortifiedTurret.HasSlots(data.def);

    [TweakField(NotAColumn = true, Style = ColumnStyle.TurretSlotList, MayRequire = TweakDatabase.FFF)]
    public List<TurretSlotRecord>? turretSlots = null;

    private object? multipleTurretProps = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // ── Atmosphere Overhaul：呼吸供给 / 压力壳（可选 Mod）──
    // 只有挂了 AtmosphereApparelExtension 的服饰（AO_OxygenMask 之类）才有这几格；
    // 真空头盔的呼吸供给是 AO 按 VacuumResistance 自动给的，不走这个扩展。
    // 四格由「大气服饰」触发列 + 配置窗口编辑（AtmosphereConfigWindow）。

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(AtmosphereConfigWindow), MayRequire = TweakDatabase.AO,
        Hidden = nameof(HiddenIfNoAoApparelExtension))]
    public TweakID? aoApparelConfig = null;

    /// <summary>
    /// **三态 int**：<c>1</c> = 强制当作呼吸供给；<c>0</c> = 强制不是；<c>-1</c> = 按真空耐性自动判断。
    /// <para>0 与 -1 语义不同（强制否 vs 自动），所以别把 0 当"未设置"。</para>
    /// </summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? breathingSupply = null;

    /// <summary>同一套三态，自动档的判据是"包住躯干"。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.AO)]
    public int? pressureShell = null;

    /// <summary>气瓶当量。<c>&lt;= 0</c> 时自动取 <c>max(1, round(3 × 真空耐性))</c>。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? capacityAirTanks = null;

    /// <summary>壳内维持的压力（hPa）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? suitPressureHpa = null;

    public static bool HiddenIfNoAoApparelExtension(TweakData data) => !AoHelper.HasExt(data.def, AoHelper.ApparelExt);

    private static object? GetNormalSharpArmor(TweakData data) => GetDefaultStuffValue(data, StatDefOf.ArmorRating_Sharp);
    private static object? GetNormalBluntArmor(TweakData data) => GetDefaultStuffValue(data, StatDefOf.ArmorRating_Blunt);
    private static object? GetNormalHeatArmor(TweakData data) => GetDefaultStuffValue(data, StatDefOf.ArmorRating_Heat);
    private static object? GetNormalInsulationCold(TweakData data) => GetDefaultStuffValue(data, StatDefOf.Insulation_Cold);
    private static object? GetNormalInsulationHeat(TweakData data) => GetDefaultStuffValue(data, StatDefOf.Insulation_Heat);
    static float? PowerArmorPoint = null;
    static float? LArmorMult = null;
    private static object? GetEquipPower(TweakData data, bool? isQ)
    {
        isQ = (isQ ?? false);
        if (LArmorMult == null)
        {
            LArmorMult = GetLegendaryFactor(StatDefOf.ArmorRating_Sharp);
        }
        var lmm = LArmorMult.Value;
        if (PowerArmorPoint == null)
        {
            var reference = DefDatabase<ThingDef>.GetNamed("Apparel_PowerArmor");
            var Sharp = Mathf.Pow(limitMaxArmor(reference.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) * lmm) * 1.3f + 1f, 2f) - 1f;
            var Blunt = Mathf.Pow(limitMaxArmor(reference.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt) * lmm) + 1f, 2f) - 1f;
            var Heat = Mathf.Pow(limitMaxArmor(reference.GetStatValueAbstract(StatDefOf.ArmorRating_Heat) * lmm) + 1f, 2f) - 1f;
            var Tcold = Mathf.Pow(reference.GetStatValueAbstract(StatDefOf.Insulation_Cold) * lmm, 0.5f) / 10f;
            var Theat = Mathf.Pow(reference.GetStatValueAbstract(StatDefOf.Insulation_Heat) * lmm, 0.5f) / 10f;
            PowerArmorPoint = Mathf.Max(Sharp + Blunt + Heat + Tcold + Theat, 1f);
        }
        lmm = isQ.Value ? LArmorMult.Value : 1f;

        // 复用 SetDef / Apply 已经算好的五个显示字段，只有字段为 null 时才回退重新求值。
        var ad = data as ApparelData;
        var nSharp = ad?.normalSharpArmor ?? (float?)GetNormalSharpArmor(data) ?? 0f;
        var nBlunt = ad?.normalBluntArmor ?? (float?)GetNormalBluntArmor(data) ?? 0f;
        var nHeat = ad?.normalHeatArmor ?? (float?)GetNormalHeatArmor(data) ?? 0f;
        var nInsulationCold = ad?.normalInsulationCold ?? (float?)GetNormalInsulationCold(data);
        var nInsulationHeat = ad?.normalInsulationHeat ?? (float?)GetNormalInsulationHeat(data);

        var bSharp = Mathf.Pow((limitMaxArmor(nSharp) * lmm) * 1.3f + 1f, 2f) - 1f;
        var bBlunt = Mathf.Pow((limitMaxArmor(nBlunt) * lmm) + 1f, 2f) - 1f;
        var bHeat = Mathf.Pow((limitMaxArmor(nHeat) * lmm) + 1f, 2f) - 1f;
        var bTcold = Mathf.Pow((nInsulationCold * lmm ?? 0f), 0.5f) / 10f;
        var bTheat = Mathf.Pow((nInsulationHeat * lmm ?? 0f), 0.5f) / 10f;

        var shield = 0f;
        if (data.def is ThingDef def && def.GetCompProperties<CompProperties_Shield>() != null)
        {
            shield += def.GetStatValueAbstract(StatDefOf.EnergyShieldEnergyMax) * 2f * (isQ.Value ? 2.1f : 1f);
            shield += def.GetStatValueAbstract(StatDefOf.EnergyShieldRechargeRate) * 20f * (isQ.Value ? 1.3f : 1f);
        }
        var r = (bSharp + bBlunt + bHeat + bTcold + bTheat + shield);
        return r / PowerArmorPoint.Value * 100f;

        static float limitMaxArmor(float num)
        {
            var max = StatDefOf.ArmorRating_Sharp.maxValue;
            if (TweakDatabase.VCTAMDown && num > 2f) { num = 2f + Mathf.Log(num - 1f, 5f); }
            return Mathf.Min(num, max);
        }
    }

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        costStuffCount ??= (int?)GetCostStuffCount(this);
        isCompQuality ??= (bool?)GetIsCompQuality(this);
        if (def is ThingDef d)
        {
            if (!IsFieldModified(nameof(costList))) costList ??= d.costList?.ToList();
            if (!IsFieldModified(nameof(stuffCategories))) stuffCategories ??= d.stuffCategories?.ToList();
            techLevel ??= d.techLevel;
            if (!IsFieldModified(nameof(bodyPartGroups))) bodyPartGroups ??= d.apparel.bodyPartGroups;
            if (!IsFieldModified(nameof(layers))) layers ??= d.apparel.layers;
            if (!IsFieldModified(nameof(apparelTagList))) apparelTagList ??= d.apparel.tags;
            careIfWornByCorpse ??= d.apparel.careIfWornByCorpse;
            careIfDamaged ??= d.apparel.careIfDamaged;
            gender ??= d.apparel.gender;
            developmentalStageFilter ??= d.apparel.developmentalStageFilter;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(thingSetMakerTags))) thingSetMakerTags ??= d.thingSetMakerTags;
            if (!IsFieldModified(nameof(equippedStatOffsets))) equippedStatOffsets ??= d.equippedStatOffsets;
            var shield = d.GetCompProperties<CompProperties_Shield>();
            if (shield != null)
            {
                blocksRangedWeapons ??= shield.blocksRangedWeapons;
            }
            var reloadable = d.GetCompProperties<CompProperties_ApparelReloadable>();
            if (reloadable != null)
            {
                reloadableAmmoDef ??= reloadable.ammoDef;
                reloadableAmmoCountToRefill ??= reloadable.ammoCountToRefill;
                reloadableAmmoCountPerCharge ??= reloadable.ammoCountPerCharge;
                reloadableBaseReloadTicks ??= reloadable.baseReloadTicks;
                reloadableReplenishAfterCooldown ??= reloadable.replenishAfterCooldown;
                reloadableMaxCharges ??= reloadable.maxCharges;
            }
            LoadRelay(d);
            relayHasSubRelay ??= subRelayProps != null;
            LoadTurret(d);
            LoadAoExtension(d);
        }
        //equipCP = (float?)GetEquipCP(this, isCompQuality, equipPower);
    }

    /// <summary>回填 AO 的服饰扩展；没有扩展的服饰整块跳过。</summary>
    private void LoadAoExtension(ThingDef d)
    {
        var ext = AoHelper.Ext(d, AoHelper.ApparelExt);
        if (ext == null) return;
        breathingSupply ??= AoHelper.I(ext, "breathingSupply");
        pressureShell ??= AoHelper.I(ext, "pressureShell");
        capacityAirTanks ??= AoHelper.F(ext, "capacityAirTanks");
        suitPressureHpa ??= AoHelper.F(ext, "suitPressureHpa");
    }

    /// <summary>写回 AO 的服饰扩展（没有扩展的服饰直接跳过）。</summary>
    private void ApplyAoExtension(ThingDef def)
    {
        var ext = AoHelper.Ext(def, AoHelper.ApparelExt);
        if (ext == null) return;
        // 目标字段在扩展上是非空 int（-1 只是取值），所以用 ApplyV 而不是 ApplyN。
        AoHelper.ApplyF<int>(this, ext, "breathingSupply", nameof(breathingSupply));
        AoHelper.ApplyF<int>(this, ext, "pressureShell", nameof(pressureShell));
        AoHelper.ApplyF<float>(this, ext, "capacityAirTanks", nameof(capacityAirTanks));
        AoHelper.ApplyF<float>(this, ext, "suitPressureHpa", nameof(suitPressureHpa));
    }

    public override void EnsureDisplayValues()
    {
        if (displayValuesEnsured) return;
        // def 还没绑定就先别算，也别置标志：那一刻算出来的 null 会被永久冻住。
        if (def == null) return;
        displayValuesEnsured = true;
        normalSharpArmor = (float?)GetNormalSharpArmor(this);
        normalBluntArmor = (float?)GetNormalBluntArmor(this);
        normalHeatArmor = (float?)GetNormalHeatArmor(this);
        normalInsulationCold = (float?)GetNormalInsulationCold(this);
        normalInsulationHeat = (float?)GetNormalInsulationHeat(this);
        normalMarketValue = (float?)GetNormalMarketValue(this);
        equipPower = (float?)GetEquipPower(this, isCompQuality);
    }

    private static void OnChangeStuffEffectMultiplierArmor(TweakData data)
    {
        if (data is ApparelData d)
        {
            d.normalSharpArmor = (float?)GetNormalSharpArmor(d);
            d.normalBluntArmor = (float?)GetNormalBluntArmor(d);
            d.normalHeatArmor = (float?)GetNormalHeatArmor(d);
        }
    }

    private static void OnChangeStuffEffectMultiplierInsulationCold(TweakData data)
    {
        if (data is ApparelData d)
        {
            d.normalInsulationCold = (float?)GetNormalInsulationCold(d);
        }
    }
    private static void OnChangeStuffEffectMultiplierInsulationHeat(TweakData data)
    {
        if (data is ApparelData d)
        {
            d.normalInsulationHeat = (float?)GetNormalInsulationHeat(d);
        }
    }

    //public override void SetDef(Def def, SettingType type, bool tweaked = false)
    //{
    //    base.SetDef(def, type, tweaked);
    //}

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyApparelProp(def);
        ApplyDefStats(def);
        ApplyRelay(def);
        ApplyTurret(def);
        ApplyAoExtension(def);
        OnChangeStuffEffectMultiplierArmor(this);
        OnChangeStuffEffectMultiplierInsulationCold(this);
        OnChangeStuffEffectMultiplierInsulationHeat(this);
        equipPower = (float?)GetEquipPower(this, isCompQuality);
        //equipCP = (float?)GetEquipCP(this);
        // 参考列按"当前 def 现算"：改完成本 / 材料后，下一次读就该是新值。
        displayValuesEnsured = false;
    }
    /// <summary>从 <c>CompProperties_SubRelay</c> 回填窗口字段；sync 时未修改字段跟随 Comp 当前值。</summary>
    private void LoadRelay(ThingDef d, bool syncUnmodified = false)
    {
        subRelayProps = FortifiedRelay.FindSubRelay(d);
        if (subRelayProps == null) return;
        var props = subRelayProps;
        if (!IsFieldModified(nameof(subRelayRange)) && (syncUnmodified || subRelayRange == null))
            subRelayRange = DataUtility.AsFloat(DataUtility.GetByName(props, "relayRange"));
        if (!IsFieldModified(nameof(subRelayCoverWholeMap)) && (syncUnmodified || subRelayCoverWholeMap == null))
            subRelayCoverWholeMap = DataUtility.GetByName(props, "coverWholeMap") as bool?;
    }

    /// <summary>先处理 Comp 增删，再原地写字段；没有 Comp 的服装直接跳过，并登记缓存刷新。</summary>
    private void ApplyRelay(ThingDef def)
    {
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;

        ApplyV<bool>(nameof(relayHasSubRelay),
            v => FortifiedRelay.SetCompEnabled(def, FortifiedRelay.SubRelayPropsType, v));

        LoadRelay(def, syncUnmodified: !forceWriteAll);

        if (subRelayProps != null)
        {
            var props = subRelayProps; // 闭包只捕获 object
            ApplyV<float>(nameof(subRelayRange), v => DataUtility.SetByName(props, "relayRange", v));
            ApplyV<bool>(nameof(subRelayCoverWholeMap), v => DataUtility.SetByName(props, "coverWholeMap", v));
        }

        if (RelayTouched) FortifiedRelay.RequestRefresh(def);
    }

    /// <summary>比三态多判 <c>forceWriteAll</c> / <c>forceWriteFields</c>：重置先写回原值再清标记。</summary>
    private bool RelayTouched
        => Touched(nameof(subRelayRange))
        || Touched(nameof(subRelayCoverWholeMap))
        || Touched(nameof(relayHasSubRelay));

    private bool Touched(string field)
        => IsFieldModified(field) || forceWriteAll || forceWriteFields?.Contains(field) == true;

    /// <summary>从 <c>CompPropertiesMultipleTurretGun</c> 回填副炮槽快照（列表带守卫）。</summary>
    private void LoadTurret(ThingDef d)
    {
        multipleTurretProps = FortifiedTurret.FindMultipleTurret(d);
        if (!IsFieldModified(nameof(turretSlots)))
            turretSlots ??= FortifiedTurret.ReadSlots(multipleTurretProps);
    }

    /// <summary>按槽位 ID 原地写回副炮槽；没有该 Comp 的服装直接跳过。</summary>
    private void ApplyTurret(ThingDef def)
    {
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;
        var multiple = multipleTurretProps ??= FortifiedTurret.FindMultipleTurret(def);
        ApplyL<TurretSlotRecord>(nameof(turretSlots), v => FortifiedTurret.WriteSlots(def, multiple, v));
    }

    private void ApplyApparelProp(ThingDef def)
    {
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);
        ApplyL<string>(nameof(thingSetMakerTags), v => def.thingSetMakerTags = v);
        if (IsFieldModified(nameof(thingSetMakerTags))) DataUtility.RefreshThingSetMakerFilters();
        bool costChanged = false;
        ApplyV<int>(nameof(costStuffCount), v => { def.costStuffCount = v; costChanged = true; });
        ApplyL<ThingDefCountClass>(nameof(costList), v => { def.costList = v; costChanged = true; });
        ApplyL<StuffCategoryDef>(nameof(stuffCategories), v => { def.stuffCategories = v; costChanged = true; });

        if (costChanged)
        {
            int regenerated = 0;
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.Where(r =>
                r.generated && r.products != null && r.products.Count == 1 && r.products[0].thingDef == def))
            {
                RecipeDefGenerator.SetIngredients(recipe, def, recipe.adjustedCount);
                regenerated++;
            }
            InitPerf.AddRecipeRegen(regenerated);
            CostListCalculator.Reset();
        }

        ApplyV<bool>(nameof(isCompQuality), v =>
        {
            if (!v)
            {
                def.comps.RemoveWhere(c => c.compClass == typeof(CompQuality));
            }
            else
            {
                if (!def.comps.Any(c => c.compClass == typeof(CompQuality)))
                {
                    var c = new CompProperties(typeof(CompQuality));
                    def.comps.Add(c);
                }
            }
        });
        ApplyV<bool>(nameof(blocksRangedWeapons), v =>
        {
            var shield = def.GetCompProperties<CompProperties_Shield>();
            if (shield != null)
            {
                shield.blocksRangedWeapons = v;
            }
        });
        if (StateOf(nameof(removeReloadableComp)) == FieldState.Set && (bool)RawVal(nameof(removeReloadableComp))!)
        {
            def.comps.RemoveWhere(c => c is CompProperties_ApparelReloadable);
        }
        else
        {
            var reloadable = def.GetCompProperties<CompProperties_ApparelReloadable>();
            if (reloadable != null)
            {
                ApplyD<ThingDef>(nameof(reloadableAmmoDef), v => reloadable.ammoDef = v);
                ApplyV<int>(nameof(reloadableAmmoCountToRefill), v => reloadable.ammoCountToRefill = v);
                ApplyV<int>(nameof(reloadableAmmoCountPerCharge), v => reloadable.ammoCountPerCharge = v);
                ApplyV<int>(nameof(reloadableBaseReloadTicks), v => reloadable.baseReloadTicks = v);
                ApplyV<bool>(nameof(reloadableReplenishAfterCooldown), v => reloadable.replenishAfterCooldown = v);
                ApplyV<int>(nameof(reloadableMaxCharges), v => reloadable.maxCharges = v);
            }
        }
        ApplyL<BodyPartGroupDef>(nameof(bodyPartGroups), v => def.apparel.bodyPartGroups = v);
        ApplyL<ApparelLayerDef>(nameof(layers), v => def.apparel.layers = v);
        // ApparelProperties.tags 原版默认是空列表且运行期直接 .Count / .Contains；
        // ApplyL 清空时交的就是空列表，这里 ?? 只是给"字段为 null 的权威重置"兜一层。
        ApplyL<string>(nameof(apparelTagList), v => def.apparel.tags = v ?? new List<string>());
        ApplyV<bool>(nameof(careIfWornByCorpse), v => def.apparel.careIfWornByCorpse = v);
        ApplyV<bool>(nameof(careIfDamaged), v => def.apparel.careIfDamaged = v);
        ApplyV<Gender>(nameof(gender), v => def.apparel.gender = v);
        ApplyV<DevelopmentalStage>(nameof(developmentalStageFilter), v => def.apparel.developmentalStageFilter = v);
        ApplyL<StatModifier>(nameof(statBases), v => def.statBases = v);
        ApplyL<StatModifier>(nameof(equippedStatOffsets), v => def.equippedStatOffsets = v);
    }
    public static bool AvailableIfUtility(TweakData data) => data.propType switch
    {
        (int)ApparelType.Utility => true,
        (int)ApparelType.Special => true,
        _ => false,
    };
    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ApparelType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum ApparelType
    {
        BuildinApparel,
        Headgear,
        OnSkin,
        Outerwear,
        Utility,
        Body,
        Special
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.destroyOnDrop)
            {
                return (int)ApparelType.BuildinApparel;
            }
            var apparel = def.apparel;
            var layers = apparel.layers;
            if (layers != null && layers.Count > 0)
            {
                bool allOnSkin = true;
                bool hasShellOrMiddle = false;
                bool hasHeadOrEye = false;
                foreach (var layer in layers)
                {
                    if (layer != ApparelLayerDefOf.OnSkin)
                    {
                        allOnSkin = false;
                    }
                    if (layer == ApparelLayerDefOf.Shell || layer == ApparelLayerDefOf.Middle)
                    {
                        hasShellOrMiddle = true;
                    }
                    if (layer == ApparelLayerDefOf.EyeCover || layer == ApparelLayerDefOf.Overhead)
                    {
                        hasHeadOrEye = true;
                    }
                }
                if (allOnSkin)
                {
                    return (int)ApparelType.OnSkin;
                }
                if (hasShellOrMiddle)
                {
                    return (int)ApparelType.Outerwear;
                }
                if (hasHeadOrEye)
                {
                    return (int)ApparelType.Headgear;
                }
            }
            if (apparel.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead)
            {
                return (int)ApparelType.Headgear;
            }
            if (!apparel.countsAsClothingForNudity)
            {
                return (int)ApparelType.Utility;
            }
            if (apparel.parentTagDef != null || apparel.wornGraphicPath != "" || !apparel.wornGraphicPaths.NullOrEmpty())
            {
                return (int)ApparelType.Body;
            }
        }
        return (int)ApparelType.Special;
    }





}


