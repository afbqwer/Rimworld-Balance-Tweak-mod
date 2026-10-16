#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// SC11 —— 全字段 Scribe 往返自检（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3.1 / §3.2）。
///
/// <b>为什么需要它</b>：SC1–SC9 全部只读运行中的 Def，**没有一条**验证"字段真的被写进存档、
/// 又能读回来"。于是两条链（<c>TryResolveField</c> / <c>ExposeField</c>）里漏登记一个字段类型时，
/// 后果是静默的 —— 改完重开就没了，或字段根本没进存档。
///
/// <b>做法</b>：给字段灌一个**非默认**样本值 → 跑一次真实的 Scribe 存/读往返 → 比对。
/// 漏登记 ⇒ 值丢失 ⇒ 当场报错。分两条路径：
/// <list type="bullet">
/// <item><b>SC11a —— 手工样本 + 值比较</b>：不需要 <c>DefDatabase</c> 的类型
/// （<c>float?</c>/<c>int?</c>/<c>bool?</c>/<c>string</c>/nullable enum/<c>FloatRange?</c>/<c>IntRange?</c>/
/// <c>List&lt;string&gt;</c>/<c>List&lt;int&gt;</c>）。同类字段合并成一次往返，逐字段比较值。</item>
/// <item><b>SC11b —— 真实数据样本 + 文本比较</b>：其余类型（<c>Def</c>/<c>List&lt;Def&gt;</c>/
/// <c>ThingFilter</c>/各 POCO 列表/<c>SimpleCurve</c>）。样本取自 <see cref="TweakDatabase.AllOriginals"/>
/// 里该字段的第一个非空值（<c>Def</c> 类型另有 DefDatabase 兜底）。
/// 比较用的是<b>两次存盘文本是否一致</b>，而不是对象相等 —— 因为 POCO 元素
/// （<c>StatModifier</c> 等）没有重写 <c>Equals</c>，对象比较会误报。因此这类字段逐字段单独往返。</item>
/// </list>
///
/// <b>口径铁律</b>：字段枚举条件必须与 <c>TweakData&lt;T&gt;.InitStaticFields</c> 严格一致 ——
/// 即 <c>attr != null &amp;&amp; DataUtility.MayRequire(attr.MayRequire)</c>，再按 <c>DataType</c> 过滤。
/// 首版漏掉 <c>MayRequire</c>，把"不在字段表内、<c>ExposeData</c> 永远不碰"的字段也拿去往返，
/// 报了 8 条必然失败的假阳性（见 §3.2）。
///
/// <b>本类不做的事</b>：不写游戏状态、不改 Def、不碰用户存档（临时文件用完即删）。
/// </summary>
public static class TweakFieldScribeRoundTripTests
{
    private const string Tag = "[BT/SC11]";
    private const string RootNode = "sc11";
    private const string DataNode = "data";
    private const int MaxReport = 15;

    /// <summary>
    /// 由 <see cref="TweakDataSerializationTests.RunSerializationTests"/> 调用 —— 不自己开 long event，
    /// 免得两个入口的执行顺序不确定。失败即抛异常，交由调用方的 RunTest 记账。
    /// </summary>
    public static void Run()
    {
        IReadOnlyList<Type> dataTypes;
        try
        {
            dataTypes = TweakData.AllConcreteDataTypes;
        }
        catch (Exception e)
        {
            Log.Error($"{Tag} 枚举 TweakData 子类失败：{e}");
            return;
        }

        var problems = new List<string>();
        var noSamples = new List<(string Field, string Type)>();
        int coveredTypes = 0, fieldsA = 0, fieldsB = 0, totalFields = 0, skippedMayRequire = 0;

        foreach (var dataType in dataTypes)
        {
            List<(FieldInfo Field, object? Sample)> manualSamples, dataSamples;
            List<(string Field, string Type)> fieldNoSample;
            int skippedMay;
            try
            {
                ClassifyFields(dataType, out manualSamples, out dataSamples, out fieldNoSample, out skippedMay);
                totalFields += manualSamples.Count + dataSamples.Count + fieldNoSample.Count;
                skippedMayRequire += skippedMay;
                noSamples.AddRange(fieldNoSample);
            }
            catch (Exception e)
            {
                problems.Add($"{dataType.Name}: 枚举字段失败 —— {e.Message}");
                continue;
            }

            if (manualSamples.Count > 0 || dataSamples.Count > 0) coveredTypes++;
            fieldsA += manualSamples.Count;
            fieldsB += dataSamples.Count;

            RunBatchValueCheck(dataType, manualSamples, problems);
            RunPerFieldTextCheck(dataType, dataSamples, problems);
        }

        Log.Message($"{Tag} SC11 覆盖 {fieldsA + fieldsB} 个字段"
                  + $"（SC11a 手工样本 {fieldsA} / SC11b 真实数据样本 {fieldsB}；"
                  + $"同口径字段总数 {totalFields}，其中 {noSamples.Count} 个取不到非空样本、未测；"
                  + $"另 {skippedMayRequire} 个因 MayRequire 未满足不在字段表内；"
                  + $"涉及 {coveredTypes} 个类型）。");

        // 未测字段必须列出来 —— 否则"覆盖到 100%"就是句空话。按类型分组，
        // 成片的（某个类型重复出现）说明缺构造器、值得补；散落的说明是罕见字段，可以先放着。
        if (noSamples.Count > 0)
        {
            var byType = noSamples.GroupBy(x => x.Type).OrderByDescending(g => g.Count()).ToList();
            Log.Message($"{Tag} 未测字段 {noSamples.Count} 个，按字段类型分布（前 {MaxReport}）：\n  "
                      + string.Join("\n  ", byType.Take(MaxReport).Select(g => $"{g.Count(),3} × {g.Key}")));
        }

        if (problems.Count == 0)
        {
            Log.Message($"{Tag} SC11 全部通过（{fieldsA + fieldsB} 个字段往返一致）。");
            return;
        }

        Log.Error($"{Tag} SC11 失败 {problems.Count} 项（列出前 {MaxReport} 条）：\n"
                + string.Join("\n", problems.Take(MaxReport))
                + "\n若某类型反复失败，检查 TweakData.Serialization.cs 的 ExposeField / "
                + "TweakData.Resolution.cs 的 TryResolveField 是否漏了该字段类型。");
        throw new Exception($"SC11 有 {problems.Count} 项字段往返不一致（详见上方 {Tag} 错误日志）");
    }

