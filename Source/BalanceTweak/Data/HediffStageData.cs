using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;


class HediffStageData : TweakData<HediffStageData>
{
    [TweakField(DataType = ColumnDataType.Field, Style = ColumnStyle.String, Available = nameof(NotAvailable))]
    public string? stageLabel = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? Hediff = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? minSeverity = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? becomeVisible = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? lifeThreatening = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;

    [TweakField(Style = ColumnStyle.CapModList)]
    public List<PawnCapacityModifier>? capMods = null;

    [TweakField(Style = ColumnStyle.DamageModList)]
    public List<DamageFactor>? damageFactors = null;

    [TweakField()]
    public float? painFactor = null;

    [TweakField()]
    public float? painOffset = null;

    [TweakField()]
    public float? totalBleedFactor = null;

    [TweakField()]
    public float? naturalHealingFactor = null;

    [TweakField()]
    public float? vomitMtbDays = null;

    [TweakField()]
    public float? deathMtbDays = null;

    //[TweakField(Style = ColumnStyle.Bool)]
    //public bool? mtbDeathDestroysBrain = null;

    [TweakField()]
    public float? hungerRateFactor = null;

    [TweakField()]
    public float? hungerRateFactorOffset = null;

    [TweakField()]
    public float? restFallFactor = null;

    [TweakField()]
    public float? restFallFactorOffset = null;

    [TweakField()]
    public float? fertilityFactor = null;

    [TweakField()]
    public float? severityGainFactor = null;

    [TweakField()]
    public float? partEfficiencyOffset = null;

    //[TweakField(Style = ColumnStyle.Bool)]
    //public bool? destroyPart = null;

    //[TweakField(Style = ColumnStyle.Bool)]
    //public bool? partIgnoreMissingHP = null;

    //[TweakField()]
    //public float? socialFightChanceFactor = null;

    //[TweakField()]
    //public float? foodPoisoningChanceFactor = null;

    [TweakField()]
    public float? mentalBreakMtbDays = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksMentalBreaks = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksInspirations = null;

    [TweakField()]
    public float? overrideMoodBase = null;

    [TweakField()]
    public float? opinionOfOthersFactor = null;

    [TweakField()]
    public float? forgetMemoryThoughtMtbDays = null;

    //[TweakField()]
    //public float? pctConditionalThoughtsNullified = null;

    //[TweakField()]
    //public float? pctAllThoughtNullification = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksSleeping = null;

    //[TweakField(Style = ColumnStyle.Bool)]
    //public bool? removeRoamMtb = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? preventVacuumBurns = null;

    [TweakField()]
    public float? regeneration = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? showRegenerationStat = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? multiplyStatChangesBySeverity = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public StatDef? statOffsetEffectMultiplier = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public StatDef? statFactorEffectMultiplier = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public StatDef? capacityFactorEffectMultiplier = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? enablesNeeds = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? disablesNeeds = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? makeImmuneTo = null;

    [TweakField(Style = ColumnStyle.HediffGiverList)]
    public List<HediffGiver>? hediffGivers = null;

    [TweakField(Style = ColumnStyle.MentalStateGiverList)]
    public List<MentalStateGiver>? mentalStateGivers = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? subdefLabel = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? subdefOverrideLabel = null;

    private List<string>? _hediffGiverRefs;
    private List<string>? _mentalStateGiverRefs;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    //public string? mentalBreakExplanation = null;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    //public string? overrideTooltip = null;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    //public string? extraTooltip = null;

    public override int LoadingOrd => 300;
    private HediffStage? stage = null;

