using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;


class TraitDegreeNodeData : TweakData<TraitDegreeNodeData>
{
    [TweakField(DataType = ColumnDataType.Field, Style = ColumnStyle.String, Available = nameof(NotAvailable))]
    public string? degreeLabel = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? Trait = null;

    [TweakField(Style = ColumnStyle.Int)]
    public int? degree = null;

    [TweakField()]
    public float? commonality = null;

    [TweakField()]
    public float? socialFightChanceFactor = null;

    [TweakField()]
    public float? marketValueFactorOffset = null;

    [TweakField()]
    public float? randomDiseaseMtbDays = null;

    [TweakField()]
    public float? hungerRateFactor = null;

    [TweakField()]
    public float? painOffset = null;

    [TweakField()]
    public float? painFactor = null;

    [TweakField()]
    public float? forcedMentalStateMtbDays = null;

    [TweakField()]
    public float? mentalBreakInspirationGainChance = null;

    //[TweakField(Style = ColumnStyle.DefSelector)]
    //public ThinkTreeDef? thinkTree = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public MentalStateDef? randomMentalState = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public MentalStateDef? forcedMentalState = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<MentalStateDef>? disallowedMentalStates = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThoughtDef>? disallowedThoughts = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<InspirationDef>? disallowedInspirations = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<InspirationDef>? mentalBreakInspirationGainSet = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<MeditationFocusDef>? allowedMeditationFocusTypes = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<MeditationFocusDef>? disallowedMeditationFocusTypes = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<MentalBreakDef>? theOnlyAllowedMentalBreaks = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<AbilityDef>? abilities = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? enablesNeeds = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? disablesNeeds = null;

    [TweakField(Style = ColumnStyle.SkillGainList)]
    public List<SkillGain>? skillGains = null;

    [TweakField(Style = ColumnStyle.AptitudeList)]
    public List<Aptitude>? aptitudes = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? subdefLabel = null;

    public override int LoadingOrd => 300;
    private TraitDegreeData? degreeData = null;

