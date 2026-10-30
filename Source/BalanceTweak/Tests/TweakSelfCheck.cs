using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

/// <summary>
/// L0 运行期自检（见 Docs/NullSemantics_L0_Plan.md §4）。
///
/// 921 个 [TweakField] 字段不可能手测，而 L0 的两类 bug（<b>读档复活</b>、<b>Apply 漏改/误改</b>）
/// 只有运行时才暴露 —— 所以这里用反射遍历<b>全部</b>字段自动判定，异常统一打成可 grep 的
/// <c>[BT/SELFCHECK]</c> / <c>[BT/SNAP]</c> 日志。<b>不写任何逐字段用例</b>：全部遍历 AllColumn
/// （= 该类的全部 [TweakField]）。
///
/// 除 SC3 会额外 <c>Apply()</c> 一次克隆（用于验证幂等；该操作在本工程里本就频繁发生 ——
/// 每次单元格编辑都会 Apply）之外，全部检查只读，不改动运行中的 Def。
/// </summary>
public static class TweakSelfCheck
{
#if BT_SELFCHECK
    /// <summary>只有显式 <c>-p:SelfCheck=true</c> 才编入自检（见 BalanceTweak.csproj）。发布产物恒为 false。</summary>
    public static bool selfCheckEnabled = true;
#else
    /// <summary>只有显式 <c>-p:SelfCheck=true</c> 才编入自检（见 BalanceTweak.csproj）。发布产物恒为 false。</summary>
    public static bool selfCheckEnabled = false;
#endif

    private const string Tag = "[BT/SELFCHECK]";
    private const string SnapTag = "[BT/SNAP]";
    private const string SnapshotFilePrefix = "BalanceTweak_Snapshot_";
    private const int MaxWarnPerCheck = 50;
    private const int MaxOrigSnapLines = 20000;
    private const int SnapshotLogLines = 200;

    private readonly struct R
    {
        public readonly int Checks;
        public readonly int Issues;
        public R(int checks, int issues) { Checks = checks; Issues = issues; }
        public static R operator +(R a, R b) => new(a.Checks + b.Checks, a.Issues + b.Issues);
    }

    /// <summary>本次 Init 加载的用户数据（防御式过滤 value 为 null 的条目）。</summary>
    private static IEnumerable<TweakData> Loaded() => tweakDatas.Values.Where(d => d != null);

    /// <summary>SC1–SC10 入口。由 <see cref="TweakDatabase.Init"/> 末尾调用。</summary>
    public static void RunAll()
    {
        if (!selfCheckEnabled)
        {
            // 发布版不写快照；顺手清掉 Debug 版遗留的文件（曾误发布 Debug 构建，玩家 Config 目录会堆积）。
            RemoveStaleSnapshots();
            return;
        }

        R r = default;
        r += Guard("SC1", SC1_States);
        r += Guard("SC2", SC2_Revived);
        r += Guard("SC3", SC3_ApplyIdempotent);
        r += Guard("SC4", SC4_Revert);
        r += Guard("SC5", SC5_UnsetNotWritten);
        r += Guard("SC6", SC6_Snapshot);
        r += Guard("SC9", SC9_LinkageRefresh);
        r += Guard("SC10", SC10_CodecCoverage);

        if (r.Issues == 0)
            Log.Message($"{Tag} SC1–SC10 全部通过（共 {r.Checks} 项检查）。");
        else
            Log.Warning($"{Tag} SC1–SC10 完成：检查 {r.Checks} 项，异常 {r.Issues} 项（详见上方 [BT/SELFCHECK] 警告）。");
    }