    public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false)
    {
        base.SetParentTweak(data, type, tweaked);
        propType = data.propType;
        if (data.def is HediffDef def && index.HasValue)
        {
            stage = def.stages[index.Value];
            if (stage == null) { return false; }
            var l = stage.label ?? stage.overrideLabel ?? $"{def.label}_{index}";
            if (!stageLabel.NullOrEmpty() && stage.untranslatedLabel != stageLabel)
            {
                Log.Warning($"[BalanceTweak]{def} stage的名称错误：{stageLabel}");
                return false;
            }
            stageLabel = stage.untranslatedLabel;
            label = l;
            subdefLabel ??= stage.label;
            subdefOverrideLabel ??= stage.overrideLabel;
            id = new($"{def.defName}_{index}", type);
            searchString = $"{def.defName}{index}{l}";
            var li = GetAllData(data.id);
            foreach (var item in li)
            {
                ((HediffData)item).FirstStage ??= this.id;
            }
            Hediff = data.id;
            minSeverity = stage.minSeverity;
            becomeVisible ??= stage.becomeVisible;
            lifeThreatening ??= stage.lifeThreatening;
            painFactor ??= stage.painFactor;
            painOffset ??= stage.painOffset;
            totalBleedFactor ??= stage.totalBleedFactor;
            naturalHealingFactor ??= stage.naturalHealingFactor;
            vomitMtbDays ??= stage.vomitMtbDays;
            deathMtbDays ??= stage.deathMtbDays;
            hungerRateFactor ??= stage.hungerRateFactor;
            hungerRateFactorOffset ??= stage.hungerRateFactorOffset;
            restFallFactor ??= stage.restFallFactor;
            restFallFactorOffset ??= stage.restFallFactorOffset;
            fertilityFactor ??= stage.fertilityFactor;
            severityGainFactor ??= stage.severityGainFactor;
            partEfficiencyOffset ??= stage.partEfficiencyOffset;
            mentalBreakMtbDays ??= stage.mentalBreakMtbDays;
            blocksMentalBreaks ??= stage.blocksMentalBreaks;
            blocksInspirations ??= stage.blocksInspirations;
            overrideMoodBase ??= stage.overrideMoodBase;
            opinionOfOthersFactor ??= stage.opinionOfOthersFactor;
            forgetMemoryThoughtMtbDays ??= stage.forgetMemoryThoughtMtbDays;
            blocksSleeping ??= stage.blocksSleeping;
            preventVacuumBurns ??= stage.preventVacuumBurns;
            regeneration ??= stage.regeneration;
            showRegenerationStat ??= stage.showRegenerationStat;
            multiplyStatChangesBySeverity ??= stage.multiplyStatChangesBySeverity;
            statOffsetEffectMultiplier ??= stage.statOffsetEffectMultiplier;
            statFactorEffectMultiplier ??= stage.statFactorEffectMultiplier;
            capacityFactorEffectMultiplier ??= stage.capacityFactorEffectMultiplier;
            statOffsets ??= stage.statOffsets;
            statFactors ??= stage.statFactors;
            capMods ??= stage.capMods;
            damageFactors ??= stage.damageFactors;
            enablesNeeds ??= stage.enablesNeeds;
            disablesNeeds ??= stage.disablesNeeds;
            makeImmuneTo ??= stage.makeImmuneTo;

            if (_hediffGiverRefs != null)
            {
                hediffGivers = stage.hediffGivers?
                    .Where(hg => hg.hediff != null && _hediffGiverRefs.Contains(hg.hediff.defName))
                    .ToList();
            }
            else
            {
                hediffGivers ??= stage.hediffGivers?.ToList();
            }

            if (_mentalStateGiverRefs != null)
            {
                var parsedRefs = _mentalStateGiverRefs
                    .Select(s =>
                    {
                        var parts = s.Split('|');
                        if (parts.Length == 2 && float.TryParse(parts[1], out var mtb))
                            return (defName: parts[0], mtbDays: mtb);
                        return (defName: parts.Length > 0 ? parts[0] : null, mtbDays: 0f);
                    })
                    .ToList();
                mentalStateGivers = stage.mentalStateGivers?
                    .Where(msg => msg.mentalState != null && parsedRefs.Any(r => r.defName == msg.mentalState.defName))
                    .Select(msg =>
                    {
                        var refData = parsedRefs.FirstOrDefault(r => r.defName == msg.mentalState.defName);
                        return new MentalStateGiver
                        {
                            mentalState = msg.mentalState,
                            mtbDays = refData.mtbDays
                        };
                    })
                    .ToList();
            }
            else
            {
                mentalStateGivers ??= stage.mentalStateGivers?.Select(m => new MentalStateGiver
                {
                    mentalState = m.mentalState,
                    mtbDays = m.mtbDays
                }).ToList();
            }
        }
        return true;
    }

    public override void Apply()
    {
        if (this.stage is not HediffStage stage)
        {
            Log.Error($"[BalanceTweak]{this}的stage为{this.stage}!");
            return;
        }
        if (subdefLabel != null) stage.label = subdefLabel;
        if (subdefOverrideLabel != null) stage.overrideLabel = subdefOverrideLabel;
        if (stage != null)
        {
            if (becomeVisible.HasValue) { stage.becomeVisible = becomeVisible.Value; }
            if (lifeThreatening.HasValue) { stage.lifeThreatening = lifeThreatening.Value; }
            if (painFactor.HasValue) { stage.painFactor = painFactor.Value; }
            if (painOffset.HasValue) { stage.painOffset = painOffset.Value; }
            if (totalBleedFactor.HasValue) { stage.totalBleedFactor = totalBleedFactor.Value; }
            if (naturalHealingFactor.HasValue) { stage.naturalHealingFactor = naturalHealingFactor.Value; }
            if (vomitMtbDays.HasValue) { stage.vomitMtbDays = vomitMtbDays.Value; }
            if (deathMtbDays.HasValue) { stage.deathMtbDays = deathMtbDays.Value; }
            if (hungerRateFactor.HasValue) { stage.hungerRateFactor = hungerRateFactor.Value; }
            if (hungerRateFactorOffset.HasValue) { stage.hungerRateFactorOffset = hungerRateFactorOffset.Value; }
            if (restFallFactor.HasValue) { stage.restFallFactor = restFallFactor.Value; }
            if (restFallFactorOffset.HasValue) { stage.restFallFactorOffset = restFallFactorOffset.Value; }
            if (fertilityFactor.HasValue) { stage.fertilityFactor = fertilityFactor.Value; }
            if (severityGainFactor.HasValue) { stage.severityGainFactor = severityGainFactor.Value; }
            if (partEfficiencyOffset.HasValue) { stage.partEfficiencyOffset = partEfficiencyOffset.Value; }
            if (mentalBreakMtbDays.HasValue) { stage.mentalBreakMtbDays = mentalBreakMtbDays.Value; }
            if (blocksMentalBreaks.HasValue) { stage.blocksMentalBreaks = blocksMentalBreaks.Value; }
            if (blocksInspirations.HasValue) { stage.blocksInspirations = blocksInspirations.Value; }
            if (overrideMoodBase.HasValue) { stage.overrideMoodBase = overrideMoodBase.Value; }
            if (opinionOfOthersFactor.HasValue) { stage.opinionOfOthersFactor = opinionOfOthersFactor.Value; }
            if (forgetMemoryThoughtMtbDays.HasValue) { stage.forgetMemoryThoughtMtbDays = forgetMemoryThoughtMtbDays.Value; }
            if (blocksSleeping.HasValue) { stage.blocksSleeping = blocksSleeping.Value; }
            if (preventVacuumBurns.HasValue) { stage.preventVacuumBurns = preventVacuumBurns.Value; }
            if (regeneration.HasValue) { stage.regeneration = regeneration.Value; }
            if (showRegenerationStat.HasValue) { stage.showRegenerationStat = showRegenerationStat.Value; }
            if (multiplyStatChangesBySeverity.HasValue) { stage.multiplyStatChangesBySeverity = multiplyStatChangesBySeverity.Value; }
            if (statOffsetEffectMultiplier != null) { stage.statOffsetEffectMultiplier = statOffsetEffectMultiplier; }
            if (statFactorEffectMultiplier != null) { stage.statFactorEffectMultiplier = statFactorEffectMultiplier; }
            if (capacityFactorEffectMultiplier != null) { stage.capacityFactorEffectMultiplier = capacityFactorEffectMultiplier; }
            if (statOffsets != null) { stage.statOffsets = statOffsets; }
            if (statFactors != null) { stage.statFactors = statFactors; }
            if (capMods != null) { stage.capMods = capMods; }
            if (damageFactors != null) { stage.damageFactors = damageFactors; }
            if (enablesNeeds != null) { stage.enablesNeeds = enablesNeeds; }
            if (disablesNeeds != null) { stage.disablesNeeds = disablesNeeds; }
            if (makeImmuneTo != null) { stage.makeImmuneTo = makeImmuneTo; }
            if (hediffGivers != null) { stage.hediffGivers = hediffGivers; }
            if (mentalStateGivers != null) { stage.mentalStateGivers = mentalStateGivers; }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _hediffGiverRefs = hediffGivers?.Select(hg => hg.hediff?.defName ?? "NULL").ToList();
        }
        Scribe_Collections.Look(ref _hediffGiverRefs, "hediffGivers", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _mentalStateGiverRefs = mentalStateGivers?.Select(msg => $"{msg.mentalState?.defName ?? "NULL"}|{msg.mtbDays}").ToList();
        }
        Scribe_Collections.Look(ref _mentalStateGiverRefs, "mentalStateGivers", LookMode.Value);
    }

    public static bool NotAvailable(TweakData data) => data.propType switch
    {
        _ => false,
    };

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(HediffData.HediffCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (parentTweakId.HasValue)
        {
            var parent = GetData(parentTweakId.Value);
            if (parent != null) return parent.propType;
        }
        return (int)HediffData.HediffCategory.Other;
    }
}