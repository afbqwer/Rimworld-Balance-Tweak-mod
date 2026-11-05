using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

[StaticConstructorOnStartup]
public static partial class TweakDatabase
{
    private static Dictionary<TweakID, TweakData> originalData = [];

    /// <summary>
    /// 全量行源（分类结果）。窗口打开时用它建行，也用它把延迟的快照补齐。
    /// <para>
    /// 与 <see cref="originalData"/> 的区别：那个只装"已经 SetDef 过的"行。分类阶段实例就造好了
    /// （34k 个空对象约 15ms），贵的是 SetDef（读 def 填字段，20µs/行），那部分可以推迟。
    /// </para>
    /// </summary>
    private static List<(Def def, TweakRule rule, SettingType type)> allRows = [];
    public static IReadOnlyList<(Def def, TweakRule rule, SettingType type)> AllRows => allRows;

    /// <summary>
    /// 延迟建快照的行。Init 只建"Apply 期间会被读到或写到"的那些，其余在这里等：
    /// 窗口打开（<c>AdvanceInit</c>）或首次 <see cref="GetOriginal"/> 时才 SetDef。
    /// <para>
    /// 不变量：<b>被 Apply 写过的 def，其快照一定已在 <see cref="originalData"/> 里</b>。
    /// 所以走到补建的行，def 必然还是原值，补出来的值是对的。
    /// </para>
    /// </summary>
    private static Dictionary<TweakID, (Def def, TweakRule rule)>? deferredRows;

    private static int deferredBuilt;

    /// <summary>
    /// 补建时顺带产生的子项行（招式 / 想法阶段 / 健康阶段 / 特性程度）。
    /// 它们只存在于父项 SetDef 之后，而 UI 的行源是 <see cref="AllRows"/>（父项）——
    /// 所以要把这些追加进去，否则这几千行在表格里整段消失。
    /// </summary>
    private static readonly List<TweakData> deferredSubs = [];
    public static IReadOnlyList<TweakData> DeferredSubs => deferredSubs;

    /// <summary>获取原始数据（未建的行会在此刻补建）。</summary>
    public static TweakData? GetOriginal(TweakID id)
    {
        if (originalData.TryGetValue(id, out var data)) return data;
        return deferredRows == null ? null : MaterializeDeferred(id);
    }

    /// <summary>尝试获取原始数据（未建的行会在此刻补建）。</summary>
    public static bool TryGetOriginal(TweakID id, out TweakData? data)
    {
        data = GetOriginal(id);
        return data != null;
    }

    /// <summary>尝试获取原始数据的克隆</summary>
    public static TweakData? CloneOriginal(TweakID id)
        => GetOriginal(id)?.Clone();

    /// <summary>遍历所有原始数据（<b>只含已建的行</b>；要全量请用 <see cref="AllRows"/>）。</summary>
    public static Dictionary<TweakID, TweakData> AllOriginals => originalData;

    /// <summary>把一行延迟的快照补建出来。失败返回 null（该行随后按"无原值"处理）。</summary>
    private static TweakData? MaterializeDeferred(TweakID id)
    {
        if (!deferredRows!.TryGetValue(id, out var row)) return null;
        deferredRows.Remove(id);
        try
        {
            var data = row.rule.Factory();
            data.SetDef(row.def, id.settingType, false);
            originalData[id] = data;
            deferredBuilt++;

            // 子项（招式 / 想法阶段 / 健康阶段 / 特性程度）同样要补 ——
            // 它们是独立的行，UI 与 Apply 都按 id 直接取，缺了就是整段列表空着。
            if (data is ISubItemHost host)
            {
                foreach (var sub in host.CreateSubItems(data, false))
                {
                    if (originalData.TryAdd(sub.id, sub)) deferredSubs.Add(sub);
                }
            }
            return data;
        }
        catch (Exception e)
        {
            Log.Error($"[BalanceTweak] 补建延迟快照失败，id={id}: {e}");
            return null;
        }
    }
    public static Dictionary<string, TweakID> turretBuildingDatas = [];
    public static Dictionary<string, TweakID> projectileDatas = [];
    public static Dictionary<string, TweakID> raceDatas = [];

    /// <summary>
    /// 正在建原始快照（<see cref="Init"/> 的 SetDef 循环）。
    /// <para>
    /// 期间所有跨行读取都必须走原始层：存档里的修改值此时已经反序列化好了，
    /// 走"当前生效层"会把现值写进 <see cref="GetOriginal"/> 那份快照，
    /// 而 Revert / ResetTo / 强度刻度全靠它当原值基准。
    /// </para>
    /// <para>不用 `SetDef(tweaked: false)` 判断：自检探针（<c>MakeProbe</c>）也用 false 建实例，那种探针要读的是当前 def。</para>
    /// </summary>
    internal static bool buildingOriginals;

    // 记录失败的修改项（供 UI 清理使用）
    public static List<TweakData> failedResolveItems = new();
    public static List<TweakData> failedDirectItems = new();

    /// <summary>
    /// 延迟项（子项）失败的原因。两种都是"应用之前就被跳过"，但用户看到的东西不一样：
    /// 前者是父项本身没了，后者是父项还在、只是它的阶段/招式表变了
    /// （<see cref="TweakData.SetParentTweak"/> 里 label 对不上就返回 false）。
    /// 以前两者共用一句「父项已不存在」，对后一种是明着写错 —— 父项明明在表里点得开。
    /// </summary>
    public enum DelayedFailCause
    {
        /// <summary>找不到父项数据或其 Def。</summary>
        ParentMissing,
        /// <summary>父项在，但该子项已不匹配（阶段 / 招式被重排或改名）。</summary>
        ParentMismatch,
    }

    public static List<(TweakData data, DelayedFailCause cause)> failedDelayedItems = new();

