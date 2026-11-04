using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ResearchProjectDef), SettingType.Research)]
class ResearchData : TweakData<ResearchData>
{
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNoAnomaly))]
    public ResearchTabDef? researchTab = null;
    [TweakField()]
    public float? researchViewX = null;
    [TweakField()]
    public float? researchViewY = null;
    [TweakField(Available = nameof(AvailableIfNoAnomaly))]
    public float? baseCost = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel), Available = nameof(AvailableIfNoAnomaly))]
    public TechLevel? techLevel = null;
    [TweakField(Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public float? knowledgeCost = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfAnomaly), MayRequire = "ludeon.rimworld.anomaly")]
    public KnowledgeCategoryDef? knowledgeCategory = null;
    [TweakField(Style = ColumnStyle.Int, MayRequire = "ludeon.rimworld.royalty", Available = nameof(AvailableIfNoAnomaly))]
    public int? techprintCount = null;
    [TweakField(MayRequire = "ludeon.rimworld.royalty", Available = nameof(AvailableIfNoAnomaly))]
    public float? techprintCommonality = null;
    [TweakField(MayRequire = "ludeon.rimworld.royalty", Available = nameof(AvailableIfNoAnomaly))]
    public float? techprintMarketValue = null;

    // —— 研究树结构（原版 XML 的 prerequisites / hiddenPrerequisites / tags）——
    // 三者都是「前置研究」：prerequisites 在树上显示连线，hiddenPrerequisites 只做硬性门禁不画线。
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectDef>? prerequisites = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectDef>? hiddenPrerequisites = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectTagDef>? tags = null;

    // —— 研究台要求 ——
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThingDef? requiredResearchBuilding = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? requiredResearchFacilities = null;

    // —— DLC 专属门禁 ——
    // requiredAnalyzed / requiresMechanitor：Biotech（无 Biotech 时原版 PostLoad 会把 requiredAnalyzed 置空）
    [TweakField(Style = ColumnStyle.DefList, MayRequire = "ludeon.rimworld.biotech")]
    public List<ThingDef>? requiredAnalyzed = null;
    [TweakField(Style = ColumnStyle.Bool, MayRequire = "ludeon.rimworld.biotech")]
    public bool? requiresMechanitor = null;
    // requireGravEngineInspected / customUnlockTexts：Odyssey（重力引擎检视、自定义解锁文案）
    [TweakField(Style = ColumnStyle.Bool, MayRequire = "ludeon.rimworld.odyssey")]
    public bool? requireGravEngineInspected = null;
    [TweakField(Style = ColumnStyle.StringList, MayRequire = "ludeon.rimworld.odyssey")]
    public List<string>? customUnlockTexts = null;
    // heldByFactionCategoryTags：Royalty（蓝图所属派系类别；凡 techprintCount > 0 的原版 def 都必须有）
    [TweakField(Style = ColumnStyle.StringList, MayRequire = "ludeon.rimworld.royalty")]
    public List<string>? heldByFactionCategoryTags = null;

    // —— 其它 ——
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ConceptDef? teachConcept = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? recalculatePower = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? discoveredLetterTitle = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? discoveredLetterText = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // —— 派生展示列：本项研究解锁了什么 ——
    // 原版没有 unlocks 字段，这条关系存在目标 Def 上（建筑/地形/配方的 researchPrerequisites 等），
    // 由 ResearchUnlocks 全库反查得出。所以它只能是 Display：不落盘、不参与 Apply，
    // 也不存在"研究行上存一份解锁列表"这种第二真相（改解锁 = 改目标 Def 的那一行）。
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.UnlockList)]
    public List<Def>? unlockedDefs = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is ResearchProjectDef d)
        {
            defLabel ??= def?.label;
            baseCost ??= d.baseCost;
            researchViewX ??= d.researchViewX;
            researchViewY ??= d.researchViewY;
            techLevel ??= d.techLevel;
            researchTab ??= d.tab;
            knowledgeCost ??= d.knowledgeCost;
            knowledgeCategory ??= d.knowledgeCategory;
            techprintCount ??= d.techprintCount;
            techprintCommonality ??= d.techprintCommonality;
            techprintMarketValue ??= d.techprintMarketValue;

            // 列表字段一律带 !IsFieldModified 守卫：已清空的字段不得被 ??= 复活（硬约束，SC2 自检）。
            if (!IsFieldModified(nameof(prerequisites))) prerequisites ??= d.prerequisites;
            if (!IsFieldModified(nameof(hiddenPrerequisites))) hiddenPrerequisites ??= d.hiddenPrerequisites;
            if (!IsFieldModified(nameof(tags))) tags ??= d.tags;
            if (!IsFieldModified(nameof(requiredResearchFacilities))) requiredResearchFacilities ??= d.requiredResearchFacilities;
            if (!IsFieldModified(nameof(requiredAnalyzed))) requiredAnalyzed ??= d.requiredAnalyzed;
            if (!IsFieldModified(nameof(customUnlockTexts))) customUnlockTexts ??= d.customUnlockTexts;
            if (!IsFieldModified(nameof(heldByFactionCategoryTags))) heldByFactionCategoryTags ??= d.heldByFactionCategoryTags;

            requiredResearchBuilding ??= d.requiredResearchBuilding;
            teachConcept ??= d.teachConcept;
            requiresMechanitor ??= d.requiresMechanitor;
            requireGravEngineInspected ??= d.requireGravEngineInspected;
            recalculatePower ??= d.recalculatePower;
            discoveredLetterTitle ??= d.discoveredLetterTitle;
            discoveredLetterText ??= d.discoveredLetterText;
        }
    }

    public override void Apply()
    {
        if (this.def is not ResearchProjectDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyV<float>(nameof(baseCost), v => def.baseCost = v);
        ApplyV<float>(nameof(researchViewX), v => def.researchViewX = v);
        ApplyV<float>(nameof(researchViewY), v => def.researchViewY = v);
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);
        ApplyD<ResearchTabDef>(nameof(researchTab), v => def.tab = v);
        ApplyV<float>(nameof(knowledgeCost), v => def.knowledgeCost = v);
        ApplyD<KnowledgeCategoryDef>(nameof(knowledgeCategory), v => def.knowledgeCategory = v);
        ApplyV<int>(nameof(techprintCount), v =>
        {
            // 图纸物品是**加载期**按 techprintCount 生成的（ThingDefGenerator_Techprints.ImpliedTechprintDefs，
            // 读的是 XML 原值），而我们的写入永远晚于那一刻（StaticConstructorOnStartup 之后）——
            // 所以 0 → N 不会补生成物品，只会造出一个"要求 N 张图纸、世界里没有图纸"的项目：
            // TechprintRequirementMet 恒 false（该研究永久不可完成），且 Techprint 为 null 会让
            // GetTip() 的 Techprint.LabelCap 与 TechprintUtility 的 p.Techprint.BaseMarketValue 直接 NRE。
            // 这里拒绝写入并把字段退回原值，让 UI 与 def 保持一致。
            // 判据看"物品是否真的存在"而不是 def 当前值：重置场景下 def 可能是 0、而 XML 原值 >0（物品已生成）。
            if (v > 0 && def.techprintCount <= 0 && ModLister.RoyaltyInstalled && FindTechprintThing(def) == null)
            {
                Log.Warning($"[BalanceTweak] {def.defName} 的 techprintCount 不能从 0 改为 {v}："
                          + "图纸物品只在加载期按 XML 原值生成，运行期不会补生成，改后该研究将永远无法完成。已忽略本次修改。");
                this.techprintCount = def.techprintCount;
                RemoveFieldModified(nameof(techprintCount));
                if (CountFieldModified() == 0) tweaked = false;
                return;
            }
            def.techprintCount = v;
        });
        ApplyV<float>(nameof(techprintCommonality), v => def.techprintCommonality = v);
        ApplyV<float>(nameof(techprintMarketValue), v =>
        {
            def.techprintMarketValue = v;
            // 自动生成的图纸物品在加载期就把市场价写进了自己的 statBases（ImpliedTechprintDefs 里的
            // SetStatBaseValue），改研究项不会回更新它 —— 这里同步过去，让这一列真的改得到图纸的价钱。
            // label / description 是拼好的字符串，同步不了（见 Readme §8.6 的已知边界）。
            if (def.techprintCount > 0) def.Techprint?.SetStatBaseValue(StatDefOf.MarketValue, v);
        });
        ApplyL<ResearchProjectDef>(nameof(prerequisites), v => def.prerequisites = v);
        ApplyL<ResearchProjectDef>(nameof(hiddenPrerequisites), v => def.hiddenPrerequisites = v);
        ApplyL<ResearchProjectTagDef>(nameof(tags), v => def.tags = v);
        ApplyD<ThingDef>(nameof(requiredResearchBuilding), v => def.requiredResearchBuilding = v);
        ApplyL<ThingDef>(nameof(requiredResearchFacilities), v => def.requiredResearchFacilities = v);
        ApplyL<ThingDef>(nameof(requiredAnalyzed), v => def.requiredAnalyzed = v);
        ApplyV<bool>(nameof(requiresMechanitor), v => def.requiresMechanitor = v);
        ApplyV<bool>(nameof(requireGravEngineInspected), v => def.requireGravEngineInspected = v);
        ApplyV<bool>(nameof(recalculatePower), v => def.recalculatePower = v);
        ApplyS(nameof(discoveredLetterTitle), v => def.discoveredLetterTitle = v);
        ApplyS(nameof(discoveredLetterText), v => def.discoveredLetterText = v);
        ApplyL<string>(nameof(customUnlockTexts), v => def.customUnlockTexts = v);
        ApplyL<string>(nameof(heldByFactionCategoryTags), v => def.heldByFactionCategoryTags = v);
        ApplyD<ConceptDef>(nameof(teachConcept), v => def.teachConcept = v);
        ApplyDefLabel(nameof(defLabel), def);
    }

    /// <summary>
    /// 找出为该项目生成的图纸物品（<c>Techprint_&lt;defName&gt;</c>，加载期由 ThingDefGenerator_Techprints 创建）。
    /// <para>
    /// 不用 <see cref="ResearchProjectDef.Techprint"/>：那个在 <c>techprintCount &lt;= 0</c> 时直接返回 null，
    /// 而这里恰恰要在"count 还是 0"的时候判断物品到底存不存在。只在写入 techprintCount 时调一次，
    /// 几千个 ThingDef 的一次遍历 ≈ 0.5ms，不值得为它维护缓存。
    /// </para>
    /// </summary>
    private static ThingDef? FindTechprintThing(ResearchProjectDef project)
    {
        var things = DefDatabase<ThingDef>.AllDefsListForReading;
        for (int i = 0; i < things.Count; i++)
            if (things[i].GetCompProperties<CompProperties_Techprint>()?.project == project)
                return things[i];
        return null;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ResearchType).GetEnumNames().Select(s => "MST." + s).ToList();

    /// <summary>
    /// 只读解锁列的派生值。按需计算（<c>UpdateFilters</c> 重建列表时调），不做缓存标记 ——
    /// 一次查询是索引的字典命中 + 一次列表遍历，比维护"已算过"标记更便宜，也不会漏刷新。
    /// </summary>
    public override void EnsureDisplayValues()
    {
        if (this.def is not ResearchProjectDef project)
        {
            unlockedDefs = null;
            return;
        }

        var entries = ResearchUnlocks.Get(project);
        if (entries.Count == 0)
        {
            unlockedDefs = null;
            return;
        }

        // 同一产物可能由多条配方解锁，表格里只该出现一次（与原版 UnlockedDefs 的 Distinct 一致）。
        var list = new List<Def>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var display = entries[i].display;
            if (display != null && !list.Contains(display)) list.Add(display);
        }
        list.Sort(CompareByLabel);
        unlockedDefs = list;
    }

    private static int CompareByLabel(Def a, Def b)
        => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.CurrentCulture);

    public static bool AvailableIfAnomaly(TweakData data) => data.propType switch
    {
        (int)ResearchType.Anomaly => true,
        _ => false,
    };

    public static bool AvailableIfNoAnomaly(TweakData data) => data.propType switch
    {
        (int)ResearchType.Anomaly => false,
        _ => true,
    };

    private enum ResearchType
    {
        Basic,
        Anomaly,
    }

    protected override string ResolveDesc() => DescriptionOrDefName();

    public override int GetPropType()
    {
        if (this.def is ResearchProjectDef def)
        {
            if (def.knowledgeCost > 0)
            {
                return (int)ResearchType.Anomaly;
            }
            if (def.techLevel == TechLevel.Neolithic || def.techLevel == TechLevel.Medieval || def.techLevel == TechLevel.Industrial)
            {
                return (int)ResearchType.Basic;
            }
        }
        return (int)ResearchType.Basic;
    }
}