    /// <summary>
    /// 发布版专用：删除 Config 目录里 Debug 版遗留的 <c>BalanceTweak_Snapshot_*.txt</c>。
    /// 只删本模组自己的快照命名，异常一律吞掉（清理失败绝不能影响启动）。
    /// </summary>
    private static void RemoveStaleSnapshots()
    {
        try
        {
            string dir = GenFilePaths.ConfigFolderPath;
            if (!Directory.Exists(dir)) return;
            var files = Directory.GetFiles(dir, SnapshotFilePrefix + "*.txt");
            foreach (var file in files) File.Delete(file);
            if (files.Length > 0)
                Log.Message($"{SnapTag} 已清理调试快照 {files.Length} 个：{dir}");
        }
        catch (Exception e)
        {
            Log.Warning($"{SnapTag} 清理调试快照失败（不影响游戏）：{e.Message}");
        }
    }

    /// <summary>自检本身绝不允许影响游戏：任何异常都吞掉并记 error。</summary>
    private static R Guard(string name, Func<R> check)
    {
        try { return check(); }
        catch (Exception e)
        {
            Log.Error($"{Tag} {name} 自身异常（自检不影响游戏，继续）：{e}");
            return new R(0, 1);
        }
    }

    private static void Warn(string check, string msg, ref int issues, int printed)
    {
        issues++;
        if (printed < MaxWarnPerCheck) Log.Warning($"{Tag} {check} {msg}");
    }

    // ─────────────────────────── SC1 三态一致性 ───────────────────────────

    /// <summary>
    /// 判据：<c>f ∈ cleared</c> ⇒ Cleared（开关关时退化为 Unset）；<c>f ∈ modified ∧ 非空</c> ⇒ Set；否则 Unset。
    /// 顺带抓"字段名已改名/删除"这种陈留（取状态会抛 KeyNotFound）。
    /// </summary>
    private static R SC1_States()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            foreach (var col in data.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Field) continue;
                checks++;
                string f = col.fieldName;
                FieldState actual;
                try { actual = data.StateOfField(f); }
                catch (Exception e)
                {
                    Warn("SC1", $"{data.id} 字段 {f} 无法取三态（字段已改名/删除？）：{e.Message}", ref issues, printed++);
                    continue;
                }
                bool cleared = data.IsFieldCleared(f);
                bool mod = data.IsFieldModified(f);
                bool empty = IsEmptyValue(col.GetRawValue(data));
                FieldState expected = cleared
                    ? FieldState.Cleared
                    : (mod && !empty ? FieldState.Set : FieldState.Unset);
                if (actual != expected)
                {
                    Warn("SC1", $"{data.id} 字段 {f}: 实际 {actual}，期望 {expected}"
                             + $"（cleared={cleared}, modified={mod}, empty={empty}）", ref issues, printed++);
                }
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── SC2 复活检测（最关键） ───────────────────────────

