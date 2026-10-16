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
/// 通用的「字段表驱动」小对象文本编解码（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3.5）。
///
/// <para>
/// 本类是从 <see cref="StockGeneratorCodec"/> 提炼出来的：那套机制（显式字段表 + 逐字段
/// <see cref="Kind"/> 分派 + 全字符转义）本来就与服务对象无关，只是此前只用在一个类型上。
/// </para>
///
/// <para>
/// <b>文本格式</b>（一行一个元素；<b>不含类型名</b> —— 类型名由调用方按需加前缀）：
/// <code>
/// countRange=1~4|tradeTag=ExoticMisc|excludedThingDefs=[Steel;Cloth]
/// </code>
/// 每个字段值写进记录前整体转义（<see cref="Escape"/> 用反斜杠转义 <c>\ | = [ ] ;</c>），
/// 列表元素再各自多转义一层 —— <b>值里出现任何分隔符都不会破坏结构</b>。
/// 这是它相对早期手写格式（<c>statDefName|value</c> 那种裸拼接）的关键优势：
/// 裸拼接在值含分隔符时必然解析错，而那种错误是静默的。
/// </para>
///
/// <para>
/// <b>省略默认值</b>：等于"新实例上的初始值"的字段不写进文本，读回来由新实例的初始值给出同样的值
/// （与原版 XML 的习惯一致）。基准实例由 <see cref="BaselineFor"/> 沿继承链找第一个可实例化的祖先。
/// </para>
///
/// <para>
/// <b>不做反射遍历</b>：每个类型用 <see cref="Declare"/> 显式列出<b>自己声明的</b>字段。
/// 未声明的类型 <see cref="FieldsFor"/> 返回空（调用方据此当作"不认识的类型"，只读透传）。
/// 原版加了新字段而没人更新声明时，完整性断言会先炸出来，而不是等用户存档时静默丢字段。
/// </para>
/// </summary>
internal static class PocoCodec
{
    #region 字段表

