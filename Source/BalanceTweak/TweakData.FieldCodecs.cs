using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 「字段类型 → 处理方式」的**单一登记处**（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3 ①）。
///
/// 改造前，「哪种字段类型怎么落盘 / 怎么解析」这件事写在两条平行的 <c>else if</c> 链里
/// （<c>ExposeField</c> 32 条、<c>TryResolveField</c> 24 条），新增一个字段类型要改 5~7 处，
/// 其中"读档分支"和"落盘分支"漏改是**静默**的。
///
/// 现在全部登记在本文件的 <see cref="FieldCodecs"/> 里，两条链只做一次查表。
///
/// <b>口径要点</b>：<see cref="FieldCodec.Resolve"/> 为 <c>null</c> 表示"已登记、但不参与解析"
/// （标量的读档由 Scribe 在 LoadingVars 阶段直接落值，不存在解析层）。
/// 这与"类型压根没登记"（<see cref="FieldCodecs.Find"/> 返回 <c>null</c>）是**两回事** ——
/// 把标量当成"未登记"处理会把它误标成损坏字段。见 §7.6。
/// </summary>
public abstract partial class TweakData
{
    #region 登记表

    /// <summary>落盘 / 读档一个字段。<paramref name="data"/> 为字段所在实例。</summary>
    public delegate void ExposeDelegate(TweakData data, StatFieldMeta meta);

    /// <summary>读档后把原始数据还原成字段值；返回 <c>false</c> = 该字段损坏（调用方标损坏并回滚）。</summary>
    public delegate bool ResolveDelegate(TweakData data, StatFieldMeta meta);

    /// <summary>一条登记项。</summary>
    public sealed class FieldCodec
    {
        /// <summary>诊断名（写日志用）。</summary>
        public required string Name { get; init; }

        /// <summary>类型匹配谓词。精确类型用 <c>t =&gt; t == someType</c>；通配类型（Def 子类 / 可空枚举 / List&lt;Def&gt;）写各自的条件。</summary>
        public required Func<Type, bool> Matches { get; init; }

        public required ExposeDelegate Expose { get; init; }

        /// <summary><c>null</c> = 已登记但不参与解析（标量）。</summary>
        public ResolveDelegate? Resolve { get; init; }
    }

    /// <summary>
    /// 字段类型的登记表。**加新字段类型只改这里**，两条链与 SC10 都从这张表读。
    /// <para>
    /// 查表分两级：精确类型走字典（O(1)），未命中再依次试少量通配项
    /// （<c>Def</c> 子类 / 可空枚举 / <c>List&lt;T&gt; where T : Def</c>，共 3 条）。
    /// 精确项与通配项天然不相交（<c>List&lt;string&gt;</c> 不是 <c>Def</c> 子类，反之亦然），
    /// 所以没有顺序陷阱。
    /// </para>
    /// </summary>
    internal static class FieldCodecs
    {
        private static readonly Dictionary<Type, FieldCodec> exact = new();
        private static readonly List<FieldCodec> patterns = new();

        /// <summary>全部登记项（精确 + 通配），供文档与诊断使用。</summary>
        public static IReadOnlyList<FieldCodec> All { get; private set; } = Array.Empty<FieldCodec>();

        public static FieldCodec? Find(Type fieldType)
        {
            if (exact.TryGetValue(fieldType, out var hit)) return hit;
            for (int i = 0; i < patterns.Count; i++)
                if (patterns[i].Matches(fieldType)) return patterns[i];
            return null;
        }

        static FieldCodecs() => Build();

        private static void Exact(Type type, string name, ExposeDelegate expose, ResolveDelegate? resolve = null)
            => exact[type] = new FieldCodec { Name = name, Matches = t => t == type, Expose = expose, Resolve = resolve };

        private static void Pattern(string name, Func<Type, bool> matches, ExposeDelegate expose, ResolveDelegate? resolve = null)
            => patterns.Add(new FieldCodec { Name = name, Matches = matches, Expose = expose, Resolve = resolve });

