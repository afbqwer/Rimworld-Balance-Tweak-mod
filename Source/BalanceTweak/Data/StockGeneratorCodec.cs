using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <see cref="TraderKindDef.stockGenerators"/>（<c>List&lt;StockGenerator&gt;</c>）的字段表、文本编解码与克隆。
///
/// <para>
/// <b>不做反射遍历</b>：原版每个 <see cref="StockGenerator"/> 子类都在 <see cref="ownFieldNames"/> 里
/// 显式列出<b>自己的</b>字段，逐字段解析成 <see cref="FieldInfo"/>（不少字段是 private，只能按名字经
/// <see cref="Reflect.Field"/> 拿）。<b>不在这张表里的类型一律视为"非原版"</b>（其它 Mod 新增的子类）：
/// 只暴露它继承链上表内类型声明的字段（即"只可改其基类的字段"），见 <see cref="FieldsFor"/> /
/// <see cref="IsVanilla"/>。原版加了新生成器类型 / 新字段时，测试会先炸出来（见
/// <c>testStockGen_FieldTableIntegrity</c>）。
/// </para>
///
/// <para>
/// 文本（<b>非 XML</b>）一行一个生成器，只服务本 mod 的存档与剪贴板：
/// <code>
/// StockGenerator_Tag|countRange=1~4|tradeTag=ExoticMisc|excludedThingDefs=[Steel;Cloth]
/// </code>
/// 每个字段值写进记录前整体转义（<see cref="Escape"/> 用反斜杠转义 <c>\ | = [ ] ;</c>），列表元素
/// 再各自多转义一层 —— 值里出现任何分隔符都不会破坏结构。<b>等于"新实例初始值"的字段不写</b>
/// （与原版 XML 省略默认值的习惯一致，读回来由新实例的初始值给出同样的值）。
/// </para>
/// </summary>
internal static class StockGeneratorCodec
{
    #region 字段表

    /// <summary>一个字段的编解码 / 编辑方式。文本写法（本类）与表单控件（<c>StockGeneratorFieldUi</c>）都看它。</summary>
    public enum Kind
    {
        Float,
        Int,
        Bool,
        Text,
        IntRange,
        FloatRange,
        Enum,
        /// <summary>单个 <see cref="Def"/> 引用。</summary>
        Def,
        /// <summary><c>List&lt;T&gt; where T : Def</c>。</summary>
        DefList,
        /// <summary><c>List&lt;string&gt;</c>。</summary>
        StringList,
        /// <summary>字段表里也声明了字段的小对象列表（<see cref="CountChance"/> / <see cref="ThingDefCountRangeClass"/>）。</summary>
        ComplexList,
        /// <summary>表里不认识的类型（原版换了字段类型？）：编解码跳过、表单提示不支持。</summary>
        Unsupported,
    }

    /// <summary>表里声明的一个字段。</summary>
    public sealed class SgField
    {
        public string Name = "";
        public Kind Kind;
        /// <summary>读写句柄（public / private 字段都由 <see cref="Reflect.Field"/> 拿到）。</summary>
        public FieldInfo Info = null!;
        /// <summary><see cref="Kind.Enum"/> 的枚举类型。</summary>
        public Type? EnumType;
        /// <summary><see cref="Kind.Def"/> / <see cref="Kind.DefList"/> / <see cref="Kind.ComplexList"/> 的元素类型。</summary>
        public Type? ElementType;

        public override string ToString() => $"{Name}({Kind})";
    }