    public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false)
    {
        base.SetParentTweak(data, type, tweaked);
        propType = data.propType;
        if (data.def is TraitDef def && index.HasValue)
        {
            if (index.Value < 0 || index.Value >= def.degreeDatas.Count)
            {
                return false;
            }
            degreeData = def.degreeDatas[index.Value];
            if (degreeData == null) { return false; }
            var l = degreeData.LabelCap ?? degreeData.label ?? def.label;
            if (!degreeLabel.NullOrEmpty() && degreeData.untranslatedLabel != degreeLabel)
            {
                Log.Warning($"[BalanceTweak]{def} degree的名称错误：{degreeLabel}");
                return false;
            }
            degreeLabel = degreeData.untranslatedLabel;
            label = l;
            subdefLabel ??= degreeData.label;
            id = new($"{def.defName}_{degreeData.degree}", type);
            searchString = $"{def.defName}{index}{l}";
            var li = GetAllData(data.id);
            foreach (var item in li)
            {
                ((TraitData)item).FirstDegree ??= this.id;
            }
            Trait = data.id;
            degree ??= degreeData.degree;
            commonality ??= degreeData.commonality;
            socialFightChanceFactor ??= degreeData.socialFightChanceFactor;
            marketValueFactorOffset ??= degreeData.marketValueFactorOffset;
            randomDiseaseMtbDays ??= degreeData.randomDiseaseMtbDays;
            hungerRateFactor ??= degreeData.hungerRateFactor;
            painOffset ??= degreeData.painOffset;
            painFactor ??= degreeData.painFactor;
            forcedMentalStateMtbDays ??= degreeData.forcedMentalStateMtbDays;
            mentalBreakInspirationGainChance ??= degreeData.mentalBreakInspirationGainChance;
            randomMentalState ??= degreeData.randomMentalState;
            forcedMentalState ??= degreeData.forcedMentalState;
            statOffsets ??= degreeData.statOffsets;
            statFactors ??= degreeData.statFactors;
            disallowedMentalStates ??= degreeData.disallowedMentalStates;
            disallowedThoughts ??= degreeData.disallowedThoughts;
            disallowedInspirations ??= degreeData.disallowedInspirations;
            mentalBreakInspirationGainSet ??= degreeData.mentalBreakInspirationGainSet;
            allowedMeditationFocusTypes ??= degreeData.allowedMeditationFocusTypes;
            disallowedMeditationFocusTypes ??= degreeData.disallowedMeditationFocusTypes;
            theOnlyAllowedMentalBreaks ??= degreeData.theOnlyAllowedMentalBreaks;
            abilities ??= degreeData.abilities;
            enablesNeeds ??= degreeData.enablesNeeds;
            disablesNeeds ??= degreeData.disablesNeeds;
            skillGains ??= degreeData.skillGains;
            aptitudes ??= degreeData.aptitudes;
        }
        return true;
    }

    public override void Apply()
    {
        if (this.degreeData is not TraitDegreeData dd)
        {
            Log.Error($"[BalanceTweak]{this}的degreeData为{this.degreeData}!");
            return;
        }
        if (subdefLabel != null) dd.label = subdefLabel;
        if (dd != null)
        {
            if (degree.HasValue) { dd.degree = degree.Value; }
            if (commonality.HasValue) { dd.commonality = commonality.Value; }
            if (socialFightChanceFactor.HasValue) { dd.socialFightChanceFactor = socialFightChanceFactor.Value; }
            if (marketValueFactorOffset.HasValue) { dd.marketValueFactorOffset = marketValueFactorOffset.Value; }
            if (randomDiseaseMtbDays.HasValue) { dd.randomDiseaseMtbDays = randomDiseaseMtbDays.Value; }
            if (hungerRateFactor.HasValue) { dd.hungerRateFactor = hungerRateFactor.Value; }
            if (painOffset.HasValue) { dd.painOffset = painOffset.Value; }
            if (painFactor.HasValue) { dd.painFactor = painFactor.Value; }
            if (forcedMentalStateMtbDays.HasValue) { dd.forcedMentalStateMtbDays = forcedMentalStateMtbDays.Value; }
            if (mentalBreakInspirationGainChance.HasValue) { dd.mentalBreakInspirationGainChance = mentalBreakInspirationGainChance.Value; }
            if (randomMentalState != null) { dd.randomMentalState = randomMentalState; }
            if (forcedMentalState != null) { dd.forcedMentalState = forcedMentalState; }
            if (statOffsets != null) { dd.statOffsets = statOffsets; }
            if (statFactors != null) { dd.statFactors = statFactors; }
            if (disallowedMentalStates != null) { dd.disallowedMentalStates = disallowedMentalStates; }
            if (disallowedThoughts != null) { dd.disallowedThoughts = disallowedThoughts; }
            if (disallowedInspirations != null) { dd.disallowedInspirations = disallowedInspirations; }
            if (mentalBreakInspirationGainSet != null) { dd.mentalBreakInspirationGainSet = mentalBreakInspirationGainSet; }
            if (allowedMeditationFocusTypes != null) { dd.allowedMeditationFocusTypes = allowedMeditationFocusTypes; }
            if (disallowedMeditationFocusTypes != null) { dd.disallowedMeditationFocusTypes = disallowedMeditationFocusTypes; }
            if (theOnlyAllowedMentalBreaks != null) { dd.theOnlyAllowedMentalBreaks = theOnlyAllowedMentalBreaks; }
            if (abilities != null) { dd.abilities = abilities; }
            if (enablesNeeds != null) { dd.enablesNeeds = enablesNeeds; }
            if (disablesNeeds != null) { dd.disablesNeeds = disablesNeeds; }
            if (skillGains != null) { dd.skillGains = skillGains; }
            if (aptitudes != null) { dd.aptitudes = aptitudes; }
        }
    }

    public static bool NotAvailable(TweakData data) => data.propType switch
    {
        _ => false,
    };
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(TraitData.TraitCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (parentTweakId.HasValue)
        {
            var parent = GetData(parentTweakId.Value);
            if (parent != null) return parent.propType;
        }
        return (int)TraitData.TraitCategory.SingleDegree;
    }
}
