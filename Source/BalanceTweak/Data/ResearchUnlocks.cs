using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 「这个研究项目解锁了哪些 Def」的反查索引。
///
/// <para>
/// <b>为什么需要它，而不是直接用 <see cref="ResearchProjectDef.UnlockedDefs"/>：</b>
/// 原版那个 getter（<c>Verse/ResearchProjectDef.cs</c>）做了两件我们不要的事 ——
/// ① 把配方的 <c>products</c> 摊平成产物，**载体身份**（是哪条配方解锁了它）就此丢失，而"改解锁"
/// 最终必须落到载体上；② 结果缓存在私有字段 <c>cachedUnlockedDefs</c> 里且永不失效，改完 Def 它就过期。
/// 所以这里按同一份判定重扫一遍，保留载体，并在重建时负责把原版那份过期缓存清掉。
/// </para>
///
/// <para>
/// <b>性能：</b>建表是<b>一次正向扫描</b>（遍历各 Def 表、把关系挂到对应项目上），不是"每个项目扫一遍全库"。
/// 生命周期 = 一次 <c>TweakUtility.UpdateFilters</c>（见那里的 Invalidate 调用），
/// 单行查询是字典命中 + 一次列表读取，绘制期零查询。
/// </para>
///
/// <para>
/// <b>覆盖范围严格对齐原版 6 条来源</b>（多一条会让本列与研究台对不上，少一条会漏）：
/// 配方（单值 / 列表）的产物、配方的列表前置且是手术（登记配方本身）、ThingDef 前置、
/// 植物播种前置、地形前置、心能仪式前置。
/// </para>
/// </summary>
internal static class ResearchUnlocks
{
    /// <summary>这条解锁关系挂在载体 Def 的哪个槽位上（第二步的写回要按它选字段）。</summary>
    internal enum RelationSlot
    {
        /// <summary>列表槽位：<c>researchPrerequisites</c> / <c>sowResearchPrerequisites</c>。</summary>
        List,
        /// <summary>单值槽位：<c>researchPrerequisite</c>（与列表是 AND 关系，两边独立判定）。</summary>
        Single,
    }

    /// <summary>一条解锁关系：<paramref name="carrier"/> 上写着「需要 <paramref name="display"/> 所对应的研究」。</summary>
    internal readonly struct Entry
    {
        /// <summary>承载关系的 Def（配方 / 物品 / 地形 / 仪式）。移除时要动的是它。</summary>
        public readonly Def carrier;
        /// <summary>展示用的 Def（配方的产物，或载体自身）。</summary>
        public readonly Def display;
        public readonly RelationSlot slot;

        public Entry(Def carrier, Def display, RelationSlot slot)
        {
            this.carrier = carrier;
            this.display = display;
            this.slot = slot;
        }
    }

    private static Dictionary<ResearchProjectDef, List<Entry>>? cache;

    /// <summary>
    /// 上一次建成的表，<b>只用于 diff</b>（判断哪些项目的解锁集合真的变了，只清它们的原版缓存）。
    /// 与 <see cref="cache"/> 分开是因为 <see cref="Invalidate"/> 只丢查询侧 —— 若连它也置空，
    /// 每次重建都会退化成"全清"，diff 永远没有基准。
    /// </summary>
    private static Dictionary<ResearchProjectDef, List<Entry>>? previousCache;

    private static readonly List<Entry> Empty = new();

    /// <summary>原版两个懒加载缓存的字段（private，只能反射清）。</summary>
    private static readonly FieldInfo? UnlockedDefsField = AccessTools.Field(typeof(ResearchProjectDef), "cachedUnlockedDefs");
    private static readonly FieldInfo? HyperlinksField = AccessTools.Field(typeof(ResearchProjectDef), "cachedHyperlinks");

    /// <summary>丢弃索引。下一次 <see cref="Get"/> 会重建。</summary>
    internal static void Invalidate() => cache = null;

    /// <summary>承载"研究解锁"关系的三个字段名（建筑/地形/配方列表、植物播种、配方单值）。</summary>
    private static readonly string[] PrereqFieldNames =
    {
        "researchPrerequisites",
        "sowResearchPrerequisites",
        "researchPrerequisite",
    };

    /// <summary>
    /// 编辑器改了某个字段之后调。命中上面三个字段名才动作 —— 这是一次编辑里唯一需要知道
    /// "这格改了会不会影响解锁关系"的地方，写在这里就不必在每个 Data 类里各挂一次。
    /// </summary>
    internal static void OnFieldEdited(string fieldName)
    {
        for (int i = 0; i < PrereqFieldNames.Length; i++)
        {
            if (PrereqFieldNames[i] != fieldName) continue;
            Invalidate();
            ClearAllVanillaCaches();
            return;
        }
    }

    /// <summary>
    /// 清掉全部研究项目的原版解锁缓存。<b>只是置 null，不触发重算</b> —— 原版那两个 getter 是懒的，
    /// 谁来读谁付扫描成本，因此这里的成本恒为 N 次反射写（N = 研究项目数，几百量级）。
    /// </summary>
    internal static void ClearAllVanillaCaches()
    {
        if (UnlockedDefsField == null && HyperlinksField == null) return;
        var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
        for (int i = 0; i < all.Count; i++) ClearOne(all[i]);
    }