    /// <summary>
    /// ResolveDefs 末尾记下的"modified ∧ 空"字段，在 Apply 之后必须仍为空。
    /// 被 SetDef / GenValues 的 <c>??=</c> 回填 = 复活 ⇒ A3 漏加了 <c>!IsFieldModified</c> 守卫。
    /// 这一条自动抓出漏加守卫，<b>不需要人工枚举字段</b>。
    /// </summary>
    private static R SC2_Revived()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            checks++;
            foreach (var f in data.DetectRevivedFields())
            {
                Warn("SC2", $"复活: {data.id} 字段 {f} 在 Apply 后被回填（应为 Cleared）"
                          + " —— 检查该字段的 ??= 回填点是否漏了 !IsFieldModified 守卫", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── SC3 Apply 幂等 ───────────────────────────

    /// <summary>
    /// 连续 Apply 两次 → 快照相等。用克隆实例做第二次 Apply，避免直接重复写运行中的数据实例；
    /// 幂等时 Def 终态不变（克隆与本体字段一致），非幂等/有副作用时会在日志里报出不一致的字段。
    /// 比较用 <see cref="StatColumnConfig.GetString"/>（各字段类型的规范化显示），
    /// 避免为 921 个字段各写一份值比较器。
    /// </summary>
    private static R SC3_ApplyIdempotent()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            // def 缺失（Mod 卸载导致 371 条"未找到对应 Def"）的条目无法 Apply：
            // clone.Apply() 只会对每条刷一行 Log.Error「的def为!」，既测不出幂等性又淹没日志 —— 跳过。
            if (data.def == null) continue;
            checks++;
            var before = DisplayLines(data);
            if (before.Count == 0) continue;

            TweakData clone;
            try
            {
                clone = data.Clone();
                clone.Apply();
            }
            catch (Exception e)
            {
                Warn("SC3", $"{data.id} 克隆二次 Apply 抛异常（Apply 非幂等或有副作用）：{e.Message}", ref issues, printed++);
                continue;
            }

            var after = DisplayLines(clone);
            if (before.Count != after.Count)
            {
                Warn("SC3", $"{data.id} 二次 Apply 后字段数变化（{before.Count} → {after.Count}）", ref issues, printed++);
                continue;
            }
            for (int i = 0; i < before.Count; i++)
            {
                if (!string.Equals(before[i], after[i], StringComparison.Ordinal))
                    Warn("SC3", $"{data.id} 连续 Apply 两次结果不一致（幂等性被破坏）：{before[i]} → {after[i]}", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── SC4 Revert 正确性 ───────────────────────────

    /// <summary>
    /// Cleared 的<b>非集合</b>字段（ApplyV/ApplyD/ApplyS ⇒ "还原原值"），Apply 之后 Def 上的值必须是
    /// <b>原值</b>或<b>空</b>；出现第三种值 ⇒ Apply 把 def 写坏了。
    /// <para>
    /// 集合字段不在这里验：清空集合的终态是"空列表"，且在 Def 上无法与"原本就是空列表"区分 ——
    /// 那由 SC2（复活检测）覆盖。可空标量也不例外：<c>ApplyN</c> 的 Cleared 同样走还原，
    /// 不再有"清空 ⇒ 写 null"这条路（null 只出现在重置的权威写入里）。
    /// </para>
    ///
    /// 探手法：新建同类型空实例 + <c>SetDef(def, type, false)</c>，让字段从 <b>当前 def</b> 读回 —— 这样读到的
    /// 就是"Apply 之后 def 上的实际值"，且不触碰 def。原值本就为空的字段跳过（Revert 的兜底语义是保持现状）。
    ///
    /// 已知边界：Revert 成功的终态与"Apply 根本没碰该字段"不可区分（两者都是原值），
    /// 所以 SC4 只能抓"第三种值"，不能证明 Apply 一定动过手 —— 那由 SC2 与自检快照覆盖。
    /// </summary>
    private static R SC4_Revert()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            if (TweakDatabase.GetOriginal(data.id) is not TweakData orig) continue;

            TweakData? probe = null;
            foreach (var col in data.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Field) continue;
                if (TweakDatabase.IsCollectionColumn(col)) continue; // 集合的 Cleared = 清空，由 SC2 覆盖
                if (data.StateOfField(col.fieldName) != FieldState.Cleared) continue;

                checks++;
                object? origVal = col.GetRawValue(orig);
                if (IsEmptyValue(origVal)) continue; // 原值缺席：Revert 兜底为"保持现状"，不算异常

                if (probe == null)
                {
                    try { probe = MakeProbe(data); }
                    catch (Exception e)
                    {
                        Warn("SC4", $"{data.id} 无法构造探针实例（跳过该条其余字段）：{e.Message}", ref issues, printed++);
                        break;
                    }
                }

                object? defVal = col.GetRawValue(probe);
                if (SameValue(defVal, origVal) || IsEmptyValue(defVal)) continue;
                Warn("SC4", $"{data.id} 字段 {col.fieldName}: Cleared 后 def 上的值既非原值也非空"
                          + $"（def={Str(defVal)}，原值={Str(origVal)}）", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    /// <summary>新建同类型空实例并从当前 def 读回字段值（不修改 def，也不影响游戏中任何状态）。</summary>
    private static TweakData MakeProbe(TweakData data)
    {
        if (data.def == null) throw new InvalidOperationException("data.def 为 null");
        var probe = (TweakData)Activator.CreateInstance(data.GetType())!;
        probe.SetDef(data.def, data.id.settingType, false);
        return probe;
    }

    // ─────────────────────────── SC5 未改零写入 ───────────────────────────

    /// <summary>
    /// Unset 字段经 Apply 后仍应等于 <see cref="TweakDatabase.GetOriginal"/> 的同字段。
    /// 跳过 ApplyDefStats Phase 3 会"从 def 反同步回字段"的列（StatDef 标量 / 属性 stat / stat 列表）：
    /// 它们的值由 def 决定，不是"误改未改字段"。
    /// </summary>
    private static R SC5_UnsetNotWritten()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            if (TweakDatabase.GetOriginal(data.id) is not TweakData orig) continue;
            foreach (var col in data.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Field) continue;
                if (col.stat != null || col.isSPStat || col.columnType == ColumnStyle.StatModList) continue;
                if (data.StateOfField(col.fieldName) != FieldState.Unset) continue;
                // Def 引用字段（如 ProjectileData.damageType）只在"非 tweaked"路径从 def 回填
                // （SetDef 的 GetProjectileProp 在 tweaked==true 时整段跳过），已改条目的实例侧合法为 null，
                // 与实例比较必误报；这类字段由 SC4 走探针读真实 def 覆盖，故此处跳过。
                if (col.GetRawValue(data) is Def || col.GetRawValue(orig) is Def) continue;

                checks++;
                string now = col.GetString(data) ?? "";
                string expected = col.GetString(orig) ?? "";
                if (!string.Equals(now, expected, StringComparison.Ordinal))
                    Warn("SC5", $"{data.id} 未改字段 {col.fieldName} 在 Apply 后不等于原值（{expected} → {now}）", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── SC6 全量快照 + 跨运行 diff ───────────────────────────

    private static R SC6_Snapshot()
    {
        try
        {
            int lines = SnapshotAll();
            return new R(lines, 0);
        }
        catch (Exception e)
        {
            Log.Error($"{Tag} SC6 快照导出失败：{e}");
            return new R(0, 1);
        }
    }

    /// <summary>
    /// SC6：全量快照。每行 <c>[BT/SNAP] &lt;src&gt; &lt;Data类(defName)&gt; | &lt;field&gt; | &lt;state&gt; | &lt;值&gt;</c>；
    /// 输出前 <see cref="SnapshotLogLines"/> 行到日志，全量落盘到 Config 目录。返回行数。
    ///
    /// 用法（= 全字段自动化回归，替代手测）：
    /// <list type="number">
    /// <item><b>改动前</b>跑一次 → A；</item>
    /// <item><b>改动后</b>跑一次 → B；<b>A 与 B 的差异 = 本次改动的实际影响面</b>（无差异 = 无回归）。</item>
    /// </list>
    /// 跨运行比较用 <see cref="DiffSnapshots"/>。
    ///
    /// <b>顺序：先 user 后 orig；只有 orig 会被 <see cref="MaxOrigSnapLines"/> 截断。</b>
    /// <c>src=user</c> 是已存修改的 <c>state</c> + 值，是跨运行 diff 的唯一信号，绝不能截断；
    /// <c>src=orig</c> 是原始库基线、运行期稳定，全量可达数十万行，故截断。
    /// （旧实现 orig 在前且与 user 共用同一个 20000 上限，被 orig 吃满 ⇒ <b>user 一行都写不进</b>，
    /// diff 恒为空 —— 这是早先修掉的假阴性。）
    /// </summary>
    public static int SnapshotAll()
    {
        var lines = new List<string>();

        // user（已存修改）在前：跨运行 diff 的唯一信号 —— 全量，绝不截断。
        foreach (var data in Loaded().OrderBy(d => d.id.ToString()))
            foreach (var col in data.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Field) continue;
                lines.Add(SnapLine(data, "user", col));
            }

        // orig（原始库）在后：运行期稳定，仅作基线 —— 超 MaxOrigSnapLines 截断。
        int nOrig = 0;
        foreach (var data in TweakDatabase.AllOriginals.Values.OrderBy(d => d.id.ToString()))
        {
            if (nOrig >= MaxOrigSnapLines) break;
            foreach (var col in data.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Field) continue;
                if (nOrig >= MaxOrigSnapLines) break;
                lines.Add(SnapLine(data, "orig", col));
                nOrig++;
            }
        }

        string text = string.Join("\n", lines);
        string file = Path.Combine(GenFilePaths.ConfigFolderPath,
            $"{SnapshotFilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        try { File.WriteAllText(file, text); }
        catch (Exception e) { Log.Error($"{SnapTag} 写盘失败：{file}: {e.Message}"); }

        // 全量进文件，日志只给头部 —— 否则每次启动都要把上万行灌进 Player.log。
        string head = string.Join("\n", lines.Take(SnapshotLogLines));
        Log.Message($"{SnapTag} 共 {lines.Count} 行（src=user 已存修改在前、不截断；src=orig 原始库基线截断 {nOrig}/{MaxOrigSnapLines}）。\n{head}"
                  + $"\n{SnapTag} 已落盘：{file}（跨运行 diff：TweakSelfCheck.DiffSnapshots(before, after)）");
        return lines.Count;
    }

    private static string SnapLine(TweakData data, string src, StatColumnConfig col)
        => $"{SnapTag} {src} {data.GetType().Name}({data.id.defName}) | {col.fieldName} | "
         + $"{data.StateOfField(col.fieldName)} | {col.GetString(data) ?? ""}";

    /// <summary>
    /// 跨运行 diff：逐行比较两份快照文件，打印"状态/值发生变化"的字段清单与计数。
    /// 典型用法见 <see cref="SnapshotAll"/>。
    /// </summary>
    public static void DiffSnapshots(string beforePath, string afterPath)
    {
        var a = ReadSnapshot(beforePath);
        var b = ReadSnapshot(afterPath);
        int changed = 0, printed = 0;

        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var v2))
            {
                changed++;
                if (printed++ < MaxWarnPerCheck) Log.Warning($"{SnapTag} 仅 before 有: {kv.Key} = {kv.Value}");
            }
            else if (!string.Equals(kv.Value, v2, StringComparison.Ordinal))
            {
                changed++;
                if (printed++ < MaxWarnPerCheck) Log.Warning($"{SnapTag} 变化: {kv.Key}: {kv.Value} → {v2}");
            }
        }
        foreach (var kv in b)
        {
            if (a.ContainsKey(kv.Key)) continue;
            changed++;
            if (printed++ < MaxWarnPerCheck) Log.Warning($"{SnapTag} 仅 after 有: {kv.Key} = {kv.Value}");
        }
        Log.Message($"{SnapTag} diff 完成：before={a.Count} 行，after={b.Count} 行，差异 {changed} 项。");
    }

    private static Dictionary<string, string> ReadSnapshot(string path)
    {
        var dict = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (!line.StartsWith(SnapTag, StringComparison.Ordinal)) continue;
            var parts = line[SnapTag.Length..].Split('|');
            if (parts.Length < 4) continue;
            string key = parts[0].Trim() + " | " + parts[1].Trim();
            string value = parts[2].Trim() + " | " + string.Join("|", parts.Skip(3)).Trim();
            dict[key] = value;
        }
        return dict;
    }

    // ─────────────────────────── SC9 联动覆盖（派生列自洽） ───────────────────────────

    /// <summary>
    /// 关联刷新缺口检测：对 <see cref="WeaponData"/> 的派生显示列，用克隆 <c>GenValues()</c> 重算一次
    /// 再与运行实例比较。克隆的 <c>GenValues</c> 经 <c>GetData</c> 读到的是<b>运行中的依赖实例</b>
    /// （弹丸 / 炮塔建筑），因此某次依赖编辑后若忘了刷新武器，运行实例的派生值会与重算值不一致。
    ///
    /// 只读：除克隆自身外不改任何实例，也不写 Def。仅在自检开启时运行。
    /// 跳过 <c>Projectile == null</c> 的武器 —— 那会走 GenValues 里解析 defaultProjectile 的分支，
    /// 该分支会写回 ProjectileData.RangedWeapon，属副作用，不适合放进自检。
    /// </summary>
    private static R SC9_LinkageRefresh()
    {
        int checks = 0, issues = 0, printed = 0;
        foreach (var data in Loaded())
        {
            if (data is not WeaponData weapon || weapon.def == null) continue;
            if (weapon.Projectile == null) continue;
            checks++;

            TweakData clone;
            try
            {
                clone = weapon.Clone();
                ((WeaponData)clone).GenValues();
            }
            catch (Exception e)
            {
                Warn("SC9", $"{weapon.id} 克隆重算抛异常：{e.Message}", ref issues, printed++);
                continue;
            }

            foreach (var col in weapon.AllColumn)
            {
                if (col.columnDataType != ColumnDataType.Display) continue;
                string now = col.GetString(weapon) ?? "";
                string expect = col.GetString(clone) ?? "";
                if (!string.Equals(now, expect, StringComparison.Ordinal))
                    Warn("SC9", $"{weapon.id} 派生列 {col.fieldName} 与重算值不一致（{now} -> 重算 {expect}）"
                              + " —— 检查关联项（弹丸 / 炮塔）改动后是否漏了刷新调用", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── SC10 登记表完备性 ───────────────────────────

    /// <summary>
    /// 断言每个 <c>[TweakField]</c> 字段的类型都登记在 <c>TweakData.FieldCodecs</c> 里。
    /// <para>
    /// 这条是 ①（登记表）的直接收益：改造前"漏登记"是**静默**的 —— 字段不落盘、读档也不还原，
    /// 只在用户改完重开后才发现数据没了。现在启动即报。
    /// </para>
    /// <para>
    /// 字段枚举口径必须与 <c>TweakData&lt;T&gt;.InitStaticFields</c> 严格一致：
    /// <c>MayRequire</c> 未满足的字段不进字段表、不参与序列化，也就不该检查（否则误报 —— SC11 首跑踩过这个坑）。
    /// </para>
    /// </summary>
    private static R SC10_CodecCoverage()
    {
        int checks = 0, issues = 0, printed = 0;

        foreach (var dataType in TweakData.AllConcreteDataTypes)
        {
            foreach (var field in dataType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<TweakFieldAttribute>();
                if (attr == null || attr.DataType != ColumnDataType.Field) continue;
                if (!DataUtility.MayRequire(attr.MayRequire)) continue;

                checks++;
                if (TweakData.FieldCodecs.Find(field.FieldType) == null)
                    Warn("SC10", $"{dataType.Name}.{field.Name}（{field.FieldType.Name}）的类型未登记在 FieldCodecs 里"
                                + " —— 该字段既不会落盘也不会还原", ref issues, printed++);
            }
        }
        return new R(checks, issues);
    }

    // ─────────────────────────── 小工具 ───────────────────────────

    private static List<string> DisplayLines(TweakData data)
    {
        var list = new List<string>();
        foreach (var col in data.AllColumn)
        {
            if (col.columnDataType != ColumnDataType.Field) continue;
            list.Add($"{col.fieldName}={col.GetString(data)}");
        }
        return list;
    }

    private static bool SameValue(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a is Def || b is Def) return false; // Def 是单例：引用不等即不等
        return Equals(a, b);
    }

    private static string Str(object? v) => v switch
    {
        null => "null",
        ThingFilter tf => $"{tf.AllowedDefCount} 项",
        System.Collections.ICollection c => $"{c.Count} 项",
        _ => v.ToString() ?? "null",
    };
}
