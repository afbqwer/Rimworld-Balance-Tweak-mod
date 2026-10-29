using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThoughtDef), SettingType.Thought)]
class ThoughtData : TweakData<ThoughtData>, ISubItemHost
{
    /// <summary>想法阶段子项（原 Init() 里的硬编码块）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not ThoughtDef d || d.stages.NullOrEmpty()) yield break;
        for (int i = 0; i < d.stages.Count; i++)
        {
            if (d.stages[i] == null) continue;
            var sd = new ThoughtStageData { index = i };
            sd.SetParentTweak(parent, SettingType.ThoughtStage, tweaked);
            yield return sd;
        }
    }

    // 字段定义
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? FirstStage = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int)]
    public int? StageNum = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? stackLimit = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? stackedEffectMultiplier = null;
    [TweakField()]
    public float? durationDays = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? stackLimitForSameOtherPawn = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? lerpMoodToZero = null;

    // 简单字段
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? stagesStack = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? invert = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? validWhileDespawned = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? nullifiedIfNotColonist = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? showBubble = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? ignoreSubhumans = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? doNotApplyToQuestLodgers = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? lerpOpinionToZeroAfterDurationPct = null;
    [TweakField()]
    public float? maxCumulatedOpinionOffset = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? requiredTraitsDegree = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(Gender))]
    public Gender? gender = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(DevelopmentalStage))]
    public DevelopmentalStage? developmentalStageFilter = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(DevelopmentalStage))]
    public DevelopmentalStage? socialTargetDevelopmentalStageFilter = null;

    // Def 引用字段
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThoughtDef? nextThought = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThoughtDef? producesMemoryThought = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThoughtDef? thoughtToMake = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public HediffDef? hediff = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public MentalStateDef? mentalState = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public GameConditionDef? gameCondition = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public StatDef? effectMultiplyingStat = null;
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? effectMultiplyingStatCurve = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ExpectationDef? minExpectation = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ExpectationDef? minExpectationForNegativeThought = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ChemicalDef? chemicalDef = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public TaleDef? taleDef = null;

    // Def 列表字段
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThoughtDef>? replaceThoughts = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TraitDef>? nullifyingTraits = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TraitDef>? neverNullifyIfAnyTrait = null;
    [TweakField(Style = ColumnStyle.TraitReqList)]
    public List<TraitRequirement>? nullifyingTraitDegrees = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TraitDef>? requiredTraits = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<GeneDef>? nullifyingGenes = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<GeneDef>? requiredGenes = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? nullifyingHediffs = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? requiredHediffs = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<PreceptDef>? nullifyingPrecepts = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TaleDef>? nullifyingOwnTales = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is ThoughtDef d)
        {
            var fs = d.stages?.FirstOrDefault();
            label = d.label ?? fs?.LabelAbstractCap ?? d.defName;
            defLabel ??= def?.label;
            desc = d.description ?? $"{d.defName}{fs?.description}" ?? "";
            if (d.Icon != null && d.Icon != BaseContent.BadTex)
            {
                uiIcon = d.Icon;
            }
            searchString = label + d.defName;
            StageNum ??= d.stages?.Count ?? 0;
            stackLimit ??= d.stackLimit;
            stackedEffectMultiplier ??= d.stackedEffectMultiplier;
            durationDays ??= d.durationDays;
            stackLimitForSameOtherPawn ??= d.stackLimitForSameOtherPawn;
            lerpMoodToZero ??= d.lerpMoodToZero;
            stagesStack ??= d.stagesStack;
            invert ??= d.invert;
            validWhileDespawned ??= d.validWhileDespawned;
            nullifiedIfNotColonist ??= d.nullifiedIfNotColonist;
            showBubble ??= d.showBubble;
            ignoreSubhumans ??= d.ignoreSubhumans;
            doNotApplyToQuestLodgers ??= d.doNotApplyToQuestLodgers;
            lerpOpinionToZeroAfterDurationPct ??= d.lerpOpinionToZeroAfterDurationPct;
            maxCumulatedOpinionOffset ??= d.maxCumulatedOpinionOffset;
            requiredTraitsDegree ??= d.requiredTraitsDegree;
            gender ??= d.gender;
            developmentalStageFilter ??= d.developmentalStageFilter;
            socialTargetDevelopmentalStageFilter ??= d.socialTargetDevelopmentalStageFilter;
            nextThought ??= d.nextThought;
            producesMemoryThought ??= d.producesMemoryThought;
            thoughtToMake ??= d.thoughtToMake;
            hediff ??= d.hediff;
            mentalState ??= d.mentalState;
            gameCondition ??= d.gameCondition;
            effectMultiplyingStat ??= d.effectMultiplyingStat;
            effectMultiplyingStatCurve ??= d.effectMultiplyingStatCurve;
            minExpectation ??= d.minExpectation;
            minExpectationForNegativeThought ??= d.minExpectationForNegativeThought;
            chemicalDef ??= d.chemicalDef;
            taleDef ??= d.taleDef;
            if (!IsFieldModified(nameof(replaceThoughts))) replaceThoughts ??= d.replaceThoughts;
            if (!IsFieldModified(nameof(nullifyingTraits))) nullifyingTraits ??= d.nullifyingTraits;
            if (!IsFieldModified(nameof(neverNullifyIfAnyTrait))) neverNullifyIfAnyTrait ??= d.neverNullifyIfAnyTrait;
            if (!IsFieldModified(nameof(nullifyingTraitDegrees))) nullifyingTraitDegrees ??= d.nullifyingTraitDegrees;
            if (!IsFieldModified(nameof(requiredTraits))) requiredTraits ??= d.requiredTraits;
            if (!IsFieldModified(nameof(nullifyingGenes))) nullifyingGenes ??= d.nullifyingGenes;
            if (!IsFieldModified(nameof(requiredGenes))) requiredGenes ??= d.requiredGenes;
            if (!IsFieldModified(nameof(nullifyingHediffs))) nullifyingHediffs ??= d.nullifyingHediffs;
            if (!IsFieldModified(nameof(requiredHediffs))) requiredHediffs ??= d.requiredHediffs;
            if (!IsFieldModified(nameof(nullifyingPrecepts))) nullifyingPrecepts ??= d.nullifyingPrecepts;
            if (!IsFieldModified(nameof(nullifyingOwnTales))) nullifyingOwnTales ??= d.nullifyingOwnTales;
        }
    }

    public override void Apply()
    {
        if (this.def is not ThoughtDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<int>(nameof(stackLimit), v => def.stackLimit = v);
        ApplyV<float>(nameof(stackedEffectMultiplier), v => def.stackedEffectMultiplier = v);
        ApplyV<float>(nameof(durationDays), v => def.durationDays = v);
        ApplyV<int>(nameof(stackLimitForSameOtherPawn), v => def.stackLimitForSameOtherPawn = v);
        ApplyV<bool>(nameof(lerpMoodToZero), v => def.lerpMoodToZero = v);
        ApplyV<bool>(nameof(stagesStack), v => def.stagesStack = v);
        ApplyV<bool>(nameof(invert), v => def.invert = v);
        ApplyV<bool>(nameof(validWhileDespawned), v => def.validWhileDespawned = v);
        ApplyV<bool>(nameof(nullifiedIfNotColonist), v => def.nullifiedIfNotColonist = v);
        ApplyV<bool>(nameof(showBubble), v => def.showBubble = v);
        ApplyV<bool>(nameof(ignoreSubhumans), v => def.ignoreSubhumans = v);
        ApplyV<bool>(nameof(doNotApplyToQuestLodgers), v => def.doNotApplyToQuestLodgers = v);
        ApplyV<float>(nameof(lerpOpinionToZeroAfterDurationPct), v => def.lerpOpinionToZeroAfterDurationPct = v);
        ApplyV<float>(nameof(maxCumulatedOpinionOffset), v => def.maxCumulatedOpinionOffset = v);
        ApplyV<int>(nameof(requiredTraitsDegree), v => def.requiredTraitsDegree = v);
        ApplyV<Gender>(nameof(gender), v => def.gender = v);
        ApplyV<DevelopmentalStage>(nameof(developmentalStageFilter), v => def.developmentalStageFilter = v);
        ApplyV<DevelopmentalStage>(nameof(socialTargetDevelopmentalStageFilter), v => def.socialTargetDevelopmentalStageFilter = v);
        ApplyD<ThoughtDef>(nameof(nextThought), v => def.nextThought = v);
        ApplyD<ThoughtDef>(nameof(producesMemoryThought), v => def.producesMemoryThought = v);
        ApplyD<ThoughtDef>(nameof(thoughtToMake), v => def.thoughtToMake = v);
        ApplyD<HediffDef>(nameof(hediff), v => def.hediff = v);
        ApplyD<MentalStateDef>(nameof(mentalState), v => def.mentalState = v);
        ApplyD<GameConditionDef>(nameof(gameCondition), v => def.gameCondition = v);
        ApplyD<StatDef>(nameof(effectMultiplyingStat), v => def.effectMultiplyingStat = v);
        ApplyV<SimpleCurve>(nameof(effectMultiplyingStatCurve), v => def.effectMultiplyingStatCurve = v);
        ApplyD<ExpectationDef>(nameof(minExpectation), v => def.minExpectation = v);
        ApplyD<ExpectationDef>(nameof(minExpectationForNegativeThought), v => def.minExpectationForNegativeThought = v);
        ApplyD<ChemicalDef>(nameof(chemicalDef), v => def.chemicalDef = v);
        ApplyD<TaleDef>(nameof(taleDef), v => def.taleDef = v);
        ApplyL<ThoughtDef>(nameof(replaceThoughts), v => def.replaceThoughts = v);
        ApplyL<TraitDef>(nameof(nullifyingTraits), v => def.nullifyingTraits = v);
        ApplyL<TraitDef>(nameof(neverNullifyIfAnyTrait), v => def.neverNullifyIfAnyTrait = v);
        ApplyL<TraitRequirement>(nameof(nullifyingTraitDegrees), v => def.nullifyingTraitDegrees = v);
        ApplyL<TraitDef>(nameof(requiredTraits), v => def.requiredTraits = v);
        ApplyL<GeneDef>(nameof(nullifyingGenes), v => def.nullifyingGenes = v);
        ApplyL<GeneDef>(nameof(requiredGenes), v => def.requiredGenes = v);
        ApplyL<HediffDef>(nameof(nullifyingHediffs), v => def.nullifyingHediffs = v);
        ApplyL<HediffDef>(nameof(requiredHediffs), v => def.requiredHediffs = v);
        ApplyL<PreceptDef>(nameof(nullifyingPrecepts), v => def.nullifyingPrecepts = v);
        ApplyL<TaleDef>(nameof(nullifyingOwnTales), v => def.nullifyingOwnTales = v);
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ThoughtType).GetEnumNames().Select(s => "MST." + s).ToList();

    public enum ThoughtType
    {
        Situational,
        Memory,
        Social
    }

    public override int GetPropType()
    {
        if (this.def is ThoughtDef def)
        {
            if (def.IsSocial)
            {
                return (int)ThoughtType.Social;
            }
            if (def.IsMemory)
            {
                return (int)ThoughtType.Memory;
            }
        }
        return (int)ThoughtType.Situational;
    }
}


