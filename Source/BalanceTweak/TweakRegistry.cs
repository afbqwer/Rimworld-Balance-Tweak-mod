using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using Verse;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak;

/// <summary>
/// 声明"这个 TweakData 子类负责哪一类 Def、显示在哪个页签、以什么优先级参与分类"。
///
/// 取代原先两处手写的分类逻辑（TweakDatabase.Init() 的 if/else-if 链、
/// TweakData.GetData(ThingDef) 的探测链）以及 Apply() 的 switch 表达式。
/// 新增一个可编辑类型时，只需给自己的 Data 类加这个特性，不必再改 TweakDatabase。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class TweakForAttribute(Type defType, SettingType tab) : Attribute
{
    /// <summary>目标 Def 类型。跨 Mod 类型改用 TypeName 指定。</summary>
    public Type DefType { get; } = defType;

    /// <summary>归属页签（SettingType 枚举值），也就是这条数据在 UI 里的分类。</summary>
    public SettingType Tab { get; } = tab;

    /// <summary>优先级，大者先匹配。ThingDef 的"谁先抢到"由它决定（顺序即原 if/else-if 链的顺序）。</summary>
    public int Priority { get; set; }

    /// <summary>
    /// 匹配方法名，须为 <c>public static bool M(Def)</c>。
    /// 省略表示"该 Def 类型全部适用"（如 WeatherDef）。
    /// 沿用 StatFieldMeta 里 Available / GetAbstract 的字符串反射约定。
    /// 名字不用 Match：Attribute.Match(object) 已存在，会触发 CS0108。
    /// </summary>
    public string? MatchMethod { get; set; }

    /// <summary>兜底规则：只在同 Def 类型下没有任何非兜底规则命中时生效。</summary>
    public bool IsFallback { get; set; }

    /// <summary>
    /// 是否独占。true（默认）表示命中后不再附加同 Def 类型下的其它规则 —— 与原
    /// if/else-if 链语义一致；false 表示允许"一个 Def 同时挂多个 Data"（并列挂载）。
    /// </summary>
    public bool Exclusive { get; set; } = true;

    /// <summary>跨 Mod 类型名（反射获取），填写时优先于 DefType。</summary>
    public string? TypeName { get; set; }

    /// <summary>需要的 Mod packageId，与 MayRequire 同义。不满足时整条规则被跳过。</summary>
    public string? MayRequire { get; set; }
}

/// <summary>规则的运行时形态。</summary>
public sealed class TweakRule
{
    public Type DataType = null!;
    public Type DefType = null!;
    public Type DeclaredDefType = null!;
    public SettingType Tab;
    public int Priority;
    public bool IsFallback;
    public bool Exclusive;
    public string? MayRequire;
    public string? MatchName;

    /// <summary>匹配委托；null 表示该 Def 类型全部适用。</summary>
    public Func<Def, bool>? Matcher;

    /// <summary>Data 实例工厂（表达式树编译，避免 Activator 开销）。</summary>
    public Func<TweakData> Factory = null!;

    private bool? _available;

    /// <summary>所需 Mod 是否就绪。延迟求值：首次使用时才读 ModsConfig。</summary>
    public bool IsAvailable => _available ??= DataUtility.MayRequire(MayRequire);

    public override string ToString() => $"{DataType.Name}->{DefType.Name}:{Tab}";
}

/// <summary>
/// 规则注册表：启动时反射扫描全部 TweakData 子类的 [TweakFor] 特性，
/// 并统一维护"defName → Def"的查询表，替代原先散落的一堆 allXxxdef 静态字典。
/// </summary>
public static class TweakRegistry
{
    // 注意：必须用显式静态构造函数控制初始化顺序。
    // 若写成 `rules = Build()`，Build() 会在 buildErrors 字段初始化之前执行，
    // 导致它引用到尚未构造的 null 列表而抛 NullReferenceException（即本题崩溃）。
    private static readonly List<TweakRule> rules;
    private static readonly Dictionary<Type, List<TweakRule>> byDataType = new();
    private static readonly Dictionary<Type, Dictionary<string, Def>> defDicts = new();
    private static readonly List<string> buildErrors;

    static TweakRegistry()
    {
        var errors = new List<string>();
        rules = Build(errors);
        buildErrors = errors;
    }

    /// <summary>全部规则（含 MayRequire 不满足的，用前请查 IsAvailable）。</summary>
    public static IReadOnlyList<TweakRule> Rules => rules;

    /// <summary>注册期发现的问题（缺 Match 方法、找不到跨 Mod 类型等），供启动日志输出。</summary>
    public static IReadOnlyList<string> BuildErrors => buildErrors;

    #region 注册

    private static List<TweakRule> Build(List<string> buildErrors)
    {
        var list = new List<TweakRule>();
        Type[] types;
        try
        {
            types = typeof(TweakData).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).Cast<Type>().ToArray();
            Log.Warning($"[BalanceTweak] TweakRegistry 反射加载类型时部分类型不可用：{e.Message}");
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(TweakData).IsAssignableFrom(type)) continue;

