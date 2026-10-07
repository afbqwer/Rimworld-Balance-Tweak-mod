using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.DataUtility;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Weapon, Priority = 200, MatchMethod = nameof(DataUtility.MatchWeapon))]
public class WeaponData : TweakData<WeaponData>, ISubItemHost
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

    // 字段定义
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfTurret))]
    public TweakID? GunTurret = null;

    [TweakField(StatDef = "MarketValue", Available = nameof(AvailableIfNonBuildin))]
    public float? marketValue = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNonBuildin))]
    public float? normalMarketValue = null;

    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? forcedMissRadius = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyTouch", Available = nameof(AvailableIfRanged))]
    public float? accuracyTouch = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyShort", Available = nameof(AvailableIfRanged))]
    public float? accuracyShort = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyMedium", Available = nameof(AvailableIfRanged))]
    public float? accuracyMedium = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyLong", Available = nameof(AvailableIfRanged))]
    public float? accuracyLong = null;
    [TweakField(StatDef = "RangedWeapon_Cooldown", Available = nameof(AvailableIfRanged))]
    public float? cooldown = null;

    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged), GetAbstract = nameof(GetdamageAmountBaseAbstract))]
    public int? damageAmountBase = null;// -1
    [TweakField(Available = nameof(AvailableIfRanged), GetAbstract = nameof(GetArmorPenetrationAbstract))]
    public float? armorPenetrationBase = null;// -1
    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? warmup = null;// -1
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged))]
    public int? ticksBetweenBurstShots = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged))]
    public int? burstShotCount = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? OptimalRange = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? MaxRangedDPS = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? AverageRangedDPS = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? LRangedDPS = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfRanged))]
    public TweakID? Projectile = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfMelee))]
    public float? MeleeDPS = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? LMeleeDPS = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? WeaponTool = null;

    [TweakField(DataType = ColumnDataType.Display)]
    public float? equipPower = null;
    //[TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNonBuildin))]
    //public float? equipCP = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isCompQuality = null;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Default = nameof(GetIsMadeofStuff), Available = nameof(AvailableIfNonBuildin))]
    //public bool? isMadeofStuff = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public int? costStuffCount = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList, Available = nameof(AvailableIfNonBuildin))]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfNonBuildin))]
    public List<StuffCategoryDef>? stuffCategories = null;
    [TweakField(StatDef = "WorkToMake", Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public float? workToMake = null;
    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public float? maxHitPoints = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec, Available = nameof(AvailableIfNonBuildin))]
    public float? flammability = null;

    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? minRange = null;
    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? maxRange = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;

    [TweakField(StatDef = "MeleeWeapon_DamageMultiplier", Style = ColumnStyle.Prec)]
    public float? meleeDamageMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_WarmupMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? warmupMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_DamageMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeDamageMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_ArmorPenetrationMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeArmorPenetrationMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_RangeMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeMultiplier = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? equippedStatOffsets = null;

    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonMelee))]
    public bool? removeReloadableComp = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableMaxCharges = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonMelee))]
    public ThingDef? reloadableAmmoDef = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableAmmoCountToRefill = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableAmmoCountPerCharge = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableBaseReloadTicks = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonMelee))]
    public bool? reloadableReplenishAfterCooldown = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;


    private float? LMAXRangedDPS = null;
    private VerbProperties? verb = null;
    static FieldInfo ForcedMissRadius = AccessTools.Field(typeof(VerbProperties), "forcedMissRadius");
    public override int LoadingOrd => 300;

    private void ApplyWeaponProp(ThingDef def)
    {
        if (techLevel.HasValue) { def.techLevel = techLevel.Value; }
        bool costChanged = false;
        if (costStuffCount.HasValue) { def.costStuffCount = costStuffCount.Value; costChanged = true; }
        if (costList != null) { def.costList = costList?.ToList(); costChanged = true; }
        if (stuffCategories != null) { def.stuffCategories = stuffCategories.ToList(); costChanged = true; }

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
        if (verb != null)
        {
            if (maxRange.HasValue) { verb.range = maxRange.Value; }
            if (minRange.HasValue) { verb.minRange = minRange.Value; }
            if (warmup.HasValue) { verb.warmupTime = warmup.Value; }
            if (ticksBetweenBurstShots.HasValue) { verb.ticksBetweenBurstShots = ticksBetweenBurstShots.Value; }
            if (burstShotCount.HasValue) { verb.burstShotCount = burstShotCount.Value; }
            if (forcedMissRadius.HasValue) { ForcedMissRadius.SetValue(verb, forcedMissRadius.Value); }
            var p = (ProjectileData?)GetData(Projectile);
            if (p != null && p.def is ThingDef pdef)
            {
                p.damageAmountBase = damageAmountBase;
                p.armorPenetrationBase = armorPenetrationBase;
                p.SetProjectileProp(pdef);
            }
        }
    }

    static float? meleeLQualityMult = null;
    static float? AccuracyMediumLMult = null;
    static float? DamageLMultiplier = null;
    static float? APLMultiplier = null;
    static List<Tool>? HumanTool = null;
    public void GenValues()
    {
        if (!meleeLQualityMult.HasValue || !AccuracyMediumLMult.HasValue || !DamageLMultiplier.HasValue)
        {
            meleeLQualityMult = GetLegendaryFactor(StatDefOf.MeleeWeapon_DamageMultiplier);
            AccuracyMediumLMult = GetLegendaryFactor(StatDefOf.AccuracyMedium);
            DamageLMultiplier = GetLegendaryFactor(StatDefOf.RangedWeapon_DamageMultiplier);
            APLMultiplier = GetLegendaryFactor(StatDefOf.RangedWeapon_ArmorPenetrationMultiplier);
        }
        if (HumanTool == null)
        {
            var humanreference = DefDatabase<ThingDef>.GetNamed("Human");
            HumanTool = humanreference.tools;
        }
        if (!isCompQuality.HasValue) { isCompQuality = (bool?)GetIsCompQuality(this) ?? false; }
        if (def is ThingDef d)
        {
            techLevel ??= d.techLevel;
            verb ??= d.Verbs.FirstOrDefault(v => !v.IsMeleeAttack);
            if (verb != null)
            {
                ProjectileData? projectile = null;
                if (Projectile == null && verb.defaultProjectile != null
                    && TweakDatabase.projectileDatas.TryGetValue(verb.defaultProjectile.defName, out var pid))
                {
                    Projectile = pid;
                    var l = GetAllData(pid);
                    projectile = (ProjectileData?)l.FirstOrDefault();
                    foreach (var item in l)
                    {
                        ((ProjectileData)item).RangedWeapon = id;
                    }
                }
                else
                {
                    projectile = (ProjectileData?)GetData(Projectile);
                }
                damageAmountBase ??= projectile?.damageAmountBase;
                armorPenetrationBase ??= projectile?.armorPenetrationBase;
                warmup ??= verb.warmupTime;
                minRange ??= verb.minRange;
                maxRange ??= verb.range;
                ticksBetweenBurstShots ??= verb.ticksBetweenBurstShots;
                burstShotCount ??= verb.burstShotCount;
                forcedMissRadius ??= (float?)ForcedMissRadius.GetValue(verb);
                GenRangeDPS(d, verb);
            }
            var stuff = GetDefaultStuffFromDef(d);
            var md = CalculateExpectedDPS(null, d.tools, meleeDamageMultiplier ?? 1f, 1f, false, stuff);
            MeleeDPS = md;
            if (d.HasAssignableCompFrom(typeof(CompQuality)))
            {
                LMeleeDPS = CalculateExpectedDPS(null, d.tools, meleeLQualityMult.Value * (meleeDamageMultiplier ?? 1f), 1f, false, stuff);
            }
            else
            {
                LMeleeDPS = md;
            }
            //var mm = TryGetStat(d, StatDefOf.MeleeWeapon_DamageMultiplier);
            //if (mm.HasValue)
            //{
            //    MeleeDPS *= mm.Value;
            //    LMeleeDPS *= mm.Value;
            //}

            equipPower = (float?)GetEquipPower(this, HumanTool, isCompQuality.Value, stuff);
            //equipCP = (float?)GetEquipCP(this, equipPower!.Value, isCompQuality);
        }
    }

    private void GenRangeDPS(ThingDef d, VerbProperties verb)
    {
        var dmg = damageAmountBase;
        var cd = cooldown;
        var aim = warmup;
        if (!dmg.HasValue || dmg.Value == -1)
        {
            if (d.projectile?.damageDef == DamageDefOf.Extinguish)
            {
                MaxRangedDPS = 0f;
                AverageRangedDPS = 0f;
                LRangedDPS = 0f;
                return;
            }
            if (Projectile != null)
            {
                var p = GetData(Projectile);
                dmg = (int)ProjectileData.GetdamageAmountBaseAbstract(p);
            }
            else if (verb.beamDamageDef is DamageDef bid)
            {
                dmg = bid.defaultDamage;
            }
        }
        if (!cd.HasValue && verb.defaultCooldownTime > 0) { cd = verb.defaultCooldownTime; }
        if (aim == 0f && GunTurret != null && GetData(GunTurret) is BuildingData bd && bd.def is ThingDef builddef)
        {
            aim = builddef.building?.turretBurstWarmupTime.Average;
        }
        if (!dmg.HasValue) { dmg = 0; } else { dmg = Math.Max(dmg.Value, 0); }
        if (cd.HasValue && aim.HasValue && burstShotCount.HasValue && cd > 0f)
        {
            MaxRangedDPS = (dmg * burstShotCount * (rangeDamageMultiplier ?? 1f))
                / (aim * (warmupMultiplier ?? 1f) + cd + (burstShotCount - 1) * (ticksBetweenBurstShots ?? 0) / 60f);
        }
        else
        {
            return;
        }
        if (verb.verbClass == typeof(Verb_ArcSprayIncinerator))
        {
            MaxRangedDPS /= burstShotCount;
        }
        if (!minRange.HasValue || !maxRange.HasValue)
        {
            AverageRangedDPS = MaxRangedDPS;
            LRangedDPS = AverageRangedDPS * DamageLMultiplier ?? 1f;
        }
        else
        {
            OptimalRange = FindOptimalRange(d, minRange.Value, maxRange.Value);
            var hit = GetHitChanceFactor(d, OptimalRange.Value);
            AverageRangedDPS = MaxRangedDPS * hit;
            if (isCompQuality.GetValueOrDefault())
            {
                var dmgl = (int?)(dmg.Value * DamageLMultiplier ?? 1f);
                var hitl = Mathf.Clamp01(hit * AccuracyMediumLMult ?? 1f);
                var maxdps = (dmgl * burstShotCount * (rangeDamageMultiplier ?? 1f))
                    / (aim * (warmupMultiplier ?? 1f) + cd + (burstShotCount - 1) * ticksBetweenBurstShots / 60f);
                LRangedDPS = maxdps * hitl;
                LMAXRangedDPS = maxdps;
            }
            else { LRangedDPS = AverageRangedDPS; LMAXRangedDPS = MaxRangedDPS; }
        }
    }

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        normalMarketValue ??= (float?)GetNormalMarketValue(this);
        costStuffCount ??= (int?)GetCostStuffCount(this);
        if (def is ThingDef td)
        {
            costList ??= td.costList;
            stuffCategories ??= td.stuffCategories;
            statBases ??= td.statBases;
            equippedStatOffsets ??= td.equippedStatOffsets;
            var reloadable = td.GetCompProperties<CompProperties_ApparelReloadable>();
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
        GenValues();
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        ApplyDefStats(def);
        ApplyWeaponProp(def);
        GenValues();
    }

    static float? RDPS = null;
    static float? HumanDPS = null;

    static float? CRPower = null;
    private static object? GetEquipPower(TweakData data, List<Tool> humantool, bool? isQ, ThingDef? stuff)
    {
        isQ = (isQ ?? false);
        if (data is WeaponData wd && wd.def is ThingDef def)
        {
            if (!RDPS.HasValue || !HumanDPS.HasValue)
            {
                var reference = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
                if (MayRequire("ludeon.rimworld.royalty"))
                {
                    reference = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_MonoSword");
                }
                var rtool = reference.tools;
                RDPS = CalculateExpectedDPS(humantool, rtool, meleeLQualityMult ?? 1.65f, 1f, true);
                HumanDPS = CalculateExpectedDPS(humantool, null, 1f, 1f, true);
            }
            var mdps = CalculateExpectedDPS(def.tools, humantool, isQ.Value ? ((wd.meleeDamageMultiplier ?? 1f) * meleeLQualityMult ?? 1.65f) : (wd.meleeDamageMultiplier ?? 1f), 1f, true, stuff);

            var meleePower = Mathf.Max(0f, (mdps - HumanDPS.Value)
                / (RDPS.Value - HumanDPS.Value)
                * 100f);
            if (wd.propType == (int)WeaponType.MeleeWeapon) //近战则不计算远程
            {
                return meleePower;
            }
            if (!CRPower.HasValue)
            {
                var reference = DefDatabase<ThingDef>.GetNamed("Gun_ChargeRifle");
                (var a, var b) = CalculateRangeDPS(reference, true);
                if (a.HasValue && b.HasValue && reference != null)
                {
                    var ap = reference.Verbs.FirstOrDefault().defaultProjectile.projectile.GetArmorPenetration(null, null) * (APLMultiplier ?? 1f);
                    if (TweakDatabase.VCTAPDown && ap > 0.5f) { ap = 0.5f + Mathf.Log(ap + 0.5f, 5f); }
                    CRPower = Mathf.Sqrt(a.Value * b.Value) * Mathf.Pow(
                        1f + ap,
                        2f);
                    var chargeVerb = reference.Verbs.FirstOrDefault(v => !v.IsMeleeAttack);
                    if (chargeVerb != null && chargeVerb.range > 0f)
                    {
                        CRPower *= Mathf.Log10(chargeVerb.range);
                    }
                }
                CRPower ??= Mathf.Sqrt(20.33f * 21.18f) * Mathf.Pow(1f + 0.18f * 1.5f, 2f) * Mathf.Log10(30f);//19.52134 * log10(30)
            }
            var rangePower = 0f;
            if (wd.LRangedDPS.HasValue)
            {
                var ap = GetArmorPenetrationAbstract(wd);
                if (ap == -1) { ap = 0f; }
                if (isQ.Value) { ap *= APLMultiplier!.Value; }
                if (TweakDatabase.VCTAPDown && ap > 0.5f) { ap = 0.5f + Mathf.Log(ap + 0.5f, 5f); }
                var explosionFactor = 1f;
                if (wd.Projectile != null && GetData(wd.Projectile) is ProjectileData pd && pd.explosionRadius.HasValue && pd.explosionRadius.Value > 0f)
                {
                    explosionFactor = 1f + Mathf.Log(1f + pd.explosionRadius.Value);
                }
                var llpower = Mathf.Sqrt(wd.LRangedDPS.Value * wd.LMAXRangedDPS.GetValueOrDefault(wd.LRangedDPS.Value))
                    * Mathf.Pow(1f + ap, 2f) * explosionFactor;
                rangePower = Mathf.Max(0f, llpower / CRPower.Value * 100f);
                if (wd.maxRange.HasValue && wd.maxRange.Value > 0f)
                {
                    rangePower *= Mathf.Log10(wd.maxRange.Value);
                }
            }
            return Mathf.Max(meleePower, rangePower);

        }
        return 0f;
    }

    private static float GetdamageAmountBaseAbstract(TweakData data)
    {
        if (data is WeaponData wd && GetData(wd.Projectile) is ProjectileData pd
            && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetDamageAmount(null, null, null);
        }
        return -1f;
    }
    private static float GetArmorPenetrationAbstract(TweakData data)
    {
        if (data is WeaponData wd && GetData(wd.Projectile) is ProjectileData pd
            && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetArmorPenetration(null, null);
        }
        return -1f;
    }

    public static bool AvailableIfMelee(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => true,
        _ => false,
    };
    public static bool AvailableIfRanged(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => false,
        _ => true,
    };
    public static bool AvailableIfTurret(TweakData data) => data.propType switch
    {
        (int)WeaponType.TurretWeapon => true,
        _ => false,
    };

    public static bool AvailableIfNonBuildin(TweakData data) => data.propType switch
    {
        (int)WeaponType.TurretWeapon => false,
        (int)WeaponType.BuildinWeapon => false,
        _ => true,
    };
    public static bool AvailableIfNonMelee(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => false,
        _ => true,
    };

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(WeaponType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum WeaponType
    {
        MeleeWeapon = 0,
        RangedWeapon,
        TurretWeapon,
        BuildinWeapon,
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.IsMeleeWeapon)
            {
                return (int)WeaponType.MeleeWeapon;
            }
            if (TweakDatabase.turretBuildingDatas.TryGetValue(def.defName, out var d) &&
                (def.statBases?.Find(s => s.stat == StatDefOf.WorkToMake)?.value ?? -1) <= 0)
            {
                var l = GetAllData(d);
                if (l.Count > 0) GunTurret = d;
                foreach (var item in l)
                {
                    ((BuildingData)item).TurretGun = id;
                }
                return (int)WeaponType.TurretWeapon;
            }
            if (def.destroyOnDrop == true)
            {
                return (int)WeaponType.BuildinWeapon;
            }
        }
        return (int)WeaponType.RangedWeapon;
    }
}


