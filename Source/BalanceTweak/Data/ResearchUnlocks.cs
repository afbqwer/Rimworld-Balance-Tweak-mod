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
/// 所以这里按同一份判定重扫一遍，保留载体，并负责在 Def 变化后把原版那份过期缓存清掉。
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
///
/// <para>
/// <b>原版缓存的失效策略（<see cref="Capture"/> / <see cref="Commit"/>）：</b>
/// 一次编辑只影响"这个载体 Def 三个前置槽位上写着的项目"——改前 ∪ 改后，通常 1~2 个。
/// 所以在 <c>Apply</c> 前后各读一次槽位、取并集、只清这几个项目的缓存：成本与前置列表长度成正比，
/// 与全库规模无关。<b>别改成"重建索引来 diff"或"全清"</b>：
/// 前者每次编辑一次全库扫描（拖拽涂抹 50 格就是 50~250ms），后者会把研究台打回冷启动
/// （<c>cachedUnlockedDefs</c> 是懒加载、永不失效，一旦被清，研究台下次要逐个重扫全库）。
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

    private static readonly List<Entry> Empty = new();

    /// <summary>原版三个懒加载缓存的字段（private，只能反射清）。</summary>
    private static readonly FieldInfo? UnlockedDefsField = AccessTools.Field(typeof(ResearchProjectDef), "cachedUnlockedDefs");
    private static readonly FieldInfo? HyperlinksField = AccessTools.Field(typeof(ResearchProjectDef), "cachedHyperlinks");
    /// <summary>
    /// <c>GetTip()</c> 的文案缓存：把 techprint 名称、requiredAnalyzed 列表一次性拼好后存住。
    /// 它不在 <c>ClearCachedData</c> 的覆盖范围内，改那几个字段后研究项 tooltip 会一直是旧文本。
    /// </summary>
    private static readonly FieldInfo? DescriptionField = AccessTools.Field(typeof(ResearchProjectDef), "cachedDescription");

    /// <summary>丢弃索引。下一次 <see cref="Get"/> 会重建。</summary>
    internal static void Invalidate() => cache = null;

    /// <summary>取某个研究项目解锁的条目。没有解锁任何东西时返回空列表（非 null）。</summary>
    internal static List<Entry> Get(ResearchProjectDef project)
        => project != null && (cache ??= Build()).TryGetValue(project, out var list) ? list : Empty;

    #region 原版缓存失效

    /// <summary>
    /// 这个 Def 身上是否可能挂着"研究前置"。只有这四类才需要走 Capture/Commit ——
    /// 其余 Def（武器、服饰、特性……）的 Apply 不该为一次缓存失效付任何成本。
    /// </summary>
    private static bool IsCarrier(Def? def) => def is ThingDef or TerrainDef or RecipeDef or PsychicRitualDef;

    /// <summary>
    /// 采集 carrier 上<b>此刻（写入前）</b>挂着的项目集合。非载体 Def、以及原本就没挂任何前置的 Def
    /// 都返回 null —— 调用方据此跳过整段逻辑。后者是关键：`ApplyFullReset` 会把全部 `ThingDef`
    /// 逐个 `ApplyAuthoritative` 一遍（几千个），绝大多数与解锁关系无关，不该为它们各分配一个列表。
    /// </summary>
    internal static List<ResearchProjectDef>? Capture(Def? def)
    {
        if (!IsCarrier(def) || !HasAnyPrerequisite(def!)) return null;
        var list = new List<ResearchProjectDef>(4);
        CollectPrerequisites(list, def!);
        return list;
    }

    /// <summary>
    /// carrier 写入后调用：把"写入后"的项目并进 <paramref name="before"/>（去重），
    /// 再逐个项目清原版缓存、丢弃索引。
    ///
    /// <para>
    /// 取<b>并集</b>而不是只看写入后的集合：被移出前置的那个项目，它的解锁列表里也少了一条，
    /// 只清新集合会让"刚被解除前置"的项目一直显示着旧关系（而且是研究台里最容易被看到的那一条）。
    /// </para>
    /// <para>
    /// <paramref name="before"/> 为 null 有两种来源（非载体 / 改前没有前置），只有后者需要在这里
    /// 补读一次"改后"—— 改前改后都没有前置就整段跳过，这是绝大多数编辑的实际路径。
    /// </para>
    /// </summary>
    internal static void Commit(Def? def, List<ResearchProjectDef>? before)
    {
        if (def == null || !IsCarrier(def)) return;

        if (before == null)
        {
            if (!HasAnyPrerequisite(def)) return;
            before = new List<ResearchProjectDef>(4);
        }
        CollectPrerequisites(before, def);

        for (int i = 0; i < before.Count; i++) ClearOne(before[i]);
        cache = null;
    }

    /// <summary>def 上是否挂着任何研究前置（只看引用，不建列表）。</summary>
    private static bool HasAnyPrerequisite(Def def) => def switch
    {
        RecipeDef recipe => recipe.researchPrerequisite != null
                            || (recipe.researchPrerequisites?.Count ?? 0) > 0,
        ThingDef thing => (thing.researchPrerequisites?.Count ?? 0) > 0
                          || (thing.plant?.sowResearchPrerequisites?.Count ?? 0) > 0,
        TerrainDef terrain => (terrain.researchPrerequisites?.Count ?? 0) > 0,
        PsychicRitualDef ritual => ritual.researchPrerequisite != null,
        _ => false,
    };

    /// <summary>把 def 三个前置槽位上的项目追加进 <paramref name="into"/>（去重；列表都很短，线性判重更省）。</summary>
    private static void CollectPrerequisites(List<ResearchProjectDef> into, Def def)
    {
        switch (def)
        {
            case RecipeDef recipe:
                Add(into, recipe.researchPrerequisite);
                AddRange(into, recipe.researchPrerequisites);
                break;
            case ThingDef thing:
                AddRange(into, thing.researchPrerequisites);
                // 植物播种是另一条槽位（"能种了"），移除时要一并清，否则删不干净。
                AddRange(into, thing.plant?.sowResearchPrerequisites);
                break;
            case TerrainDef terrain:
                AddRange(into, terrain.researchPrerequisites);
                break;
            case PsychicRitualDef ritual:
                Add(into, ritual.researchPrerequisite);
                break;
        }
    }

    private static void Add(List<ResearchProjectDef> into, ResearchProjectDef? project)
    {
        if (project != null && !into.Contains(project)) into.Add(project);
    }

    private static void AddRange(List<ResearchProjectDef> into, List<ResearchProjectDef>? projects)
    {
        if (projects == null) return;
        for (int i = 0; i < projects.Count; i++) Add(into, projects[i]);
    }

    /// <summary>
    /// 清掉全部研究项目的原版缓存。<b>只在 <c>TweakDatabase.Init</c> 末尾调一次</b> ——
    /// 那时不知道哪些项目被改动过，而加载期 <c>CompProperties_Techprint.ResolveReferences</c>
    /// 已经按"未应用修改"的 def 填过一份，必须整体打回。
    /// </summary>
    internal static void ClearAllVanillaCaches()
    {
        if (UnlockedDefsField == null && HyperlinksField == null && DescriptionField == null) return;
        var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
        for (int i = 0; i < all.Count; i++) ClearOne(all[i]);
    }

    /// <summary>
    /// 清掉单个项目的原版缓存。<b>只是置 null，不触发重算</b> —— 这三个 getter 都是懒的，
    /// 谁来读谁付扫描成本，因此这里的成本恒为 3 次反射写。
    /// </summary>
    private static void ClearOne(ResearchProjectDef project)
    {
        try
        {
            UnlockedDefsField?.SetValue(project, null);
            HyperlinksField?.SetValue(project, null);
            DescriptionField?.SetValue(project, null);
        }
        catch (Exception e)
        {
            // 原版字段改名/移位：宁可这列显示旧值，也不要打断整次 Apply。
            Log.Warning($"[BalanceTweak] 清空研究缓存失败 {project.defName}: {e.Message}");
        }
    }

    #endregion

    private static Dictionary<ResearchProjectDef, List<Entry>> Build()
    {
        var map = new Dictionary<ResearchProjectDef, List<Entry>>();

        void AddEntry(ResearchProjectDef project, Def carrier, Def? display, RelationSlot slot)
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
                AddEntry(project, recipe, recipe.products[i]?.thingDef, slot);
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
                        AddEntry(list[j], recipe, recipe, RelationSlot.List);
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
                    AddEntry(prereqs[j], thing, thing, RelationSlot.List);

            // —— ThingDef：植物播种前置（"能种了"是另一条槽位，移除时必须一并清，否则删不干净）——
            var sow = thing.plant?.sowResearchPrerequisites;
            if (sow != null)
                for (int j = 0; j < sow.Count; j++)
                    AddEntry(sow[j], thing, thing, RelationSlot.List);
        }

        // —— TerrainDef ——
        var terrains = DefDatabase<TerrainDef>.AllDefsListForReading;
        for (int i = 0; i < terrains.Count; i++)
        {
            var terrain = terrains[i];
            var prereqs = terrain.researchPrerequisites;
            if (prereqs != null)
                for (int j = 0; j < prereqs.Count; j++)
                    AddEntry(prereqs[j], terrain, terrain, RelationSlot.List);
        }

        // —— 心能仪式（Anomaly；未装 DLC 时这张表是空的，类型本身在原版程序集里）——
        var rituals = DefDatabase<PsychicRitualDef>.AllDefsListForReading;
        for (int i = 0; i < rituals.Count; i++)
        {
            var ritual = rituals[i];
            if (ritual.researchPrerequisite != null)
                AddEntry(ritual.researchPrerequisite, ritual, ritual, RelationSlot.Single);
        }

        cache = map;
        return map;
    }
}