            var attrs = type.GetCustomAttributes<TweakForAttribute>().ToList();
            if (attrs.Count == 0) continue;

            foreach (var attr in attrs)
            {
                var rule = TryCreateRule(type, attr);
                if (rule != null) list.Add(rule);
            }
        }

        Log.Message($"[BalanceTweak] TweakRegistry：注册 {list.Count} 条规则，覆盖 {list.Select(r => r.DefType).Distinct().Count()} 种 Def 类型。");
        foreach (var err in buildErrors)
            Log.Error($"[BalanceTweak] TweakRegistry 注册失败：{err}");

        return list;
    }

    private static TweakRule? TryCreateRule(Type dataType, TweakForAttribute attr)
    {
        // 跨 Mod 类型：用反射拿 Type，拿不到就跳过（不算错误）
        Type? defType;
        if (!attr.TypeName.NullOrEmpty())
        {
            defType = AccessTools.TypeByName(attr.TypeName!);
            if (defType == null)
                return null;
        }
        else
        {
            defType = attr.DefType;
        }

        var rule = new TweakRule
        {
            DataType = dataType,
            DefType = defType,
            DeclaredDefType = attr.DefType,
            Tab = attr.Tab,
            Priority = attr.Priority,
            IsFallback = attr.IsFallback,
            Exclusive = attr.Exclusive,
            MayRequire = attr.MayRequire,
            MatchName = attr.MatchMethod,
        };

        try
        {
            rule.Factory = Expression.Lambda<Func<TweakData>>(Expression.New(dataType)).Compile();
        }
        catch (Exception e)
        {
            buildErrors.Add($"{dataType.Name} 缺少无参构造函数：{e.Message}");
            return null;
        }

        if (!attr.MatchMethod.NullOrEmpty())
        {
            // 先在本类里找，找不到再退回 DataUtility（与 StatFieldMeta 处理 Available 的约定一致）。
            // 这样多个类型共用的 Def 形状判定（如 ThingDef 的各种判定）可以集中放在一处，避免重复实现再次漂移。
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var method = dataType.GetMethod(attr.MatchMethod!, flags)
                ?? typeof(DataUtility).GetMethod(attr.MatchMethod!, flags);
            if (method == null || method.ReturnType != typeof(bool))
            {
                buildErrors.Add($"{dataType.Name} 找不到 public static bool {attr.MatchMethod}(Def)（本类与 DataUtility 均无）");
                return null;
            }
            try
            {
                rule.Matcher = (Func<Def, bool>)Delegate.CreateDelegate(typeof(Func<Def, bool>), method);
            }
            catch (Exception e)
            {
                buildErrors.Add($"{dataType.Name}.{attr.MatchMethod} 签名不匹配（应为 static bool M(Def)）：{e.Message}");
                return null;
            }
        }

        return rule;
    }

    #endregion

    #region 查询

    /// <summary>
    /// 取某个 Data 类型适用的规则（按优先级降序）。会沿继承链向上找，
    /// 以兼容"为旧存档保留的空壳子类"（见 Docs/ThingDefCoverage_Design.md §10.4）。
    /// </summary>
    public static List<TweakRule> RulesOf(Type dataType)
    {
        if (byDataType.TryGetValue(dataType, out var cached)) return cached;

        var result = new List<TweakRule>();
        for (var t = dataType; t != null && t != typeof(TweakData); t = t.BaseType)
        {
            var found = rules.Where(r => r.DataType == t).OrderByDescending(r => r.Priority).ToList();
            if (found.Count > 0) { result = found; break; }
        }
        byDataType[dataType] = result;
        return result;
    }

    /// <summary>
    /// defName → Def 查询表（按 DefType 缓存）。
    /// 语义与原 <c>DefDatabase&lt;T&gt;.AllDefs.ToDictionary(d =&gt; d.defName)</c> 一致，
    /// 但同名时用后者覆盖前者而不是抛异常。
    /// </summary>
    public static Dictionary<string, Def> DefsOf(Type defType)
    {
        if (defDicts.TryGetValue(defType, out var cached)) return cached;

        var dict = new Dictionary<string, Def>();
        try
        {
            var prop = typeof(DefDatabase<>).MakeGenericType(defType)
                .GetProperty("AllDefs", BindingFlags.Public | BindingFlags.Static);
            if (prop?.GetValue(null) is IEnumerable all)
            {
                foreach (var item in all)
                {
                    if (item is Def def) dict[def.defName] = def;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"[BalanceTweak] 读取 DefDatabase<{defType.Name}>.AllDefs 失败：{e}");
        }

        defDicts[defType] = dict;
        return dict;
    }

    /// <summary>按规则解析目标 Def。</summary>
    public static bool TryResolve(TweakRule rule, string defName, out Def? def)
    {
        if (DefsOf(rule.DefType).TryGetValue(defName, out var found))
        {
            def = found;
            return true;
        }
        def = null;
        return false;
    }

    /// <summary>清空 Def 查询缓存（仅测试/重载用）。</summary>
    public static void ClearDefCache() => defDicts.Clear();

    #endregion
}
