using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
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
    [TweakField(StatDef = "PsychicSensitivity", Available = nameof(AvailableIfLive))]
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

    static System.Reflection.FieldInfo CompPropertiesStudiableAnomalyKnowledge = AccessTools.Field(typeof(CompProperties_Studiable), "anomalyKnowledge");

    private static object? GetCorepartHP(TweakData data) =>
    (data.def is ThingDef d ? (d.race?.body?.corePart?.def?.hitPoints * d.race?.baseHealthScale) ?? null : null);

    public static object? GetDPS(TweakData data) =>
    (data.def is ThingDef d ? DataUtility.CalculateExpectedDPS(d.tools) : null);

    public override void SetDef(Def def, BalanceTweakSettings.SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        MeleeDPS ??= (float?)GetDPS(this);
        corepartHP ??= (float?)GetCorepartHP(this);
        if (def is ThingDef d)
        {
            bodyDef ??= d.race?.body != null ? new TweakID(d.race.body.defName, SettingType.BodyDef) : null;
            mechFixedSkillLevel ??= d.race?.mechFixedSkillLevel;
            baseHealthScale ??= d.race?.baseHealthScale;
            baseBodySize ??= d.race?.baseBodySize;
            baseHungerRate ??= d.race?.baseHungerRate;
            lifeExpectancy ??= d.race?.lifeExpectancy;
            statBases ??= d.statBases;
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
        ApplyRaceProp(def);
        ApplyDefStats(def);
        ApplyEntityCompProps(def);
        ApplyAnimalCompProps(def);
    }

    private void ApplyRaceProp(ThingDef def)
    {
        if (mechFixedSkillLevel.HasValue) { def.race.mechFixedSkillLevel = mechFixedSkillLevel.Value; }
        if (baseHealthScale.HasValue)
        {
            def.race.baseHealthScale = baseHealthScale.Value;
            corepartHP = (float?)GetCorepartHP(this);
        }
        if (baseBodySize.HasValue) { def.race.baseBodySize = baseBodySize.Value; }
        if (baseHungerRate.HasValue) { def.race.baseHungerRate = baseHungerRate.Value; }
        if (lifeExpectancy.HasValue) { def.race.lifeExpectancy = lifeExpectancy.Value; }
    }

    private void ApplyEntityCompProps(ThingDef def)
    {
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_ProducesBioferrite bioferrite)
            {
                if (bioferriteDensity.HasValue) bioferrite.bioferriteDensity = bioferriteDensity.Value;
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
            else if (cp is CompProperties_ProjectileInterceptor interceptor)
            {
                if (projectileInterceptorRadius.HasValue) interceptor.radius = projectileInterceptorRadius.Value;
                if (projectileInterceptorHitPoints.HasValue) interceptor.hitPoints = projectileInterceptorHitPoints.Value;
                if (projectileInterceptorChargeDurationTicks.HasValue) interceptor.chargeDurationTicks = projectileInterceptorChargeDurationTicks.Value;
                if (projectileInterceptorRechargeHitPointsIntervalTicks.HasValue) interceptor.rechargeHitPointsIntervalTicks = projectileInterceptorRechargeHitPointsIntervalTicks.Value;
                if (projectileInterceptorDisarmedByEmpForTicks.HasValue) interceptor.disarmedByEmpForTicks = projectileInterceptorDisarmedByEmpForTicks.Value;
            }
        }
    }

    private void ApplyAnimalCompProps(ThingDef def)
    {
        foreach (var cp in def.comps)
        {
            if (cp is CompProperties_EggLayer eggLayer)
            {
                if (eggLayIntervalDays.HasValue) eggLayer.eggLayIntervalDays = eggLayIntervalDays.Value;
                if (eggCountRangeMin.HasValue || eggCountRangeMax.HasValue)
                {
                    eggLayer.eggCountRange = new IntRange(
                        eggCountRangeMin ?? eggLayer.eggCountRange.min,
                        eggCountRangeMax ?? eggLayer.eggCountRange.max);
                }
                if (eggFertilizationCountMax.HasValue) eggLayer.eggFertilizationCountMax = eggFertilizationCountMax.Value;
                if (eggLayFemaleOnly.HasValue) eggLayer.eggLayFemaleOnly = eggLayFemaleOnly.Value;
                if (eggProgressUnfertilizedMax.HasValue) eggLayer.eggProgressUnfertilizedMax = eggProgressUnfertilizedMax.Value;
            }
            else if (cp is CompProperties_Milkable milkable)
            {
                if (milkIntervalDays.HasValue) milkable.milkIntervalDays = milkIntervalDays.Value;
                if (milkAmount.HasValue) milkable.milkAmount = milkAmount.Value;
                if (milkFemaleOnly.HasValue) milkable.milkFemaleOnly = milkFemaleOnly.Value;
            }
            else if (cp is CompProperties_Shearable shearable)
            {
                if (shearIntervalDays.HasValue) shearable.shearIntervalDays = shearIntervalDays.Value;
                if (woolAmount.HasValue) shearable.woolAmount = woolAmount.Value;
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