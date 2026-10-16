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
        private static readonly List<string> duplicateRegistrations = new();

        /// <summary>全部登记项（精确 + 通配），供文档与诊断使用。</summary>
        public static IReadOnlyList<FieldCodec> All { get; private set; } = Array.Empty<FieldCodec>();

        /// <summary>
        /// 被 <see cref="Exact"/> 登记两次的类型（后者静默覆盖前者 ⇒ 前一条登记<b>白写</b>：其字段既不落盘也不还原）。
        /// 由 <see cref="Exact"/> 在覆盖前记下，测试断言为空。见 Docs §3.9.3。
        /// </summary>
        public static IReadOnlyList<string> DuplicateRegistrations => duplicateRegistrations;

        /// <summary>
        /// 显式确保**全部**字段声明已就绪（C# 的静态构造是惰性的，光读 <see cref="All"/> 只保证本表那部分）。
        ///
        /// <para>
        /// <b>谁需要调它</b>：只用 <see cref="PocoCodec"/> 而不查表的调用点 —— GUI 剪贴板
        /// （<c>StatColumnConfig.TryGetDeclaredListElementType</c>）与编辑器窗口的默认
        /// <c>SerializeList</c> / <c>DeserializeList</c>。它们判 <c>PocoCodec.IsDeclared</c>，
        /// 而 <b><see cref="PocoCodec"/> 不会自己去拉这些声明类</b>（反向依赖会构成静态构造环）——
        /// 少这一次触碰，未初始化的进程里 <c>IsDeclared</c> 会**全部返回 false**，
        /// 表现为"复制/粘贴/拖拽涂抹按了没反应"，且无任何日志。
        /// </para>
        /// <para>
        /// <b>声明有两个来源，两处都得拉</b>：本表（<c>PocoList&lt;T&gt;</c> 登记的 17 个 POCO 类型）
        /// 与 <see cref="StockGeneratorCodec"/>（<c>StockGenerator</c> 及其子类等）。只拉一处会让
        /// <c>IsDeclared</c> 随"哪条链先被触发"而漂移 —— 实测代价见 Docs §3.9.3（一个契约测试
        /// 单独跑红、全量跑绿）。
        /// </para>
        /// <para>
        /// 正常流程下（Mod 设置先于 GUI 加载，<c>ExposeData</c> 里查过表）两处都早已建好，这里只是白跑两次属性读取。
        /// </para>
        /// </summary>
        public static void EnsureBuilt()
        {
            _ = All;
            StockGeneratorCodec.EnsureBuilt();
        }

        public static FieldCodec? Find(Type fieldType)
        {
            if (exact.TryGetValue(fieldType, out var hit)) return hit;
            for (int i = 0; i < patterns.Count; i++)
                if (patterns[i].Matches(fieldType)) return patterns[i];
            return null;
        }

        static FieldCodecs() => Build();

        private static void Exact(Type type, string name, ExposeDelegate expose, ResolveDelegate? resolve = null)
        {
            // 重复登记 = 静默覆盖：前一条登记白写（该类型字段既不落盘也不还原），运行期无任何信号 ——
            // 与"两条链漂移""剪贴板截胡"是同一类失效。这里记下并报错，让测试能抓到，
            // 比事后从"某字段怎么不生效"倒查便宜得多。
            if (exact.TryGetValue(type, out var prev))
            {
                duplicateRegistrations.Add($"{type.Name}（先「{prev.Name}」，后「{name}」）");
                Log.Error($"[BalanceTweak] 字段表重复登记：{type.Name} 被登记两次，后一条「{name}」覆盖了「{prev.Name}」。");
            }
            exact[type] = new FieldCodec { Name = name, Matches = t => t == type, Expose = expose, Resolve = resolve };
        }

        private static void Pattern(string name, Func<Type, bool> matches, ExposeDelegate expose, ResolveDelegate? resolve = null)
            => patterns.Add(new FieldCodec { Name = name, Matches = matches, Expose = expose, Resolve = resolve });

        /// <summary>
        /// 登记一个"字段表驱动的 POCO 列表"字段类型（<see cref="PocoCodec"/> 管编解码）。
        ///
        /// <para>
        /// 一行完成三件事：把字段声明写进 <see cref="PocoCodec"/>（**顺序即文本里的字段顺序**）、
        /// 可选取值校验、把 <c>List&lt;T&gt;</c> 登记进本表。
        /// </para>
        ///
        /// <para>
        /// <b>为什么声明也放在这里</b>：原先 17 个类型的字段声明写在 <c>SerializationHelper</c> 的
        /// 静态构造里，而登记项写在本表 —— 于是"加一个类型"要改两个文件，且两张静态构造的
        /// 初始化顺序没有保证（谁先被访问谁先跑），SC10 有可能在声明尚未生效时误报。
        /// 现在两者同一处、同一次初始化。
        /// </para>
        /// </summary>
        private static void PocoList<T>(params string[] fields) where T : class => PocoList<T>(null, fields);

        /// <inheritdoc cref="PocoList{T}(string[])"/>
        /// <param name="validate">
        /// 字段表表达不了的**取值范围**约束（如 <c>count &gt;= 0</c>），解码后校验；返回 <c>false</c> ⇒ 整条被拒。
        /// 不传则不做额外校验。
        /// </param>
        private static void PocoList<T>(Func<T, bool>? validate, params string[] fields) where T : class
        {
            PocoCodec.Declare(typeof(T), fields);
            if (validate != null) PocoCodec.SetValidator(typeof(T), o => validate((T)o));
            Exact(typeof(List<T>), $"List<{typeof(T).Name}>", ExposePocoList<T>, ResolvePocoList<T>);
        }

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

            // ── 字段表驱动的 POCO 列表（④ 第二步迁到 PocoCodec 的那 17 个）──
            // 一行完成三件事：声明字段（顺序即文本里的字段顺序）、可选取值校验、把 List<T> 登记进本表。
            // **加一个新的 POCO 列表类型 = 在这里加一行**，不用再同步改别的文件。
            PocoList<StatModifier>("stat", "value");
            PocoList<PawnCapacityModifier>(
                "capacity", "offset", "setMax", "postFactor", "statFactorMod", "setMaxCurveEvaluateStat");
            PocoList<DamageFactor>("damageDef", "factor");
            PocoList<SkillGain>("skill", "amount");
            PocoList<Aptitude>("skill", "level");
            PocoList<GeneticTraitData>("def", "degree");
            PocoList<ThingDefCountClass>(c => c.count >= 0, "thingDef", "stuff", "count");
            PocoList<SkillRequirement>("skill", "minLevel");
            PocoList<TraitRequirement>("def", "degree");
            PocoList<BiomePlantRecord>("plant", "commonality");
            PocoList<BiomeAnimalRecord>("animal", "commonality");
            PocoList<FishChance>("fishDef", "chance");
            PocoList<BiomeDiseaseRecord>("diseaseInc", "commonality");
            PocoList<WeatherCommonalityRecord>("weather", "commonality");
            PocoList<TerrainThreshold>("terrain", "min", "max");
            PocoList<ProcessIngredientItem>("thing", "thingCategory", "disallowedThingDefs", "countNeeded");
            PocoList<ProcessResultItem>(r => r.count >= 0, "thing", "count");

            // ── bespoke：各有不走字段表的硬理由 ──
            // IngredientCount 挂在 ThingFilter + GetBaseCount/SetBaseCount 上，不是普通字段；
            // StockGenerator 是**多态**列表，每行要带类型标记；ThingFilter 见 SerializationHelper 的说明。
            Exact(typeof(List<IngredientCount>), "List<IngredientCount>", ExposeIngredientList, ResolveIngredientList);
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

    /// <summary>
    /// 字段表驱动的 POCO 列表 —— 落盘（对象列表 → 文本行列表）。17 个类型共用这一份，
    /// 解析侧是 <see cref="ResolvePocoList{T}"/>。
    /// </summary>
    private static void ExposePocoList<T>(TweakData data, StatFieldMeta meta) where T : class
        => ExposeList(data, meta, v =>
        {
            var list = (List<T>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(x => PocoCodec.Encode(x)).ToList();
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
            // 文本格式的唯一实现在 SerializationHelper.IngredientCountToText（剪贴板那份也调它）。
            return list.Select(SerializationHelper.IngredientCountToText).OfType<string>().ToList();
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

    /// <summary>
    /// 字段表驱动的 POCO 列表 —— 解析（文本行列表 → 对象列表）。17 个类型共用这一份。
    /// 任一行失败 ⇒ 整字段标损坏（绝不写入半截数据）。落盘侧是 <see cref="ExposePocoList{T}"/>。
    /// </summary>
    private static bool ResolvePocoList<T>(TweakData data, StatFieldMeta meta) where T : class
        => ResolveList(data, meta, strList =>
        {
            var result = new List<T>(strList.Count);
            foreach (var str in strList)
            {
                // 解码失败（含取值校验不过）与"解出来不是 T"都算损坏：前者是存档文本坏了，
                // 后者是本表登记错了类型 —— 两种都不该把半截数据写进字段。
                if (!PocoCodec.TryDecode(str, typeof(T), out var item) || item is not T typed) return (null, false);
                result.Add(typed);
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
            // 解析逻辑的唯一实现在 SerializationHelper.TextToIngredientCount（剪贴板那份也调它）。
            var ic = SerializationHelper.TextToIngredientCount(str, out var malformed);
            if (ic == null)
            {
                // 语义为空（Def 缺失 / 无允许项）属预期，内部已记 info —— 不在这里报 Warning。
                if (malformed)
                    Log.Warning($"[BalanceTweak] IngredientCount 解析失败：\"{str}\"（期望 filter|count、count 应为正浮点数，字段: {meta.Field.Name}），数据: {data.id}");
                return (null, false);
            }
            result.Add(ic);
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
