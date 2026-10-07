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
using static BalanceTweak.TweakData;

namespace BalanceTweak;

[StaticConstructorOnStartup]
public static partial class TweakDatabase
{
    private static Dictionary<TweakID, TweakData> originalData = [];

    /// <summary>获取原始数据</summary>
    public static TweakData? GetOriginal(TweakID id) => originalData.GetValueOrDefault(id);
    /// <summary>尝试获取原始数据</summary>
    public static bool TryGetOriginal(TweakID id, out TweakData? data) => originalData.TryGetValue(id, out data);
    /// <summary>尝试获取原始数据的克隆</summary>
    public static TweakData? CloneOriginal(TweakID id)
        => originalData.TryGetValue(id, out var data) ? data.Clone() : null;
    /// <summary>遍历所有原始数据</summary>
    public static Dictionary<TweakID, TweakData> AllOriginals => originalData;
    public static Dictionary<string, TweakID> turretBuildingDatas = [];
    public static Dictionary<string, TweakID> projectileDatas = [];
    public static Dictionary<string, TweakID> raceDatas = [];

    // 记录失败的修改项（供 UI 清理使用）
    public static List<TweakData> failedResolveItems = new();
    public static List<TweakData> failedDirectItems = new();
    public static List<TweakData> failedDelayedItems = new();

    public static Dictionary<ToolCapacityDef, VerbProperties> allMeleeVerb = [];
    public const string CE = "ceteam.combatextended";
    public const string VEF = "oskarpotocki.vanillafactionsexpanded.core";
    public const string VEP = "vanillaexpanded.vpsycastse";
    public static bool VCTAPDown = false;
    public static bool VCTAMDown = false;

    static TweakDatabase()
    {
        //Init();
        LongEventHandler.QueueLongEvent(Init, "BalanceTweak initializing", doAsynchronously: false, null);
    }

    #region 规则驱动的分类与派发

    /// <summary>分类结果（含诊断用的统计）。</summary>
    private sealed class ClassifyResult
    {
        public readonly List<(Def, TweakData, SettingType)> Items = new();
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

                        res.Items.Add((def, rule.Factory(), rule.Tab));
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

                    res.Items.Add((def, fallback.Factory(), fallback.Tab));
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