    // ─────────────────────────── 路径 A：手工样本 + 值比较（SC11a） ───────────────────────────

    /// <summary>同类字段合并成一次往返，然后逐字段比较值。省 IO，且失败信息精确到字段。</summary>
    private static void RunBatchValueCheck(
        Type dataType, List<(FieldInfo Field, object? Sample)> suspects, List<string> problems)
    {
        if (suspects.Count == 0) return;

        TweakData source;
        try { source = (TweakData)Activator.CreateInstance(dataType)!; }
        catch (Exception e) { problems.Add($"{dataType.Name}: 无法实例化 —— {e.Message}"); return; }

        // 灌入非默认样本值并标记为"已改"——否则 ExposeData 会跳过未 modify 的字段，测了个空。
        foreach (var (field, sample) in suspects)
        {
            field.SetValue(source, sample);
            source.MarkFieldModified(field.Name);
        }

        TweakData target;
        try { target = ScribeRoundTrip(source, dataType); }
        catch (Exception e) { problems.Add($"{dataType.Name}: Scribe 往返抛异常 —— {e.Message}"); return; }

        foreach (var (field, sample) in suspects)
        {
            object? got;
            try { got = field.GetValue(target); }
            catch (Exception e) { problems.Add($"{dataType.Name}.{field.Name}: 读回失败 —— {e.Message}"); continue; }

            if (!ValuesEqual(sample, got))
                problems.Add($"{dataType.Name}.{field.Name}（{field.FieldType.Name}）："
                           + $"样本 {Describe(sample)} → 读回 {Describe(got)}");
        }
    }

    // ─────────────────────────── 路径 B：真实数据样本 + 文本比较（SC11b） ───────────────────────────

    /// <summary>逐字段单独往返，比较两次存盘文本。POCO 元素没有可用的值比较器，只能看文本。</summary>
    private static void RunPerFieldTextCheck(
        Type dataType, List<(FieldInfo Field, object? Sample)> suspects, List<string> problems)
    {
        foreach (var (field, sample) in suspects)
        {
            try
            {
                if (!FieldRoundTripsByText(dataType, field, sample, out var detail))
                    problems.Add($"{dataType.Name}.{field.Name}（{field.FieldType.Name}）："
                               + $"往返后存盘文本不一致{detail}");
            }
            catch (Exception e)
            {
                problems.Add($"{dataType.Name}.{field.Name}: 文本往返抛异常 —— {e.Message}");
            }
        }
    }