    public static Dictionary<ToolCapacityDef, VerbProperties> allMeleeVerb = [];
    public const string CE = "ceteam.combatextended";
    public const string VEF = "oskarpotocki.vanillafactionsexpanded.core";
    public const string VEP = "vanillaexpanded.vpsycastse";
    /// <summary>Fortified Features Framework（重型装备 / 多炮塔 / 中继指挥的提供方）。</summary>
    public const string FFF = "AOBA.Framework";
    /// <summary>Atmosphere Overhaul（大气模拟：气体 / 全球气体 / 爆炸剖面 / 建筑大气属性）。</summary>
    public const string AO = "acutus.AtmosphereOverhaul.release";
    public static bool VCTAPDown = false;
    public static bool VCTAMDown = false;

    static TweakDatabase()
    {
        //Init();
        // 诊断报告放在 finally：Init 抛异常时也要把已登记的反射失配吐出来 ——
        // 那些失配往往正是 Init 失败的原因，跟着异常一起消失等于什么都没查。
        LongEventHandler.QueueLongEvent(() =>
        {
            try
            {
                Init();
            }
            finally
            {
                // Init 抛异常也要关掉计量：否则 initPerf 会一直 Active，运行期每次 Apply / GenValues
                // 都往一张永远不会被打印的表里累加。
                InitPerf.Finish();
                DataUtility.AbortDeferredStuffRefresh();
                // 诊断层自己绝不能变成新的故障源：TweakRegistry 的静态构造若抛异常，
                // 这里会变成 TypeInitializationException 并把真正的 Initialize 异常挤掉。
                TryReport("可选 Mod 兼容概览", TweakRegistry.ReportCompat);
                TryReport("反射失配汇总", Diagnostics.Report);
            }
        }, "BalanceTweak initializing", doAsynchronously: false, null);
    }

    private static void TryReport(string what, Action report)
    {
        try
        {
            report();
        }
        catch (Exception e)
        {
            Log.Warning($"[BalanceTweak] {what}输出失败（不影响 Mod 运行）：{e}");
        }
    }

    #region 规则驱动的分类与派发

    /// <summary>分类结果（含诊断用的统计）。</summary>
    private sealed class ClassifyResult
    {
        /// <summary>
        /// 命中的行：<c>Def + 规则 + 页签</c>，<b>不含实例</b>。
        /// 实例推迟到"确实要建快照"时再 <c>Factory()</c> —— 34k 个 Data 对象合计 20~30MB，
        /// 在分类阶段造出来只是为了稍后把其中大半放进延迟表，等于白发一轮 GC。
        /// </summary>
        public readonly List<(Def, TweakRule, SettingType)> Items = new();
        public readonly Dictionary<SettingType, int> ByTab = new();
        public readonly Dictionary<Type, int> ByDefType = new();
        public readonly Dictionary<ThingCategory, int> FallbackByCategory = new();
        public readonly List<string> FallbackSamples = new();
        public readonly List<string> MultiMatched = new();
        public int Fallback;

        /// <summary>兜底规则声明了收容范围（MatchMethod）但该 Def 不在其内 —— 不创建实例。</summary>
        public int Uncovered;
        public readonly List<string> UncoveredSamples = new();
    }

    /// <summary>
    /// 按 TweakRegistry 的规则表遍历所有 Def 并生成 TweakData，取代原 if/else-if 链与一排 ProcessDefs&lt;T&gt;()。
    ///
    /// 同 Def 类型内按 Priority 降序匹配：
    /// - 第一条命中的规则是主类型；若它标了 Exclusive（默认），到此为止 —— 与原链语义完全一致；
    /// - 若标了 Exclusive = false，则继续收集其它命中的规则，实现"一个 Def 并列挂多个 Data"；
    /// - 一条规则都没命中时使用该 Def 类型的兜底规则（IsFallback）；
    ///   兜底规则自己也可以用 MatchMethod 声明收容范围（如 MiscThingData 只收 Item/Filth），
    ///   范围之外的 Def 直接放弃，计入 Uncovered。
    /// </summary>
    private static ClassifyResult ClassifyAll()
    {
        var res = new ClassifyResult();
        var available = TweakRegistry.Rules.Where(r => r.IsAvailable).ToList();

        foreach (var group in available.GroupBy(r => r.DefType))
        {
            var ordered = group.OrderByDescending(r => r.Priority).ToList();
            var dict = TweakRegistry.DefsOf(group.Key);
            res.ByDefType[group.Key] = dict.Count;

            foreach (var def in dict.Values)
            {
                try
                {
                    bool primaryDone = false;
                    foreach (var rule in ordered)
                    {
                        if (rule.IsFallback) continue;
                        if (rule.Matcher != null && !rule.Matcher(def)) continue;

                        res.Items.Add((def, rule, rule.Tab));
                        res.ByTab[rule.Tab] = res.ByTab.GetValueOrDefault(rule.Tab) + 1;

                        if (primaryDone)
                        {
                            res.MultiMatched.Add($"{def.defName}(+{rule.Tab})");
                        }
                        else
                        {
                            primaryDone = true;
                            if (rule.Exclusive) break;
                        }
                    }

                    if (primaryDone) continue;

                    var fallback = ordered.FirstOrDefault(r => r.IsFallback);
                    if (fallback == null) continue;
                    // 兜底规则同样可以声明收容范围：范围之外的 Def 不创建实例（无编辑价值 + 省初始化耗时）。
                    if (fallback.Matcher != null && !fallback.Matcher(def))
                    {
                        res.Uncovered++;
                        if (res.UncoveredSamples.Count < 30) res.UncoveredSamples.Add(def.defName);
                        continue;
                    }

                    res.Items.Add((def, fallback, fallback.Tab));
                    res.ByTab[fallback.Tab] = res.ByTab.GetValueOrDefault(fallback.Tab) + 1;
                    res.Fallback++;
                    if (def is ThingDef thing)
                    {
                        res.FallbackByCategory[thing.category] = res.FallbackByCategory.GetValueOrDefault(thing.category) + 1;
                        if (res.FallbackSamples.Count < 30) res.FallbackSamples.Add($"{thing.defName}({thing.category})");
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"[BalanceTweak] 分类出错，def={def.defName}（{group.Key.Name}）: {e}");
                }
            }
        }
        return res;
    }

