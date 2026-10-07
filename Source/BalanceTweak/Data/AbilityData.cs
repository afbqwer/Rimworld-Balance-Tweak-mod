using System.Collections.Generic;
using System.Linq;
using RimWorld;
using VanillaPsycastsExpanded;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(AbilityDef), SettingType.Ability)]
[TweakFor(typeof(AbilityDef), SettingType.Ability, TypeName = "VEF.Abilities.AbilityDef", MayRequire = TweakDatabase.VEP)]
class AbilityData : TweakData<AbilityData>
{
    [TweakField(StatDef = "Ability_Duration", Available = nameof(AvailableIfNonVEFAbility))]
    public float? abilityDuration = null;
    [TweakField(StatDef = "Ability_EntropyGain", Available = nameof(AvailableIfPsycast))]
    public float? abilityEntropyGain = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "Ability_PsyfocusCost", Available = nameof(AvailableIfPsycast))]
    public float? abilityPsyfocusCost = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfPsycast))]
    public int? psylevel = null;

    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public int? cooldownTime = null;
    [TweakField(Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public float? distanceToTarget = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public int? castTime = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public int? durationTime = null;
    [TweakField(Style = ColumnStyle.Prec, Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public float? psyfocusCost = null;
    [TweakField(Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public float? entropyGain = null;
    [TweakField(Available = nameof(AvailableIfVEFAbility), MayRequire = TweakDatabase.VEP)]
    public float? radius = null;

    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfNonVEFAbility))]
    public IntRange? cooldownTicksRange = null;
    [TweakField(Available = nameof(AvailableIfNonVEFAbility))]
    public float? minRange = null;
    [TweakField(Available = nameof(AvailableIfNonVEFAbility))]
    public float? maxRange = null;
    [TweakField(Available = nameof(AvailableIfNonVEFAbility))]
    public float? warmup = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonVEFAbility))]
    public bool? requireLineOfSight = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonVEFAbility))]
    public bool? aiCanUse = null;
    [TweakField(Style = ColumnStyle.StatModList, Available = nameof(AvailableIfNonVEFAbility))]
    public List<StatModifier>? statBases = null;


    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String, Available = nameof(AvailableIfOther))]
    public string? AbilityGroup = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfOther))]
    public int? abilityGroupCooldownTicks = null;
    private AbilityGroupDef? AbilityGroupIns = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    private VerbProperties? Verb = null;
    public bool isVEP = false;

    public override int LoadingOrd => 200;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (this.def is AbilityDef d)
        {
            if (d.uiIcon != null && d.uiIcon != BaseContent.BadTex)
            {
                uiIcon = d.uiIcon;
            }
            desc = d.GetTooltip() ?? "";
            psylevel ??= d.level;
            aiCanUse ??= d.aiCanUse;
            cooldownTicksRange ??= d.cooldownTicksRange;
            AbilityGroupIns ??= d.groupDef;
            AbilityGroup = AbilityGroupIns?.defName;
            abilityGroupCooldownTicks ??= AbilityGroupIns?.cooldownTicks;
            Verb = d.verbProperties;
            if (Verb != null)
            {
                warmup ??= Verb.warmupTime;
                minRange ??= Verb.minRange;
                maxRange ??= Verb.range;
                requireLineOfSight ??= Verb.requireLineOfSight;
            }
            statBases ??= d.statBases?.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList();
        }
        if (DataUtility.MayRequire(TweakDatabase.VEP))
        {
            GetVEP();
        }
    }

    private void GetVEP()
    {
        if (this.def is VEF.Abilities.AbilityDef va)
        {
            if (va.icon != null && va.icon != BaseContent.BadTex)
            {
                uiIcon = va.icon;
            }
            desc = va.description != null ? va.description : "";
            cooldownTime = va.cooldownTime;
            distanceToTarget = va.distanceToTarget;
            castTime = va.castTime;
            durationTime = va.durationTime;
            radius = va.radius;
            var m = va.GetModExtension<AbilityExtension_Psycast>();
            if (m != null)
            {
                psyfocusCost = m.psyfocusCost;
                entropyGain = m.entropyGain;
            }
        }
    }

    public override void Apply()
    {
        if (this.def is AbilityDef def)
        {
            if (defLabel != null) this.def.label = defLabel;
            ApplyDefStats(def);
            desc = def.GetTooltip() ?? "";
            // 应用基础属性
            if (psylevel.HasValue) def.level = psylevel.Value;
            if (aiCanUse.HasValue) def.aiCanUse = aiCanUse.Value;
            if (cooldownTicksRange.HasValue) def.cooldownTicksRange = cooldownTicksRange.Value;

            // 应用Verb属性
            if (Verb != null)
            {
                if (warmup.HasValue) Verb.warmupTime = warmup.Value;
                if (minRange.HasValue) Verb.minRange = minRange.Value;
                if (maxRange.HasValue) Verb.range = maxRange.Value;
                if (requireLineOfSight.HasValue) Verb.requireLineOfSight = requireLineOfSight.Value;
            }


            //应用能力组冷却时间
            if (AbilityGroupIns != null && abilityGroupCooldownTicks.HasValue)
            {
                AbilityGroupIns.cooldownTicks = abilityGroupCooldownTicks.Value;
            }
        }
        else
        {
            // 应用VEP相关属性
            if (DataUtility.MayRequire(TweakDatabase.VEP))
            {
                SetVEP();
            }
            else
            {
                Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
                return;
            }
        }
    }

    private void SetVEP()
    {
        if (this.def is VEF.Abilities.AbilityDef va)
        {
            if (cooldownTime.HasValue) va.cooldownTime = cooldownTime.Value;
            if (distanceToTarget.HasValue) va.distanceToTarget = distanceToTarget.Value;
            if (castTime.HasValue) va.castTime = castTime.Value;
            if (durationTime.HasValue) va.durationTime = durationTime.Value;
            if (radius.HasValue) va.radius = radius.Value;
            // VanillaPsycastsExpanded.AbilityExtension_Psycast
            var m = va.GetModExtension<AbilityExtension_Psycast>();
            if (m != null)
            {
                if (psyfocusCost.HasValue) m.psyfocusCost = psyfocusCost.Value;
                if (entropyGain.HasValue) m.entropyGain = entropyGain.Value;
            }
        }
    }

    public static bool AvailableIfOther(TweakData data) => data.propType switch
    {
        (int)AbilityType.Other => true,
        _ => false,
    };

    public static bool AvailableIfPsycast(TweakData data) => data.propType switch
    {
        (int)AbilityType.Psychic => true,
        _ => false,
    };

    public static bool AvailableIfVEFAbility(TweakData data) => data.propType switch
    {
        (int)AbilityType.VPEAbility => true,
        _ => false,
    };

    public static bool AvailableIfNonVEFAbility(TweakData data) => !AvailableIfVEFAbility(data);

    enum AbilityType
    {
        Psychic,
        Sanguophage,
        Other,
        VPEAbility,
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(AbilityType).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (this.def is AbilityDef def)
        {
            if (def.category?.defName == "Psychic" || def.abilityClass == typeof(Psycast)) { return (int)AbilityType.Psychic; }
            if (def.category?.defName == "Sanguophage") { return (int)AbilityType.Sanguophage; }
        }
        else if (this.def != null)
        {
            isVEP = true;
            return (int)AbilityType.VPEAbility;
        }
        return (int)AbilityType.Other;
    }
}