    /// <summary>
    /// 原版类型 → 它<b>自己声明</b>的字段（继承来的不在此列，由继承链上的那一项给出）。
    /// 顺序 = 文本与表单里的出现顺序。
    /// <para>
    /// 特意不列 <c>StockGenerator_Techprints.tmpGenerated</c>：那是生成库存时临时收集、生成完就
    /// <c>Clear</c> 的字段，不给用户一个"填了也没用"的输入行，也不写进存档。
    /// </para>
    /// </summary>
    private static readonly Dictionary<Type, string[]> ownFieldNames = new()
    {
        [typeof(StockGenerator)] = ["countRange", "customCountRanges", "totalPriceRange", "maxTechLevelGenerate", "maxTechLevelBuy", "price"],

        [typeof(StockGenerator_BuyExpensiveSimple)] = ["minValuePerUnit"],
        [typeof(StockGenerator_BuySingleDef)] = ["thingDef"],
        [typeof(StockGenerator_BuyTradeTag)] = ["tag"],
        [typeof(StockGenerator_BuySlaves)] = [],
        [typeof(StockGenerator_MiscItems)] = [],
        [typeof(StockGenerator_MarketValue)] = ["tradeTag", "weaponTag", "apparelTag"],
        [typeof(StockGenerator_ReinforcedBarrels)] = [],
        [typeof(StockGenerator_Tomes)] = [],
        [typeof(StockGenerator_Animals)] = ["tradeTagsSell", "tradeTagsBuy", "createMatingPair", "kindCountRange", "minWildness", "maxWildness", "checkTemperature"],
        [typeof(StockGenerator_Category)] = ["categoryDef", "thingDefCountRange", "excludedThingDefs", "excludedCategories"],
        [typeof(StockGenerator_MultiDef)] = ["thingDefs"],
        [typeof(StockGenerator_SingleDef)] = ["thingDef"],
        [typeof(StockGenerator_Slaves)] = ["respectPopulationIntent", "slaveKindDef"],
        [typeof(StockGenerator_Tag)] = ["tradeTag", "thingDefCountRange", "excludedThingDefs"],
        [typeof(StockGenerator_Techprints)] = ["countChances"],

        // 小对象列表的元素类型：表单（SGComplexListEditorWindow）与文本编解码共用同一张字段表。
        [typeof(CountChance)] = ["count", "chance"],
        [typeof(ThingDefCountRangeClass)] = ["thingDef", "countRange"],
    };

    private static readonly Dictionary<Type, List<SgField>> fieldCache = new();
    private static readonly List<string> unresolvedFields = new();
    private static readonly HashSet<Type> warnedNonVanilla = new();
    private static readonly Dictionary<Type, object?> baselines = new();
    private static Dictionary<string, Type>? vanillaByName;

    /// <summary>声明了但解析不到的字段（被原版改名 / 移除），形如 <c>StockGenerator_Animals.foo</c>。测试会断言它是空的。</summary>
    public static IReadOnlyList<string> UnresolvedFields => unresolvedFields;