    /// <summary>
    /// 单字段往返：存盘 → 读回 → 再存盘，两次文本必须逐字节相同。
    /// <para>
    /// 为什么不用对象比较：SC11b 覆盖的 POCO（<c>StatModifier</c>/<c>ThingDefCountClass</c> 等）
    /// 没有重写 <c>Equals</c>，读回的是新实例，<c>Equals</c> 必然为 false ⇒ 假阳性。
    /// 文本比较绕开了"需要给每种类型写一份深比较"的负担，且更严格（连编码顺序不稳定也能抓到）。
    /// </para>
    /// </summary>
    private static bool FieldRoundTripsByText(Type dataType, FieldInfo field, object? sample, out string detail)
    {
        detail = "";
        string path = TempFilePath();
        try
        {
            var source = (TweakData)Activator.CreateInstance(dataType)!;
            field.SetValue(source, sample);
            source.MarkFieldModified(field.Name);

            string before = SaveToFile(path, source);
            var target = LoadFromFile(path, dataType);
            string after = SaveToFile(path, target);

            if (string.Equals(before, after, StringComparison.Ordinal)) return true;
            detail = "\n" + FirstDiff(before, after);
            return false;
        }
        finally
        {
            ResetScribeIfStuck();
            SafeDelete(path);
        }
    }

    private static string FirstDiff(string before, string after)
    {
        var b = before.Split('\n');
        var a = after.Split('\n');
        int n = Math.Min(b.Length, a.Length);
        for (int i = 0; i < n; i++)
        {
            if (string.Equals(b[i], a[i], StringComparison.Ordinal)) continue;
            return $"  首个差异在第 {i + 1} 行：\n    往返前: {b[i].Trim()}\n    往返后: {a[i].Trim()}";
        }
        return $"  行数不同：往返前 {b.Length} 行、往返后 {a.Length} 行";
    }

    // ─────────────────────────── 字段分类与样本值 ───────────────────────────

    /// <summary>
    /// 按 <c>InitStaticFields</c> 的口径取字段，再按"能造什么样本"分成两条路径。
    /// 两条都取不到样本的字段进入 <paramref name="noSample"/>（不静默 —— 覆盖率要能被审查）。
    /// <paramref name="skippedByMayRequire"/> 是被 <c>MayRequire</c> 挡掉的字段数。
    /// </summary>
    private static void ClassifyFields(
        Type dataType,
        out List<(FieldInfo Field, object? Sample)> manualSamples,
        out List<(FieldInfo Field, object? Sample)> dataSamples,
        out List<(string Field, string Type)> noSample,
        out int skippedByMayRequire)
    {
        manualSamples = new List<(FieldInfo, object?)>();
        dataSamples = new List<(FieldInfo, object?)>();
        noSample = new List<(string, string)>();
        skippedByMayRequire = 0;

        foreach (var field in dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = field.GetCustomAttribute<TweakFieldAttribute>();
            if (attr == null || attr.DataType != ColumnDataType.Field) continue;

            // 与 InitStaticFields 同一口径：MayRequire 未满足 ⇒ 不进 Fields ⇒ 不参与序列化。
            if (!DataUtility.MayRequire(attr.MayRequire)) { skippedByMayRequire++; continue; }

            if (TrySampleValue(field, out var manual)) { manualSamples.Add((field, manual)); continue; }
            if (TrySampleFromData(dataType, field, out var fromData)) { dataSamples.Add((field, fromData)); continue; }
            noSample.Add(($"{dataType.Name}.{field.Name}", field.FieldType.Name));
        }
    }

    /// <summary>
    /// SC11a 的样本：**手工**造非默认值，只覆盖不依赖 <c>DefDatabase</c> 的类型。
    /// 默认值与样本值相同 ⇒ 漏登记测不出来（见类注释），故每个类型都必须给非默认值。
    /// </summary>
    private static bool TrySampleValue(FieldInfo field, out object? sample)
    {
        var t = field.FieldType;

        if (t == typeof(float?)) { sample = 7.25f; return true; }
        if (t == typeof(int?)) { sample = 4242; return true; }
        if (t == typeof(bool?)) { sample = true; return true; }
        if (t == typeof(FloatRange?)) { sample = new FloatRange(1.5f, 9.75f); return true; }
        if (t == typeof(IntRange?)) { sample = new IntRange(2, 5); return true; }
        // 含分隔符与转义字符 —— 顺带覆盖各 codec 的转义路径
        if (t == typeof(string)) { sample = "SC11|样本=值;[x]\\y"; return true; }
        if (t == typeof(List<string>)) { sample = new List<string> { "a|x", "b;[c]" }; return true; }
        if (t == typeof(List<int>)) { sample = new List<int> { 3, 5, 8 }; return true; }

        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is { IsEnum: true })
        {
            // 可空枚举的默认值是 null，故任一枚举值都非默认 —— 取第一个即可。
            foreach (var value in Enum.GetValues(underlying))
            {
                sample = value;
                return true;
            }
        }