    /// <summary>
    /// 按规则表把一条已存修改应用到目标 Def。
    /// 一个 Data 类可能有多条规则（不同 DefType），逐条尝试解析 defName。
    /// <paramref name="drifted"/> 收集"存档里的 settingType 与当前规则不一致"的条目，供调用方归一化字典键。
    /// </summary>
    private static bool TryApplyData(TweakData data, TweakID id, List<(TweakID oldKey, TweakData data)> drifted)
    {
        var rules = TweakRegistry.RulesOf(data.GetType());
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!rule.IsAvailable) continue;
            if (!TweakRegistry.TryResolve(rule, id.defName, out var def) || def == null) continue;

            if (rule.Tab != id.settingType) drifted.Add((id, data));

            // 二次 SetDef 与 Apply 分开计：前者是"重新绑 def + 回填默认值"（每个项目都付一次），
            // 后者才是各数据类自己的写回与派生计算。混在一起就分不清是反射贵还是算法贵。
            long tSetDef = InitPerf.T();
            data.SetDef(def, rule.Tab, true);
            double setDefMs = InitPerf.MsSince(tSetDef);

            long tApply = InitPerf.T();
            try
            {
                data.Apply();
            }
            finally
            {
                // 异常路径同样记账：抛异常的那条也是花了时间才抛的，丢掉会让总数对不上。
                InitPerf.AddApply(rule.Tab, setDefMs, InitPerf.MsSince(tApply));
            }
            return true;
        }
        return false;
    }

    #endregion

    /// <summary>
    /// 建「defName → 数据行」反查索引。
    /// <para>
    /// 这些索引原先是在各自 <c>GetPropType</c> 里顺手写的，靠"初始化期 propType 会被无条件求值一次"
    /// 这个前提活着。propType 改成懒加载后那个前提没了：谁先被读谁才写，于是
    /// <list type="bullet">
    /// <item>武器解析弹丸时 <see cref="projectileDatas"/> 还是空的 → 伤害/穿透镜像全空、强度除零；</item>
    /// <item>武器判炮塔时 <see cref="turretBuildingDatas"/> 还是空的 → 炮塔枪全被判成内置武器；</item>
    /// <item>兵种挂种族时 <see cref="raceDatas"/> 还是空的。</item>
    /// </list>
    /// 顺带这也是本该如此的分工：索引是"行与行之间的关系"，属于建表阶段，不该藏在展示属性的 getter 里
    /// （那等于让读一个只用于绘制的值产生跨行副作用）。
    /// </para>
    /// <para>只依赖 Def 本身，与 SetDef / Apply 的先后无关，所以放在分类之后、建快照之前。</para>
    /// </summary>
    internal static void BuildLookupIndices(List<(Def def, TweakRule rule, SettingType type)> items)
    {
        projectileDatas.Clear();
        turretBuildingDatas.Clear();
        raceDatas.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            var (def, rule, type) = items[i];
            if (def == null || rule == null) continue;

            // 键必须在这里现拼，不能用 data.id：id 是在随后的 SetDef 里才赋值的
            // （`TweakData<T>.SetDef` → `id = new(def.defName, type)`），本方法跑在建快照之前，
            // 那一刻 data.id 还是 `default`（defName 为空、settingType 为 None）。
            // 用它建表会把所有条目塌缩到同一个 ":None" 键上 —— 武器于是解析不到弹丸（DPS = 0），
            // 炮塔枪拿到一个空 TweakID（点链接还会 KeyNotFound）。
            var id = new TweakID(def.defName, type);

            // 判类型改看规则的 DataType（实例还没造）。用 IsAssignableFrom 而不是 ==：
            // 规则可以声明在子类上（HeavyEquippableDefData : BuildingData），== 会漏掉它们。
            if (typeof(ProjectileData).IsAssignableFrom(rule.DataType))
            {
                projectileDatas.SetOrAdd(def.defName, id);
            }
            else if (typeof(RaceData).IsAssignableFrom(rule.DataType))
            {
                raceDatas.SetOrAdd(def.defName, id);
            }
            else if (typeof(BuildingData).IsAssignableFrom(rule.DataType)
                     && def is ThingDef td && td.building?.turretGunDef is ThingDef gun
                     && !td.IsFrame && td.thingClass?.Namespace != "VFEProps")
            {
                // 多座建筑挂同一把枪时保留第一个（与原来的 TryAdd 一致）：
                // 武器只需要一个能拿到 turretBurstCooldownTime / turretBurstWarmupTime 的把手。
                // 排除 Frame / VFE 道具：它们也是 BuildingData 行，但建筑属性是复制来的空壳，
                // 拿它们当把手会把射速算成默认值（原先写在各行 GetPropType 里，天然被那两处分支挡掉）。
                turretBuildingDatas.TryAdd(gun.defName, id);
            }
        }
    }

    private static void Init()
    {
        var sw = Stopwatch.StartNew();
        var totalSw = Stopwatch.StartNew();
        var timings = new List<string>();
        double measuredMs = 0;
        InitPerf.Start();

        // 上一轮（热重载 / 重复初始化）的延迟状态不能带进来：allRows 已换新，旧的 deferred 键会指向失效的 Def。
        allRows = [];
        deferredRows = null;
        deferredBuilt = 0;
        deferredSubs.Clear();

        // 阶段计时统一走这里：读表 → 累加已归属耗时 → 记一行 → 重新计时。
        // 之前各阶段散落的 sw.Restart()/sw.Elapsed 容易出现"忘记读"或"忘记重启"，让阶段之和与总计对不上。
        void Mark(string name)
        {
            double ms = sw.Elapsed.TotalMilliseconds;
            measuredMs += ms;
            timings.Add($"  {name}: {ms:F1}ms");
            sw.Restart();
        }

        // 清除上次记录
        failedResolveItems.Clear();
        failedDirectItems.Clear();
        failedDelayedItems.Clear();

        InitAllMeleeDamageDef();
        Mark("InitAllMeleeDamageDef");

        var pendingItems = new List<(Def, TweakRule, SettingType)>();
        int thingDefCount = DefDatabase<ThingDef>.DefCount;
        TweakLog.Message($"[BalanceTweak] Def 统计：ThingDef={thingDefCount}, PawnKindDef={DefDatabase<PawnKindDef>.DefCount}, AbilityDef={DefDatabase<AbilityDef>.DefCount}, ThoughtDef={DefDatabase<ThoughtDef>.DefCount}, GeneDef={DefDatabase<GeneDef>.DefCount}, MemeDef={DefDatabase<MemeDef>.DefCount}, IncidentDef={DefDatabase<IncidentDef>.DefCount}, ResearchProjectDef={DefDatabase<ResearchProjectDef>.DefCount}, FactionDef={DefDatabase<FactionDef>.DefCount}, HediffDef={DefDatabase<HediffDef>.DefCount}, TraitDef={DefDatabase<TraitDef>.DefCount}, TerrainDef={DefDatabase<TerrainDef>.DefCount}");
        // 上面那句 Def 统计日志不计入任何阶段（会落到"未归属"），这里重新起表，让"规则分类"只含分类本身。
        sw.Restart();

        // 分类完全由 TweakRegistry 的规则表驱动（规则来自各 Data 类上的 [TweakFor]），
        // 取代原先的 7 分支 if/else-if 链 + 一排 ProcessDefs<T>() 调用 + 两个跨 Mod 专用方法。
        var classify = ClassifyAll();
        pendingItems.AddRange(classify.Items);
        classify.ByDefType.TryGetValue(typeof(ThingDef), out int thingProcessed);
        if (classify.MultiMatched.Count > 0)
            TweakLog.Message($"[BalanceTweak] 并列挂载：{classify.MultiMatched.Count} 条（{string.Join(", ", classify.MultiMatched.Take(20))}）");
        Mark($"规则分类（ThingDef {thingProcessed} 个，兜底 {classify.Fallback} 个）");
        // 反查索引必须在这里建：后面每个 SetDef 都可能读它（武器解析弹丸、判炮塔、兵种挂种族），
        // 而索引来源是"有哪些行"，与 SetDef 的顺序、与展示属性有没有被读过都无关。
        BuildLookupIndices(pendingItems);
        Mark($"建反查索引（弹丸 {projectileDatas.Count} / 炮塔 {turretBuildingDatas.Count} / 种族 {raceDatas.Count}）");
        //Log.Message($"[BalanceTweak] ThingDef 覆盖：总数 {thingProcessed}，规则分类 {thingProcessed - classify.Fallback - classify.Uncovered}，兜底 Misc {classify.Fallback}，放弃 {classify.Uncovered}（无编辑价值，不建实例）");
        //Log.Message($"[BalanceTweak] 兜底 Misc 按 ThingCategory：{string.Join(", ", classify.FallbackByCategory.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}"))}");
        //if (classify.FallbackSamples.Count > 0)
        //    Log.Message($"[BalanceTweak] 兜底 Misc 示例（最多 30）：{string.Join(", ", classify.FallbackSamples)}");
        //if (classify.UncoveredSamples.Count > 0)
        //    Log.Message($"[BalanceTweak] 放弃的 ThingDef 示例（最多 30）：{string.Join(", ", classify.UncoveredSamples)}");
        // 按 LoadingOrd 升序的稳定桶排序：组内保持 pendingItems 原有相对顺序，组间按键升序。
        // 手工分桶（而不是 LINQ GroupBy/OrderBy），避免 3 万项上的 Lookup/排序分配。
        var sortedItems = new List<(Def, TweakRule, SettingType)>(pendingItems.Count);
        var loadingBuckets = new SortedDictionary<int, List<(Def, TweakRule, SettingType)>>();
        foreach (var item in pendingItems)
        {
            int ord = item.Item2.LoadingOrd;
            if (!loadingBuckets.TryGetValue(ord, out var bucket))
            {
                bucket = new List<(Def, TweakRule, SettingType)>();
                loadingBuckets.Add(ord, bucket);
            }
            bucket.Add(item);
        }
        foreach (var bucket in loadingBuckets.Values)
            sortedItems.AddRange(bucket);

        var typeCounts = sortedItems.GroupBy(x => x.Item3)
                                     .Select(g => $"{g.Key}: {g.Count()}")
                                     .ToList();
        TweakLog.Message($"[BalanceTweak] pendingItems 按类型统计：{string.Join(", ", typeCounts)}");
        Mark("Def 排序与统计");

        int originalAdded = 0;

        // 细分耗时改用累加式，而不是"Start/Stop 配对的共享 Stopwatch"：
        // 后者一旦 SetDef / CreateSubItems 抛异常，Stop() 就会被跳过，把异常日志和后续 item 之间的空档
        // 全算进该类型；subItemSw 还是单个共享实例，一旦挂住会跨类型传播污染。
        // itemSw 每个 item 都 Restart()，配 finally 兜底，异常路径下读到的也只是一次 item 的耗时。
        var setDefMs = new Dictionary<SettingType, double>();
        var setDefCount = new Dictionary<SettingType, int>();
        var subItemCount = new Dictionary<SettingType, int>();
        var itemSw = new Stopwatch();
        double subItemMs = 0;

        // 建原始快照阶段：跨行读取必须锁原始层，否则快照里会记住"存档改过的值"，
        // 而 Revert / ResetTo / 强度刻度全靠它当原值基准。见 WeaponData.ResolveProjectile。
        // 用静态开关而不是 `tweaked == false` 判断：自检里的探针也会用 false 建实例（MakeProbe），
        // 那种探针要的是"读当前 def"，不该被顺手切成原始层。
        // 全量行源留给 UI。分类阶段只记了"Def + 规则"，实例到这里才按需造 ——
        // 3.4 万个 Data 对象合计 20~30MB，其中九成会被推迟，先造出来等于白发一轮 GC。
        allRows = sortedItems;
        deferredRows = [];

        // 哪些行必须现在建 —— 判据只有一条：**本轮 Apply 会读或写它的 def**。
        //   · tweakDatas 的行：Apply 会改它们的 def，快照必须抓到改前值；
        //   · Incident / Recipe：Init 里有按全量遍历的逻辑（概率重算、processUser 回填），此刻要原值。
        // 其余行推迟到窗口打开或首次 GetOriginal 时才 Factory + SetDef —— 它们只读不写，def 之后仍是原值。
        var mustBuild = new HashSet<TweakID>(tweakDatas.Count);
        foreach (var key in tweakDatas.Keys) mustBuild.Add(key);

        buildingOriginals = true;
        try
        {
            foreach (var (def, rule, settingType) in sortedItems)
            {
                var rowId = new TweakID(def.defName, settingType);
                if (!mustBuild.Contains(rowId) && !MustBuildInInit(settingType))
                {
                    deferredRows[rowId] = (def, rule);
                    continue;
                }
                TweakData data = rule.Factory();
                try
                {
                    itemSw.Restart();
                    try
                    {
                        data.SetDef(def, settingType, false);
                        originalData.Add(data.id, data);
                        originalAdded++;
                    }
                    finally
                    {
                        setDefMs[settingType] = setDefMs.GetValueOrDefault(settingType) + itemSw.Elapsed.TotalMilliseconds;
                    }
                    setDefCount[settingType] = setDefCount.GetValueOrDefault(settingType) + 1;

                    // 子项（武器招式 / 想法阶段 / 健康阶段 / 特性程度）由宿主类自己实现 ISubItemHost 产出，
                    // 这里不再为每种类型维护一段硬编码块 —— 新增子项类型只需宿主类加个方法。
                    if (data is ISubItemHost host)
                    {
                        itemSw.Restart();
                        try
                        {
                            foreach (var sub in host.CreateSubItems(data, false))
                            {
                                originalData.TryAdd(sub.id, sub);
                                subItemCount[sub.id.settingType] = subItemCount.GetValueOrDefault(sub.id.settingType) + 1;
                            }
                        }
                        finally
                        {
                            subItemMs += itemSw.Elapsed.TotalMilliseconds;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"[BalanceTweak]初始化 TweakData 时出错，def={def.defName} (类型 {settingType}): {e}");
                }
            }
        }
        finally
        {
            buildingOriginals = false;
        }

        // 武器行的 Apply 会写它关联的弹丸 def（PushWeaponEditsToProjectile），而弹丸行可能被推迟了。
        // 必须在 Apply 之前把这类"会被写的目标"补建出来 —— 之后再建，读到的就是改后的值。
        int promoted = PromoteCrossRowTargets();

        var totalMs = setDefMs.Values.Sum() + subItemMs;
        measuredMs += totalMs;
        timings.Add($"  originalData 创建 ({totalMs:F1}ms):");
        foreach (var kv in setDefMs.OrderBy(kv => kv.Key))
        {
            timings.Add($"    {kv.Key}: {kv.Value:F1}ms ({setDefCount[kv.Key]} 项)");
        }
        timings.Add($"    子项合计: {subItemMs:F1}ms（{string.Join(", ", subItemCount.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value}"))}）");
        if (deferredRows != null && deferredRows.Count > 0)
        {
            timings.Add($"    延迟到窗口打开: {deferredRows.Count} 行"
                        + (promoted > 0 ? $"（其中 {promoted} 行因被武器跨行写入已提前建）" : ""));
        }
        // 该段耗时已由上面的细分统计认领，这里只把外层表归零，免得它被算进下一个阶段。
        sw.Restart();

        if (DataUtility.MayRequire(VEF))
        {
            RecipeProcessHelper.PopulateProcessUsers();
            Mark("PopulateProcessUsers");
        }
        // 计算 IncidentData 的实际概率
        IncidentData.RecalculateFromOriginals();
        Mark("IncidentData.RecalculateFromOriginals");

        int resolvedCount = 0;
        int skipResolveCount = 0;
        int nullEntryCount = 0;
        //var skipIds = new HashSet<TweakID>();
        foreach ((var id, var data) in tweakDatas)
        {
            // 防御：数据类被改名/删除后，ScribeExtractor.SaveableFromNode 会返回 null，
            // 字典里会留下 value 为 null 的条目。
            // 不加判断会在下面的 data.ResolveDefs() 抛 NRE 并中断整个 Init，导致全部已存修改失效。
            if (data == null)
            {
                nullEntryCount++;
                continue;
            }
            if (!data.ResolveDefs())
            {
                //skipIds.Add(id);
                skipResolveCount++;
                failedResolveItems.Add(data);
                continue;
            }
            resolvedCount++;
        }
        if (nullEntryCount > 0)
            Log.Warning($"[BalanceTweak] 有 {nullEntryCount} 条已存数据的类型无法识别（数据类被改名或删除？），已跳过。");
        if (skipResolveCount > 0)
            TweakLog.Message($"[BalanceTweak] 有 {skipResolveCount} 条已存数据含无法解析的引用（相关 Mod 可能已移除），已保留原值待日后重试："
                      + string.Join(", ", failedResolveItems.Take(20).Select(d => d.id.ToString())));
        Mark($"已存数据 ResolveDefs ({resolvedCount} 解析/{skipResolveCount} 未解析)");

        // 不再有"迁移"步骤：Cleared 只由编辑器显式写入（MarkFieldCleared），旧档里"已改且空"的字段
        // 一律按 Unset 处理，无需任何转换；旧档（无 modifiedFieldNames 节点）的加载期适配由
        // TweakData.Serialization 的 backcompat 启发式完成，与初始化流程无关。

        var lazy = new List<TweakData>();
        int appliedModified = 0;
        int notFoundCount = 0;
        var drifted = new List<(TweakID oldKey, TweakData data)>();

        // 展示值定格单独计时：它做的是"逐行 EnsureDisplayValues"，成本与改过的行数成正比，
        // 而且会顺带触发 CombatPower 建装备池（武器 / 服装）。混在 Apply 里，
        // Apply 就会被算成"改一个字段要 1ms"，从而看不出真正的问题是派生计算而不是写回。
        PrepareDeferredDisplayValues();
        Mark($"Prepare 基准定格（存档修改 {tweakDatas.Count} 行；锚 + 武器刻度）");
        if (InitPerf.PoolBuilds > 0)
            timings.Add($"    其中建池 {InitPerf.PoolMs:F1}ms / {InitPerf.PoolBuilds} 次");

        // 材料配对表的刷新先登记后合并：窗口内每条材料 Apply 只记一笔，
        // 循环结束后统一刷一次表（见 DataUtility.FlushDeferredStuffRefresh）。
        DataUtility.BeginDeferredStuffRefresh();

        // 注意：必须先过滤 null 再 OrderBy —— 排序键 p.Value.LoadingOrd 本身就会解引用，
        // 把 null 判断写在循环体内是来不及的（会在 OrderBy 求键时抛 NRE）。
        foreach ((var id, var data) in tweakDatas.Where(p => p.Value != null).OrderBy(p => p.Value.LoadingOrd))
        {
            try
            {
                if (data.parentTweakId.HasValue)
                {
                    lazy.Add(data);
                    continue;
                }

                // 按规则表解析目标 Def。一个 Data 类可以有多条规则（RecipeData 同时管 RecipeDef 与
                // PipeSystem.ProcessDef；AbilityData 同时管 AbilityDef 与 VEF 的 AbilityDef），
                // 逐条尝试即可 —— 这也顺带修掉了原先靠 isVEP / isProcess 标志位选字典的缺陷：
                // 那两个标志位不参与序列化，读档后恒为 false，导致 VEF 能力与 ProcessDef 的已存修改
                // 永远走错字典、静默失效。
                if (TryApplyData(data, id, drifted))
                {
                    appliedModified++;
                }
                else
                {
                    notFoundCount++;
                    failedDirectItems.Add(data);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[BalanceTweak] 应用修改数据时出错，id={id}: {e}");
            }
        }

        if (notFoundCount > 0)
            TweakLog.Message($"[BalanceTweak] 有 {notFoundCount} 个修改项未找到对应的 Def（可能因 Mod 变动导致），已跳过。");
        Mark($"直接修改项 Apply ({appliedModified} 已应用)");

        int mergedStuffRefreshes = DataUtility.FlushDeferredStuffRefresh();
        if (mergedStuffRefreshes > 0)
            Mark($"材料配对表刷新（{mergedStuffRefreshes} 条材料改动合并为一次）");

        // Apply 会把一部分行的展示值重新置脏（等 UI 首次读时再算）。统计有多少行欠着账：
        // 这个数字越接近总数，说明 Apply 阶段里算过的派生值越多是白算的。
        InitPerf.StaleTotal = tweakDatas.Count;
        foreach (var d in tweakDatas.Values)
        {
            if (d != null && d.DisplayValuesStale) InitPerf.StaleAfterApply++;
        }

        // 分类规则调整会让旧存档里的 settingType 与新规则不一致。
        // 数据本身已按新规则正确落到对应 def 上，这里只把字典键一并归一化 —— 否则
        // TweakUtility.InitAndUpdateData 按新键查找会落空，UI 会把已修改项显示成"未修改"。
        if (drifted.Count > 0)
        {
            foreach (var (oldKey, _) in drifted)
                tweakDatas.Remove(oldKey);
            int rekeyed = 0;
            foreach (var (oldKey, data) in drifted)
            {
                if (tweakDatas.ContainsKey(data.id))
                {
                    Log.Warning($"[BalanceTweak] 分类迁移时键冲突，保留原条目：{oldKey} -> {data.id}");
                    continue;
                }
                tweakDatas[data.id] = data;
                rekeyed++;
            }
            TweakLog.Message($"[BalanceTweak] 分类漂移：{drifted.Count} 条已按当前规则重新归类，{rekeyed} 条已改挂页签。示例：{string.Join(", ", drifted.Take(10).Select(d => $"{d.oldKey} -> {d.data.id}"))}");
            Mark($"分类漂移重挂 ({rekeyed} 条改挂页签)");
        }

        int lazyApplied = 0;
        int parentNotFoundCount = 0;
        int setParentFailedCount = 0;
        int parentBrokenCount = 0;

        foreach (var data in lazy)
        {
            // 定位父项与"挂到父项上"单独一个 try：这两步里抛的异常说明父项结构与存档不符
            // （HediffStageData / ToolData 按 index 取 stage/tool，父项的阶段表变短就直接越界，
            // 越界不会走到 return false 那条路），属于"父项结构已变"，该进清理列表。
            // Apply 必须放在外面 —— 它抛异常时数据可能已经写了一半，归类成"从未生效"会误导用户。
            try
            {
                if (!tweakDatas.TryGetValue(data.parentTweakId!.Value, out var pdata))
                    TryGetOriginal(data.parentTweakId!.Value, out pdata);

                if (pdata?.def == null)
                {
                    parentNotFoundCount++;
                    failedDelayedItems.Add((data, DelayedFailCause.ParentMissing));
                    continue;
                }

                if (!data.SetParentTweak(pdata, data.id.settingType, true))
                {
                    setParentFailedCount++;
                    failedDelayedItems.Add((data, DelayedFailCause.ParentMismatch));
                    continue;
                }
            }
            catch (Exception e)
            {
                // 以前这里只打日志：条目既没应用、也不在任何一个清理列表里 —— 用户看不到、
                // 也清不掉，只能一直躺在存档里。归进「父项结构已变」至少让它可被清理。
                parentBrokenCount++;
                failedDelayedItems.Add((data, DelayedFailCause.ParentMismatch));
                Log.Error($"[BalanceTweak] 挂载延迟项到父项时出错（按父项结构已变处理），id={data.id}: {e}");
                continue;
            }

            try
            {
                // 子项（招式等）的 Display 列同样是按需计算，这里不必提前算。
                data.Apply();
                lazyApplied++;
            }
            catch (Exception e)
            {
                Log.Error($"[BalanceTweak] 应用延迟项时出错，id={data.id}: {e}");
            }
        }

        if (parentNotFoundCount > 0)
            TweakLog.Message($"[BalanceTweak] 有 {parentNotFoundCount} 个延迟项未找到父项，已跳过。");
        if (setParentFailedCount > 0)
            TweakLog.Message($"[BalanceTweak] 有 {setParentFailedCount} 个延迟项与父项对不上（阶段/招式被重排或改名），已跳过。");
        if (parentBrokenCount > 0)
            TweakLog.Message($"[BalanceTweak] 有 {parentBrokenCount} 个延迟项挂载到父项时抛异常（父项结构变化），已跳过。");
        Mark($"延迟修改项 Apply ({lazyApplied} 已应用)");

        // 上游错误优先：目标 Def / 父项都找不到的条目，不再重复计入「内部解析失败」。
        // 同一条数据同时出现在两个清理列表里，计数会翻倍，更麻烦的是会让「点下去会回滚」的说明失真 ——
        // 目标 Def 都没了，根本没有可回滚的对象。一条数据只在一个桶里报，报最上游的那个错。
        // 放在这里是因为三个桶这时才全部填好（延迟项要等父项解析完）。
        // 按 id 比对而不是按实例：两个桶里的引用本来就是同一条 tweakDatas 条目，但 id 是它们的身份定义，
        // 拿 id 判定才不会因为将来某处存了克隆而悄悄失效。
        HashSet<TweakID> upstream = new();
        foreach (var d in failedDirectItems) upstream.Add(d.id);
        foreach (var (d, _) in failedDelayedItems) upstream.Add(d.id);
        int reclassified = upstream.Count == 0 ? 0 : failedResolveItems.RemoveAll(d => upstream.Contains(d.id));
        if (reclassified > 0)
            TweakLog.Message($"[BalanceTweak] 有 {reclassified} 条数据的目标 Def/父项已不存在，已归入上游错误，不再计入内部引用解析失败。");

        Mark("上游错误归类");

        // 研究解锁缓存必须在两轮 Apply 之后再打回一次，理由是时序：
        // 加载期 CompProperties_Techprint.ResolveReferences 就调过 project.UnlockedDefs（拼物品描述），
        // 那时 def 还是 XML 原值；而我们的写入在 StaticConstructorOnStartup 之后才发生。
        // 于是"改过解锁关系、但没改任何研究项字段"的存档，原版那份缓存会一直是改前的关系 ——
        // 那种情况下 PrepareDeferredDisplayValues 不会触发索引重建（它只遍历有修改的行），
        // 也就没人替我们清。这里一次性全清（N 次反射写），把运行期第一次读交给懒加载重算。
        // 顺带丢掉索引本身：换存档/热重载后静态缓存里的 Def 实例可能已经作废。
        ResearchUnlocks.Invalidate();
        ResearchUnlocks.ClearAllVanillaCaches();
        Mark($"清空研究项原版缓存（{DefDatabase<ResearchProjectDef>.DefCount} 项）");

        // 中继 Comp 的运行时缓存（带宽 / 指挥范围 / 平方距离）：两轮 Apply 结束后统一补刷一次。
        // 读档期地图还没建好时 Flush 会直接丢弃 —— 那时新建的 Comp 会按新 Props 初始化。
        FortifiedRelay.Flush();
        Mark("FortifiedRelay.Flush");

        // C3：运行期自检（SC1–SC6）。Debug 构建里强制开启；发布版默认关闭（零开销）。
        TweakSelfCheck.RunAll();
        Mark("TweakSelfCheck.RunAll");

        // 各阶段之和若小于总计，差额就是没被任何阶段认领的开销 —— 显式打出来，免得"账对不上"却无从判断。
        //
        // 收口必须放在上面三步之后：以前 totalSw.Stop() 在清缓存 / 刷新 / 自检之前就停表了，
        // 那三步（研究缓存清理是 831 项 × 3 次反射写，自检在 Debug 版强制开启）从来没进过"总计"——
        // 报告看着 2.1s，真实启动开销比它高。
        InitPerf.Finish();
        totalSw.Stop();
        double elapsedMs = totalSw.Elapsed.TotalMilliseconds;
        timings.Add($"  未归属: {elapsedMs - measuredMs:F1}ms");
        InitPerf.AppendReport(timings);

        var report = string.Join("\n", timings);
        TweakLog.Message($"[BalanceTweak] ===== 初始化耗时报告 =====\n{report}\n总计: {elapsedMs:F1}ms");
    }

    /// <summary>
    /// 在 Apply 之前定格"跨行取数的基准"—— 只有基准，不是每一行的展示值。
    /// <para>
    /// 展示值（DPS / 战斗力 / 强度）都改成 UI 首次读时按当前 def 现算了，所以快照行不需要预热：
    /// 重置走 clone 快照 → ApplyAuthoritative → EnsureDisplayValues，克隆体重算，值一定是对的。
    /// </para>
    /// <para>
    /// 还需要提前算的只剩两处刻度，它们读的是"改动前"的 def，Apply 之后就算不出来了：
    /// 战斗力锚（<see cref="CombatPower.EnsureAnchors"/>）与武器强度基准
    /// （<see cref="WeaponData.FreezePowerBaseline"/>）。成本恒定，与存档里改了多少行无关。
    /// </para>
    /// </summary>
    /// <summary>
    /// Init 期间存在"按全量遍历快照"的逻辑，因此这些页签不能延迟：
    /// Incident（<see cref="IncidentData.RecalculateFromOriginals"/> 要遍历全部行重算概率）、
    /// Recipe（<see cref="RecipeProcessHelper.PopulateProcessUsers"/> 要给每一行回填 processUser）。
    /// <para>
    /// <b>新增一处按全量遍历的 Init 逻辑时，记得把对应页签加进来</b> —— 否则它只会看到一半的行，
    /// 而且不会报错，表现是"某些项的概率 / 显示值悄悄没算"。
    /// </para>
    /// </summary>
    private static bool MustBuildInInit(SettingType type)
        => type is SettingType.Incident or SettingType.Recipe;

    /// <summary>
    /// 把"会被其它行写 def"的目标提前建快照。目前只有一处：武器的 Apply 会把伤害 / 穿透推进
    /// 关联弹丸的 def（<c>PushWeaponEditsToProjectile</c>）。这些目标如果被推迟，之后再补建就会
    /// 读到"已被武器改过"的值 —— 重置时会还原成改后值，且没有任何报错。
    /// </summary>
    private static int PromoteCrossRowTargets()
    {
        int before = deferredBuilt;
        // 先复制一份：GetOriginal 会往 originalData 里加行，直接遍历 Values 会抛"集合已修改"。
        // 目标从 <c>def.Verbs[].defaultProjectile</c> 现读，不用 <c>WeaponData.Projectile</c> ——
        // 那个字段要到 GenValues / ResolveProjectile 才填，此刻可能还是 null。
        foreach (var data in originalData.Values.ToList())
        {
            if (data is not WeaponData { def: ThingDef d } || d.Verbs.NullOrEmpty()) continue;
            for (int i = 0; i < d.Verbs.Count; i++)
            {
                var projectile = d.Verbs[i]?.defaultProjectile;
                if (projectile != null)
                    GetOriginal(new TweakID(projectile.defName, SettingType.Projectile));
            }
        }
        return deferredBuilt - before;
    }

    private static int PrepareDeferredDisplayValues()
    {
        if (tweakDatas.Count == 0) return 0;

        // 只定格"跨行取数基准"，不再逐行预热展示值。
        //
        // 展示值（DPS / 战斗力 / 强度）是 UI 的派生列：活行在 UI 首次读时按当前 def 现算，
        // 那时 def 已是最终值，与在 Init 里算完全一致。而快照行预先算过的那份更没有消费者 ——
        // 重置路径（RemoveData / DropFailedItem）都是 clone 快照 → ApplyAuthoritative → EnsureDisplayValues，
        // 克隆体的 displayValuesEnsured 被 Clone 置 false，会在 def 已还原的状态下重算。
        // 原来这里按"存档里改过的每一行"跑一遍 EnsureDisplayValues，实测 957 行里实算 576 行、
        // 633ms —— 全部是白算。
        //
        // 真正必须在 Apply 之前定格的是两处基准，否则 Apply 改完 def 之后刻度会跟着漂：
        //   · 战斗力锚（动物 / 机械兵种的攻防）—— EnsureAnchors 直接读 def 算；
        //   · 武器强度刻度（电荷步枪基准 + 近战分母 RDPS / HumanDPS）。
        // 两者都与"改了多少行"无关，各算一次即可。
        CombatPower.EnsureAnchors();
        WeaponData.FreezePowerBaseline();
        return 2;
    }

    /// <summary>集合类字段（List&lt;&gt; / ThingFilter）：Cleared 的语义是"移除全部项"（写空集合，不写 null）；其余是"还原原值"。</summary>
    internal static bool IsCollectionColumn(StatColumnConfig col)
        => col.isListStyle || col.columnType == ColumnStyle.IngredientFilter;

    public static ApparelData? GetApparelDataFromName(string defName)
    {
        var id = new TweakID(defName, SettingType.Apparel);
        return TryGetOriginal(id, out var data) ? data as ApparelData : null;
    }

    public static WeaponData? GetWeaponDataFromName(string defName)
    {
        var id = new TweakID(defName, SettingType.Weapon);
        return TryGetOriginal(id, out var data) ? data as WeaponData : null;
    }

    private static void InitAllMeleeDamageDef()
    {
        // 没有 MayRequire 常量（这个 Mod 没有登记 packageId），退回"程序集短名"判定：
        // 不在就静默跳过，在却拿不到类型则由 Diagnostics 记一条失配 —— 否则 VCT 更新改名后
        // APNerf 永远读到 false，DPS 换算静默换了一套口径。
        var t = DataUtility.OptionalType("VanillaCombatTweaks.VCTModSettings", null, nameof(InitAllMeleeDamageDef));
        if (t != null)
        {
            var apn = AccessTools.Field(t, "APNerf");
            if (apn?.GetValue(null) is bool apDown) VCTAPDown = apDown;
            var arn = AccessTools.Field(t, "ArmorUnlimitNerf");
            if (arn?.GetValue(null) is bool amDown) VCTAMDown = amDown;
        }
        foreach (var m in DefDatabase<ManeuverDef>.AllDefsListForReading)
        {
            if (m.requiredCapacity != null && m.verb != null)
                allMeleeVerb.TryAdd(m.requiredCapacity, m.verb);
        }
    }
}