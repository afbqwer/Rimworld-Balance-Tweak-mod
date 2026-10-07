using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(HediffDef), SettingType.Hediff)]
class HediffData : TweakData<HediffData>, ISubItemHost
{
    /// <summary>健康阶段子项 + VerbGiver 招式子项（原 Init() 里的两段硬编码）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not HediffDef d) yield break;

        if (!d.stages.NullOrEmpty())
        {
            for (int i = 0; i < d.stages.Count; i++)
            {
                if (d.stages[i] == null) continue;
                var sd = new HediffStageData { index = i };
                sd.SetParentTweak(parent, SettingType.HediffStage, tweaked);
                yield return sd;
            }
        }

        if (d.CompProps<HediffCompProperties_VerbGiver>() is HediffCompProperties_VerbGiver vg && !vg.tools.NullOrEmpty())
        {
            for (int i = 0; i < vg.tools.Count; i++)
            {
                var td = new ToolData { index = i };
                td.SetParentTweak(parent, SettingType.MeleeTool, tweaked);
                yield return td;
            }
        }
    }

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? FirstStage = null;
    // [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int)]
    // public int? stageCount = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfAddedPart))]
    public float? partEfficiency = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAddedPart))]
    public bool? solid = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAddedPart))]
    public bool? isGoodWeapon = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAddedPart))]
    public bool? betterThanNatural = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? initialSeverity = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? minSeverity = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? maxSeverity = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? lethalSeverity = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? tendable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isBad = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? makesSickThought = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? makesAlert = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? everCurableByItem = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? keepOnBodyPartRestoration = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksSocialInteraction = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? forceRemoveOnResurrection = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? preventsDeath = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? duplicationAllowed = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canAffectBionicOrImplant = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? alwaysShowSeverity = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool)]
    public bool? organicAddedBodypart = null;



    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Available = nameof(AvailableIfInjury))]
    public bool? injuryProps = null;

    // InjuryProps sub-object fields (only effective for Injury category)
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfInjury))]
    public float? painPerSeverity = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfInjury))]
    public float? averagePainPerSeverityPermanent = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfInjury))]
    public float? bleedRate = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfInjury))]
    public bool? canMerge = null;

    // AddedBodyPartProps sub-object fields (only effective for AddedPart category)
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfAddedPart))]
    public TweakID? VerbGiverTool = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Available = nameof(AvailableIfAddedPart))]
    public bool? addedPartProps = null;


    // === TendDuration Comp (no category restriction) ===
    [TweakField(Style = ColumnStyle.Float)]
    public float? tendDurationHours = null;

    [TweakField(Style = ColumnStyle.Prec)]
    public float? severityPerDayTended = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? tendAllAtOnce = null;

    // === Disappears Comp (no category restriction) ===
    [TweakField(Style = ColumnStyle.Range)]
    public IntRange? disappearsAfterTicks = null;

    // === SeverityPerDay Comp (no category restriction) ===
    [TweakField(Style = ColumnStyle.Float)]
    public float? severityPerDay = null;

    // === Immunizable Comp (only Disease category) ===
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHasImmunizable))]
    public float? immunityPerDaySick = null;

    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHasImmunizable))]
    public float? severityPerDayImmune = null;

    // === Infecter Comp (only Injury category) ===
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHasInfecter))]
    public float? infectionChance = null;

    // === TendDuration Comp extended fields ===
    [TweakField(Style = ColumnStyle.Int)]
    public int? disappearsAtTotalTendQuality = null;

    // === SeverityPerDay Comp extended fields ===
    [TweakField(Style = ColumnStyle.Range)]
    public FloatRange? severityPerDayRange = null;

    // === Immunizable Comp extended fields ===
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHasImmunizable))]
    public float? severityPerDayNotImmune = null;

    // === GetsPermanent Comp (only Injury, removable) ===
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfInjury))]
    public bool? hasGetsPermanent = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? becomePermanentChanceFactor = null;

    // === GiveHediff Comp (removable) ===
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? hasGiveHediff = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    public string? giveHediffLabel = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? giveHediffAtSeverity = null;

    // === ReactOnDamage Comp (only AddedPart) ===
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfAddedPart))]
    public bool? hasReactOnDamage = null;

    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfAddedPart))]
    public HediffDef? reactOnDamageCreateHediff = null;

    // === Phase 5: Extension fields ===
    [TweakField(Style = ColumnStyle.DefList)]
    public List<AbilityDef>? abilities = null;

    [TweakField(Style = ColumnStyle.HediffGiverList)]
    public List<HediffGiver>? hediffGivers = null;

    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? removeOnRedressChanceByDaysCurve = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    private List<string>? _hediffGiverRefs;

    private static readonly FieldInfo TendDurationBaseHours = AccessTools.Field(typeof(HediffCompProperties_TendDuration), "baseTendDurationHours");

    public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is HediffDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            searchString = label + d.defName;
            initialSeverity ??= d.initialSeverity;
            lethalSeverity ??= d.lethalSeverity;
            minSeverity ??= d.minSeverity;
            maxSeverity ??= d.maxSeverity;
            tendable ??= d.tendable;
            isBad ??= d.isBad;
            makesSickThought ??= d.makesSickThought;
            makesAlert ??= d.makesAlert;
            everCurableByItem ??= d.everCurableByItem;
            keepOnBodyPartRestoration ??= d.keepOnBodyPartRestoration;
            blocksSocialInteraction ??= d.blocksSocialInteraction;
            forceRemoveOnResurrection ??= d.forceRemoveOnResurrection;
            preventsDeath ??= d.preventsDeath;
            duplicationAllowed ??= d.duplicationAllowed;
            canAffectBionicOrImplant ??= d.canAffectBionicOrImplant;
            alwaysShowSeverity ??= d.alwaysShowSeverity;
            organicAddedBodypart ??= d.organicAddedBodypart;
            // stageCount = d.stages?.Count ?? 0;
            injuryProps = d.injuryProps != null;
            addedPartProps = d.addedPartProps != null;
            if (d.injuryProps != null)
            {
                painPerSeverity ??= d.injuryProps.painPerSeverity;
                averagePainPerSeverityPermanent ??= d.injuryProps.averagePainPerSeverityPermanent;
                bleedRate ??= d.injuryProps.bleedRate;
                canMerge ??= d.injuryProps.canMerge;
            }
            else
            {
                painPerSeverity ??= 0;
                averagePainPerSeverityPermanent ??= 0;
                bleedRate ??= 0;
                canMerge ??= false;
            }
            if (d.addedPartProps != null)
            {
                partEfficiency ??= d.addedPartProps.partEfficiency;
                solid ??= d.addedPartProps.solid;
                isGoodWeapon ??= d.addedPartProps.isGoodWeapon;
                betterThanNatural ??= d.addedPartProps.betterThanNatural;
            }
            else
            {
                partEfficiency ??= 0;
                solid ??= false;
                isGoodWeapon ??= false;
                betterThanNatural ??= false;
            }

            // Comp props reading
            if (d.CompProps<HediffCompProperties_TendDuration>() is HediffCompProperties_TendDuration tend)
            {
                tendDurationHours ??= (float?)TendDurationBaseHours.GetValue(tend);
                severityPerDayTended ??= tend.severityPerDayTended;
                tendAllAtOnce ??= tend.tendAllAtOnce;
                disappearsAtTotalTendQuality ??= tend.disappearsAtTotalTendQuality;
            }
            else
            {
                tendDurationHours ??= 0;
                severityPerDayTended ??= 0;
                tendAllAtOnce ??= false;
                disappearsAtTotalTendQuality ??= 0;
            }
            if (d.CompProps<HediffCompProperties_Disappears>() is HediffCompProperties_Disappears dis)
            {
                disappearsAfterTicks ??= dis.disappearsAfterTicks;
            }
            else
            {
                disappearsAfterTicks ??= new IntRange(0, 0);
            }
            if (d.CompProps<HediffCompProperties_SeverityPerDay>() is HediffCompProperties_SeverityPerDay spd)
            {
                severityPerDay ??= spd.severityPerDay;
                severityPerDayRange ??= spd.severityPerDayRange;
            }
            else
            {
                severityPerDay ??= 0;
                severityPerDayRange ??= new FloatRange(0, 0);
            }
            if (d.CompProps<HediffCompProperties_Immunizable>() is HediffCompProperties_Immunizable imm)
            {
                immunityPerDaySick ??= imm.immunityPerDaySick;
                severityPerDayImmune ??= imm.severityPerDayImmune;
                severityPerDayNotImmune ??= imm.severityPerDayNotImmune;
            }
            else
            {
                immunityPerDaySick ??= 0;
                severityPerDayImmune ??= 0;
                severityPerDayNotImmune ??= 0;
            }
            if (d.CompProps<HediffCompProperties_Infecter>() is HediffCompProperties_Infecter inf)
            {
                infectionChance ??= inf.infectionChance;
            }
            else
            {
                infectionChance ??= 0;
            }
            if (d.CompProps<HediffCompProperties_GetsPermanent>() is HediffCompProperties_GetsPermanent gp)
            {
                hasGetsPermanent ??= true;
                becomePermanentChanceFactor ??= gp.becomePermanentChanceFactor;
            }
            else
            {
                hasGetsPermanent ??= false;
                becomePermanentChanceFactor ??= 0;
            }
            if (d.CompProps<HediffCompProperties_GiveHediff>() is HediffCompProperties_GiveHediff gh)
            {
                hasGiveHediff ??= true;
                giveHediffLabel = gh.hediffDef?.LabelCap ?? "(none)";
                giveHediffAtSeverity ??= gh.atSeverity;
            }
            else
            {
                hasGiveHediff ??= false;
                giveHediffLabel = "(none)";
                giveHediffAtSeverity ??= 0;
            }
            if (d.CompProps<HediffCompProperties_ReactOnDamage>() is HediffCompProperties_ReactOnDamage rod)
            {
                hasReactOnDamage ??= true;
                reactOnDamageCreateHediff ??= rod.createHediff;
            }
            else
            {
                hasReactOnDamage ??= false;
                reactOnDamageCreateHediff ??= null;
            }

            // Phase 5: Extension fields
            abilities ??= d.abilities;

            if (_hediffGiverRefs != null)
            {
                hediffGivers = d.hediffGivers?
                    .Where(hg => hg.hediff != null && _hediffGiverRefs.Contains(hg.hediff.defName))
                    .ToList();
            }
            else
            {
                hediffGivers ??= d.hediffGivers;
            }

            removeOnRedressChanceByDaysCurve ??= d.removeOnRedressChanceByDaysCurve;
        }
    }

    public override void Apply()
    {
        if (this.def is not HediffDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (initialSeverity.HasValue) { def.initialSeverity = initialSeverity.Value; }
        if (lethalSeverity.HasValue) { def.lethalSeverity = lethalSeverity.Value; }
        if (minSeverity.HasValue) { def.minSeverity = minSeverity.Value; }
        if (maxSeverity.HasValue) { def.maxSeverity = maxSeverity.Value; }
        if (tendable.HasValue) { def.tendable = tendable.Value; }
        if (isBad.HasValue) { def.isBad = isBad.Value; }
        if (makesSickThought.HasValue) { def.makesSickThought = makesSickThought.Value; }
        if (makesAlert.HasValue) { def.makesAlert = makesAlert.Value; }
        if (everCurableByItem.HasValue) { def.everCurableByItem = everCurableByItem.Value; }
        if (keepOnBodyPartRestoration.HasValue) { def.keepOnBodyPartRestoration = keepOnBodyPartRestoration.Value; }
        if (blocksSocialInteraction.HasValue) { def.blocksSocialInteraction = blocksSocialInteraction.Value; }
        if (forceRemoveOnResurrection.HasValue) { def.forceRemoveOnResurrection = forceRemoveOnResurrection.Value; }
        if (preventsDeath.HasValue) { def.preventsDeath = preventsDeath.Value; }
        if (duplicationAllowed.HasValue) { def.duplicationAllowed = duplicationAllowed.Value; }
        if (canAffectBionicOrImplant.HasValue) { def.canAffectBionicOrImplant = canAffectBionicOrImplant.Value; }
        if (alwaysShowSeverity.HasValue) { def.alwaysShowSeverity = alwaysShowSeverity.Value; }
        if (organicAddedBodypart.HasValue) { def.organicAddedBodypart = organicAddedBodypart.Value; }
        if (def.injuryProps != null)
        {
            if (painPerSeverity.HasValue) def.injuryProps.painPerSeverity = painPerSeverity.Value;
            if (averagePainPerSeverityPermanent.HasValue) def.injuryProps.averagePainPerSeverityPermanent = averagePainPerSeverityPermanent.Value;
            if (bleedRate.HasValue) def.injuryProps.bleedRate = bleedRate.Value;
            if (canMerge.HasValue) def.injuryProps.canMerge = canMerge.Value;
        }
        if (def.addedPartProps != null)
        {
            if (partEfficiency.HasValue) def.addedPartProps.partEfficiency = partEfficiency.Value;
            if (solid.HasValue) def.addedPartProps.solid = solid.Value;
            if (isGoodWeapon.HasValue) def.addedPartProps.isGoodWeapon = isGoodWeapon.Value;
            if (betterThanNatural.HasValue) def.addedPartProps.betterThanNatural = betterThanNatural.Value;
        }

        // Comp props writing
        if (def.CompProps<HediffCompProperties_TendDuration>() is HediffCompProperties_TendDuration tend)
        {
            if (tendDurationHours.HasValue) TendDurationBaseHours.SetValue(tend, tendDurationHours.Value);
            if (severityPerDayTended.HasValue) tend.severityPerDayTended = severityPerDayTended.Value;
            if (tendAllAtOnce.HasValue) tend.tendAllAtOnce = tendAllAtOnce.Value;
            if (disappearsAtTotalTendQuality.HasValue) tend.disappearsAtTotalTendQuality = disappearsAtTotalTendQuality.Value;
        }
        if (def.CompProps<HediffCompProperties_Disappears>() is HediffCompProperties_Disappears dis)
        {
            if (disappearsAfterTicks.HasValue) dis.disappearsAfterTicks = disappearsAfterTicks.Value;
        }
        if (def.CompProps<HediffCompProperties_SeverityPerDay>() is HediffCompProperties_SeverityPerDay spd)
        {
            if (severityPerDay.HasValue) spd.severityPerDay = severityPerDay.Value;
            if (severityPerDayRange.HasValue) spd.severityPerDayRange = severityPerDayRange.Value;
        }
        if (def.CompProps<HediffCompProperties_Immunizable>() is HediffCompProperties_Immunizable imm)
        {
            if (immunityPerDaySick.HasValue) imm.immunityPerDaySick = immunityPerDaySick.Value;
            if (severityPerDayImmune.HasValue) imm.severityPerDayImmune = severityPerDayImmune.Value;
            if (severityPerDayNotImmune.HasValue) imm.severityPerDayNotImmune = severityPerDayNotImmune.Value;
        }
        if (def.CompProps<HediffCompProperties_Infecter>() is HediffCompProperties_Infecter inf)
        {
            if (infectionChance.HasValue) inf.infectionChance = infectionChance.Value;
        }

        // GetsPermanent Comp toggle
        if (hasGetsPermanent.HasValue)
        {
            if (hasGetsPermanent.Value == false)
            {
                def.comps?.RemoveWhere(c => c is HediffCompProperties_GetsPermanent);
            }
            else
            {
                if (def.CompProps<HediffCompProperties_GetsPermanent>() == null)
                {
                    def.comps ??= new List<HediffCompProperties>();
                    def.comps.Add(new HediffCompProperties_GetsPermanent());
                }
                if (def.CompProps<HediffCompProperties_GetsPermanent>() is HediffCompProperties_GetsPermanent gp)
                {
                    if (becomePermanentChanceFactor.HasValue) gp.becomePermanentChanceFactor = becomePermanentChanceFactor.Value;
                }
            }
        }
        else if (becomePermanentChanceFactor.HasValue && def.CompProps<HediffCompProperties_GetsPermanent>() is HediffCompProperties_GetsPermanent gp)
        {
            gp.becomePermanentChanceFactor = becomePermanentChanceFactor.Value;
        }

        // GiveHediff Comp toggle
        if (hasGiveHediff.HasValue)
        {
            if (hasGiveHediff.Value == false)
            {
                def.comps?.RemoveWhere(c => c is HediffCompProperties_GiveHediff);
            }
            else
            {
                if (def.CompProps<HediffCompProperties_GiveHediff>() == null)
                {
                    def.comps ??= new List<HediffCompProperties>();
                    def.comps.Add(new HediffCompProperties_GiveHediff());
                }
                if (def.CompProps<HediffCompProperties_GiveHediff>() is HediffCompProperties_GiveHediff gh)
                {
                    if (giveHediffAtSeverity.HasValue) gh.atSeverity = giveHediffAtSeverity.Value;
                }
            }
        }
        else if (giveHediffAtSeverity.HasValue && def.CompProps<HediffCompProperties_GiveHediff>() is HediffCompProperties_GiveHediff gh)
        {
            gh.atSeverity = giveHediffAtSeverity.Value;
        }

        // ReactOnDamage Comp toggle
        if (hasReactOnDamage.HasValue)
        {
            if (hasReactOnDamage.Value == false)
            {
                def.comps?.RemoveWhere(c => c is HediffCompProperties_ReactOnDamage);
            }
            else
            {
                if (def.CompProps<HediffCompProperties_ReactOnDamage>() is HediffCompProperties_ReactOnDamage rod)
                {
                    if (reactOnDamageCreateHediff != null) rod.createHediff = reactOnDamageCreateHediff;
                }
            }
        }

        // Phase 5: Extension fields
        if (abilities != null) { def.abilities = abilities; }
        if (hediffGivers != null) { def.hediffGivers = hediffGivers; }
        if (removeOnRedressChanceByDaysCurve != null) { def.removeOnRedressChanceByDaysCurve = removeOnRedressChanceByDaysCurve; }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _hediffGiverRefs = hediffGivers?.Select(hg => hg.hediff?.defName ?? "NULL").ToList();
        }
        Scribe_Collections.Look(ref _hediffGiverRefs, "hediffGivers", LookMode.Value);
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(HediffCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    public enum HediffCategory
    {
        AddedPart,
        Injury,
        Addiction,
        Level,
        Disease,
        Pregnancy,
        Psyche,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is HediffDef def)
        {
            Type hediffClass = def.hediffClass;
            if (def.countsAsAddedPartOrImplant || typeof(Hediff_Implant).IsAssignableFrom(hediffClass))
            {
                return (int)HediffCategory.AddedPart;
            }
            if (typeof(Hediff_Injury).IsAssignableFrom(hediffClass))
            {
                return (int)HediffCategory.Injury;
            }
            if (typeof(Hediff_Addiction).IsAssignableFrom(hediffClass)
                || typeof(Hediff_High).IsAssignableFrom(hediffClass)
                || def.CompProps<HediffCompProperties_DrugEffectFactor>() != null
                || hediffClass == typeof(Hediff_ChemicalDependency))
            {
                return (int)HediffCategory.Addiction;
            }
            if (typeof(Hediff_Level).IsAssignableFrom(hediffClass) || def.levelIsQuantity)
            {
                return (int)HediffCategory.Level;
            }
            if (def.makesSickThought || def.chronic)
            {
                return (int)HediffCategory.Disease;
            }
            if (def.preventsPregnancy)
            {
                return (int)HediffCategory.Pregnancy;
            }
            string dn = def.defName;
            if (dn.IndexOf("mind", StringComparison.OrdinalIgnoreCase) >= 0
                || dn.IndexOf("psychic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return (int)HediffCategory.Psyche;
            }
        }
        return (int)HediffCategory.Other;
    }

    public static bool AvailableIfInjury(TweakData data) => data.propType switch
    {
        (int)HediffCategory.Injury => true,
        _ => false,
    };

    public static bool AvailableIfAddedPart(TweakData data) => data.propType switch
    {
        (int)HediffCategory.AddedPart => true,
        _ => false,
    };

    public static bool AvailableIfDisease(TweakData data) => data.propType switch
    {
        (int)HediffCategory.Disease => true,
        _ => false,
    };

    public static bool AvailableIfHasImmunizable(TweakData data) => data.propType == (int)HediffCategory.Disease;
    public static bool AvailableIfHasInfecter(TweakData data) => data.propType == (int)HediffCategory.Injury;
}