            data.SetDef(def, rule.Tab, true);
            data.Apply();
            return true;
        }
        return false;
    }

    #endregion

    private static void Init()
    {
        var sw = Stopwatch.StartNew();
        var totalSw = Stopwatch.StartNew();
        var timings = new List<string>();

        // 清除上次记录
        failedResolveItems.Clear();
        failedDirectItems.Clear();
        failedDelayedItems.Clear();

        InitAllMeleeDamageDef();
        timings.Add($"  InitAllMeleeDamageDef: {sw.Elapsed.TotalMilliseconds:F1}ms");

        var pendingItems = new List<(Def, TweakData, SettingType)>();
        int thingDefCount = DefDatabase<ThingDef>.DefCount;
        Log.Message($"[BalanceTweak] Def 统计：ThingDef={thingDefCount}, PawnKindDef={DefDatabase<PawnKindDef>.DefCount}, AbilityDef={DefDatabase<AbilityDef>.DefCount}, ThoughtDef={DefDatabase<ThoughtDef>.DefCount}, GeneDef={DefDatabase<GeneDef>.DefCount}, MemeDef={DefDatabase<MemeDef>.DefCount}, IncidentDef={DefDatabase<IncidentDef>.DefCount}, ResearchProjectDef={DefDatabase<ResearchProjectDef>.DefCount}, FactionDef={DefDatabase<FactionDef>.DefCount}, HediffDef={DefDatabase<HediffDef>.DefCount}, TraitDef={DefDatabase<TraitDef>.DefCount}, TerrainDef={DefDatabase<TerrainDef>.DefCount}");
        sw.Restart();

        // 分类完全由 TweakRegistry 的规则表驱动（规则来自各 Data 类上的 [TweakFor]），
        // 取代原先的 7 分支 if/else-if 链 + 一排 ProcessDefs<T>() 调用 + 两个跨 Mod 专用方法。
        var classify = ClassifyAll();
        pendingItems.AddRange(classify.Items);
        classify.ByDefType.TryGetValue(typeof(ThingDef), out int thingProcessed);
        timings.Add($"  规则分类: {sw.Elapsed.TotalMilliseconds:F1}ms（ThingDef {thingProcessed} 个，兜底 {classify.Fallback} 个）");
        //Log.Message($"[BalanceTweak] ThingDef 覆盖：总数 {thingProcessed}，规则分类 {thingProcessed - classify.Fallback - classify.Uncovered}，兜底 Misc {classify.Fallback}，放弃 {classify.Uncovered}（无编辑价值，不建实例）");
        //Log.Message($"[BalanceTweak] 兜底 Misc 按 ThingCategory：{string.Join(", ", classify.FallbackByCategory.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}"))}");
        //if (classify.FallbackSamples.Count > 0)
        //    Log.Message($"[BalanceTweak] 兜底 Misc 示例（最多 30）：{string.Join(", ", classify.FallbackSamples)}");
        //if (classify.UncoveredSamples.Count > 0)
        //    Log.Message($"[BalanceTweak] 放弃的 ThingDef 示例（最多 30）：{string.Join(", ", classify.UncoveredSamples)}");
        if (classify.MultiMatched.Count > 0)
            Log.Message($"[BalanceTweak] 并列挂载：{classify.MultiMatched.Count} 条（{string.Join(", ", classify.MultiMatched.Take(20))}）");
        var sortedItems = pendingItems.OrderBy(item => item.Item2.LoadingOrd).ToList();

        var typeCounts = sortedItems.GroupBy(x => x.Item3)
                                     .Select(g => $"{g.Key}: {g.Count()}")
                                     .ToList();
        Log.Message($"[BalanceTweak] pendingItems 按类型统计：{string.Join(", ", typeCounts)}");
        sw.Restart();

        int originalAdded = 0;

        var setDefSw = new Dictionary<SettingType, Stopwatch>();
        var setDefCount = new Dictionary<SettingType, int>();
        var subItemCount = new Dictionary<SettingType, int>();
        var subItemSw = new Stopwatch();

        foreach (var (def, data, settingType) in sortedItems)
        {
            try
            {
                if (!setDefSw.TryGetValue(settingType, out var stSw))
                {
                    stSw = new Stopwatch();
                    setDefSw[settingType] = stSw;
                }
                stSw.Start();
                data.SetDef(def, settingType, false);
                originalData.Add(data.id, data);
                originalAdded++;
                stSw.Stop();
                setDefCount.TryGetValue(settingType, out var cnt);
                setDefCount[settingType] = cnt + 1;

                // 子项（武器招式 / 想法阶段 / 健康阶段 / 特性程度）由宿主类自己实现 ISubItemHost 产出，
                // 这里不再为每种类型维护一段硬编码块 —— 新增子项类型只需宿主类加个方法。
                if (data is ISubItemHost host)
                {
                    subItemSw.Start();
                    foreach (var sub in host.CreateSubItems(data, false))
                    {
                        originalData.TryAdd(sub.id, sub);
                        subItemCount.TryGetValue(sub.id.settingType, out var sCnt);
                        subItemCount[sub.id.settingType] = sCnt + 1;
                    }
                    subItemSw.Stop();
                }
            }
            catch (Exception e)
            {
                Log.Error($"[BalanceTweak]初始化 TweakData 时出错，def={def.defName} (类型 {settingType}): {e}");
            }
        }

        var subItemMs = subItemSw.Elapsed.TotalMilliseconds;
        var setDefMs = setDefSw.Values.Sum(sw => sw.Elapsed.TotalMilliseconds);
        var totalMs = setDefMs + subItemMs;
        timings.Add($"  originalData 创建 ({totalMs:F1}ms):");
        foreach (var kv in setDefSw.OrderBy(kv => kv.Key))
        {
            timings.Add($"    {kv.Key}: {kv.Value.Elapsed.TotalMilliseconds:F1}ms ({setDefCount[kv.Key]} 项)");
        }
        timings.Add($"    子项合计: {subItemMs:F1}ms（{string.Join(", ", subItemCount.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value}"))}）");

        if (DataUtility.MayRequire(VEF))
        {
            sw.Restart();
            RecipeProcessHelper.PopulateProcessUsers();
            timings.Add($"  PopulateProcessUsers: {sw.Elapsed.TotalMilliseconds:F1}ms");
        }
        sw.Restart();
        // 计算 IncidentData 的实际概率
        IncidentData.RecalculateFromOriginals();
        timings.Add($"  IncidentData.RecalculateFromOriginals: {sw.Elapsed.TotalMilliseconds:F1}ms");
        sw.Restart();

        int resolvedCount = 0;
        int skipResolveCount = 0;
        int nullEntryCount = 0;
        //var skipIds = new HashSet<TweakID>();
        foreach ((var id, var data) in tweakDatas)
        {
            // 防御：数据类被改名/删除后，ScribeExtractor.SaveableFromNode 会返回 null，
            // 字典里会留下 value 为 null 的条目（见 Docs/ThingDefCoverage_Design.md §10.3）。
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
        timings.Add($"  已存数据 ResolveDefs: {sw.Elapsed.TotalMilliseconds:F1}ms ({resolvedCount} 解析/{skipResolveCount} 未解析)");
        if (nullEntryCount > 0)
            Log.Warning($"[BalanceTweak] 有 {nullEntryCount} 条已存数据的类型无法识别（数据类被改名或删除？），已跳过。");
        sw.Restart();

        var lazy = new List<TweakData>();
        int appliedModified = 0;
        int notFoundCount = 0;
        var drifted = new List<(TweakID oldKey, TweakData data)>();

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

        // 分类规则调整会让旧存档里的 settingType 与新规则不一致（见 Docs/ThingDefCoverage_Design.md §11.2）。
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
            Log.Message($"[BalanceTweak] 分类漂移：{drifted.Count} 条已按当前规则重新归类，{rekeyed} 条已改挂页签。示例：{string.Join(", ", drifted.Take(10).Select(d => $"{d.oldKey} -> {d.data.id}"))}");
        }

        if (notFoundCount > 0)
            Log.Message($"[BalanceTweak] 有 {notFoundCount} 个修改项未找到对应的 Def（可能因 Mod 变动导致），已跳过。");
        timings.Add($"  直接修改项 Apply: {sw.Elapsed.TotalMilliseconds:F1}ms ({appliedModified} 已应用)");
        sw.Restart();

        int lazyApplied = 0;
        int parentNotFoundCount = 0;
        int setParentFailedCount = 0;

        foreach (var data in lazy)
        {
            try
            {
                if (!tweakDatas.TryGetValue(data.parentTweakId!.Value, out var pdata))
                    TryGetOriginal(data.parentTweakId!.Value, out pdata);

                if (pdata?.def == null)
                {
                    parentNotFoundCount++;
                    failedDelayedItems.Add(data);
                    continue;
                }

                if (!data.SetParentTweak(pdata, data.id.settingType, true))
                {
                    setParentFailedCount++;
                    failedDelayedItems.Add(data);
                    continue;
                }
                data.Apply();
                lazyApplied++;
            }
            catch (Exception e)
            {
                Log.Error($"[BalanceTweak] 应用延迟项时出错，id={data.id}: {e}");
            }
        }

        if (parentNotFoundCount > 0)
            Log.Message($"[BalanceTweak] 有 {parentNotFoundCount} 个延迟项未找到父项，已跳过。");
        if (setParentFailedCount > 0)
            Log.Message($"[BalanceTweak] 有 {setParentFailedCount} 个延迟项设置父项失败，已跳过。");
        timings.Add($"  延迟修改项 Apply: {sw.Elapsed.TotalMilliseconds:F1}ms ({lazyApplied} 已应用)");

        var report = string.Join("\n", timings);
        Log.Message($"[BalanceTweak] ===== 初始化耗时报告 =====\n{report}\n总计: {totalSw.Elapsed.TotalMilliseconds:F1}ms");
    }

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
        var t = AccessTools.TypeByName("VanillaCombatTweaks.VCTModSettings");
        if (t != null)
        {
            var apn = AccessTools.Field(t, "APNerf");
            if (apn != null) VCTAPDown = (bool)apn.GetValue(null);
            var arn = AccessTools.Field(t, "ArmorUnlimitNerf");
            if (arn != null) VCTAMDown = (bool)arn.GetValue(null);
        }
        foreach (var m in DefDatabase<ManeuverDef>.AllDefsListForReading)
        {
            if (m.requiredCapacity != null && m.verb != null)
                allMeleeVerb.TryAdd(m.requiredCapacity, m.verb);
        }
    }
}