        private static void Build()
        {
            // ── 标量：Resolve = null。读档时 ExposeSimple/ExposeString/... 已直接落值，无需解析层 ──
            Exact(typeof(float?), "float?", ExposeSimpleFloat);
            Exact(typeof(int?), "int?", ExposeSimpleInt);
            Exact(typeof(bool?), "bool?", ExposeSimpleBool);
            Exact(typeof(FloatRange?), "FloatRange?", ExposeSimpleFloatRange);
            Exact(typeof(IntRange?), "IntRange?", ExposeSimpleIntRange);
            Exact(typeof(string), "string", ExposeString);
            Exact(typeof(SimpleCurve), "SimpleCurve", ExposeCurve);
            Pattern("可空枚举", t => Nullable.GetUnderlyingType(t)?.IsEnum == true, ExposeEnum);

            // ── 由子类自理：本表不分派 ──
            // HediffData / HediffStageData 覆写了 ExposeData，用自己的私有缓存
            // （_hediffGiverRefs / _mentalStateGiverRefs）序列化这两个字段，且只记"保留了哪些"，
            // 实际对象在它们各自的 ResolveDefs 里从 Def 的现列表按 defName 取回
            // （见 HediffData.cs:345、HediffStageData.cs:245）—— 基类分派碰不到它们。
            //
            // 登记成 no-op 是为了两件事：① SC10 不再把它们误报成"漏登记"；
            // ② 基类分派不再在运行期刷"未登记"错误（改造前那条 else-if 链是不匹配就静默跳过，
            //   查表后变成报错 —— 对这两个字段就成了纯噪声）。
            // 这不是"已知缺口"，而是一套既有的平行机制；是否收编进本表，另议。
            Exact(typeof(List<HediffGiver>), "List<HediffGiver>（子类自理）", ExposeHandledElsewhere);
            Exact(typeof(List<MentalStateGiver>), "List<MentalStateGiver>（子类自理）", ExposeHandledElsewhere);

            // ── 结构化：Resolve 非 null ──
            Pattern("Def 子类", t => t.IsSubclassOf(typeof(Def)), ExposeDef, ResolveDef);

            Exact(typeof(List<StatModifier>), "List<StatModifier>", ExposeStatModifierList, ResolveStatModifierList);
            Exact(typeof(List<PawnCapacityModifier>), "List<PawnCapacityModifier>", ExposePawnCapacityList, ResolvePawnCapacityList);
            Exact(typeof(List<DamageFactor>), "List<DamageFactor>", ExposeDamageFactorList, ResolveDamageFactorList);
            Exact(typeof(List<SkillGain>), "List<SkillGain>", ExposeSkillGainList, ResolveSkillGainList);
            Exact(typeof(List<Aptitude>), "List<Aptitude>", ExposeAptitudeList, ResolveAptitudeList);
            Exact(typeof(List<GeneticTraitData>), "List<GeneticTraitData>", ExposeGeneticTraitDataList, ResolveGeneticTraitDataList);
            Exact(typeof(List<ThingDefCountClass>), "List<ThingDefCountClass>", ExposeThingDefCountList, ResolveThingDefCountList);
            Exact(typeof(List<IngredientCount>), "List<IngredientCount>", ExposeIngredientList, ResolveIngredientList);
            Exact(typeof(List<SkillRequirement>), "List<SkillRequirement>", ExposeSkillReqList, ResolveSkillReqList);
            Exact(typeof(List<TraitRequirement>), "List<TraitRequirement>", ExposeTraitReqList, ResolveTraitReqList);
            Exact(typeof(List<ProcessIngredientItem>), "List<ProcessIngredientItem>", ExposeProcessIngredientList, ResolveProcessIngredientList);
            Exact(typeof(List<ProcessResultItem>), "List<ProcessResultItem>", ExposeProcessResultList, ResolveProcessResultList);
            Exact(typeof(List<BiomePlantRecord>), "List<BiomePlantRecord>", ExposeBiomePlantList, ResolveBiomePlantList);
            Exact(typeof(List<BiomeAnimalRecord>), "List<BiomeAnimalRecord>", ExposeBiomeAnimalList, ResolveBiomeAnimalList);
            Exact(typeof(List<FishChance>), "List<FishChance>", ExposeFishChanceList, ResolveFishChanceList);
            Exact(typeof(List<BiomeDiseaseRecord>), "List<BiomeDiseaseRecord>", ExposeDiseaseList, ResolveDiseaseList);
            Exact(typeof(List<WeatherCommonalityRecord>), "List<WeatherCommonalityRecord>", ExposeWeatherCommonalityList, ResolveWeatherCommonalityList);
            Exact(typeof(List<TerrainThreshold>), "List<TerrainThreshold>", ExposeTerrainThresholdList, ResolveTerrainThresholdList);
            Exact(typeof(List<StockGenerator>), "List<StockGenerator>", ExposeStockGeneratorList, ResolveStockGeneratorList);
            Exact(typeof(ThingFilter), "ThingFilter", ExposeIngredientFilter, ResolveIngredientFilter);
            Exact(typeof(List<string>), "List<string>", ExposeStringList, ResolveStringList);
            Exact(typeof(List<int>), "List<int>", ExposeIntList, ResolveIntList);
            Pattern("List<T> where T : Def",
                t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)
                     && t.GetGenericArguments()[0].IsSubclassOf(typeof(Def)),
                ExposeDefList, ResolveDefList);

