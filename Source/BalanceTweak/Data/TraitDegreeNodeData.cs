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
            if (!IsFieldModified(nameof(statOffsets))) statOffsets ??= degreeData.statOffsets;
            if (!IsFieldModified(nameof(statFactors))) statFactors ??= degreeData.statFactors;
            if (!IsFieldModified(nameof(disallowedMentalStates))) disallowedMentalStates ??= degreeData.disallowedMentalStates;
            if (!IsFieldModified(nameof(disallowedThoughts))) disallowedThoughts ??= degreeData.disallowedThoughts;
            if (!IsFieldModified(nameof(disallowedInspirations))) disallowedInspirations ??= degreeData.disallowedInspirations;
            if (!IsFieldModified(nameof(mentalBreakInspirationGainSet))) mentalBreakInspirationGainSet ??= degreeData.mentalBreakInspirationGainSet;
            if (!IsFieldModified(nameof(allowedMeditationFocusTypes))) allowedMeditationFocusTypes ??= degreeData.allowedMeditationFocusTypes;
            if (!IsFieldModified(nameof(disallowedMeditationFocusTypes))) disallowedMeditationFocusTypes ??= degreeData.disallowedMeditationFocusTypes;
            if (!IsFieldModified(nameof(theOnlyAllowedMentalBreaks))) theOnlyAllowedMentalBreaks ??= degreeData.theOnlyAllowedMentalBreaks;
            if (!IsFieldModified(nameof(abilities))) abilities ??= degreeData.abilities;
            if (!IsFieldModified(nameof(enablesNeeds))) enablesNeeds ??= degreeData.enablesNeeds;
            if (!IsFieldModified(nameof(disablesNeeds))) disablesNeeds ??= degreeData.disablesNeeds;
            if (!IsFieldModified(nameof(skillGains))) skillGains ??= degreeData.skillGains;
            if (!IsFieldModified(nameof(aptitudes))) aptitudes ??= degreeData.aptitudes;
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
        ApplyS(nameof(subdefLabel), v => dd.label = v);
        if (dd != null)
        {
            ApplyV<int>(nameof(degree), v => dd.degree = v);
            ApplyV<float>(nameof(commonality), v => dd.commonality = v);
            ApplyV<float>(nameof(socialFightChanceFactor), v => dd.socialFightChanceFactor = v);
            ApplyV<float>(nameof(marketValueFactorOffset), v => dd.marketValueFactorOffset = v);
            ApplyV<float>(nameof(randomDiseaseMtbDays), v => dd.randomDiseaseMtbDays = v);
            ApplyV<float>(nameof(hungerRateFactor), v => dd.hungerRateFactor = v);
            ApplyV<float>(nameof(painOffset), v => dd.painOffset = v);
            ApplyV<float>(nameof(painFactor), v => dd.painFactor = v);
            ApplyV<float>(nameof(forcedMentalStateMtbDays), v => dd.forcedMentalStateMtbDays = v);
            ApplyV<float>(nameof(mentalBreakInspirationGainChance), v => dd.mentalBreakInspirationGainChance = v);
            ApplyD<MentalStateDef>(nameof(randomMentalState), v => dd.randomMentalState = v);
            ApplyD<MentalStateDef>(nameof(forcedMentalState), v => dd.forcedMentalState = v);
            ApplyL<StatModifier>(nameof(statOffsets), v => dd.statOffsets = v);
            ApplyL<StatModifier>(nameof(statFactors), v => dd.statFactors = v);
            ApplyL<MentalStateDef>(nameof(disallowedMentalStates), v => dd.disallowedMentalStates = v);
            ApplyL<ThoughtDef>(nameof(disallowedThoughts), v => dd.disallowedThoughts = v);
            ApplyL<InspirationDef>(nameof(disallowedInspirations), v => dd.disallowedInspirations = v);
            ApplyL<InspirationDef>(nameof(mentalBreakInspirationGainSet), v => dd.mentalBreakInspirationGainSet = v);
            ApplyL<MeditationFocusDef>(nameof(allowedMeditationFocusTypes), v => dd.allowedMeditationFocusTypes = v);
            ApplyL<MeditationFocusDef>(nameof(disallowedMeditationFocusTypes), v => dd.disallowedMeditationFocusTypes = v);
            ApplyL<MentalBreakDef>(nameof(theOnlyAllowedMentalBreaks), v => dd.theOnlyAllowedMentalBreaks = v);
            ApplyL<AbilityDef>(nameof(abilities), v => dd.abilities = v);
            ApplyL<NeedDef>(nameof(enablesNeeds), v => dd.enablesNeeds = v);
            ApplyL<NeedDef>(nameof(disablesNeeds), v => dd.disablesNeeds = v);
            ApplyL<SkillGain>(nameof(skillGains), v => dd.skillGains = v);
            ApplyL<Aptitude>(nameof(aptitudes), v => dd.aptitudes = v);
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
