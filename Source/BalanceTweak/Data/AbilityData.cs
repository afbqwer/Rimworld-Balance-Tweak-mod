using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

    // ── Fortified 主动防御系统（APS，见 APSConfigWindow）──
    // 4 个平衡字段全在 AbilityDef.comps 的 CompProperties_ActiveProtectionSystem 上。
    // 表格里只留一个只读触发列，字段本身 NotAColumn，由窗口统一编辑。

    /// <summary>只读触发列：打开主动防御配置窗口。没有 APS Comp 的行整格留白。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.APSConfig,
        MayRequire = TweakDatabase.FFF, Hidden = nameof(HiddenIfNoActiveProtectionSystem))]
    public TweakID? apsConfig = null;

    public static bool HiddenIfNoActiveProtectionSystem(TweakData data)
        => !FortifiedProtection.HasActiveProtection(data.def);

    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? apsRadius = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Float, MayRequire = TweakDatabase.FFF)]
    public float? apsChanceToFail = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? apsActiveTicks = null;
    [TweakField(NotAColumn = true, Style = ColumnStyle.Int, MayRequire = TweakDatabase.FFF)]
    public int? apsInterceptCountMax = null;

    private object? apsProps = null;

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
        apsProps = FortifiedProtection.FindActiveProtection(this.def);
        if (apsProps != null) LoadAps(apsProps);
        if (DataUtility.MayRequire(TweakDatabase.VEP))
        {
            GetVEP();
        }
        // desc 只用于行 tooltip；GetTooltip 会构建完整 StatSummary。它现在是懒加载的
        // （见 TweakData.desc），SetProps 已经丢过缓存，这里不需要再补一次。
    }

    /// <summary>
    /// 能力图标：本体的 <c>uiIcon</c> 优先；VEF 能力走反射取 <c>icon</c>（与原先 GetVEP 里的赋值同源）。
    /// </summary>
    protected override Texture2D? ResolveUiIcon()
    {
        // VEF 先判：它的 icon 覆盖本体 uiIcon（与原 GetVEP 的赋值顺序一致）。
        if (def != null && DataUtility.IsTypeOrSubclassOfName(def.GetType(), VefAbilityDefType)
            && DataUtility.GetByName(def, "icon") is Texture2D ic && ic != BaseContent.BadTex)
            return ic;
        if (def is AbilityDef d && d.uiIcon != null && d.uiIcon != BaseContent.BadTex) return d.uiIcon;
        return base.ResolveUiIcon();
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

    /// <summary>从 <c>CompProperties_ActiveProtectionSystem</c> 回填窗口字段。</summary>
    private void LoadAps(object props)
    {
        apsRadius ??= DataUtility.AsInt(DataUtility.GetByName(props, "Radius"));
        apsChanceToFail ??= DataUtility.AsFloat(DataUtility.GetByName(props, "chanceToFail"));
        apsActiveTicks ??= DataUtility.AsInt(DataUtility.GetByName(props, "activeTicks"));
        apsInterceptCountMax ??= DataUtility.AsInt(DataUtility.GetByName(props, "interceptCountMax"));
    }

    /// <summary>把窗口里的值写回 APS Comp。没有 Comp 的能力直接跳过。</summary>
    private void ApplyAps()
    {
        // FFF 缺席时 APS 字段全部不在 Fields 里，任何 ApplyV 都可能撞到不存在的字段；直接跳过。
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;

        var props = apsProps ??= FortifiedProtection.FindActiveProtection(this.def);
        if (props == null) return;
        var p = props; // 闭包只捕获 object

        ApplyV<int>(nameof(apsRadius), v => DataUtility.SetByName(p, "Radius", Mathf.Max(0, v)));
        ApplyV<float>(nameof(apsChanceToFail), v => DataUtility.SetByName(p, "chanceToFail", Mathf.Clamp01(v)));
        ApplyV<int>(nameof(apsActiveTicks), v => DataUtility.SetByName(p, "activeTicks", Mathf.Max(0, v)));
        ApplyV<int>(nameof(apsInterceptCountMax), v => DataUtility.SetByName(p, "interceptCountMax", Mathf.Max(0, v)));
    }

    private void GetVEP()
    {
        var def = this.def;
        if (def == null || !DataUtility.IsTypeOrSubclassOfName(def.GetType(), VefAbilityDefType)) return;

        // 图标不在这里取：VEF 的 icon 由 ResolveUiIcon 在首次绘制时反射读取（见上）。
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
    private void ApplyMember<TV>(object target, string memberName, string tweakField,
        [CallerMemberName] string context = "") where TV : struct
    {
        var member = DataUtility.FindMember(target.GetType(), memberName, context);
        if (member == null) return;
        ApplyV<TV>(tweakField, v => DataUtility.SetMember(target, member, v));
    }

    public override void Apply()
    {
        if (this.def is AbilityDef def)
        {
            ApplyDefLabel(nameof(defLabel), def);
            ApplyDefStats(def);
            ResetDescCache();
            // 应用基础属性
            ApplyV<int>(nameof(psylevel), v => def.level = v);
            ApplyV<bool>(nameof(aiCanUse), v => def.aiCanUse = v);
            ApplyV<IntRange>(nameof(cooldownTicksRange), v => def.cooldownTicksRange = v);
            ApplyAps();

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