    /// <summary>取某个研究项目解锁的条目。没有解锁任何东西时返回空列表（非 null）。</summary>
    internal static List<Entry> Get(ResearchProjectDef project)
        => project != null && (cache ??= Build()).TryGetValue(project, out var list) ? list : Empty;

    private static Dictionary<ResearchProjectDef, List<Entry>> Build()
    {
        var previous = previousCache;
        var map = new Dictionary<ResearchProjectDef, List<Entry>>();

        void Add(ResearchProjectDef project, Def carrier, Def? display, RelationSlot slot)
        {
            if (project == null || carrier == null || display == null) return;
            if (!map.TryGetValue(project, out var list))
                map[project] = list = new List<Entry>();
            // 同一 (载体, 显示) 只留一条：产物可能同时被列表前置与手术前置命中，
            // 而原版最后会 Distinct，多留会让本列的计数比研究台多。
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i].carrier, carrier) && ReferenceEquals(list[i].display, display)) return;
            list.Add(new Entry(carrier, display, slot));
        }

        void AddRecipeProducts(RecipeDef recipe, ResearchProjectDef project, RelationSlot slot)
        {
            if (recipe.products == null) return;
            for (int i = 0; i < recipe.products.Count; i++)
                Add(project, recipe, recipe.products[i]?.thingDef, slot);
        }

        // —— 配方 ——（原版：先按"单值或列表前置命中"取 products，再把手术配方本身登记一次）
        var recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            if (recipe.researchPrerequisite != null)
                AddRecipeProducts(recipe, recipe.researchPrerequisite, RelationSlot.Single);

            var list = recipe.researchPrerequisites;
            if (list != null)
            {
                for (int j = 0; j < list.Count; j++)
                    AddRecipeProducts(recipe, list[j], RelationSlot.List);
                // 手术配方本身也进解锁清单（它们通常没有 products，上面那条取不到东西）。
                if (recipe.IsSurgery)
                    for (int j = 0; j < list.Count; j++)
                        Add(list[j], recipe, recipe, RelationSlot.List);
            }
        }

        // —— ThingDef：建造/制作前置 ——
        var things = DefDatabase<ThingDef>.AllDefsListForReading;
        for (int i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            var prereqs = thing.researchPrerequisites;
            if (prereqs != null)
                for (int j = 0; j < prereqs.Count; j++)
                    Add(prereqs[j], thing, thing, RelationSlot.List);

            // —— ThingDef：植物播种前置（"能种了"是另一条槽位，移除时必须一并清，否则删不干净）——
            var sow = thing.plant?.sowResearchPrerequisites;
            if (sow != null)
                for (int j = 0; j < sow.Count; j++)
                    Add(sow[j], thing, thing, RelationSlot.List);
        }

        // —— TerrainDef ——
        var terrains = DefDatabase<TerrainDef>.AllDefsListForReading;
        for (int i = 0; i < terrains.Count; i++)
        {
            var terrain = terrains[i];
            var prereqs = terrain.researchPrerequisites;
            if (prereqs != null)
                for (int j = 0; j < prereqs.Count; j++)
                    Add(prereqs[j], terrain, terrain, RelationSlot.List);
        }

        // —— 心能仪式（Anomaly；未装 DLC 时这张表是空的，类型本身在原版程序集里）——
        var rituals = DefDatabase<PsychicRitualDef>.AllDefsListForReading;
        for (int i = 0; i < rituals.Count; i++)
        {
            var ritual = rituals[i];
            if (ritual.researchPrerequisite != null)
                Add(ritual.researchPrerequisite, ritual, ritual, RelationSlot.Single);
        }

        ClearVanillaCaches(previous, map);
        previousCache = map;
        cache = map;
        return map;
    }

    /// <summary>
    /// 把原版那两个私有缓存清掉。<b>只清"解锁集合真的变了"的项目</b>——全清会让研究台下次打开时
    /// 为每个项目重扫一遍全库（几百个项目 × 几千个 Def），而这些项目根本没被这次编辑碰到。
    /// </summary>
    private static void ClearVanillaCaches(Dictionary<ResearchProjectDef, List<Entry>>? previous,
        Dictionary<ResearchProjectDef, List<Entry>> current)
    {
        if (UnlockedDefsField == null && HyperlinksField == null) return;

        if (previous == null)
        {
            // 首次建表：不知道原版缓存是按哪一版 Def 算的（玩家进游戏就点开过研究台），全清一次最稳。
            ClearAllVanillaCaches();
            return;
        }

        foreach (var kv in current)
            if (!previous.TryGetValue(kv.Key, out var old) || !SameEntries(old, kv.Value))
                ClearOne(kv.Key);

        foreach (var kv in previous)
            if (!current.ContainsKey(kv.Key)) ClearOne(kv.Key);
    }

    private static bool SameEntries(List<Entry> a, List<Entry> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!ReferenceEquals(a[i].carrier, b[i].carrier) || !ReferenceEquals(a[i].display, b[i].display))
                return false;
        return true;
    }

    private static void ClearOne(ResearchProjectDef project)
    {
        try
        {
            UnlockedDefsField?.SetValue(project, null);
            HyperlinksField?.SetValue(project, null);
        }
        catch (Exception e)
        {
            // 原版字段改名/移位：宁可这列显示旧值，也不要打断整次重建。
            Log.Warning($"[BalanceTweak] 清空研究解锁缓存失败 {project.defName}: {e.Message}");
        }
    }
}
