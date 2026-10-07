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
            replaceThoughts ??= d.replaceThoughts;
            nullifyingTraits ??= d.nullifyingTraits;
            neverNullifyIfAnyTrait ??= d.neverNullifyIfAnyTrait;
            nullifyingTraitDegrees ??= d.nullifyingTraitDegrees;
            requiredTraits ??= d.requiredTraits;
            nullifyingGenes ??= d.nullifyingGenes;
            requiredGenes ??= d.requiredGenes;
            nullifyingHediffs ??= d.nullifyingHediffs;
            requiredHediffs ??= d.requiredHediffs;
            nullifyingPrecepts ??= d.nullifyingPrecepts;
            nullifyingOwnTales ??= d.nullifyingOwnTales;
        }
    }

    public override void Apply()
    {
        if (this.def is not ThoughtDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (stackLimit.HasValue) { def.stackLimit = stackLimit.Value; }
        if (stackedEffectMultiplier.HasValue) { def.stackedEffectMultiplier = stackedEffectMultiplier.Value; }
        if (durationDays.HasValue) { def.durationDays = durationDays.Value; }
        if (stackLimitForSameOtherPawn.HasValue) { def.stackLimitForSameOtherPawn = stackLimitForSameOtherPawn.Value; }
        if (lerpMoodToZero.HasValue) { def.lerpMoodToZero = lerpMoodToZero.Value; }
        if (stagesStack.HasValue) { def.stagesStack = stagesStack.Value; }
        if (invert.HasValue) { def.invert = invert.Value; }
        if (validWhileDespawned.HasValue) { def.validWhileDespawned = validWhileDespawned.Value; }
        if (nullifiedIfNotColonist.HasValue) { def.nullifiedIfNotColonist = nullifiedIfNotColonist.Value; }
        if (showBubble.HasValue) { def.showBubble = showBubble.Value; }
        if (ignoreSubhumans.HasValue) { def.ignoreSubhumans = ignoreSubhumans.Value; }
        if (doNotApplyToQuestLodgers.HasValue) { def.doNotApplyToQuestLodgers = doNotApplyToQuestLodgers.Value; }
        if (lerpOpinionToZeroAfterDurationPct.HasValue) { def.lerpOpinionToZeroAfterDurationPct = lerpOpinionToZeroAfterDurationPct.Value; }
        if (maxCumulatedOpinionOffset.HasValue) { def.maxCumulatedOpinionOffset = maxCumulatedOpinionOffset.Value; }
        if (requiredTraitsDegree.HasValue) { def.requiredTraitsDegree = requiredTraitsDegree.Value; }
        if (gender.HasValue) { def.gender = gender.Value; }
        if (developmentalStageFilter.HasValue) { def.developmentalStageFilter = developmentalStageFilter.Value; }
        if (socialTargetDevelopmentalStageFilter.HasValue) { def.socialTargetDevelopmentalStageFilter = socialTargetDevelopmentalStageFilter.Value; }
        if (nextThought != null) def.nextThought = nextThought;
        if (producesMemoryThought != null) def.producesMemoryThought = producesMemoryThought;
        if (thoughtToMake != null) def.thoughtToMake = thoughtToMake;
        if (hediff != null) def.hediff = hediff;
        if (mentalState != null) def.mentalState = mentalState;
        if (gameCondition != null) def.gameCondition = gameCondition;
        if (effectMultiplyingStat != null) def.effectMultiplyingStat = effectMultiplyingStat;
        if (effectMultiplyingStatCurve != null) def.effectMultiplyingStatCurve = effectMultiplyingStatCurve;
        if (minExpectation != null) def.minExpectation = minExpectation;
        if (minExpectationForNegativeThought != null) def.minExpectationForNegativeThought = minExpectationForNegativeThought;
        if (chemicalDef != null) def.chemicalDef = chemicalDef;
        if (taleDef != null) def.taleDef = taleDef;
        if (replaceThoughts != null) def.replaceThoughts = replaceThoughts;
        if (nullifyingTraits != null) def.nullifyingTraits = nullifyingTraits;
        if (neverNullifyIfAnyTrait != null) def.neverNullifyIfAnyTrait = neverNullifyIfAnyTrait;
        if (nullifyingTraitDegrees != null) def.nullifyingTraitDegrees = nullifyingTraitDegrees;
        if (requiredTraits != null) def.requiredTraits = requiredTraits;
        if (nullifyingGenes != null) def.nullifyingGenes = nullifyingGenes;
        if (requiredGenes != null) def.requiredGenes = requiredGenes;
        if (nullifyingHediffs != null) def.nullifyingHediffs = nullifyingHediffs;
        if (requiredHediffs != null) def.requiredHediffs = requiredHediffs;
        if (nullifyingPrecepts != null) def.nullifyingPrecepts = nullifyingPrecepts;
        if (nullifyingOwnTales != null) def.nullifyingOwnTales = nullifyingOwnTales;
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


