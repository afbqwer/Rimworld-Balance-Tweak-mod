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

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is ResearchProjectDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
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
        ApplyV<int>(nameof(techprintCount), v => def.techprintCount = v);
        ApplyV<float>(nameof(techprintCommonality), v => def.techprintCommonality = v);
        ApplyV<float>(nameof(techprintMarketValue), v => def.techprintMarketValue = v);
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

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ResearchType).GetEnumNames().Select(s => "MST." + s).ToList();

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
