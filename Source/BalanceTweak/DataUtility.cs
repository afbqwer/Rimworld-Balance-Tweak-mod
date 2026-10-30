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

    #region 可选 Mod 兼容（只走反射，绝不静态引用其类型）

    // 背景：C# 会把 lambda 捕获的变量编成闭包类的**公共字段**。若该变量类型来自可选 Mod，
    // 未加载该 Mod 时，RimWorld 启动期对全部类型调用 GetFields()（StaticConstructorOnStartupUtility
    // .ReportProbablyMissingAttributes）会抛 TypeLoadException（Could not load type of field ...）。
    // 因此凡涉及可选 Mod 类型之处，一律"按名字反射"，并把结果装进 object / MemberInfo 再交给闭包 ——
    // 这样闭包字段只会是 object，绝不出现可选 Mod 类型。**也不得使用 typeof/泛型参数引用这些类型。**

    /// <summary>类型自身或其继承链上是否有某个全名。</summary>
    public static bool IsTypeOrSubclassOfName(Type? type, string fullName)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.FullName == fullName) return true;
        return false;
    }

    /// <summary>按名字找字段或属性（含继承链）；找不到返回 null。</summary>
    public static MemberInfo? FindMember(Type? type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.GetField(name, flags) is FieldInfo f) return f;
            if (t.GetProperty(name, flags) is PropertyInfo p) return p;
        }
        return null;
    }

    public static object? GetMember(object? target, MemberInfo? member)
        => target == null || member == null ? null
         : member is FieldInfo f ? f.GetValue(target)
         : ((PropertyInfo)member).GetValue(target);

    public static void SetMember(object? target, MemberInfo? member, object? value)
    {
        if (target == null || member == null) return;
        if (member is FieldInfo f) f.SetValue(target, value);
        else ((PropertyInfo)member).SetValue(target, value);
    }

    /// <summary>按名字直读直写（便捷版）。</summary>
    public static object? GetByName(object? target, string name) => GetMember(target, FindMember(target?.GetType(), name));
    public static void SetByName(object? target, string name, object? value) => SetMember(target, FindMember(target?.GetType(), name), value);

    public static float? AsFloat(object? v) => v switch { float f => f, int i => i, double d => (float)d, _ => null };
    public static int? AsInt(object? v) => v switch { int i => i, float f => (int)f, double d => (int)d, _ => null };
    public static bool AsBool(object? v) => v is bool b && b;
    public static List<int>? AsIntList(object? v) => v is System.Collections.IEnumerable e && v is not string ? e.Cast<int>().ToList() : null;

    /// <summary>在 <c>def.modExtensions</c> 里按类型全名找可选 Mod 的 <see cref="DefModExtension"/>（返回基类，调用方再反射取成员）。</summary>
    public static DefModExtension? FindModExtension(Def? def, string fullTypeName)
        => def?.modExtensions?.FirstOrDefault(e => e != null && e.GetType().FullName == fullTypeName);

    #endregion

    #region thingSetMakerTags 缓存刷新

    // ThingFilter.allowedDefs 在 ResolveReferences 时一次性构建，且 ResolveReferences 只做
    // 增量 SetAllow（不清空旧集合）—— 因此 ThingDef.thingSetMakerTags 改动后：
    //   新增标签不刷新也能命中（SetAllow 会加），但删除标签永远不会从缓存里消失。
    // 必须先清掉引用了 thingSetMakerTags 的过滤器缓存，再重新 Resolve。
    // thingSetMakerTags 的原版消费方全部在 ThingSetMakerDef 树里
    // （Reward_ItemsStandard、物品藏匿点、各类 MapGen/Reward 表），只刷这一处即可。
    private static FieldInfo? ThingFilterAllowedDefsField;

    /// <summary>在任何 ThingDef.thingSetMakerTags 写回之后调用，重建 ThingSetMaker 过滤器缓存。</summary>
    public static void RefreshThingSetMakerFilters()
    {
        var defs = DefDatabase<ThingSetMakerDef>.AllDefsListForReading;
        if (defs.Count == 0) return;

        var filters = new HashSet<ThingFilter>();
        foreach (var def in defs)
        {
            CollectThingSetMakerFilters(def.root, filters);
        }
        if (filters.Count == 0) return;

        ThingFilterAllowedDefsField ??= Reflect.Field(typeof(ThingFilter), "allowedDefs");
        foreach (var filter in filters)
        {
            if (ThingFilterAllowedDefsField.GetValue(filter) is HashSet<ThingDef> allowedDefs)
            {
                allowedDefs.Clear();
            }
        }
        foreach (var def in defs)
        {
            def.ResolveReferences();
        }
    }

    /// <summary>
    /// 收集一个 ThingSetMaker 树里的全部 ThingFilter（含各层 fixedParams.filter）。
    /// 走公共字段反射而不是硬编码组合类型，Sum/RandomOption 的 Option、Conditional 的子节点都能覆盖。
    /// </summary>
    private static void CollectThingSetMakerFilters(ThingSetMaker? maker, HashSet<ThingFilter> filters)
    {
        if (maker == null) return;
        if (maker.fixedParams.filter != null)
        {
            filters.Add(maker.fixedParams.filter);
        }
        foreach (var field in maker.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.GetValue(maker) is ThingSetMaker child)
            {
                CollectThingSetMakerFilters(child, filters);
                continue;
            }
            // Sum.Option / RandomOption.Option 这类"承载子 maker 的选项对象"
            if (field.GetValue(maker) is System.Collections.IEnumerable list
                && field.FieldType.IsGenericType
                && field.FieldType.GetGenericArguments()[0] is { IsClass: true } elemType
                && elemType != typeof(string)
                && elemType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Any(f => typeof(ThingSetMaker).IsAssignableFrom(f.FieldType)))
            {
                foreach (var item in list)
                {
                    if (item == null) continue;
                    foreach (var optionField in item.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (optionField.GetValue(item) is ThingSetMaker optionChild)
                        {
                            CollectThingSetMakerFilters(optionChild, filters);
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region 材料随机生成缓存

    // 原版这三处缓存在 PlayDataLoader.ResetStaticDataPre/Post 里各建一次，之后不再重建：
    //   · GenStuff.cachedStuffDefs —— 所有 IsStuff 的 ThingDef 列表
    //   · PawnWeaponGenerator.allWeaponPairs / PawnApparelGenerator.allApparelPairs ——
    //     "物品 × 材料"配对表。ThingStuffPair 在构造时就把 MarketValue / 保温 / 毒抗等 stat
    //     算进 cachedPrice 等字段，Commonality 里的 commonalityMultiplier 也是那时按"平均
    //     commonality"归一化好的。
    // 所以改完材料的 commonality / allowedInStuffGeneration / 价格类 stat 之后，配对表里存的
    // 仍是旧值：武器与服饰的生成权重、weaponMoney 价格门槛看起来"改了没反应"。
    // 这里统一重建，语义等同重启游戏。
    //
    // 注意：只在当前 Def 上有实际改动时调用（见 StuffData.NeedsGenerationCacheRefresh）。
    // 一次重建要遍历全部 ThingDef × 可用材料，不是每帧能做的事。目前已知的重复调用点只有
    // TweakDatabase.Init —— 读档时每个被改过的材料行各触发一次（原版材料三十来个，量级可接受，
    // 但未实测）；若哪天真被玩家察觉（改了大量材料后读档变慢），把 Init 里那条链路包成批次
    // （首行置脏、末尾统一刷一次）即可，不必改这里的语义。
    public static void RefreshStuffGenerationCaches()
    {
        GenStuff.ResetStaticData();
        PawnWeaponGenerator.Reset();
        PawnApparelGenerator.Reset();
    }

    #endregion

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

        // 四个精度只求一次；原实现对每个距离都重新 GetStatValueAbstract，
        // 一把 30 射程的武器要重复求值数十次。
        float touch = AdjustedAccuracy(RangeCategory.Touch, weapon);
        float shortAcc = AdjustedAccuracy(RangeCategory.Short, weapon);
        float medium = AdjustedAccuracy(RangeCategory.Medium, weapon);
        float longAcc = AdjustedAccuracy(RangeCategory.Long, weapon);

        int bestRange = start;
        float bestChance = HitChance(touch, shortAcc, medium, longAcc, start);
        for (int i = 1; i < count; i++)
        {
            int range = start + i;
            float chance = HitChance(touch, shortAcc, medium, longAcc, range);
            // 严格大于：与 MaxBy 在并列时取首个元素的语义一致。
            if (chance > bestChance)
            {
                bestChance = chance;
                bestRange = range;
            }
        }
        return bestRange;
    }

    private static float HitChance(float touch, float shortAcc, float medium, float longAcc, float dist)
    {
        float value = dist <= 3f
            ? touch
            : dist <= 12f
                ? Mathf.Lerp(touch, shortAcc, (dist - 3f) / 9f)
                : dist <= 25f
                    ? Mathf.Lerp(shortAcc, medium, (dist - 12f) / 13f)
                    : dist <= 40f
                        ? Mathf.Lerp(medium, longAcc, (dist - 25f) / 15f)
                        : longAcc;
        return Mathf.Clamp(value, 0.01f, 1f);
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
            object? val = _factorLegendaryField.GetValue(part);
            if (val is float f)
            {
                return f;
            }
        }

        return 1f;
    }
}