    /// <summary>表里所有"具体的"原版生成器类型（类型菜单用），按名字排序。</summary>
    public static List<Type> VanillaGeneratorTypes() =>
        ownFieldNames.Keys
            .Where(t => t != typeof(StockGenerator) && typeof(StockGenerator).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>是否原版生成器类型：判据是"在本类的字段表里"。表外的一律按非原版处理。</summary>
    public static bool IsVanilla(Type type) => ownFieldNames.ContainsKey(type);

    /// <summary>类型的显示名 = 原版 XML 里 <c>Class</c> 的写法（如 <c>StockGenerator_Tag</c>），也是存档里的类型标记。</summary>
    public static string TypeLabel(Type type) => GenTypes.GetTypeNameWithoutIgnoredNamespaces(type);

    /// <summary>
    /// 某类型可读写的字段 = 继承链上每个"表内类型"声明的字段（<see cref="StockGenerator"/> 的基类字段在前，
    /// 各级子类字段随后）。表外类型（其它 Mod 新增的子类）只拿到链上表内那部分 —— 即"只可改其基类的字段"。
    /// <para>也支持字段表里的非生成器小对象类型（<see cref="CountChance"/> / <see cref="ThingDefCountRangeClass"/>）。</para>
    /// </summary>
    public static IReadOnlyList<SgField> FieldsFor(Type type)
    {
        if (fieldCache.TryGetValue(type, out var cached)) return cached;

        var chain = new List<Type>();
        for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            chain.Add(t);
        chain.Reverse(); // 基类字段在前

        var fields = new List<SgField>();
        foreach (var t in chain)
        {
            if (!ownFieldNames.TryGetValue(t, out var names)) continue;
            foreach (var name in names)
            {
                var info = Reflect.Field(t, name); // 找不到会打一条 Log.Error
                if (info == null)
                {
                    unresolvedFields.Add($"{t.Name}.{name}");
                    continue;
                }
                var field = new SgField { Name = name, Info = info };
                Classify(field);
                fields.Add(field);
            }
        }
        fieldCache[type] = fields;
        return fields;
    }

    /// <summary>
    /// 字段的 C# 类型 → 编解码 / 编辑方式。只认这里显式列出的类型：原版一旦换了字段类型，会打一条
    /// <c>Log.Error</c> 并把该字段标成 <see cref="Kind.Unsupported"/>（编解码跳过、表单提示不支持），
    /// 由 <c>testStockGen_FieldTableIntegrity</c> 先炸出来，而不是等用户存档时静默丢字段。
    /// </summary>
    private static void Classify(SgField field)
    {
        var type = Nullable.GetUnderlyingType(field.Info.FieldType) ?? field.Info.FieldType;

        if (type == typeof(float)) { field.Kind = Kind.Float; return; }
        if (type == typeof(int)) { field.Kind = Kind.Int; return; }
        if (type == typeof(bool)) { field.Kind = Kind.Bool; return; }
        if (type == typeof(string)) { field.Kind = Kind.Text; return; }
        if (type == typeof(IntRange)) { field.Kind = Kind.IntRange; return; }
        if (type == typeof(FloatRange)) { field.Kind = Kind.FloatRange; return; }
        if (type.IsEnum) { field.Kind = Kind.Enum; field.EnumType = type; return; }
        if (typeof(Def).IsAssignableFrom(type)) { field.Kind = Kind.Def; field.ElementType = type; return; }
        if (type == typeof(List<string>)) { field.Kind = Kind.StringList; return; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var element = type.GetGenericArguments()[0];
            if (typeof(Def).IsAssignableFrom(element)) { field.Kind = Kind.DefList; field.ElementType = element; return; }
            // 小对象列表：元素类型自己也必须在表里（否则不知道怎么编解码它的字段）
            if (ownFieldNames.ContainsKey(element)) { field.Kind = Kind.ComplexList; field.ElementType = element; return; }
        }

        field.Kind = Kind.Unsupported;
        Log.Error($"[BalanceTweak] stockGenerators：字段 {field.Info.DeclaringType?.Name}.{field.Name} 的类型 {type.Name}"
            + " 不在支持列表里 —— 该字段不会被读写（原版换了字段类型？需要在本表里为它加一种 Kind）。");
    }

    #endregion

    #region 存档 / 剪贴板入口

    /// <summary>一组生成器 → 每行一条的文本；空列表返回 null。</summary>
    public static List<string>? Serialize(List<StockGenerator>? list)
    {
        if (list.NullOrEmpty()) return null;
        var lines = new List<string>(list!.Count);
        foreach (var gen in list)
            if (gen != null) lines.Add(ToItemString(gen));
        return lines.Count > 0 ? lines : null;
    }

    /// <summary>单条 → 一行文本（不含换行）。格式见类型注释。</summary>
    public static string ToItemString(StockGenerator gen)
    {
        var type = gen.GetType();
        if (!IsVanilla(type) && warnedNonVanilla.Add(type))
        {
            Log.Warning($"[BalanceTweak] stockGenerators：{TypeLabel(type)} 是非原版生成器（字段表里没有它的字段），"
                + "存档只保留类型名和它继承的原版字段，它自己的字段不会被保存。");
        }

        var baseline = BaselineFor(type);
        var sb = new StringBuilder(TypeLabel(type));
        foreach (var field in FieldsFor(type))
        {
            if (field.Kind == Kind.Unsupported) continue;
            var value = ReadField(field, gen);
            if (IsDefault(field, value, baseline)) continue;
            var text = EncodeFieldValue(field, value!);
            if (text.Length == 0) continue;
            sb.Append(FieldSep).Append(field.Name).Append(PairSep).Append(Escape(text));
        }
        return sb.ToString();
    }

    /// <summary>
    /// 文本行 → 生成器列表。任一行解析失败（类型不存在 / 字段不认识 / 值非法 / Def 缺失）⇒ 返回 null，
    /// 调用方据此把字段标为损坏并保持 Def 现状，绝不写入半截数据。
    /// </summary>
    public static List<StockGenerator>? Deserialize(List<string>? lines)
    {
        if (lines.NullOrEmpty()) return null;
        var result = new List<StockGenerator>(lines!.Count);
        foreach (var line in lines)
        {
            if (line.NullOrEmpty()) continue;
            if (!TryParse(line, out var gen) || gen == null) return null;
            result.Add(gen);
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>单行 → 单条生成器。</summary>
    public static bool TryParse(string? line, out StockGenerator? gen)
    {
        gen = null;
        if (line.NullOrEmpty()) return false;

        var parts = SplitUnescaped(line!, FieldSep);
        if (!TryResolveType(Unescape(parts[0]), out var type)) return false;
        if (!TryCreateInstance(type, out var instance) || instance is not StockGenerator generator) return false;

        // 余下各段原样拼回记录体，交给与"小对象元素"同一段代码填字段。
        if (!FillFromRecord(string.Join(FieldSep.ToString(), parts.Skip(1)), generator, line!)) return false;

        gen = generator;
        return true;
    }

    #endregion

    #region 深克隆

    /// <summary>
    /// 克隆：按字段表复制（<b>不</b>经过文本），因此字段里的列表都换成新实例，派生自 Def 的活对象不会
    /// 被字段的编辑影响。用于 Def → 字段（避免共享）与字段 → 编辑窗口的副本（Cancel 不影响字段）。
    /// <para>
    /// <b>表外的非原版类型不克隆，直接沿用原实例</b>：它的自有字段不在表里，克隆只会把它们换成默认值
    /// （等于丢数据）。代价是该条目的基类字段是<b>就地</b>改在 Def 的活对象上的。
    /// </para>
    /// </summary>
    public static StockGenerator? Clone(StockGenerator? gen)
    {
        if (gen == null) return null;
        var type = gen.GetType();
        if (!IsVanilla(type)) return gen;

        if (TryCreateInstance(type, out var copyObj) && copyObj is StockGenerator copy)
        {
            CopyFields(gen, copy);
            return copy;
        }
        return gen; // 抽象 / 无法实例化：同样沿用原实例
    }

    public static List<StockGenerator>? CloneAll(List<StockGenerator>? list)
    {
        if (list.NullOrEmpty()) return null;
        var result = new List<StockGenerator>(list!.Count);
        foreach (var gen in list)
        {
            var clone = Clone(gen);
            if (clone != null) result.Add(clone);
        }
        return result.Count > 0 ? result : null;
    }

    /// <summary>按字段表把 <paramref name="src"/> 的字段复制到 <paramref name="dst"/>（值类型 / Def / 字符串共享，列表新建）。</summary>
    private static void CopyFields(object src, object dst)
    {
        foreach (var field in FieldsFor(src.GetType()))
        {
            if (field.Kind == Kind.Unsupported) continue;
            var value = ReadField(field, src);
            if (value == null) continue;
            WriteField(field, dst, CopyFieldValue(field, value));
        }
    }

    private static object CopyFieldValue(SgField field, object value)
    {
        if (field.Kind is not (Kind.StringList or Kind.DefList or Kind.ComplexList)) return value; // 值类型 / 枚举 / Def / 字符串：按值或按引用共享

        var elementType = field.ElementType ?? typeof(object);
        var source = (IList)value;
        var copy = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in source)
        {
            if (item == null) continue;
            if (field.Kind == Kind.ComplexList && TryCreateInstance(elementType, out var itemCopy) && itemCopy != null)
            {
                CopyFields(item, itemCopy);
                copy.Add(itemCopy);
            }
            else
            {
                copy.Add(item); // Def / 字符串不可变，直接共享；CountChance 是值类型，装箱即复制
            }
        }
        return copy;
    }

    #endregion

    #region 编码

    /// <summary>单个字段值 → 文本（列表元素已各自转义；调用方负责把整段再 <see cref="Escape"/> 进记录）。空串 = 列表为空。</summary>
    private static string EncodeFieldValue(SgField field, object value)
    {
        switch (field.Kind)
        {
            case Kind.Float: return ((float)value).ToString("G9", CultureInfo.InvariantCulture);
            case Kind.Int: return ((int)value).ToString(CultureInfo.InvariantCulture);
            case Kind.Bool: return (bool)value ? "true" : "false";
            case Kind.Text: return (string)value;
            case Kind.IntRange: return IntRangeText((IntRange)value);
            case Kind.FloatRange: return FloatRangeText((FloatRange)value);
            case Kind.Enum: return value.ToString() ?? "";
            case Kind.Def: return ((Def)value).defName ?? "";
            case Kind.DefList: return JoinList(((IEnumerable)value).Cast<Def>().Select(d => d.defName ?? ""));
            case Kind.StringList: return JoinList(((IEnumerable)value).Cast<string>());
            case Kind.ComplexList: return JoinList(((IEnumerable)value).Cast<object>().Select(EncodeRecord));
            default: return "";
        }
    }

    /// <summary>小对象列表的一个元素 → 记录文本（"<c>字段名=值</c>"，字段值各自转义）。元素<b>总是</b>写全字段，
    /// 免得"所有字段都是默认值"的元素编码成空串、解码时被当作空元素丢掉。</summary>
    private static string EncodeRecord(object value)
    {
        var sb = new StringBuilder();
        foreach (var field in FieldsFor(value.GetType()))
        {
            if (field.Kind == Kind.Unsupported) continue;
            var fieldValue = ReadField(field, value);
            var text = fieldValue == null ? "" : EncodeFieldValue(field, fieldValue);
            if (sb.Length > 0) sb.Append(FieldSep);
            sb.Append(field.Name).Append(PairSep).Append(Escape(text));
        }
        return sb.ToString();
    }

    private static string JoinList(IEnumerable<string?> items)
    {
        var sb = new StringBuilder(ListStart);
        bool any = false;
        foreach (var item in items)
        {
            if (item.NullOrEmpty()) continue; // 空元素无意义（解码端同样跳过）
            if (any) sb.Append(ItemSep);
            sb.Append(Escape(item!));
            any = true;
        }
        return any ? sb.Append(ListEnd).ToString() : "";
    }

    /// <summary>整数区间 → <c>min~max</c>（不变文化，与 <see cref="ParseHelper"/> 一致）。</summary>
    private static string IntRangeText(IntRange range)
        => range.min.ToString(CultureInfo.InvariantCulture) + "~" + range.max.ToString(CultureInfo.InvariantCulture);

    /// <summary>浮点区间 → <c>min~max</c>。用 "G9" 而不是默认的 "G7"，保证 1.6 的 .NET Framework 上不丢精度。</summary>
    private static string FloatRangeText(FloatRange range)
        => range.min.ToString("G9", CultureInfo.InvariantCulture) + "~" + range.max.ToString("G9", CultureInfo.InvariantCulture);

    /// <summary>
    /// 字段值是否等于"新实例上的初始值"（如 <c>countRange = IntRange.Zero</c>）：是则不写进文本，
    /// 读回来由新实例的初始值给出同样的值（与其它列表列、原版 XML 的"省略默认值"一致）。
    /// </summary>
    private static bool IsDefault(SgField field, object? value, object? baseline)
    {
        if (value == null) return true;
        if (value is string str) return str.Length == 0;
        if (value is ICollection collection) return collection.Count == 0;
        // 引用类型（Def / 列表 / 小对象）非 null 就是有值；值类型与基准实例比一比。
        if (!field.Info.FieldType.IsValueType || baseline == null) return false;
        try { return Equals(field.Info.GetValue(baseline), value); }
        catch { return false; }
    }

    /// <summary>
    /// "新实例上的初始值"基准。抽象类型（<see cref="StockGenerator"/> 本身、<see cref="StockGenerator_MiscItems"/>）
    /// 取不到实例，就往上找第一个能实例化的祖先 —— 字段初始值写在声明它的类里，取哪个具体子类都一样。
    /// </summary>
    private static object? BaselineFor(Type type)
    {
        if (baselines.TryGetValue(type, out var cached)) return cached;

        object? result = null;
        for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
        {
            if (t.IsAbstract) continue;
            try { result = Activator.CreateInstance(t); break; }
            catch { /* 没有无参构造：继续往上找 */ }
        }
        baselines[type] = result;
        return result;
    }

    #endregion

    #region 解析

    /// <summary>类型标记 → 具体类型。先查字段表（原版 / 官方 DLC 精确命中），再回退到"在已加载程序集里按名字找"（其它 Mod 新增的子类）。</summary>
    private static bool TryResolveType(string typeName, out Type type)
    {
        type = null!;
        if (typeName.NullOrEmpty()) return false;

        if (VanillaTypesByName().TryGetValue(typeName, out var vanilla)) { type = vanilla; return true; }

        var modType = GenTypes.GetTypeInAnyAssembly(typeName, typeof(StockGenerator).Namespace);
        if (modType != null && modType != typeof(StockGenerator)
            && typeof(StockGenerator).IsAssignableFrom(modType) && !modType.IsAbstract)
        {
            type = modType;
            return true;
        }

        Log.Warning($"[BalanceTweak] stockGenerators：找不到可实例化的生成器类型 {typeName}，无法解析。");
        return false;
    }

    private static Dictionary<string, Type> VanillaTypesByName()
    {
        if (vanillaByName != null) return vanillaByName;
        vanillaByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in VanillaGeneratorTypes()) vanillaByName[TypeLabel(t)] = t;
        return vanillaByName;
    }

    private static bool TryCreateInstance(Type type, out object? instance)
    {
        instance = null;
        try { instance = Activator.CreateInstance(type); }
        catch (Exception e)
        {
            Log.Warning($"[BalanceTweak] stockGenerators：无法创建 {type.FullName}：{e.Message}");
            return false;
        }
        if (instance == null)
        {
            Log.Warning($"[BalanceTweak] stockGenerators：{type.FullName} 无法实例化。");
            return false;
        }
        return true;
    }

    /// <summary>"字段名=值"记录体 → 填进 <paramref name="target"/>。生成器行（去掉类型名段后）与小对象列表元素共用。</summary>
    private static bool FillFromRecord(string body, object target, string wholeLine)
    {
        var fields = FieldsFor(target.GetType());
        foreach (var part in SplitUnescaped(body, FieldSep))
        {
            if (part.Length == 0) continue;
            var pair = SplitUnescaped(part, PairSep);
            if (pair.Count != 2)
            {
                Log.Warning($"[BalanceTweak] stockGenerators：\"{part}\" 不是“字段名=值”，放弃整条：{wholeLine}");
                return false;
            }
            var name = Unescape(pair[0]);
            var field = fields.FirstOrDefault(f => f.Name == name);
            if (field == null)
            {
                Log.Warning($"[BalanceTweak] stockGenerators：{target.GetType().Name} 没有字段 {name}"
                    + $"（存档来自不同版本？），放弃整条：{wholeLine}");
                return false;
            }
            if (field.Kind == Kind.Unsupported) continue; // Classify 已报过，不当作损坏
            if (!TryDecodeFieldValue(field, Unescape(pair[1]), out var value)) return false;
            WriteField(field, target, value);
        }
        return true;
    }

    private static bool TryDecodeFieldValue(SgField field, string text, out object? value)
    {
        value = null;
        switch (field.Kind)
        {
            case Kind.Float:
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    return Fail(field, text, "浮点数");
                value = f;
                return true;

            case Kind.Int:
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return Fail(field, text, "整数");
                value = i;
                return true;

            case Kind.Bool:
                if (!bool.TryParse(text, out var b)) return Fail(field, text, "true / false");
                value = b;
                return true;

            case Kind.Text:
                value = text.Length > 0 ? text : null;
                return true;

            case Kind.IntRange:
                if (!TrySplitRange(text, out var min, out var max)
                    || !int.TryParse(min, NumberStyles.Integer, CultureInfo.InvariantCulture, out int imin)
                    || !int.TryParse(max, NumberStyles.Integer, CultureInfo.InvariantCulture, out int imax))
                    return Fail(field, text, "整数区间 min~max");
                value = new IntRange(imin, imax);
                return true;

            case Kind.FloatRange:
                if (!TrySplitRange(text, out var fminText, out var fmaxText)
                    || !float.TryParse(fminText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fmin)
                    || !float.TryParse(fmaxText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fmax))
                    return Fail(field, text, "浮点区间 min~max");
                value = new FloatRange(fmin, fmax);
                return true;

            case Kind.Enum:
                if (!Enum.TryParse(field.EnumType!, text, out var parsed) || parsed == null
                    || !Enum.IsDefined(field.EnumType!, parsed))
                    return Fail(field, text, $"{field.EnumType!.Name} 的枚举名");
                value = parsed;
                return true;

            case Kind.Def:
            {
                if (text.NullOrEmpty()) return true; // 空 = 没有引用
                if (!TryResolveDef(field.ElementType!, text, out var def)) return false;
                value = def;
                return true;
            }

            case Kind.DefList:
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(field.ElementType!))!;
                value = list;
                if (!TrySplitList(text, out var items)) return Fail(field, text, "[defName;defName]");
                foreach (var item in items)
                {
                    if (!TryResolveDef(field.ElementType!, item, out var def)) return false;
                    list.Add(def);
                }
                return true;
            }

            case Kind.StringList:
            {
                if (!TrySplitList(text, out var items)) return Fail(field, text, "[a;b]");
                value = items.Count > 0 ? items : null;
                return true;
            }

            case Kind.ComplexList:
            {
                var elementType = field.ElementType!;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                value = list;
                if (!TrySplitList(text, out var items)) return Fail(field, text, "[字段名=值;…]");
                foreach (var item in items)
                {
                    if (!TryCreateInstance(elementType, out var element) || element == null) return false;
                    if (!FillFromRecord(item, element, item)) return false;
                    list.Add(element);
                }
                return true;
            }

            default:
                return true; // Unsupported：不读不写
        }
    }

    /// <summary>Def 引用：缺失即失败（调用方保持 Def 现状，等用户处理 Mod 缺失问题）。</summary>
    private static bool TryResolveDef(Type defType, string defName, out Def def)
    {
        def = GenDefDatabase.GetDefSilentFail(defType, defName)!;
        if (def != null) return true;
        SerializationHelper.LogMissingDef($"stockGenerators 的 {defType.Name}", defName);
        return false;
    }

    /// <summary>列表文本（<c>[a;b]</c>）→ 元素文本。空文本 = 空列表；元素里的 <c>;</c> 是转义过的，不会被切错。</summary>
    private static bool TrySplitList(string text, out List<string> items)
    {
        items = new List<string>();
        if (text.Length == 0) return true;
        if (text[0] != ListStart || text[^1] != ListEnd) return false;

        var inner = text[1..^1];
        if (inner.Length == 0) return true;
        foreach (var raw in SplitUnescaped(inner, ItemSep))
        {
            if (raw.Length == 0) continue; // 空元素（编码端不会写）跳过，避免还原出空元素
            items.Add(Unescape(raw));
        }
        return true;
    }

    private static bool TrySplitRange(string text, out string min, out string max)
    {
        min = max = "";
        var parts = text.Split('~');
        if (parts.Length != 2) return false;
        min = parts[0];
        max = parts[1];
        return true;
    }

    private static bool Fail(SgField field, string text, string expected)
    {
        Log.Warning($"[BalanceTweak] stockGenerators：字段 {field.Name} 的值 \"{text}\" 不是合法内容（期望 {expected}）。");
        return false;
    }

    #endregion

    #region 字段读写（UI 与内部编解码共用）

    /// <summary>读字段值；失败只告警并返回 null。</summary>
    public static object? ReadField(SgField field, object target)
    {
        try { return field.Info.GetValue(target); }
        catch (Exception e)
        {
            Log.Warning($"[BalanceTweak] stockGenerators：读取 {target.GetType().Name}.{field.Name} 失败：{e.Message}");
            return null;
        }
    }

    /// <summary>写字段值；失败只告警（<c>CountChance</c> 这类值类型要传装箱对象，<see cref="FieldInfo.SetValue"/> 会就地改写这个箱子）。</summary>
    public static void WriteField(SgField field, object target, object? value)
    {
        try { field.Info.SetValue(target, value); }
        catch (Exception e)
        {
            Log.Error($"[BalanceTweak] stockGenerators：写入 {target.GetType().Name}.{field.Name} 失败：{e.Message}");
        }
    }

    #endregion

    #region 转义 / 切分

    private const char FieldSep = '|';
    private const char PairSep = '=';
    private const char ListStart = '[';
    private const char ListEnd = ']';
    private const char ItemSep = ';';
    private const char EscapeChar = '\\';

    /// <summary>转义时必须在前面加 <see cref="EscapeChar"/> 的字符。</summary>
    private static readonly char[] SpecialChars = [EscapeChar, FieldSep, PairSep, ListStart, ListEnd, ItemSep];

    /// <summary>把内容文本转义成"结构无关"的一段：所有分隔符前加 <see cref="EscapeChar"/>。</summary>
    public static string Escape(string text)
    {
        if (text.IndexOfAny(SpecialChars) < 0) return text;
        var sb = new StringBuilder(text.Length + 8);
        foreach (char c in text)
        {
            if (Array.IndexOf(SpecialChars, c) >= 0) sb.Append(EscapeChar);
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary><see cref="Escape"/> 的逆：<c>\x</c> 一律还原成字面量 <c>x</c>。</summary>
    public static string Unescape(string text)
    {
        if (text.IndexOf(EscapeChar) < 0) return text;
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == EscapeChar && i + 1 < text.Length) c = text[++i];
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>按未转义的分隔符切分（转义序列原样保留在片段里，由调用方按需 <see cref="Unescape"/>）。</summary>
    private static List<string> SplitUnescaped(string text, char delim)
    {
        var parts = new List<string>();
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == EscapeChar && i + 1 < text.Length)
            {
                sb.Append(c).Append(text[++i]);
                continue;
            }
            if (c == delim) { parts.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts;
    }

    #endregion

    #region 摘要

    /// <summary>一行摘要：最多两个非默认字段值，用于列表窗口行上的副标题。非原版类型不打摘要（字段名无从翻译）。</summary>
    public static string Summarize(StockGenerator gen)
    {
        var type = gen.GetType();
        if (!IsVanilla(type)) return "";

        var baseline = BaselineFor(type);
        var parts = new List<string>(2);
        foreach (var field in FieldsFor(type))
        {
            if (parts.Count >= 2) break;
            if (field.Kind == Kind.Unsupported) continue;
            var value = ReadField(field, gen);
            if (IsDefault(field, value, baseline)) continue;
            parts.Add($"{field.Name}={DescribeValue(value!)}");
        }
        return parts.Count > 0 ? string.Join("  ", parts) : "";
    }

    private static string DescribeValue(object value)
    {
        switch (value)
        {
            case Def d: return d.defName;
            case string s: return s.Length > 24 ? s[..24] + "…" : s;
            case Enum e: return e.ToString();
            case bool b: return b ? "true" : "false";
            case IEnumerable en:
            {
                int n = 0;
                foreach (var _ in en) n++;
                return $"[{n}]";
            }
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }
    }

    #endregion
}
