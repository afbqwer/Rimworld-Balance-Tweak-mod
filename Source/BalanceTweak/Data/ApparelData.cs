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
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
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

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

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

        var bSharp = Mathf.Pow((limitMaxArmor((float?)GetNormalSharpArmor(data) ?? 0f) * lmm) * 1.3f + 1f, 2f) - 1f;
        var bBlunt = Mathf.Pow((limitMaxArmor((float?)GetNormalBluntArmor(data) ?? 0f) * lmm) + 1f, 2f) - 1f;
        var bHeat = Mathf.Pow((limitMaxArmor((float?)GetNormalHeatArmor(data) ?? 0f) * lmm) + 1f, 2f) - 1f;
        var bTcold = Mathf.Pow(((float?)GetNormalInsulationCold(data) * lmm ?? 0f), 0.5f) / 10f;
        var bTheat = Mathf.Pow(((float?)GetNormalInsulationHeat(data) * lmm ?? 0f), 0.5f) / 10f;

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
        normalSharpArmor ??= (float?)GetNormalSharpArmor(this);
        normalBluntArmor ??= (float?)GetNormalBluntArmor(this);
        normalHeatArmor ??= (float?)GetNormalHeatArmor(this);
        normalInsulationCold ??= (float?)GetNormalInsulationCold(this);
        normalInsulationHeat ??= (float?)GetNormalInsulationHeat(this);
        normalMarketValue ??= (float?)GetNormalMarketValue(this);
        costStuffCount ??= (int?)GetCostStuffCount(this);
        isCompQuality ??= (bool?)GetIsCompQuality(this);
        equipPower = (float?)GetEquipPower(this, isCompQuality);
        if (def is ThingDef d)
        {
            costList ??= d.costList?.ToList();
            stuffCategories ??= d.stuffCategories?.ToList();
            techLevel ??= d.techLevel;
            bodyPartGroups ??= d.apparel.bodyPartGroups;
            layers ??= d.apparel.layers;
            careIfWornByCorpse ??= d.apparel.careIfWornByCorpse;
            careIfDamaged ??= d.apparel.careIfDamaged;
            gender ??= d.apparel.gender;
            developmentalStageFilter ??= d.apparel.developmentalStageFilter;
            statBases ??= d.statBases;
            equippedStatOffsets ??= d.equippedStatOffsets;
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
        }
        //equipCP = (float?)GetEquipCP(this, isCompQuality, equipPower);
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
        if (defLabel != null) this.def.label = defLabel;
        ApplyApparelProp(def);
        ApplyDefStats(def);
        OnChangeStuffEffectMultiplierArmor(this);
        OnChangeStuffEffectMultiplierInsulationCold(this);
        OnChangeStuffEffectMultiplierInsulationHeat(this);
        equipPower = (float?)GetEquipPower(this, isCompQuality);
        //equipCP = (float?)GetEquipCP(this);
    }
    private void ApplyApparelProp(ThingDef def)
    {
        if (techLevel.HasValue) { def.techLevel = techLevel.Value; }
        bool costChanged = false;
        if (costStuffCount.HasValue) { def.costStuffCount = costStuffCount.Value; costChanged = true; }
        if (costList != null) { def.costList = costList; costChanged = true; }
        if (stuffCategories != null) { def.stuffCategories = stuffCategories; costChanged = true; }

        if (costChanged)
        {
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.Where(r =>
                r.generated && r.products != null && r.products.Count == 1 && r.products[0].thingDef == def))
            {
                RecipeDefGenerator.SetIngredients(recipe, def, recipe.adjustedCount);
            }
            CostListCalculator.Reset();
        }

        if (isCompQuality.HasValue)
        {
            if (isCompQuality.Value == false)
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
        }
        if (blocksRangedWeapons.HasValue)
        {
            var shield = def.GetCompProperties<CompProperties_Shield>();
            if (shield != null)
            {
                shield.blocksRangedWeapons = blocksRangedWeapons.Value;
            }
        }
        if (removeReloadableComp.HasValue && removeReloadableComp.Value)
        {
            def.comps.RemoveWhere(c => c is CompProperties_ApparelReloadable);
        }
        else
        {
            var reloadable = def.GetCompProperties<CompProperties_ApparelReloadable>();
            if (reloadable != null)
            {
                if (reloadableAmmoDef != null) reloadable.ammoDef = reloadableAmmoDef;
                if (reloadableAmmoCountToRefill.HasValue) reloadable.ammoCountToRefill = reloadableAmmoCountToRefill.Value;
                if (reloadableAmmoCountPerCharge.HasValue) reloadable.ammoCountPerCharge = reloadableAmmoCountPerCharge.Value;
                if (reloadableBaseReloadTicks.HasValue) reloadable.baseReloadTicks = reloadableBaseReloadTicks.Value;
                if (reloadableReplenishAfterCooldown.HasValue) reloadable.replenishAfterCooldown = reloadableReplenishAfterCooldown.Value;
                if (reloadableMaxCharges.HasValue) reloadable.maxCharges = reloadableMaxCharges.Value;
            }
        }
        if (bodyPartGroups != null) def.apparel.bodyPartGroups = bodyPartGroups;
        if (layers != null) def.apparel.layers = layers;
        if (careIfWornByCorpse.HasValue) def.apparel.careIfWornByCorpse = careIfWornByCorpse.Value;
        if (careIfDamaged.HasValue) def.apparel.careIfDamaged = careIfDamaged.Value;
        if (gender.HasValue) def.apparel.gender = gender.Value;
        if (developmentalStageFilter.HasValue) def.apparel.developmentalStageFilter = developmentalStageFilter.Value;
        if (statBases != null) def.statBases = statBases;
        if (equippedStatOffsets != null) def.equippedStatOffsets = equippedStatOffsets;
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


