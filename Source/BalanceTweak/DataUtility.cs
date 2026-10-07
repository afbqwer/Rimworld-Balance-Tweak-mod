using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public static class DataUtility
{
    private static Dictionary<string, bool> mayRequire = new();
    public static bool MayRequire(string? name)
    {
        if (name == null || name.Length == 0) { return true; }
        if (mayRequire.TryGetValue(name, out var res))
        {
            return res;
        }
        else
        {
            res = DirectXmlToObjectNew.ValidateMayRequires(name, "");
            mayRequire.Add(name, res);
            return res;
        }
    }

    #region ThingDef 分类谓词

    // 由各 Data 类上的 [TweakFor(Match = nameof(...))] 引用。
    // 逐一对应原 TweakDatabase.Init() 的 if/else-if 链，抢占顺序由 Priority 还原：
    // Race(700) > Apparel(600) > Building(500) > Projectile(400) > Stuff(300) > Weapon(200) > Food(100) > Misc(兜底)。
    // 之所以集中放在这里：原先 Init() 与 TweakData.GetData(ThingDef) 各写一份分类逻辑，已经漂移过一次
    // （deepCommonality vs deepCountPerCell），现在只保留唯一一份实现。

    public static bool MatchRace(Def def) => def is ThingDef t && t.race != null && !t.IsCorpse;
    public static bool MatchApparel(Def def) => def is ThingDef t && t.IsApparel;
    public static bool MatchBuilding(Def def) => def is ThingDef t && t.category == ThingCategory.Building;
    public static bool MatchProjectile(Def def) => def is ThingDef t && t.category == ThingCategory.Projectile && t.projectile != null;
    public static bool MatchStuff(Def def) => def is ThingDef t && (t.stuffProps != null || t.deepCommonality > 0);
    public static bool MatchWeapon(Def def) => def is ThingDef t && t.IsWeapon;
    public static bool MatchFood(Def def) => def is ThingDef t
        && (t.ingestible != null || t.GetCompProperties<CompProperties_Rottable>() != null || t.IsMedicine);

    #endregion

    private static DamageArmorCategoryDef? Blunt;

    /// <summary>
    /// 计算实际DPS期望
    /// </summary>
    public static float CalculateExpectedDPS(
        List<Tool>? defaultTools = null,
        List<Tool>? weaponTools = null,
        float powerMult = 1f,
        float cdMult = 1f,
        bool weaponMode = false,
        ThingDef? stuff = null
        )
    {
        Blunt ??= DefDatabase<DamageArmorCategoryDef>.GetNamed("Blunt");

        var allTools = new List<Tool>();
        if (defaultTools != null) allTools.AddRange(defaultTools);
        if (weaponTools != null) allTools.AddRange(weaponTools);

        if (allTools.Count == 0) return 0f;

        if (stuff != null)
        {
            cdMult *= stuff.GetStatValueAbstract(StatDefOf.MeleeWeapon_CooldownMultiplier);
        }

        var toolDataList = allTools
            .Where(t => t.cooldownTime > 0f)
            .Select(t =>
            {
                VerbProperties? v = null;
                DamageArmorCategoryDef? ac = null;
                var damage = t.power * powerMult;
                var effectiveAP = t.armorPenetration < 0f ? damage * 0.015f : t.armorPenetration * powerMult;
                var dps = damage / (t.cooldownTime * cdMult);
                if (stuff != null)
                {
                    var c = t.capacities.FirstOrDefault();
                    if (c != null && TweakDatabase.allMeleeVerb.TryGetValue(c, out v))
                    {
                        ac = v.meleeDamageDef?.armorCategory;
                        var ms = ac?.multStat;
                        if (ms != null)
                        {
                            dps *= stuff.GetStatValueAbstract(ms);
                        }
                    }
                }
                var weight = t.power * powerMult * (1 + effectiveAP) / (t.cooldownTime * cdMult);
                weight *= AdditionalSelectionFactor(t, v);

                return new ToolData
                {
                    Tool = t,
                    DPS = dps,
                    AP = effectiveAP,
                    InitialWeight = weight,
                    damageType = ac,
                };
            })
            .ToList();

        if (toolDataList.Count == 0) return 0f;

        var maxWeight = toolDataList.Max(t => t.InitialWeight);
        if (maxWeight <= 0f) return 0f;

        var goodThreshold = maxWeight * 0.95f;
        var badThreshold = maxWeight * 0.25f;
        List<ToolData>? goodTools = toolDataList.Where(t => t.InitialWeight >= goodThreshold).ToList();
        List<ToolData>? mediumTools = toolDataList.Where(t => t.InitialWeight > badThreshold && t.InitialWeight < goodThreshold).ToList();

        if (goodTools.Count == 0) return 0f;

        var totalDPS = 0f;
        var hasMedium = mediumTools.Count > 0;
        var goodTotalWeight = hasMedium ? 0.75f : 1.0f;
        var goodWeightEach = goodTotalWeight / goodTools.Count;

        foreach (var data in goodTools)
        {
            totalDPS += CalculateToolDPS(data, goodWeightEach, weaponMode);
        }

        if (hasMedium)
        {
            var mediumWeightEach = 0.25f / mediumTools.Count;
            foreach (var data in mediumTools)
            {
                totalDPS += CalculateToolDPS(data, mediumWeightEach, weaponMode);
            }
        }

        return totalDPS;
    }

    private static float CalculateToolDPS(ToolData data, float weightMultiplier, bool weaponMode)
    {
        var dps = data.DPS * weightMultiplier;

        if (!weaponMode) return dps;

        var damage = data.damageType;
        var apm = 1f;
        var ap = data.AP;
        if (data.Tool.armorPenetration < 0)
        {
            apm = 1.5f;
        }
        //if (Blunt != null && damage == Blunt)
        //{
        //    apm = 1.5f;
        //}
        //else if (damage != DamageArmorCategoryDefOf.Sharp)
        //{
        //    apm = 1.25f;
        //}
        if (TweakDatabase.VCTAPDown && ap > 0.5f) { ap = 0.5f + Mathf.Log(ap + 0.5f, 5f); }
        ap = Mathf.Min(ap * apm, ap + 1f);

        return dps * Mathf.Pow(1f + ap, 2f);
    }

    private static float AdditionalSelectionFactor(Tool tool, VerbProperties? prop)
    {
        float num = ((tool != null) ? tool.chanceFactor : 1f);
        if (prop != null && prop.meleeDamageDef != null && !prop.meleeDamageDef.additionalHediffs.NullOrEmpty())
        {
            foreach (DamageDefAdditionalHediff additionalHediff in prop.meleeDamageDef.additionalHediffs)
            {
                _ = additionalHediff;
                num += 0.1f;
            }
        }
        return num;
    }


    private static float? GetDefaultMarketValueFromData(TweakData data)
    {
        if (data.def is ThingDef def)
        {
            float v = GetDefaultMarketValue(def);
            return v == 0 ? null : v;
        }
        return null;
    }

    public static float GetDefaultMarketValue(ThingDef def)
    {
        var stuff = GetDefaultStuffFromDef(def);
        var v = StatWorker_MarketValue.CalculatedBaseMarketValue(def, stuff);
        return v;
    }

    public static ThingDef? GetDefaultStuffFromDef(ThingDef def)
    {
        var stuff = def.stuffCategories;
        if (!stuff.NullOrEmpty())
        {
            if (stuff.Contains(StuffCategoryDefOf.Metallic))
            {
                if (MayRequire(TweakDatabase.CE)) return ThingDefOf.Steel;
                return ThingDefOf.Plasteel;
            }
            else if (stuff.Contains(StuffCategoryDefOf.Leathery))
            {
                return DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Thrumbo");
            }
            else if (stuff.Contains(StuffCategoryDefOf.Fabric))
            {
                return DefDatabase<ThingDef>.GetNamedSilentFail("Hyperweave");
            }
            else if (stuff.Contains(StuffCategoryDefOf.Stony))
            {
                return ThingDefOf.BlocksGranite;
            }
            else if (stuff.Contains(StuffCategoryDefOf.Woody))
            {
                return ThingDefOf.WoodLog;
            }
        }
        return null;
    }
    public static float? GetDefaultStuffValue(TweakData data, StatDef stat)
    {
        if (data.def is ThingDef def)
        {
            var stuff = GetDefaultStuffFromDef(def);
            return def.GetStatValueAbstract(stat, stuff);
        }
        return null;
    }
    public static object? GetCostStuffCount(TweakData data) =>
        (data.def is ThingDef d ? d.costStuffCount : null);
    public static object? GetIsCompQuality(TweakData data) =>
    (data.def is ThingDef d ? d.HasAssignableCompFrom(typeof(CompQuality)) : null);
    public static object? GetIsMadeofStuff(TweakData data) =>
    (data.def is ThingDef d ? !d.stuffCategories.NullOrEmpty() : null);

    public static object? GetNormalMarketValue(TweakData data) => GetDefaultMarketValueFromData(data);

    // 辅助结构体，用于存储中间计算结果
    private struct ToolData
    {
        public Tool Tool;
        public float DPS;
        public float AP;
        public float InitialWeight;
        public DamageArmorCategoryDef? damageType;
    }

    private static float AdjustedAccuracy(RangeCategory cat, ThingDef def)
    {
        StatDef? stat = null;
        switch (cat)
        {
            case RangeCategory.Touch:
                stat = StatDefOf.AccuracyTouch;
                break;
            case RangeCategory.Short:
                stat = StatDefOf.AccuracyShort;
                break;
            case RangeCategory.Medium:
                stat = StatDefOf.AccuracyMedium;
                break;
            case RangeCategory.Long:
                stat = StatDefOf.AccuracyLong;
                break;
        }
        return def.GetStatValueAbstract(stat);
    }

    public static float GetHitChanceFactor(ThingDef weapon, float dist)
    {
        float value = ((dist <= 3f) ? AdjustedAccuracy(RangeCategory.Touch, weapon) : ((dist <= 12f) ? Mathf.Lerp(AdjustedAccuracy(RangeCategory.Touch, weapon), AdjustedAccuracy(RangeCategory.Short, weapon), (dist - 3f) / 9f) : ((dist <= 25f) ? Mathf.Lerp(AdjustedAccuracy(RangeCategory.Short, weapon), AdjustedAccuracy(RangeCategory.Medium, weapon), (dist - 12f) / 13f) : ((!(dist <= 40f)) ? AdjustedAccuracy(RangeCategory.Long, weapon) : Mathf.Lerp(AdjustedAccuracy(RangeCategory.Medium, weapon), AdjustedAccuracy(RangeCategory.Long, weapon), (dist - 25f) / 15f)))));
        return Mathf.Clamp(value, 0.01f, 1f);
    }

    public static float FindOptimalRange(ThingDef weapon, float MinRange, float MaxRange)
    {
        int start = (int)Math.Max(1.0, Math.Ceiling(MinRange));
        int count = (int)Math.Floor(MaxRange);
        if (count <= 0) return MinRange;
        return Enumerable.Range(start, count).MaxBy((int range) => GetHitChanceFactor(weapon, range));
    }

    /// <summary>
    /// 独立计算远程武器DPS相关数据
    /// </summary>
    public static (float? AverageRangedDPS, float? MaxRangedDPS) CalculateRangeDPS(ThingDef def, bool isQ)
    {
        if (def == null)
        {
            return (null, null);
        }
        var verb = def.Verbs.FirstOrDefault();
        if (verb == null)
        {
            return (null, null);
        }
        // --- 1. 提取基础数值 ---

        // 伤害值
        float? damage = null;
        if (verb.defaultProjectile?.projectile != null)
        {
            damage = verb.defaultProjectile.projectile.GetDamageAmount(null);
        }
        // 如果弹丸未定义伤害，尝试使用光束伤害
        else if (verb.beamDamageDef != null)
        {
            damage = verb.beamDamageDef.defaultDamage;
        }

        if (!damage.HasValue) damage = 0;
        damage = Math.Max(damage.Value, 0);

        // 冷却时间
        // 优先从StatDef获取，若无则使用Verb默认值
        float cooldown = def.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown);
        if (cooldown <= 0f && verb.defaultCooldownTime > 0f)
        {
            cooldown = verb.defaultCooldownTime;
        }

        // 瞄准时间
        float warmup = verb.warmupTime;

        // 爆发参数
        int burstCount = verb.burstShotCount;
        int ticksBetweenShots = verb.ticksBetweenBurstShots;

        // 射程参数
        float minRange = verb.minRange;
        float maxRange = verb.range;

        // --- 2. 提取修正系数 ---

        float dmgMultiplier = def.GetStatValueAbstract(StatDefOf.RangedWeapon_DamageMultiplier);
        if (dmgMultiplier <= 0f) dmgMultiplier = 1f;

        float warmupMultiplier = def.GetStatValueAbstract(StatDefOf.RangedWeapon_WarmupMultiplier);
        if (warmupMultiplier <= 0f) warmupMultiplier = 1f;

        // --- 3. 品质修正;
        {
            float legendaryFactor = GetLegendaryFactor(StatDefOf.RangedWeapon_DamageMultiplier);
            damage = (int)(damage.Value * legendaryFactor);
        }

        // --- 4. 计算最大DPS (MaxRangedDPS) ---

        float? maxDPS = null;

        if (cooldown > 0f)
        {
            float numerator = damage.Value * burstCount * dmgMultiplier;
            // 公式：(瞄准*瞄准倍率 + 冷却 + (爆发数-1)*间隔/60)
            float denominator = (warmup * warmupMultiplier) + cooldown + ((burstCount - 1) * ticksBetweenShots / 60f);

            if (denominator > 0f)
            {
                maxDPS = numerator / denominator;
            }
            else
            {
                maxDPS = 0f;
            }

            // 特殊Verb类型修正
            if (verb.verbClass == typeof(Verb_ArcSprayIncinerator))
            {
                maxDPS /= burstCount;
            }
        }
        else
        {
            maxDPS = 0f;
        }

        // --- 5. 计算最佳射程与平均DPS (OptimalRange & AverageRangedDPS) ---

        float? avgDPS = maxDPS;
        float? optRange = null;

        // 如果无射程限制或射程为0，平均DPS等于最大DPS
        if (maxRange > 0)
        {
            // 调用已存在的静态方法
            optRange = FindOptimalRange(def, minRange, maxRange);

            if (optRange.HasValue)
            {
                float hitChance = GetHitChanceFactor(def, optRange.Value);

                // 品质修正
                if (isQ)
                {
                    float accFactor = GetLegendaryFactor(StatDefOf.AccuracyMedium);
                    hitChance *= accFactor;
                }

                avgDPS = maxDPS.HasValue ? maxDPS.Value * hitChance : null;
            }
        }

        return (avgDPS, maxDPS);
    }

    private static FieldInfo? _factorLegendaryField;

    /// <summary>
    /// 获取指定StatDef的传奇品质因子
    /// </summary>
    public static float GetLegendaryFactor(StatDef statDef)
    {
        if (statDef == null) return 1f;

        var part = statDef.GetStatPart<StatPart_Quality>();
        if (part == null) return 1f;

        if (_factorLegendaryField == null)
        {
            _factorLegendaryField = typeof(StatPart_Quality).GetField("factorLegendary", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        if (_factorLegendaryField != null)
        {
            object val = _factorLegendaryField.GetValue(part);
            if (val is float f)
            {
                return f;
            }
        }

        return 1f;
    }
}