        sample = null;
        return false;
    }

    /// <summary>
    /// SC11b 的样本：**取自真实数据**（<see cref="TweakDatabase.AllOriginals"/> 里该字段的第一个非空值）。
    /// 好处是不必为每种 POCO 写构造器，且样本本身就是真实形态。
    /// <c>Def</c> / <c>List&lt;Def&gt;</c> 另加 DefDatabase 兜底 —— 真实数据里可能恰好都没设过。
    /// 取不到样本的字段会被跳过并计入报告（不静默）。
    /// </summary>
    private static bool TrySampleFromData(Type dataType, FieldInfo field, out object? sample)
    {
        sample = null;

        foreach (var data in TweakDatabase.AllOriginals.Values)
        {
            if (data == null || data.GetType() != dataType) continue;
            var value = field.GetValue(data);
            if (!TweakData.IsEmptyValue(value)) { sample = value; return true; }
        }

        if (typeof(Def).IsAssignableFrom(field.FieldType))
        {
            var def = GenDefDatabase.GetAllDefsInDatabaseForDef(field.FieldType).FirstOrDefault();
            if (def != null) { sample = def; return true; }
            return false;
        }

        if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var element = field.FieldType.GetGenericArguments()[0];
            if (typeof(Def).IsAssignableFrom(element))
            {
                var def = GenDefDatabase.GetAllDefsInDatabaseForDef(element).FirstOrDefault();
                if (def != null)
                {
                    var list = (IList)Activator.CreateInstance(field.FieldType)!;
                    list.Add(def);
                    sample = list;
                    return true;
                }
            }
        }

        return false;
    }

    // ─────────────────────────── Scribe 往返 ───────────────────────────

    private static TweakData ScribeRoundTrip(TweakData source, Type dataType)
    {
        string path = TempFilePath();
        try
        {
            SaveToFile(path, source);
            return LoadFromFile(path, dataType);
        }
        finally
        {
            ResetScribeIfStuck();
            SafeDelete(path);
        }
    }

    /// <summary>
    /// 存盘并返回文件文本。两条 Scribe 语义由反编译确认（踩错会静默测出假结果）：
    /// <c>InitSaving</c> 内部设置 <c>Scribe.mode</c> 并**已进入根节点**，外部只需再进出一次子节点。
    /// </summary>
    private static string SaveToFile(string path, TweakData data)
    {
        Scribe.saver.InitSaving(path, RootNode);
        if (!Scribe.EnterNode(DataNode))
            throw new Exception($"保存侧 EnterNode(\"{DataNode}\") 失败，Scribe 状态异常");
        data.ExposeData();
        Scribe.ExitNode();
        Scribe.saver.FinalizeSaving();
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 读盘并返回新实例。要点：<c>FinalizeLoading</c> 要求当前 mode 仍为 <c>LoadingVars</c>，
    /// 且由它**自己** <c>ExitNode()</c> 退出根节点 —— 故中间不得改 mode。
    /// 末尾补一次 <c>ResolveDefs()</c>：列表类字段在 LoadingVars 阶段只写 <c>_savedLoadData</c>，
    /// 要等它才落回字段（标量在 <c>ExposeSimple</c> 里已直接落值，多跑一次无害）。
    /// </summary>
    private static TweakData LoadFromFile(string path, Type dataType)
    {
        var target = (TweakData)Activator.CreateInstance(dataType)!;
        Scribe.loader.InitLoading(path);
        if (!Scribe.EnterNode(DataNode))
            throw new Exception($"读取侧 EnterNode(\"{DataNode}\") 失败，Scribe 状态异常");
        target.ExposeData();
        Scribe.ExitNode();
        Scribe.loader.FinalizeLoading();
        target.ResolveDefs();
        return target;
    }

    private static void ResetScribeIfStuck()
    {
        if (Scribe.mode == LoadSaveMode.Inactive) return;
        Log.Warning($"{Tag} 往返结束后 Scribe.mode 仍为 {Scribe.mode}，强制复位（前一次往返未收尾干净）。");
        Scribe.ForceStop();
    }

    private static string TempFilePath()
        => Path.Combine(GenFilePaths.ConfigFolderPath, $"BT_SC11_{Guid.NewGuid():N}.xml");

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) { Log.Warning($"{Tag} 清理临时文件失败：{path} —— {e.Message}"); }
    }

    // ─────────────────────────── 比较（仅 SC11a 路径） ───────────────────────────

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (a is IList la && b is IList lb)
        {
            if (la.Count != lb.Count) return false;
            for (int i = 0; i < la.Count; i++)
                if (!Equals(la[i], lb[i])) return false;
            return true;
        }
        return Equals(a, b);
    }

    private static string Describe(object? v) => v switch
    {
        null => "<null>",
        string s => $"\"{s}\"",
        IList l => $"[{l.Count}]",
        _ => v.ToString() ?? "<?>",
    };
}
#endif
