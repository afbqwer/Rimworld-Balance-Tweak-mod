using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <see cref="TraderKindDef.stockGenerators"/>（<c>List&lt;StockGenerator&gt;</c>）的字段表、文本编解码与克隆。
///
/// <para>
/// <b>通用机制住在 <see cref="PocoCodec"/></b>（字段表驱动 + 逐字符转义 + 省略默认值 + 克隆），
/// 本类只保留 StockGenerator <b>特有</b>的那部分：
/// </para>
/// <list type="bullet">
/// <item><b>多态列表要写类型名</b> —— 每一行以 <c>类型名|</c> 开头（<see cref="ToItemString"/> / <see cref="TryParse"/>），
/// 因为 <c>List&lt;StockGenerator&gt;</c> 里装的是各种子类；</item>
/// <item><b>非原版透传</b> —— 不在字段表里的类型（其它 Mod 新增的子类）只做只读展示：
/// 存档只留类型名当占位标记，回填时按类型名在 Def 里找回原实例原样放回（<see cref="MergeWithDef"/>）。</item>
/// </list>
///
/// <para>
/// 文本（<b>非 XML</b>）一行一个生成器，只服务本 mod 的存档与剪贴板：
/// <code>
/// StockGenerator_Tag|countRange=1~4|tradeTag=ExoticMisc|excludedThingDefs=[Steel;Cloth]
/// NewRatkin.StockGenerator_UniqueWeapon
/// </code>
/// </para>
///
/// <para>
/// <b>不做反射遍历</b>：每个原版子类都在静态构造里用 <see cref="PocoCodec.Declare"/> 显式列出自己的字段。
/// 原版加了新类型 / 新字段时，完整性断言会先炸出来，而不是等用户存档时静默丢字段。
/// </para>
/// </summary>
internal static class StockGeneratorCodec
{
    #region 字段表

    /// <summary>
    /// 原版各生成器类型 <b>自己声明</b>的字段（继承来的由链上那一项给出），顺序即文本与表单里的顺序。
    /// <para>
    /// 特意不列 <c>StockGenerator_Techprints.tmpGenerated</c>：那是生成库存时临时收集、生成完就 <c>Clear</c>
    /// 的字段，不给用户一个"填了也没用"的输入行，也不写进存档。
    /// </para>
    /// <para>
    /// 也顺带声明字段表里用到的两个小对象类型（<see cref="CountChance"/> / <see cref="ThingDefCountRangeClass"/>），
    /// 表单与文本编解码共用同一张字段表。
    /// </para>
    /// </summary>
    static StockGeneratorCodec()
    {
        PocoCodec.Declare(typeof(StockGenerator),
            "countRange", "customCountRanges", "totalPriceRange", "maxTechLevelGenerate", "maxTechLevelBuy", "price");

        PocoCodec.Declare(typeof(StockGenerator_BuyExpensiveSimple), "minValuePerUnit");
        PocoCodec.Declare(typeof(StockGenerator_BuySingleDef), "thingDef");
        PocoCodec.Declare(typeof(StockGenerator_BuyTradeTag), "tag");
        PocoCodec.Declare(typeof(StockGenerator_BuySlaves));
        PocoCodec.Declare(typeof(StockGenerator_MiscItems));
        PocoCodec.Declare(typeof(StockGenerator_MarketValue), "tradeTag", "weaponTag", "apparelTag");
        PocoCodec.Declare(typeof(StockGenerator_ReinforcedBarrels));
        PocoCodec.Declare(typeof(StockGenerator_Tomes));
        PocoCodec.Declare(typeof(StockGenerator_Animals),
            "tradeTagsSell", "tradeTagsBuy", "createMatingPair", "kindCountRange", "minWildness", "maxWildness", "checkTemperature");
        PocoCodec.Declare(typeof(StockGenerator_Category),
            "categoryDef", "thingDefCountRange", "excludedThingDefs", "excludedCategories");
        PocoCodec.Declare(typeof(StockGenerator_MultiDef), "thingDefs");
        PocoCodec.Declare(typeof(StockGenerator_SingleDef), "thingDef");
        PocoCodec.Declare(typeof(StockGenerator_Slaves), "respectPopulationIntent", "slaveKindDef");
        PocoCodec.Declare(typeof(StockGenerator_Tag), "tradeTag", "thingDefCountRange", "excludedThingDefs");
        PocoCodec.Declare(typeof(StockGenerator_Techprints), "countChances");

        // 小对象列表的元素类型
        PocoCodec.Declare(typeof(CountChance), "count", "chance");
        PocoCodec.Declare(typeof(ThingDefCountRangeClass), "thingDef", "countRange");
    }

    /// <summary>声明了但解析不到的字段（被原版改名 / 移除），形如 <c>StockGenerator_Animals.foo</c>。测试会断言它是空的。</summary>
    public static IReadOnlyList<string> UnresolvedFields => PocoCodec.UnresolvedFields;

