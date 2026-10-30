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

    // 原版那两张"物品 × 材料"配对表（PawnWeaponGenerator.allWeaponPairs /
    // PawnApparelGenerator.allApparelPairs）只在 PlayDataLoader.ResetStaticDataPost 里建一次。
    // ThingStuffPair 在构造时把 MarketValue / 保温 / 毒抗等 stat 算进 cachedPrice 等字段，
    // commonalityMultiplier 也是那时归一化好的 —— 所以改完材料的价格或 commonality，
    // 配对表里存的仍是旧值：武器与服饰的生成权重、weaponMoney 价格门槛看起来"改了没反应"。
    //
    // 为什么不直接调原版 Reset()：复杂度是 O(ThingDef 总数 × 可上材料物品数)。
    // ThingStuffPair.AllWith 对每个可上材料的 ThingDef 都要全表扫一遍找材料
    // （DefDatabase<ThingDef>.AllDefs.Where(st => st.IsStuff && st.stuffProps.CanMake(td))），
    // 武器 Reset 之后还有一层"每个武器 def 再扫全表求和 Commonality"。
    // 原版规模（数千 ThingDef / 几十材料）下这是百万级，能忍；装了大型 Mod 到数万 ThingDef
    // 时就是上亿次委托调用，一次重建要几秒 —— 改一格卡一下，不可用。
    //
    // 这里改成增量修补：只重建"该材料参与的那几条"，再按物品粒度重算归一化倍数。
    // 复杂度是 O(配对表长度) + O(该材料参与的条目数)，与 ThingDef 总数无关。

    /// <summary>本次改动会影响配对表的哪个方面。由 <c>StuffData</c> 按字段判定后传入。</summary>
    [Flags]
    public enum StuffCacheAspect
    {
        None = 0,
        /// <summary>价格 / 保温 / 毒抗 / 真空抗等被烘进配对条目的 stat 值。需要重建条目
        /// （并顺带重算归一化 —— derp 判定依赖这些 stat）。</summary>
        PairStats = 1,
        /// <summary>commonality。不需要重建条目，只需按物品重算归一化倍数。</summary>
        Commonality = 2,
        All = PairStats | Commonality,
    }

    private static FieldInfo? _apparelPairsField;
    private static bool _apparelPairsFieldResolved;

    /// <summary>
    /// 把 <paramref name="stuff"/> 的改动同步到原版两张配对表。
    /// 语义等同"重启游戏后重新生成配对表"，但只碰受影响的条目。
    /// </summary>
    public static void RefreshStuffGenerationCaches(ThingDef? stuff, StuffCacheAspect aspect)
    {
        if (stuff?.stuffProps == null || aspect == StuffCacheAspect.None) return;

        var weaponPairs = PawnWeaponGenerator.AllWeaponPairs;
        if (weaponPairs == null)
        {
            // 配对表还没建过（理论上 PlayDataLoader 已建好）。退回原版重建，慢但不会留错值。
            PawnWeaponGenerator.Reset();
        }
        else
        {
            // 武器：AllWith 之后 Reset 还会把"该物品全部材料的 Commonality 之和"拉回 generateCommonality。
            PatchPairTable(weaponPairs, stuff, useAllWithNormalization: false, aspect);
        }

        // allApparelPairs 是私有静态字段且每次 Reset 都会整个换掉，所以缓存 FieldInfo、每次重取实例。
        //
        // 已知不覆盖的一处：PawnApparelGenerator.Reset 里还会重算 4 个"免费保暖衣物价格阈值"
        // （freeWarmParkaMaxPrice 等，由 Cloth 等固定材料的 MarketValue 推出来）。它们是私有静态 float，
        // 本函数不重算 —— 只有改 Cloth 价格时才会偏离，影响面是"寒冷地图开局给殖民者发免费大衣"这一处判定。
        // 复制那段初始化逻辑的脆弱性大于这点收益，故留作已知偏差。
        if (!_apparelPairsFieldResolved)
        {
            _apparelPairsFieldResolved = true;   // 解析失败也不反复重试（Reflect 已记过一条 Error）
            _apparelPairsField = Reflect.Field(typeof(PawnApparelGenerator), "allApparelPairs");
        }
        if (_apparelPairsField?.GetValue(null) is List<ThingStuffPair> apparelPairs)
        {
            // 服饰：Reset 只调 AllWith，归一化口径就是 mult = 1/n/avg(stuff.commonality)，没有二次修正。
            PatchPairTable(apparelPairs, stuff, useAllWithNormalization: true, aspect);
        }
        else
        {
            // 反射没拿到字段（游戏版本改名？）：退回原版全量重建。慢，但不会留错值。
            PawnApparelGenerator.Reset();
        }
    }

    private static void PatchPairTable(List<ThingStuffPair> pairs, ThingDef stuff,
        bool useAllWithNormalization, StuffCacheAspect aspect)
    {
        bool touchStats = (aspect & StuffCacheAspect.PairStats) != 0;
        HashSet<ThingDef>? affected = null;

        // 1) 重建该材料参与的条目：cachedPrice / 保温 / 毒抗等全部按当前 stat 重算。
        //    只用 new ThingStuffPair(thing, stuff, multiplier) —— multiplier 原样带过去，
        //    归一化放在下一步统一处理，两步互不干扰。
        if (touchStats)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                ThingStuffPair p = pairs[i];
                if (p.stuff != stuff) continue;
                (affected ??= new HashSet<ThingDef>()).Add(p.thing);
                pairs[i] = new ThingStuffPair(p.thing, stuff, p.commonalityMultiplier);
            }
        }

        // 2) 归一化。除了 commonality 本身，stat 改动也可能间接触发它：
        //    IsDerpWeapon / IsDerpApparel 是按材料 stat（护甲乘数）判定的，改了护甲值可能让某条
        //    配对在"derp（×0.01）"与"正常"之间翻转，那样求和结果就变了 —— 原版 Reset 会连归一化
        //    一起重算，这里保持一致。k≈1 时是纯 no-op，所以只有 commonality 未变时代价可忽略。
        if (affected == null)
        {
            // 只改了 commonality：受影响物品集合得单独扫一遍。
            for (int i = 0; i < pairs.Count; i++)
            {
                ThingStuffPair p = pairs[i];
                if (p.stuff == stuff) (affected ??= new HashSet<ThingDef>()).Add(p.thing);
            }
            if (affected == null) return;
        }

        // 3) 按物品分组（归一化以"物品"为粒度：一个物品下所有材料共享同一个倍数）。
        var groups = new Dictionary<ThingDef, List<int>>(affected.Count);
        foreach (ThingDef t in affected) groups[t] = new List<int>();
        for (int i = 0; i < pairs.Count; i++)
        {
            if (groups.TryGetValue(pairs[i].thing, out var idxs)) idxs.Add(i);
        }

        foreach (var kv in groups)
        {
            List<int> idxs = kv.Value;
            if (idxs.Count == 0) continue;

            if (useAllWithNormalization)
            {
                // 复刻 ThingStuffPair.AllWith：mult = (avg≈0) ? 1 : 1/n/avg，
                // avg 是所有可上材料（含 commonality 为 0 的）的 commonality 算术平均。
                float sumComm = 0f;
                for (int j = 0; j < idxs.Count; j++)
                {
                    ThingDef? st = pairs[idxs[j]].stuff;
                    if (st != null) sumComm += st.stuffProps.commonality;
                }
                float avg = sumComm / idxs.Count;
                float mult = Mathf.Approximately(avg, 0f) ? 1f : 1f / idxs.Count / avg;
                for (int j = 0; j < idxs.Count; j++)
                {
                    int idx = idxs[j];
                    ThingStuffPair p = pairs[idx];
                    p.commonalityMultiplier = mult;
                    pairs[idx] = p;
                }
            }
            else
            {
                // 复刻 PawnWeaponGenerator.Reset：把该物品所有材料的 Commonality 之和缩放到
                // generateCommonality。k 是纯缩放，重复执行收敛（和为 generateCommonality 时 k=1）。
                float gen = kv.Key.generateCommonality;
                float sum = 0f;
                for (int j = 0; j < idxs.Count; j++) sum += pairs[idxs[j]].Commonality;
                if (sum <= 0f) continue;
                float k = gen / sum;
                if (Mathf.Approximately(k, 1f)) continue;
                for (int j = 0; j < idxs.Count; j++)
                {
                    int idx = idxs[j];
                    ThingStuffPair p = pairs[idx];
                    p.commonalityMultiplier *= k;
                    pairs[idx] = p;
                }
            }
        }
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