    /// <summary>一个字段的编解码 / 编辑方式。</summary>
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
        /// <summary>字段表里也声明了字段的小对象列表。</summary>
        ComplexList,
        /// <summary>表里不认识的类型：编解码跳过、表单提示不支持。</summary>
        Unsupported,
    }

    /// <summary>表里声明的一个字段。</summary>
    public sealed class Field
    {
        public string Name = "";
        public Kind Kind;
        /// <summary>读写句柄（public / private 字段都由 <see cref="Reflect.Field"/> 拿到）。</summary>
        public FieldInfo Info = null!;
        /// <summary><see cref="Kind.Enum"/> 的枚举类型。</summary>
        public Type? EnumType;
        /// <summary><see cref="Kind.Def"/> 的 Def 类型；<see cref="Kind.DefList"/> / <see cref="Kind.StringList"/> /
        /// <see cref="Kind.ComplexList"/> 的元素类型（重建 <c>List&lt;T&gt;</c> 时按它建，必须与字段类型一致）。</summary>
        public Type? ElementType;

        public override string ToString() => $"{Name}({Kind})";
    }

    /// <summary>
    /// 本类全部可变静态状态的互斥锁。
    ///
    /// <para>
    /// <b>为什么需要</b>：下面几张表是懒建的（<see cref="FieldsFor"/> / <see cref="BaselineFor"/> 边算边缓存），
    /// 而声明来自<b>多个</b>静态构造（<c>TweakData.FieldCodecs</c> / <c>StockGeneratorCodec</c>）。
    /// 在 xunit 这类"按类并行"的宿主里，多线程同时写同一个 <c>Dictionary</c> 会损坏内部状态，
    /// 症状是 <see cref="FieldsFor"/> 返回<b>不完整的字段表</b> ⇒ <c>Encode</c> 静默产出空串。
    /// 实测撞到过一次（105 项里红 1 项、只红一次、单跑不复现，见 Docs §3.9.3）。
    /// </para>
    /// <para>
    /// 用一把锁而不是 <c>ConcurrentDictionary</c>：这些访问都是<b>冷路径</b>（每个类型一次），
    /// 一把锁更简单也更容易确认正确；C# 的 <c>lock</c> 可重入，<see cref="FieldsFor"/> 内层再取一次也安全。
    /// </para>
    /// </summary>
    private static readonly object Gate = new();

    /// <summary>类型 → 它<b>自己声明</b>的字段（继承来的不在此列，由继承链上的那一项给出）。顺序即文本中的顺序。</summary>
    private static readonly Dictionary<Type, string[]> declared = new();

    private static readonly Dictionary<Type, List<Field>> fieldCache = new();
    private static readonly List<string> unresolvedFields = new();
    private static readonly Dictionary<Type, object?> baselines = new();
    private static readonly Dictionary<Type, Func<object, bool>> validators = new();

    /// <summary>
    /// 声明了但解析不到的字段（类型改名 / 字段移除），形如 <c>StatModifier.foo</c>。完整性断言会检查它是空的。
    /// <para>返回<b>快照</b>：调用方（多为测试）会在别的线程仍在解析时枚举它，直接给活列表会踩"集合已修改"。</para>
    /// </summary>
    public static IReadOnlyList<string> UnresolvedFields
    {
        get { lock (Gate) return unresolvedFields.ToArray(); }
    }

    /// <summary>声明一个类型<b>自己</b>的字段。<paramref name="fieldNames"/> 顺序即文本里的出现顺序。</summary>
    public static void Declare(Type type, params string[] fieldNames)
    {
        lock (Gate) declared[type] = fieldNames;
    }

    /// <summary>
    /// 注册"字段表表达不了"的业务校验（例如 <c>count >= 0</c>、<c>count &gt; 0</c> 这类<b>取值范围</b>约束）。
    /// 解码出一个实例后调用；返回 <c>false</c> ⇒ 整条丢弃。
    /// <para>
    /// 没有这个钩子就只能二选一：要么放弃校验（**开始接受以前拒绝的值**），要么把校验写进编解码 ——
    /// 后者正是这次要消掉的那类手写代码。
    /// </para>
    /// <para>
    /// <b>刻意只在解码侧生效</b>（<see cref="Encode"/> 不调它）：落盘的是<b>当前内存里的值</b>（来自 GUI / 默认值，
    /// 本应合法），忠实写出即可 —— 校验的意义在于挡<b>不可信的外部输入</b>（存档文本）。
    /// 若让 <see cref="Encode"/> 也校验，一个已非法的存量值会让<b>整个字段既不落盘也不还原</b>
    /// （读档后被 Def 原值覆盖），那是"静默丢数据"，与校验想达到的目的正好相反。
    /// 前提是输入侧已挡住非法值（例如编辑器窗口拒绝负数），否则该约束形同虚设。
    /// </para>
    /// </summary>
    public static void SetValidator(Type type, Func<object, bool> validate)
    {
        lock (Gate) validators[type] = validate;
    }

    /// <summary>该类型是否已被声明（未声明的调用方通常当作"不认识"处理）。</summary>
    public static bool IsDeclared(Type type)
    {
        lock (Gate) return declared.ContainsKey(type);
    }

    /// <summary>已声明的全部类型（完整性断言用）。返回<b>快照</b>，理由同 <see cref="UnresolvedFields"/>。</summary>
    public static IReadOnlyCollection<Type> DeclaredTypes
    {
        get { lock (Gate) return declared.Keys.ToArray(); }
    }

    /// <summary>
    /// 某类型可读写的字段 = 继承链上每个"已声明类型"声明的字段（基类字段在前）。
    /// 未声明的类型一律返回空列表。
    /// </summary>
    public static IReadOnlyList<Field> FieldsFor(Type type)
    {
        lock (Gate)
        {
            if (fieldCache.TryGetValue(type, out var cached)) return cached;

            var fields = new List<Field>();
            if (!declared.ContainsKey(type))
            {
                fieldCache[type] = fields; // 未声明：不读不写
                return fields;
            }

            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
                chain.Add(t);
            chain.Reverse(); // 基类字段在前

            foreach (var t in chain)
            {
                if (!declared.TryGetValue(t, out var names)) continue;
                foreach (var name in names)
                {
                    var info = Reflect.Field(t, name); // 找不到会打一条 Log.Error
                    if (info == null)
                    {
                        unresolvedFields.Add($"{t.Name}.{name}");
                        continue;
                    }
                    var field = new Field { Name = name, Info = info };
                    Classify(field);
                    fields.Add(field);
                }
            }
            fieldCache[type] = fields;
            return fields;
        }
    }

    /// <summary>
    /// 字段的 C# 类型 → 编解码方式。只认这里显式列出的类型：出现了不认识的类型会打一条
    /// <c>Log.Error</c> 并标成 <see cref="Kind.Unsupported"/>，由完整性断言先炸出来，
    /// 而不是等用户存档时静默丢字段。
    /// </summary>
    private static void Classify(Field field)
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
        if (type == typeof(List<string>)) { field.Kind = Kind.StringList; field.ElementType = typeof(string); return; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var element = type.GetGenericArguments()[0];
            if (typeof(Def).IsAssignableFrom(element)) { field.Kind = Kind.DefList; field.ElementType = element; return; }
            // 小对象列表：元素类型自己也必须在表里（否则不知道怎么编解码它的字段）
            if (declared.ContainsKey(element)) { field.Kind = Kind.ComplexList; field.ElementType = element; return; }
        }

        field.Kind = Kind.Unsupported;
        Log.Error($"[BalanceTweak] POCO 编解码：字段 {field.Info.DeclaringType?.Name}.{field.Name} 的类型 {type.Name}"
            + " 不在支持列表里 —— 该字段不会被读写（原版换了字段类型？需要在本表里为它加一种 Kind）。");
    }

    #endregion

    #region 编码

    /// <summary>
    /// 元素 → 一行文本（不含类型名）。等于新实例初始值的字段不写（见 <see cref="IsDefault"/>）。
    /// </summary>
    public static string Encode(object value)
    {
        var baseline = BaselineFor(value.GetType());
        var sb = new StringBuilder();
        foreach (var field in FieldsFor(value.GetType()))
        {
            if (field.Kind == Kind.Unsupported) continue;
            var fieldValue = ReadField(field, value);
            if (IsDefault(field, fieldValue, baseline)) continue;
            var text = EncodeFieldValue(field, fieldValue!);
            if (text.Length == 0) continue;
            if (sb.Length > 0) sb.Append(FieldSep);
            sb.Append(field.Name).Append(PairSep).Append(Escape(text));
        }
        return sb.ToString();
    }

    /// <summary>单个字段值 → 文本（不含最外层转义，由调用方做）。空串 = 列表为空。</summary>
    private static string EncodeFieldValue(Field field, object value)
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
            case Kind.ComplexList: return JoinList(((IEnumerable)value).Cast<object>().Select(EncodeNested));
            default: return "";
        }
    }

    /// <summary>
    /// 小对象列表的一个元素 → 记录文本（"<c>字段名=值</c>"，字段值各自转义）。
    /// 元素<b>总是</b>写全字段 —— 否则"所有字段都是默认值"的元素会编码成空串，解码时被当空元素丢掉。
    /// </summary>
    private static string EncodeNested(object value)
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
        // 注意：不能写 new StringBuilder(ListStart) —— char 会隐式转成 int，命中"容量"构造函数，
        // 结果是"空内容 + 容量 N"，开头的 '[' 会被静默丢掉（列表解码随即失败）。必须显式 Append。
        var sb = new StringBuilder().Append(ListStart);
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

    /// <summary>整数区间 → <c>min~max</c>（不变文化）。</summary>
    private static string IntRangeText(IntRange range)
        => range.min.ToString(CultureInfo.InvariantCulture) + "~" + range.max.ToString(CultureInfo.InvariantCulture);

    /// <summary>浮点区间 → <c>min~max</c>。用 "G9" 而不是默认的 "G7"，保证不丢精度。</summary>
    private static string FloatRangeText(FloatRange range)
        => range.min.ToString("G9", CultureInfo.InvariantCulture) + "~" + range.max.ToString("G9", CultureInfo.InvariantCulture);

    /// <summary>
    /// 字段值是否等于"新实例上的初始值"：是则不写进文本，读回来由新实例的初始值给出同样的值。
    /// 公开是为了让调用方（如摘要显示）用同一判据。
    /// </summary>
    public static bool IsDefault(Field field, object? value, object? baseline)
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
    /// "新实例上的初始值"基准。抽象类型取不到实例，就往上找第一个能实例化的祖先 ——
    /// 字段初始值写在声明它的类里，取哪个具体子类都一样。
    /// </summary>
    public static object? BaselineFor(Type type)
    {
        lock (Gate)
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
    }

    #endregion

    #region 解析

    /// <summary>
    /// 一行文本 → 该类型的新实例。任一步失败返回 <c>false</c>（调用方据此丢弃整条）。
    /// <para>
    /// <b>不要求类型已声明</b>：未声明的类型 <see cref="FieldsFor"/> 返回空字段表，于是只能解出
    /// "字段全默认的实例" —— 这正是多态列表所需要的语义（非原版子类只留类型名当占位标记，
    /// 字段由 <see cref="StockGeneratorCodec.MergeWithDef"/> 从 Def 找回原实例）。
    /// 反过来，若这里拒绝未声明类型，那些条目会被整条丢弃。
    /// </para>
    /// </summary>
    public static bool TryDecode(string text, Type type, out object? value)
    {
        value = null;
        if (!TryCreateInstance(type, out var instance) || instance == null) return false;
        if (!FillFromRecord(text, instance, text)) return false;

        // 查表在锁内、调用在锁外：validate 是外部传进来的委托，不该在持锁时执行。
        Func<object, bool>? validate;
        lock (Gate) validators.TryGetValue(type, out validate);
        if (validate != null && !validate(instance))
        {
            Log.Warning($"[BalanceTweak] POCO 编解码：{type.Name} 的值不满足业务约束，放弃整条：{text}");
            return false;
        }
        value = instance;
        return true;
    }

    /// <summary>
    /// 列表 → 多行文本（一行一个元素）。<c>null</c> / 空列表返回 <c>null</c>。
    /// <para>
    /// 元素"字段全默认"时 <see cref="Encode"/> 会给出空串 —— 那在文本里就是一个空行，而
    /// <see cref="DecodeList"/> 会**跳过空行**，于是列表**静默少一条**。所以这里遇到空编码结果
    /// 直接让整个列表失败（返回 <c>null</c>）：调用方会报"复制失败"，是**可见**的失败，
    /// 而不是静默丢数据。
    /// </para>
    /// <para>
    /// <b>这是相对旧手写编码器的行为变化</b>：旧的逐类型编码器用哨兵值保证任何元素都产出一行
    /// 非空文本（如 <c>$"{sm.stat?.defName ?? "NULL"}|{sm.value}"</c>），所以"整块失败"不会发生。
    /// 改成字段表驱动后，"等于新实例初始值就不写"是核心优化（见 <see cref="IsDefault"/>），
    /// 于是"全默认 ⇒ 空文本"成了必然，必须在这里显式兜住。
    /// </para>
    /// </summary>
    public static string? EncodeList(IEnumerable? list)
    {
        if (list == null) return null;
        var lines = new List<string>();
        foreach (var item in list)
        {
            if (item == null) return null;
            var text = Encode(item);
            if (text.Length == 0) return null;
            lines.Add(text);
        }
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    /// <summary>多行文本 → 列表。任一行解析失败 ⇒ 返回 <c>null</c>（调用方据此保持原值，绝不写入半截数据）。</summary>
    public static List<T>? DecodeList<T>(string? data) where T : class
        => DecodeList(typeof(T), data) as List<T>;

    /// <summary>
    /// 同 <see cref="DecodeList{T}"/>，但元素类型在运行期给出 —— 供"手里只有字段、拿不到泛型参数"
    /// 的调用点使用（如 GUI 剪贴板按字段类型分派）。返回实例的真实类型是 <c>List&lt;elementType&gt;</c>。
    ///
    /// <para>
    /// <b>元素类型未声明 ⇒ 一律返回 <c>null</c></b>，而不是"按空字段表解出一串默认值"。
    /// 后者会静默产出"全 0 的列表"这类垃圾（<c>int</c> 的默认值是 0、且 <c>TryCreateInstance</c> 会成功），
    /// 调用方拿它覆盖字段就毁了数据。非原版类型走空字段表那条路是 <see cref="StockGeneratorCodec"/>
    /// 专有的（它有自己的类型标记），不经过这里。
    /// </para>
    /// </summary>
    public static object? DecodeList(Type elementType, string? data)
    {
        if (data.NullOrEmpty()) return null;
        if (!IsDeclared(elementType)) return null;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var raw in data!.Split('\n'))
        {
            var line = raw.Trim();
            if (line.NullOrEmpty()) continue;
            if (!TryDecode(line, elementType, out var value) || value == null) return null;
            list.Add(value);
        }
        return list.Count > 0 ? list : null;
    }

    /// <summary>
    /// 把"字段名=值"记录填进已有实例（<see cref="TryDecode"/> 与 <see cref="Kind.ComplexList"/> 的元素共用）。
    /// <paramref name="wholeLine"/> 只用于失败时的日志，让用户看得到整条原文。
    /// </summary>
    private static bool FillFromRecord(string record, object target, string wholeLine)
    {
        var fields = FieldsFor(target.GetType());
        foreach (var part in SplitUnescaped(record, FieldSep))
        {
            if (part.Length == 0) continue;
            var pair = SplitUnescaped(part, PairSep);
            if (pair.Count != 2)
            {
                Log.Warning($"[BalanceTweak] POCO 编解码：\"{part}\" 不是“字段名=值”，放弃整条：{wholeLine}");
                return false;
            }
            var name = Unescape(pair[0]);
            var field = fields.FirstOrDefault(f => f.Name == name);
            if (field == null)
            {
                Log.Warning($"[BalanceTweak] POCO 编解码：{target.GetType().Name} 没有字段 {name}"
                    + $"（存档来自不同版本？），放弃整条：{wholeLine}");
                return false;
            }
            if (field.Kind == Kind.Unsupported) continue; // Classify 已报过，不当作损坏
            if (!TryDecodeFieldValue(field, Unescape(pair[1]), out var fieldValue)) return false;
            if (!WriteField(field, target, fieldValue)) return false;
        }
        return true;
    }

    private static bool TryDecodeFieldValue(Field field, string text, out object? value)
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

    private static bool Fail(Field field, string text, string expected)
    {
        Log.Warning($"[BalanceTweak] POCO 编解码：字段 {field.Name} 的值 \"{text}\" 不是合法内容（期望 {expected}）。");
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

    /// <summary>
    /// <b>表单文本框</b>里的区间文本 → <see cref="IntRange"/> / <see cref="FloatRange"/>（失败只返回 false，
    /// 不写日志：表单是逐键调用的，报错会刷屏）。判据与存档文本<b>有意不同</b>：这里<b>允许只写一个数</b>
    /// （= min~max 同值），因为 <c>IntRange.ToString()</c> 在 min==max 时只显示一个数，用户直接改那个数要能通过。
    /// <para>
    /// <b>"写了一半"一律拒绝</b>：<c>"1~"</c> / <c>"~4"</c> / <c>"1~2~3"</c>。绝不能退回
    /// <c>IntRange.FromString</c> 那种"靠抛异常判非法"的写法 —— <c>IntRange.FromString("1~")</c>
    /// <b>不抛异常</b>，它把缺省上界当 <c>int.MaxValue</c>，"输到一半就点走"会写出一个天文数字区间。
    /// </para>
    /// <para>
    /// 用 <see cref="NumberStyles.Float"/>（不含千位分隔符）而不是裸 <c>float.TryParse</c>：
    /// 后者会把 <c>"1.5"</c> 在逗号小数点环境下读成 15。
    /// </para>
    /// </summary>
    public static bool TryParseRangeText(bool isIntRange, string? text, out object? value)
    {
        value = null;
        if (text.NullOrEmpty()) return false;

        var parts = text!.Split('~');
        if (parts.Length > 2 || parts[0].Length == 0) return false;
        var minText = parts[0];
        var maxText = parts.Length == 2 ? parts[1] : minText;
        if (maxText.Length == 0) return false;

        if (isIntRange)
        {
            if (!int.TryParse(minText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int min)
                || !int.TryParse(maxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int max))
                return false;
            value = new IntRange(min, max);
            return true;
        }

        if (!float.TryParse(minText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fmin)
            || !float.TryParse(maxText, NumberStyles.Float, CultureInfo.InvariantCulture, out float fmax))
            return false;
        value = new FloatRange(fmin, fmax);
        return true;
    }

    private static bool TryCreateInstance(Type type, out object? instance)
    {
        instance = null;
        try { instance = Activator.CreateInstance(type); }
        catch (Exception e)
        {
            Log.Warning($"[BalanceTweak] POCO 编解码：无法创建 {type.FullName}：{e.Message}");
            return false;
        }
        if (instance == null)
        {
            Log.Warning($"[BalanceTweak] POCO 编解码：{type.FullName} 无法实例化。");
            return false;
        }
        return true;
    }

    #endregion

    #region 字段读写

    /// <summary>读字段值；失败只告警并返回 null。</summary>
    public static object? ReadField(Field field, object target)
    {
        try { return field.Info.GetValue(target); }
        catch (Exception e)
        {
            Log.Warning($"[BalanceTweak] POCO 编解码：读取 {target.GetType().Name}.{field.Name} 失败：{e.Message}");
            return null;
        }
    }

    /// <summary>写字段值；失败告警并返回 false（值类型要传装箱对象，<see cref="FieldInfo.SetValue"/> 会就地改写这个箱子）。</summary>
    public static bool WriteField(Field field, object target, object? value)
    {
        try { field.Info.SetValue(target, value); return true; }
        catch (Exception e)
        {
            Log.Error($"[BalanceTweak] POCO 编解码：写入 {target.GetType().Name}.{field.Name}（{field.Kind}）失败：{e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 按字段表深拷贝一个元素。语义与 Def→字段 的复制要求一致：
    /// <b>列表换成新实例、元素按不可变共享，<c>null</c> 不覆盖目标实例的初始值</b>。
    /// </summary>
    public static object? CloneElement(object source, Type type)
    {
        if (!TryCreateInstance(type, out var copy) || copy == null) return null;
        foreach (var field in FieldsFor(type))
        {
            if (field.Kind == Kind.Unsupported) continue;
            var value = ReadField(field, source);
            if (value == null) continue; // 源为 null ⇒ 保持目标实例的初始值，不覆盖
            if (!TryCloneValue(field, value, out var cloned)) return null;
            if (!WriteField(field, copy, cloned)) return null;
        }
        return copy;
    }

    /// <summary>
    /// 按字段值的语义拷一份。<b>失败由返回值表达，不用 <c>null</c> 兼职</b> ——
    /// <c>null</c> 本身是合法字段值，"值是 null"与"拷不出来"混在一起时，
    /// <c>ComplexList</c> 里某个元素拷失败会被写成 <c>null</c>，即**静默改掉数据**。
    /// </summary>
    private static bool TryCloneValue(Field field, object value, out object? cloned)
    {
        switch (field.Kind)
        {
            case Kind.Def:
                cloned = value; // Def 是单例，按引用共享
                return true;
            case Kind.DefList:
            case Kind.StringList:
            {
                // 元素不可变可共享，但列表本身必须新建 —— 否则"编辑副本"会落回 Def。
                var elementType = field.ElementType!;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                foreach (var item in (IEnumerable)value) list.Add(item);
                cloned = list;
                return true;
            }
            case Kind.ComplexList:
            {
                var elementType = field.ElementType!;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                foreach (var item in (IEnumerable)value)
                {
                    if (item == null) continue;
                    var copy = CloneElement(item, elementType);
                    if (copy == null) { cloned = null; return false; } // 元素拷不出来 ⇒ 整个值拷不出来
                    list.Add(copy);
                }
                cloned = list;
                return true;
            }
            default:
                cloned = value; // 标量 / 区间：值语义，直接复制
                return true;
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
    public static List<string> SplitUnescaped(string text, char delim)
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

    /// <summary>本类使用的字段分隔符（调用方拼"类型名 + 本分隔符 + 元素文本"时用）。</summary>
    public const char RecordSep = FieldSep;

    #endregion
}