    /// <summary>
    /// 显式确保本类已初始化 —— 上面那些 <see cref="PocoCodec.Declare"/> 写在**静态构造**里，
    /// 而 <c>PocoCodec.IsDeclared</c> 不会触发它（反向依赖会构成静态构造环）。
    ///
    /// <para>
    /// <b>字段声明有两个来源</b>：<c>TweakData.FieldCodecs.Build()</c>（17 个 POCO 类型）与本类
    /// （<c>StockGenerator</c> 及其子类、<c>CountChance</c>、<c>ThingDefCountRangeClass</c>）。
    /// 一般调用 <c>TweakData.FieldCodecs.EnsureBuilt()</c> 即可 —— 它会把两处都拉起来；
    /// 本方法只在"明确只依赖本类声明"的场合单独用。
    /// </para>
    /// </summary>
    public static void EnsureBuilt() => _ = UnresolvedFields;

    /// <summary>表里所有"具体的"原版生成器类型（类型菜单用），按名字排序。</summary>
    public static List<Type> VanillaGeneratorTypes() =>
        PocoCodec.DeclaredTypes
            .Where(t => t != typeof(StockGenerator) && typeof(StockGenerator).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>是否原版生成器类型：判据是"在本类的字段表里"。表外的一律按非原版处理。</summary>
    public static bool IsVanilla(Type type) => PocoCodec.IsDeclared(type);

    /// <summary>类型的显示名 = 原版 XML 里 <c>Class</c> 的写法（如 <c>StockGenerator_Tag</c>），也是存档里的类型标记。</summary>
    public static string TypeLabel(Type type) => GenTypes.GetTypeNameWithoutIgnoredNamespaces(type);

    /// <summary>类型可读写的字段（基类字段在前）；表外类型返回空 —— 本工具对它们不读不写。</summary>
    public static IReadOnlyList<PocoCodec.Field> FieldsFor(Type type) => PocoCodec.FieldsFor(type);

    // ── 转发到 PocoCodec（保持既有调用点不变） ──

    public static object? ReadField(PocoCodec.Field field, object target) => PocoCodec.ReadField(field, target);

    public static bool WriteField(PocoCodec.Field field, object target, object? value) => PocoCodec.WriteField(field, target, value);

    public static string Escape(string text) => PocoCodec.Escape(text);

    public static string Unescape(string text) => PocoCodec.Unescape(text);

    /// <summary>表单文本框里的区间文本 → <see cref="IntRange"/> / <see cref="FloatRange"/>（判据与存档文本有意不同，见 <see cref="PocoCodec.TryParseRangeText"/>）。</summary>
    public static bool TryParseRangeText(bool isIntRange, string? text, out object? value)
        => PocoCodec.TryParseRangeText(isIntRange, text, out value);

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

    /// <summary>单条 → 一行文本（不含换行）。格式：<c>类型名|字段=值|字段=值</c>；非原版类型只写类型名（占位标记）。</summary>
    public static string ToItemString(StockGenerator gen)
    {
        var type = gen.GetType();

        // 非原版（其它 Mod 新增）：本工具不读取也不修改它的字段，只留类型名当占位标记；
        // 回填时按类型名在 Def 里找回那个原实例（见 MergeWithDef），所以它的数据永远不会丢。
        if (!IsVanilla(type))
        {
            if (warnedNonVanilla.Add(type))
                Log.Message($"[BalanceTweak] stockGenerators：{TypeLabel(type)} 是非原版生成器，本工具只做只读展示"
                    + "（不读不写它的字段）：存档只留类型名，回填时按类型名在 Def 里找回原实例。");
            return TypeLabel(type);
        }

        var body = PocoCodec.Encode(gen);
        return body.Length > 0 ? TypeLabel(type) + PocoCodec.RecordSep + body : TypeLabel(type);
    }

    /// <summary>单行 → 单条生成器。</summary>
    public static bool TryParse(string? line, out StockGenerator? gen)
    {
        gen = null;
        if (line.NullOrEmpty()) return false;

        // 第一段是类型名，其余各段拼回记录体交给通用解码。类型名本身也要反转义 ——
        // 名字里原则上不会有分隔符，但保持与编码端对称更安全。
        var parts = PocoCodec.SplitUnescaped(line!, PocoCodec.RecordSep);
        if (!TryResolveType(PocoCodec.Unescape(parts[0]), out var type)) return false;

        var body = string.Join(PocoCodec.RecordSep.ToString(), parts.Skip(1));
        if (!PocoCodec.TryDecode(body, type, out var value) || value is not StockGenerator generator) return false;

        gen = generator;
        return true;
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

    private static readonly HashSet<Type> warnedNonVanilla = new();
    private static Dictionary<string, Type>? vanillaByName;

    private static Dictionary<string, Type> VanillaTypesByName()
    {
        if (vanillaByName != null) return vanillaByName;
        vanillaByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in VanillaGeneratorTypes()) vanillaByName[TypeLabel(t)] = t;
        return vanillaByName;
    }

    #endregion

    #region 与 Def 合并 / 深克隆

    /// <summary>
    /// 字段 → Def：原版条目按字段表克隆成新实例（字段与 Def 永不共享实例）；<b>非原版条目不重建</b>，
    /// 而是按"类型相同、出现次序相同"从 <paramref name="defCurrent"/> 里找回那个原实例原样放回 ——
    /// 本工具从不读也不写它的字段，所以它不会因为"这个商人被改过 + 存档读档"而丢数据，位置也保持在原处。
    /// <para>
    /// 找不到同类型原实例的条目（粘贴/复制到"本来就没有这个类型"的商人？）<b>直接丢弃并告警</b>：
    /// 绝不写入一个字段全默认的实例 —— 那等于替别的 Mod 编一份它自己都没写过的数据。
    /// 全部条目都被丢弃时返回 null，调用方据此保持 Def 现状（不写回）。
    /// </para>
    /// <para>
    /// <paramref name="defCurrent"/> 里还有没被字段认领的非原版生成器（例如两次存档之间某个 Mod 给这个
    /// Def 加了生成器）一律追加在末尾，绝不丢。
    /// </para>
    /// </summary>
    public static List<StockGenerator>? MergeWithDef(List<StockGenerator>? picked, List<StockGenerator>? defCurrent)
    {
        if (picked.NullOrEmpty()) return null;

        var pool = new List<StockGenerator>();
        if (defCurrent != null)
            foreach (var gen in defCurrent)
                if (gen != null && !IsVanilla(gen.GetType())) pool.Add(gen);

        var used = new List<StockGenerator>();
        var result = new List<StockGenerator>(picked!.Count + pool.Count);
        foreach (var entry in picked)
        {
            if (entry == null) continue;
            if (IsVanilla(entry.GetType()))
            {
                var clone = Clone(entry);
                if (clone != null) result.Add(clone);
                continue;
            }

            // 非原版：找 Def 里同类型的下一个原实例（StockGenerator 不重写 Equals，Contains 即引用比较）
            var match = pool.FirstOrDefault(g => g.GetType() == entry.GetType() && !used.Contains(g));
            if (match != null)
            {
                used.Add(match);
                result.Add(match);
            }
            else
            {
                // 不匹配只可能来自"复制/粘贴到别的商人"：编辑器里改的就是本 Def 自己的实例，必然匹配。
                // 此时绝不能把 entry 放进 Def —— 它是解析类型名占位标记时 Activator 造出来的、字段全默认的
                // 实例，写进去就是替别的 Mod 编了一份它自己都没写过的数据（"绝不能把空壳塞回 Def"）。
                // 丢掉这一条并告警：让用户看到"少了一条"，而不是让游戏拿一份坏数据去生成库存。
                Log.Warning($"[BalanceTweak] stockGenerators：Def 里找不到 {TypeLabel(entry.GetType())} 的原实例"
                    + "（粘贴来的条目，或相关 Mod 变动过？），本工具不读不写它的字段、无法重建，已丢弃这一条。");
            }
        }

        foreach (var leftover in pool)
        {
            if (used.Contains(leftover)) continue;
            Log.Message($"[BalanceTweak] stockGenerators：Def 里的非原版生成器 {TypeLabel(leftover.GetType())}"
                + " 不在字段里，已原样保留（追加在末尾）。");
            result.Add(leftover);
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 克隆：按字段表复制（<b>不</b>经过文本），因此字段里的列表都换成新实例，派生自 Def 的活对象不会
    /// 被字段的编辑影响。用于 Def → 字段（避免共享）与字段 → 编辑窗口的副本（Cancel 不影响字段）。
    /// <para>
    /// 复制过程中任何一步失败都<b>不</b>交出"半份副本"（那等于静默丢字段）：直接沿用原实例并报错。
    /// </para>
    /// <para>
    /// <b>表外的非原版类型不克隆</b>：它的字段不在表里，克隆只会把它们换成默认值（同样等于丢数据）。
    /// 这类条目本工具只做只读展示、不读不写，因此沿用原实例不会带来"编辑落到 Def 上"的问题；
    /// 回填时另有 <see cref="MergeWithDef"/> 保证 Def 里那个原实例原样归位。
    /// </para>
    /// </summary>
    public static StockGenerator? Clone(StockGenerator? gen)
    {
        if (gen == null) return null;
        var type = gen.GetType();
        if (!IsVanilla(type)) return gen;

        if (PocoCodec.CloneElement(gen, type) is StockGenerator copy) return copy;

        Log.Error($"[BalanceTweak] stockGenerators：克隆 {TypeLabel(type)} 失败，已沿用原实例（该条目的字段会就地改在 Def 上）。");
        return gen;
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

    #endregion

    #region 摘要

    /// <summary>一行摘要：最多两个非默认字段值，用于列表窗口行上的副标题。非原版类型不打摘要（字段名无从翻译）。</summary>
    public static string Summarize(StockGenerator gen)
    {
        var type = gen.GetType();
        if (!IsVanilla(type)) return "";

        var baseline = PocoCodec.BaselineFor(type);
        var parts = new List<string>(2);
        foreach (var field in FieldsFor(type))
        {
            if (parts.Count >= 2) break;
            if (field.Kind == PocoCodec.Kind.Unsupported) continue;
            var value = ReadField(field, gen);
            if (PocoCodec.IsDefault(field, value, baseline)) continue;
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
            case System.Collections.IEnumerable en:
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
