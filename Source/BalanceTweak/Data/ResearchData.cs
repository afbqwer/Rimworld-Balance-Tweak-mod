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
        ApplyS(nameof(defLabel), v => this.def.label = v);
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
