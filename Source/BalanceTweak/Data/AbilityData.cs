using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases?.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList();
        }
        if (DataUtility.MayRequire(TweakDatabase.VEP))
        {
            GetVEP();
        }
        // desc 只用于行 tooltip；GetTooltip 会构建完整 StatSummary，改为首次悬停时再生成。
        ResetDescCache();
    }

    protected override string ResolveDesc()
    {
        if (def is AbilityDef d) return d.GetTooltip() ?? "";
        if (def != null && DataUtility.IsTypeOrSubclassOfName(def.GetType(), VefAbilityDefType))
            return DataUtility.GetByName(def, "description") as string ?? "";
        // 兜底与 SetProps 原先的通用描述保持一致的文本。
        return def == null ? string.Empty : $"{def.defName}\n{def.description ?? ""}";
    }

    // 可选 Mod 类型一律按全名反射访问：静态引用（含 typeof / 泛型参数 / 捕获进闭包）会让
    // 未加载该 Mod 的玩家在启动期 TypeLoadException（详见 Docs 与 DataUtility 顶部注释）。
    private const string VefAbilityDefType = "VEF.Abilities.AbilityDef";
    private const string PsycastExtensionType = "VanillaPsycastsExpanded.AbilityExtension_Psycast";

    private void GetVEP()
    {
        var def = this.def;
        if (def == null || !DataUtility.IsTypeOrSubclassOfName(def.GetType(), VefAbilityDefType)) return;

        if (DataUtility.GetByName(def, "icon") is Texture2D ic && ic != BaseContent.BadTex) uiIcon = ic;
        cooldownTime = DataUtility.AsInt(DataUtility.GetByName(def, "cooldownTime"));
        distanceToTarget = DataUtility.AsFloat(DataUtility.GetByName(def, "distanceToTarget"));
        castTime = DataUtility.AsInt(DataUtility.GetByName(def, "castTime"));
        durationTime = DataUtility.AsInt(DataUtility.GetByName(def, "durationTime"));
        radius = DataUtility.AsFloat(DataUtility.GetByName(def, "radius"));

        var m = DataUtility.FindModExtension(def, PsycastExtensionType);
        if (m != null)
        {
            psyfocusCost = DataUtility.AsFloat(DataUtility.GetByName(m, "psyfocusCost"));
            entropyGain = DataUtility.AsFloat(DataUtility.GetByName(m, "entropyGain"));
        }
    }

    /// <summary>走 ApplyV 门控 + 反射写入；闭包只捕获 object 与 MemberInfo，不引入可选 Mod 类型。</summary>
    private void ApplyMember<TV>(object target, string memberName, string tweakField) where TV : struct
    {
        var member = DataUtility.FindMember(target.GetType(), memberName);
        if (member == null) return;
        ApplyV<TV>(tweakField, v => DataUtility.SetMember(target, member, v));
    }

    public override void Apply()
    {
        if (this.def is AbilityDef def)
        {
            ApplyS(nameof(defLabel), v => this.def.label = v);
            ApplyDefStats(def);
            ResetDescCache();
            // 应用基础属性
            ApplyV<int>(nameof(psylevel), v => def.level = v);
            ApplyV<bool>(nameof(aiCanUse), v => def.aiCanUse = v);
            ApplyV<IntRange>(nameof(cooldownTicksRange), v => def.cooldownTicksRange = v);

            // 应用Verb属性
            if (Verb != null)
            {
                ApplyV<float>(nameof(warmup), v => Verb.warmupTime = v);
                ApplyV<float>(nameof(minRange), v => Verb.minRange = v);
                ApplyV<float>(nameof(maxRange), v => Verb.range = v);
                ApplyV<bool>(nameof(requireLineOfSight), v => Verb.requireLineOfSight = v);
            }


            //应用能力组冷却时间
            if (AbilityGroupIns != null)
            {
                ApplyV<int>(nameof(abilityGroupCooldownTicks), v => AbilityGroupIns.cooldownTicks = v);
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
        var def = this.def;
        if (def == null || !DataUtility.IsTypeOrSubclassOfName(def.GetType(), VefAbilityDefType)) return;

        ApplyMember<int>(def, "cooldownTime", nameof(cooldownTime));
        ApplyMember<float>(def, "distanceToTarget", nameof(distanceToTarget));
        ApplyMember<int>(def, "castTime", nameof(castTime));
        ApplyMember<int>(def, "durationTime", nameof(durationTime));
        ApplyMember<float>(def, "radius", nameof(radius));

        var m = DataUtility.FindModExtension(def, PsycastExtensionType);
        if (m != null)
        {
            ApplyMember<float>(m, "psyfocusCost", nameof(psyfocusCost));
            ApplyMember<float>(m, "entropyGain", nameof(entropyGain));
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