            var all = new List<FieldCodec>(exact.Count + patterns.Count);
            all.AddRange(exact.Values);
            all.AddRange(patterns);
            All = all;
        }
    }

    #endregion

    #region 落盘 / 读档

    /// <summary>
    /// 本表不分派：该字段的落盘 / 还原由子类自己覆写的 <c>ExposeData</c> 负责
    /// （见 <see cref="FieldCodecs"/> 里"由子类自理"那两条登记）。
    /// 登记它的目的是让 SC10 与基类分派都别再把它当成"漏登记"。
    /// </summary>
    private static void ExposeHandledElsewhere(TweakData data, StatFieldMeta meta) { }

    private static void ExposeSimpleFloat(TweakData data, StatFieldMeta meta) => ExposeSimple<float>(data, meta);
    private static void ExposeSimpleInt(TweakData data, StatFieldMeta meta) => ExposeSimple<int>(data, meta);
    private static void ExposeSimpleBool(TweakData data, StatFieldMeta meta) => ExposeSimple<bool>(data, meta);
    private static void ExposeSimpleFloatRange(TweakData data, StatFieldMeta meta) => ExposeSimple<FloatRange>(data, meta);
    private static void ExposeSimpleIntRange(TweakData data, StatFieldMeta meta) => ExposeSimple<IntRange>(data, meta);

    private static void ExposeSimple<TU>(TweakData data, StatFieldMeta meta) where TU : struct
    {
        var value = (TU?)meta.Field.GetValue(data);
        Scribe_Values.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(data, value);
    }

    private static void ExposeString(TweakData data, StatFieldMeta meta)
    {
        var value = (string?)meta.Field.GetValue(data);
        Scribe_Values.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(data, value);
    }

    private static void ExposeCurve(TweakData data, StatFieldMeta meta)
    {
        var value = (SimpleCurve?)meta.Field.GetValue(data);
        Scribe_Deep.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(data, value);
    }

    private static void ExposeEnum(TweakData data, StatFieldMeta meta)
    {
        var fieldType = meta.Field.FieldType;
        var enumType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        var underlyingType = Enum.GetUnderlyingType(enumType);
        long? value = meta.Field.GetValue(data) != null ? Convert.ToInt64(meta.Field.GetValue(data)) : null;
        Scribe_Values.Look(ref value, meta.Field.Name);
        if (value.HasValue)
        {
            var underlyingValue = Convert.ChangeType(value.Value, underlyingType);
            meta.Field.SetValue(data, Enum.ToObject(enumType, underlyingValue));
        }
        else
        {
            meta.Field.SetValue(data, null);
        }
    }

    private static void ExposeDef(TweakData data, StatFieldMeta meta)
    {
        var defVal = meta.Field.GetValue(data) as Def;
        string? defName = defVal?.defName;
        Scribe_Values.Look(ref defName, meta.Field.Name);
        if (Scribe.mode == LoadSaveMode.LoadingVars && !defName.NullOrEmpty())
        {
            (data._savedLoadData ??= new())[meta.Field.Name] = defName;
        }
    }

    private static void ExposeList(TweakData data, StatFieldMeta meta, Func<object?, List<string>?> serialize)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            var strList = serialize(meta.Field.GetValue(data));
            if (strList == null || strList.Count == 0) return;
            Scribe_Collections.Look(ref strList, meta.Field.Name, LookMode.Value);
        }
        else
        {
            List<string>? strList = null;
            Scribe_Collections.Look(ref strList, meta.Field.Name, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                (data._savedLoadData ??= new())[meta.Field.Name] = strList;
        }
    }

    private static void ExposeStatModifierList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<StatModifier>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.StatModifierToItemString).ToList();
    });

    private static void ExposePawnCapacityList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<PawnCapacityModifier>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.PawnCapacityModifierToItemString).ToList();
    });

    private static void ExposeDamageFactorList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<DamageFactor>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.DamageFactorToItemString).ToList();
    });

    private static void ExposeSkillGainList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<SkillGain>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.SkillGainToItemString).ToList();
    });

    private static void ExposeAptitudeList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<Aptitude>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.AptitudeToItemString).ToList();
    });

    private static void ExposeGeneticTraitDataList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<GeneticTraitData>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.GeneticTraitDataToItemString).ToList();
    });

    private static void ExposeThingDefCountList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<ThingDefCountClass>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.ThingDefCountClassToItemString).ToList();
    });

    private static void ExposeDefList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (System.Collections.IList?)v;
        if (list == null || list.Count == 0) return null;
        var result = new List<string>();
        foreach (var item in list)
            if (item is Def def) result.Add(def.defName);
        return result;
    });

    private static void ExposeStringList(TweakData data, StatFieldMeta meta)
    {
        var list = (List<string>?)meta.Field.GetValue(data);
        Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            (data._savedLoadData ??= new())[meta.Field.Name] = list;
    }

    private static void ExposeIntList(TweakData data, StatFieldMeta meta)
    {
        var list = (List<int>?)meta.Field.GetValue(data);
        Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            (data._savedLoadData ??= new())[meta.Field.Name] = list;
    }

    private static void ExposeIngredientList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<IngredientCount>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(ic =>
            {
                var filterStr = SerializationHelper.IngredientFilterToString(ic.filter);
                if (filterStr.Length == 0) return null;
                return $"{filterStr}|{ic.GetBaseCount()}";
            }).Where(s => s != null).ToList()!;
        });
    }

    private static void ExposeSkillReqList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<SkillRequirement>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.SkillRequirementToItemString).ToList();
        });
    }

    private static void ExposeTraitReqList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<TraitRequirement>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.TraitRequirementToItemString).ToList();
        });
    }

    private static void ExposeIngredientFilter(TweakData data, StatFieldMeta meta)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            var filter = (ThingFilter?)meta.Field.GetValue(data);
            var str = SerializationHelper.IngredientFilterToString(filter);
            Scribe_Values.Look(ref str, meta.Field.Name);
        }
        else
        {
            string? str = null;
            Scribe_Values.Look(ref str, meta.Field.Name);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                (data._savedLoadData ??= new())[meta.Field.Name] = str;
        }
    }

    private static void ExposeProcessIngredientList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<ProcessIngredientItem>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.ProcessIngredientItemToItemString).ToList();
        });
    }

    private static void ExposeProcessResultList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<ProcessResultItem>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.ProcessResultItemToItemString).ToList();
        });
    }

    private static void ExposeBiomePlantList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<BiomePlantRecord>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.BiomePlantRecordToItemString).ToList();
        });
    }

    private static void ExposeBiomeAnimalList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<BiomeAnimalRecord>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.BiomeAnimalRecordToItemString).ToList();
        });
    }

    private static void ExposeFishChanceList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<FishChance>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.FishChanceToItemString).ToList();
        });
    }

    private static void ExposeDiseaseList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<BiomeDiseaseRecord>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.BiomeDiseaseRecordToItemString).ToList();
        });
    }

    private static void ExposeWeatherCommonalityList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<WeatherCommonalityRecord>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.WeatherCommonalityRecordToItemString).ToList();
        });
    }

    private static void ExposeTerrainThresholdList(TweakData data, StatFieldMeta meta)
    {
        ExposeList(data, meta, v =>
        {
            var list = (List<TerrainThreshold>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.TerrainThresholdToItemString).ToList();
        });
    }

    /// <summary>
    /// 库存生成器：字段值是真实对象，存档时按"一行一条生成器文本"落盘（见 <see cref="StockGeneratorCodec"/>）。
    /// 与其它列表列同一套路：<c>List&lt;T&gt;</c> ↔ <c>List&lt;string&gt;</c>，读档在
    /// <c>ResolveStockGeneratorList</c> 里还原（任一条解析失败即整字段标损坏、保持 Def 现状）。
    /// </summary>
    private static void ExposeStockGeneratorList(TweakData data, StatFieldMeta meta) => ExposeList(data, meta, v =>
    {
        var list = (List<StockGenerator>?)v;
        if (list == null || list.Count == 0) return null;
        return StockGeneratorCodec.Serialize(list);
    });

    #endregion

    #region 解析（读档后把原始数据还原成字段值）

    private static bool ResolveList(TweakData data, StatFieldMeta meta, Func<List<string>, (object? result, bool success)> parse)
    {
        if (data._savedLoadData == null || !data._savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not List<string> strList || strList.Count == 0)
        {
            // 已动过但存档无可解析值（无 key / null / 空列表）⇒ 把实例值显式置空，
            // 免得残留 SetDef 回填值；是否 Cleared 由 _clearedFieldNames 判定，此处只保证值为空。
            if (data.IsFieldModified(meta.Field.Name)) meta.Field.SetValue(data, null);
            return true;
        }
        var (result, success) = parse(strList);
        if (!success) return false;
        if (result != null)
            meta.Field.SetValue(data, result);
        return true;
    }

    private static bool ResolveDef(TweakData data, StatFieldMeta meta)
    {
        if (data._savedLoadData == null || !data._savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not string defName || defName.NullOrEmpty())
            return true;
        var def = GenDefDatabase.GetDefSilentFail(meta.Field.FieldType, defName);
        if (def != null)
        {
            meta.Field.SetValue(data, def);
            return true;
        }
        // Def 引用解析失败（例如相关模组被移除）：记录 info 级日志后跳过。
        // 调用方会将该字段标记为损坏并回滚，原始 defName 仍会被保留以便日后重新解析。
        SerializationHelper.LogMissingDef($"字段 {meta.Field.Name}（{meta.Field.FieldType.Name}）", defName);
        return false;
    }

    private static bool ResolveStatModifierList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<StatModifier>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseStatModifier(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolvePawnCapacityList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<PawnCapacityModifier>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParsePawnCapacityModifier(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveDamageFactorList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<DamageFactor>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseDamageFactor(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveSkillGainList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<SkillGain>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseSkillGain(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveAptitudeList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<Aptitude>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseAptitude(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveGeneticTraitDataList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<GeneticTraitData>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseGeneticTraitData(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveThingDefCountList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<ThingDefCountClass>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseThingDefCountClass(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveDefList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var defType = meta.Field.FieldType.GetGenericArguments()[0];
        var listType = typeof(List<>).MakeGenericType(defType);
        var result = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var str in strList)
        {
            var def = GenDefDatabase.GetDefSilentFail(defType, str);
            if (def != null) result.Add(def);
            else
            {
                SerializationHelper.LogMissingDef($"字段 {meta.Field.Name}（List<{defType.Name}>）", str);
                return (null, false);
            }
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveIngredientList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<IngredientCount>(strList.Count);
        foreach (var str in strList)
        {
            var pipeIdx = str.LastIndexOf('|');
            if (pipeIdx < 0)
            {
                Log.Warning($"[BalanceTweak] IngredientCount 语法解析失败：\"{str}\"（期望 filter|count，字段: {meta.Field.Name}），数据: {data.id}");
                return (null, false);
            }
            var filterStr = str[..pipeIdx];
            var countStr = str[(pipeIdx + 1)..];
            if (!float.TryParse(countStr, out float count) || count <= 0)
            {
                Log.Warning($"[BalanceTweak] IngredientCount 语法解析失败：\"{str}\"（count 应为正浮点数，字段: {meta.Field.Name}），数据: {data.id}");
                return (null, false);
            }
            var ic = new IngredientCount();
            ic.SetBaseCount(count);
            var filter = SerializationHelper.ParseIngredientFilter(filterStr);
            if (filter == null) return (null, false);
            ic.filter.CopyAllowancesFrom(filter);
            if (ic.filter.AllowedDefCount == 0) return (null, false); // 过滤器解析后无任何允许项：跳过（缺失的 Def 已在 ParseIngredientFilter 中记录）
            result.Add(ic);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveSkillReqList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<SkillRequirement>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseSkillRequirement(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveTraitReqList(TweakData data, StatFieldMeta meta) => ResolveList(data, meta, strList =>
    {
        var result = new List<TraitRequirement>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseTraitRequirement(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private static bool ResolveIngredientFilter(TweakData data, StatFieldMeta meta)
    {
        if (data._savedLoadData == null || !data._savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not string str || str.NullOrEmpty())
        {
            // 已动过但空（无 key / 非字符串 / 空串）⇒ 把实例值显式置空（是否 Cleared 由 _clearedFieldNames 判定）。
            if (data.IsFieldModified(meta.Field.Name)) meta.Field.SetValue(data, null);
            return true;
        }
        var filter = SerializationHelper.ParseIngredientFilter(str);
        if (filter != null)
        {
            meta.Field.SetValue(data, filter);
            return true;
        }
        return false;
    }

    private static bool ResolveProcessIngredientList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<ProcessIngredientItem>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseProcessIngredientItem(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveProcessResultList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<ProcessResultItem>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseProcessResultItem(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveBiomePlantList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<BiomePlantRecord>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseBiomePlantRecord(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveBiomeAnimalList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<BiomeAnimalRecord>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseBiomeAnimalRecord(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveFishChanceList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<FishChance>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseFishChance(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveDiseaseList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<BiomeDiseaseRecord>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseBiomeDiseaseRecord(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveWeatherCommonalityList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<WeatherCommonalityRecord>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseWeatherCommonalityRecord(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private static bool ResolveTerrainThresholdList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = new List<TerrainThreshold>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseTerrainThreshold(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    /// <summary>库存生成器：行文本 → 生成器对象（<see cref="StockGeneratorCodec"/>）。任一条失败即整字段回滚。</summary>
    private static bool ResolveStockGeneratorList(TweakData data, StatFieldMeta meta)
    {
        return ResolveList(data, meta, strList =>
        {
            var result = StockGeneratorCodec.Deserialize(strList);
            return result == null ? (null, false) : (result, true);
        });
    }

    private static bool ResolveStringList(TweakData data, StatFieldMeta meta)
    {
        if (data._savedLoadData == null || !data._savedLoadData.TryGetValue(meta.Field.Name, out var saved))
            return true;
        var list = saved as List<string>;
        if (list is { Count: > 0 })
            meta.Field.SetValue(data, list);
        else if (data.IsFieldModified(meta.Field.Name))
            meta.Field.SetValue(data, null); // 已动过但空 ⇒ 显式置空（值必须为空）
        return true;
    }

    private static bool ResolveIntList(TweakData data, StatFieldMeta meta)
    {
        if (data._savedLoadData == null || !data._savedLoadData.TryGetValue(meta.Field.Name, out var saved))
            return true;
        var list = saved as List<int>;
        if (list is { Count: > 0 })
            meta.Field.SetValue(data, list);
        else if (data.IsFieldModified(meta.Field.Name))
            meta.Field.SetValue(data, null); // 已动过但空 ⇒ 显式置空（值必须为空）
        return true;
    }

    #endregion
}
