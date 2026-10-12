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
            if (!IsFieldModified(nameof(abilities))) abilities ??= d.abilities;

            if (_hediffGiverRefs != null)
            {
                hediffGivers = d.hediffGivers?
                    .Where(hg => hg.hediff != null && _hediffGiverRefs.Contains(hg.hediff.defName))
                    .ToList();
            }
            else
            {
                if (!IsFieldModified(nameof(hediffGivers))) hediffGivers ??= d.hediffGivers;
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
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyV<float>(nameof(initialSeverity), v => def.initialSeverity = v);
        ApplyV<float>(nameof(lethalSeverity), v => def.lethalSeverity = v);
        ApplyV<float>(nameof(minSeverity), v => def.minSeverity = v);
        ApplyV<float>(nameof(maxSeverity), v => def.maxSeverity = v);
        ApplyV<bool>(nameof(tendable), v => def.tendable = v);
        ApplyV<bool>(nameof(isBad), v => def.isBad = v);
        ApplyV<bool>(nameof(makesSickThought), v => def.makesSickThought = v);
        ApplyV<bool>(nameof(makesAlert), v => def.makesAlert = v);
        ApplyV<bool>(nameof(everCurableByItem), v => def.everCurableByItem = v);
        ApplyV<bool>(nameof(keepOnBodyPartRestoration), v => def.keepOnBodyPartRestoration = v);
        ApplyV<bool>(nameof(blocksSocialInteraction), v => def.blocksSocialInteraction = v);
        ApplyV<bool>(nameof(forceRemoveOnResurrection), v => def.forceRemoveOnResurrection = v);
        ApplyV<bool>(nameof(preventsDeath), v => def.preventsDeath = v);
        ApplyV<bool>(nameof(duplicationAllowed), v => def.duplicationAllowed = v);
        ApplyV<bool>(nameof(canAffectBionicOrImplant), v => def.canAffectBionicOrImplant = v);
        ApplyV<bool>(nameof(alwaysShowSeverity), v => def.alwaysShowSeverity = v);
        ApplyV<bool>(nameof(organicAddedBodypart), v => def.organicAddedBodypart = v);
        if (def.injuryProps != null)
        {
            ApplyV<float>(nameof(painPerSeverity), v => def.injuryProps.painPerSeverity = v);
            ApplyV<float>(nameof(averagePainPerSeverityPermanent), v => def.injuryProps.averagePainPerSeverityPermanent = v);
            ApplyV<float>(nameof(bleedRate), v => def.injuryProps.bleedRate = v);
            ApplyV<bool>(nameof(canMerge), v => def.injuryProps.canMerge = v);
        }
        if (def.addedPartProps != null)
        {
            ApplyV<float>(nameof(partEfficiency), v => def.addedPartProps.partEfficiency = v);
            ApplyV<bool>(nameof(solid), v => def.addedPartProps.solid = v);
            ApplyV<bool>(nameof(isGoodWeapon), v => def.addedPartProps.isGoodWeapon = v);
            ApplyV<bool>(nameof(betterThanNatural), v => def.addedPartProps.betterThanNatural = v);
        }

        // Comp props writing
        if (def.CompProps<HediffCompProperties_TendDuration>() is HediffCompProperties_TendDuration tend)
        {
            ApplyV<float>(nameof(tendDurationHours), v => TendDurationBaseHours.SetValue(tend, v));
            ApplyV<float>(nameof(severityPerDayTended), v => tend.severityPerDayTended = v);
            ApplyV<bool>(nameof(tendAllAtOnce), v => tend.tendAllAtOnce = v);
            ApplyV<int>(nameof(disappearsAtTotalTendQuality), v => tend.disappearsAtTotalTendQuality = v);
        }
        if (def.CompProps<HediffCompProperties_Disappears>() is HediffCompProperties_Disappears dis)
        {
            ApplyV<IntRange>(nameof(disappearsAfterTicks), v => dis.disappearsAfterTicks = v);
        }
        if (def.CompProps<HediffCompProperties_SeverityPerDay>() is HediffCompProperties_SeverityPerDay spd)
        {
            ApplyV<float>(nameof(severityPerDay), v => spd.severityPerDay = v);
            ApplyV<FloatRange>(nameof(severityPerDayRange), v => spd.severityPerDayRange = v);
        }
        if (def.CompProps<HediffCompProperties_Immunizable>() is HediffCompProperties_Immunizable imm)
        {
            ApplyV<float>(nameof(immunityPerDaySick), v => imm.immunityPerDaySick = v);
            ApplyV<float>(nameof(severityPerDayImmune), v => imm.severityPerDayImmune = v);
            ApplyV<float>(nameof(severityPerDayNotImmune), v => imm.severityPerDayNotImmune = v);
        }
        if (def.CompProps<HediffCompProperties_Infecter>() is HediffCompProperties_Infecter inf)
        {
            ApplyV<float>(nameof(infectionChance), v => inf.infectionChance = v);
        }

        // GetsPermanent Comp toggle
        // 注意：hasGetsPermanent / becomePermanentChanceFactor 在 SetDef 里恒被回填为非空（见本文件 ~310-319），
        // 故旧代码 `hasGetsPermanent.HasValue` 恒真、下面的 else-if 是死分支。改用 StateOf 判"用户是否真的动过"，
        // 未动过时走统一分支（因子按 ApplyV 自我门控）。
        void ApplyBecomePermanentFactor() => ApplyV<float>(nameof(becomePermanentChanceFactor), v =>
        {
            if (def.CompProps<HediffCompProperties_GetsPermanent>() is HediffCompProperties_GetsPermanent gp)
                gp.becomePermanentChanceFactor = v;
        });
        if (StateOf(nameof(hasGetsPermanent)) == FieldState.Set)
        {
            if (!(bool)RawVal(nameof(hasGetsPermanent))!)
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
                ApplyBecomePermanentFactor();
            }
        }
        else
        {
            ApplyBecomePermanentFactor();
        }

        // GiveHediff Comp toggle
        void ApplyGiveHediffSeverity() => ApplyV<float>(nameof(giveHediffAtSeverity), v =>
        {
            if (def.CompProps<HediffCompProperties_GiveHediff>() is HediffCompProperties_GiveHediff gh)
                gh.atSeverity = v;
        });
        if (StateOf(nameof(hasGiveHediff)) == FieldState.Set)
        {
            if (!(bool)RawVal(nameof(hasGiveHediff))!)
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
                ApplyGiveHediffSeverity();
            }
        }
        else
        {
            ApplyGiveHediffSeverity();
        }

        // ReactOnDamage Comp toggle
        // 同 GetsPermanent：hasReactOnDamage 在 SetDef 里恒被回填为非空（~332-340），旧代码 `HasValue` 恒真。
        // 若把内层 reactOnDamageCreateHediff 包进外层 ApplyV 的 lambda，则外层"未改"(Unset) 时会连内层一起吞掉，
        // 导致"只改 createHediff、没动开关"失效 ⇒ 必须用 StateOf 判开关，并给未改情况保留独立兜底分支。
        void ApplyReactOnDamageHediff() => ApplyD<HediffDef>(nameof(reactOnDamageCreateHediff), sv =>
        {
            if (def.CompProps<HediffCompProperties_ReactOnDamage>() is HediffCompProperties_ReactOnDamage rod)
                rod.createHediff = sv;
        });
        if (StateOf(nameof(hasReactOnDamage)) == FieldState.Set)
        {
            if (!(bool)RawVal(nameof(hasReactOnDamage))!)
            {
                def.comps?.RemoveWhere(c => c is HediffCompProperties_ReactOnDamage);
            }
            else
            {
                ApplyReactOnDamageHediff();
            }
        }
        else
        {
            ApplyReactOnDamageHediff();
        }

        // Phase 5: Extension fields
        ApplyL<AbilityDef>(nameof(abilities), v => def.abilities = v);
        ApplyL<HediffGiver>(nameof(hediffGivers), v => def.hediffGivers = v);
        ApplyV<SimpleCurve>(nameof(removeOnRedressChanceByDaysCurve), v => def.removeOnRedressChanceByDaysCurve = v);
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