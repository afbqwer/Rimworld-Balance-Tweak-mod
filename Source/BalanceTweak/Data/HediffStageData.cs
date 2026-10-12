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
            if (!IsFieldModified(nameof(statOffsets))) statOffsets ??= stage.statOffsets;
            if (!IsFieldModified(nameof(statFactors))) statFactors ??= stage.statFactors;
            if (!IsFieldModified(nameof(capMods))) capMods ??= stage.capMods;
            if (!IsFieldModified(nameof(damageFactors))) damageFactors ??= stage.damageFactors;
            if (!IsFieldModified(nameof(enablesNeeds))) enablesNeeds ??= stage.enablesNeeds;
            if (!IsFieldModified(nameof(disablesNeeds))) disablesNeeds ??= stage.disablesNeeds;
            if (!IsFieldModified(nameof(makeImmuneTo))) makeImmuneTo ??= stage.makeImmuneTo;

            if (_hediffGiverRefs != null)
            {
                hediffGivers = stage.hediffGivers?
                    .Where(hg => hg.hediff != null && _hediffGiverRefs.Contains(hg.hediff.defName))
                    .ToList();
            }
            else
            {
                if (!IsFieldModified(nameof(hediffGivers))) hediffGivers ??= stage.hediffGivers?.ToList();
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
                if (!IsFieldModified(nameof(mentalStateGivers))) mentalStateGivers ??= stage.mentalStateGivers?.Select(m => new MentalStateGiver
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
        ApplyS(nameof(subdefLabel), v => stage.label = v);
        ApplyS(nameof(subdefOverrideLabel), v => stage.overrideLabel = v);
        if (stage != null)
        {
            ApplyV<bool>(nameof(becomeVisible), v => stage.becomeVisible = v);
            ApplyV<bool>(nameof(lifeThreatening), v => stage.lifeThreatening = v);
            ApplyV<float>(nameof(painFactor), v => stage.painFactor = v);
            ApplyV<float>(nameof(painOffset), v => stage.painOffset = v);
            ApplyV<float>(nameof(totalBleedFactor), v => stage.totalBleedFactor = v);
            ApplyV<float>(nameof(naturalHealingFactor), v => stage.naturalHealingFactor = v);
            ApplyV<float>(nameof(vomitMtbDays), v => stage.vomitMtbDays = v);
            ApplyV<float>(nameof(deathMtbDays), v => stage.deathMtbDays = v);
            ApplyV<float>(nameof(hungerRateFactor), v => stage.hungerRateFactor = v);
            ApplyV<float>(nameof(hungerRateFactorOffset), v => stage.hungerRateFactorOffset = v);
            ApplyV<float>(nameof(restFallFactor), v => stage.restFallFactor = v);
            ApplyV<float>(nameof(restFallFactorOffset), v => stage.restFallFactorOffset = v);
            ApplyV<float>(nameof(fertilityFactor), v => stage.fertilityFactor = v);
            ApplyV<float>(nameof(severityGainFactor), v => stage.severityGainFactor = v);
            ApplyV<float>(nameof(partEfficiencyOffset), v => stage.partEfficiencyOffset = v);
            ApplyV<float>(nameof(mentalBreakMtbDays), v => stage.mentalBreakMtbDays = v);
            ApplyV<bool>(nameof(blocksMentalBreaks), v => stage.blocksMentalBreaks = v);
            ApplyV<bool>(nameof(blocksInspirations), v => stage.blocksInspirations = v);
            ApplyV<float>(nameof(overrideMoodBase), v => stage.overrideMoodBase = v);
            ApplyV<float>(nameof(opinionOfOthersFactor), v => stage.opinionOfOthersFactor = v);
            ApplyV<float>(nameof(forgetMemoryThoughtMtbDays), v => stage.forgetMemoryThoughtMtbDays = v);
            ApplyV<bool>(nameof(blocksSleeping), v => stage.blocksSleeping = v);
            ApplyV<bool>(nameof(preventVacuumBurns), v => stage.preventVacuumBurns = v);
            ApplyV<float>(nameof(regeneration), v => stage.regeneration = v);
            ApplyV<bool>(nameof(showRegenerationStat), v => stage.showRegenerationStat = v);
            ApplyV<bool>(nameof(multiplyStatChangesBySeverity), v => stage.multiplyStatChangesBySeverity = v);
            ApplyD<StatDef>(nameof(statOffsetEffectMultiplier), v => stage.statOffsetEffectMultiplier = v);
            ApplyD<StatDef>(nameof(statFactorEffectMultiplier), v => stage.statFactorEffectMultiplier = v);
            ApplyD<StatDef>(nameof(capacityFactorEffectMultiplier), v => stage.capacityFactorEffectMultiplier = v);
            ApplyL<StatModifier>(nameof(statOffsets), v => stage.statOffsets = v);
            ApplyL<StatModifier>(nameof(statFactors), v => stage.statFactors = v);
            ApplyL<PawnCapacityModifier>(nameof(capMods), v => stage.capMods = v);
            ApplyL<DamageFactor>(nameof(damageFactors), v => stage.damageFactors = v);
            ApplyL<NeedDef>(nameof(enablesNeeds), v => stage.enablesNeeds = v);
            ApplyL<NeedDef>(nameof(disablesNeeds), v => stage.disablesNeeds = v);
            ApplyL<HediffDef>(nameof(makeImmuneTo), v => stage.makeImmuneTo = v);
            ApplyL<HediffGiver>(nameof(hediffGivers), v => stage.hediffGivers = v);
            ApplyL<MentalStateGiver>(nameof(mentalStateGivers), v => stage.mentalStateGivers = v);
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