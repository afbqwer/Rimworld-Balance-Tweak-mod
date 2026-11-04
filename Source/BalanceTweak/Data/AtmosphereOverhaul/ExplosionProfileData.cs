using System.Collections.Generic;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 爆炸剖面（<c>AtmosphereExplosionProfileDef</c>）：5 行，按 <c>damageDef</c> 与伤害类型配对。
///
/// <para>
/// <b>取值层级</b>：<c>DamageDef.modExtensions</c> 上的 <c>ExplosionAtmosphereExtension</c> &gt;
/// 本 Def &gt; 由原版半径/伤害推导。每一层都是"取第一个 ≥ 0 的值"，所以 AO 用 <c>-1</c> 表示
/// "沿用下一层"。这里把它映射成 <c>null</c>：单元格空白 = 未设置，填写 = 覆盖。
/// </para>
///
/// <para>
/// <b>为什么不用 ApplyV</b>：清空（Cleared）时要写回 <c>-1</c>，而 <c>ApplyV</c> 的 Cleared 分支
/// 是"从原始快照还原"，快照里这一格正是 null ⇒ 等于什么都不做，界面上"清空"看起来毫无反应。
/// 所以这一页用 <see cref="ApplySentinel"/> 显式处理三种状态。
/// </para>
///
/// <para>
/// <b>不暴露 <c>equivalentTntKg</c></b>：它是 <c>eqTNTkg</c> 的别名，读取时取第一个 ≥ 0 的值，
/// <c>eqTNTkg</c> 优先。两个可编辑列并排只会让玩家改错那一个。
/// </para>
/// </summary>
[TweakFor(typeof(Def), SettingType.ExplosionProfile, TypeName = AoHelper.Profile, MayRequire = TweakDatabase.AO)]
class ExplosionProfileData : TweakData<ExplosionProfileData>
{
    /// <summary>声波 Courant 数上限：超过它显式积分会发散（AO 自己的校验也是这条线）。</summary>
    private const float MaxAcousticCourant = 0.707f;
    private static bool warnedCourant;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    /// <summary>这份剖面对应哪个伤害类型（匹配就靠它）。</summary>
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? damageDef = null;

    /// <summary>关掉 = 这个伤害类型完全不参与大气爆炸。</summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? enabled = null;

    /// <summary>固定 TNT 当量（kg）。</summary>
    [TweakField]
    public float? eqTNTkg = null;
    /// <summary>超压 / 冲量倍率。0 = 无冲击波。</summary>
    [TweakField]
    public float? blastFactor = null;
    /// <summary>沉积热量倍率。0 = 无热。</summary>
    [TweakField]
    public float? heatFactor = null;
    /// <summary>大气效果半径 / 原版火球半径。</summary>
    [TweakField]
    public float? effectRadiusFactor = null;
    /// <summary>空气质量倍率。0 = 中性。</summary>
    [TweakField]
    public float? airMassFactor = null;
    /// <summary>固定源的高斯半径。</summary>
    [TweakField]
    public float? coreRadius = null;
    /// <summary>自动半径的倍率。</summary>
    [TweakField]
    public float? coreRadiusFactor = null;
    /// <summary>源的最大半径。</summary>
    [TweakField]
    public float? supportRadius = null;

    [TweakField]
    public float? maxOverpressureKPa = null;
    /// <summary>负压上限。**必须 &lt; 100**（绝对压力不能为负）。</summary>
    [TweakField]
    public float? maxRarefactionKPa = null;
    /// <summary>风速上限（m/s）。</summary>
    [TweakField]
    public float? maxWindSpeed = null;

    /// <summary>声波 Courant 数。**必须 ≤ 0.707**，超过会数值发散；填超了按 0.707 生效并告警一次。</summary>
    [TweakField]
    public float? acousticCourant = null;
    /// <summary>声波每 tick 阻尼。</summary>
    [TweakField]
    public float? acousticDamping = null;
    /// <summary>声波风增益（m/s per kPa/cell）。</summary>
    [TweakField]
    public float? acousticWindGain = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? acousticLifetimeTicks = null;
    /// <summary>转入主求解器前的延迟（tick）。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? acousticHandoffDelayTicks = null;
    /// <summary>每 tick 的转移比例。</summary>
    [TweakField]
    public float? acousticHandoffRatePerTick = null;

    /// <summary>残余风占比。</summary>
    [TweakField]
    public float? residualWindFraction = null;
    /// <summary>单格温升上限（℃）。</summary>
    [TweakField]
    public float? maxTemperatureDelta = null;
    /// <summary>固定沉积热量（焦耳）。</summary>
    [TweakField]
    public float? heatEnergyJoules = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        object o = def!;
        damageDef ??= AoHelper.G(o, "damageDef") as DamageDef;
        enabled ??= AoHelper.B(o, "enabled");

        eqTNTkg ??= Num(o, "eqTNTkg");
        blastFactor ??= Num(o, "blastFactor");
        heatFactor ??= Num(o, "heatFactor");
        effectRadiusFactor ??= Num(o, "effectRadiusFactor");
        airMassFactor ??= Num(o, "airMassFactor");
        coreRadius ??= Num(o, "coreRadius");
        coreRadiusFactor ??= Num(o, "coreRadiusFactor");
        supportRadius ??= Num(o, "supportRadius");
        maxOverpressureKPa ??= Num(o, "maxOverpressureKPa");
        maxRarefactionKPa ??= Num(o, "maxRarefactionKPa");
        maxWindSpeed ??= Num(o, "maxWindSpeed");
        acousticCourant ??= Num(o, "acousticCourant");
        acousticDamping ??= Num(o, "acousticDamping");
        acousticWindGain ??= Num(o, "acousticWindGain");
        acousticLifetimeTicks ??= NumInt(o, "acousticLifetimeTicks");
        acousticHandoffDelayTicks ??= NumInt(o, "acousticHandoffDelayTicks");
        acousticHandoffRatePerTick ??= Num(o, "acousticHandoffRatePerTick");
        residualWindFraction ??= Num(o, "residualWindFraction");
        maxTemperatureDelta ??= Num(o, "maxTemperatureDelta");
        heatEnergyJoules ??= Num(o, "heatEnergyJoules");
    }

    /// <summary>读 <c>-1 = 未设置</c> 的 float：负值一律当作"沿用下一层"。</summary>
    private static float? Num(object target, string member)
    {
        var v = AoHelper.F(target, member);
        return v is float f && f >= 0f ? f : null;
    }

    private static int? NumInt(object target, string member)
    {
        var v = AoHelper.I(target, member);
        return v is int i && i >= 0 ? i : null;
    }

    public override void Apply()
    {
        if (this.def == null)
        {
            Log.Error($"[BalanceTweak]{this}的def为null!");
            return;
        }
        object o = this.def!;

        ApplyD<DamageDef>(nameof(damageDef), v => AoHelper.S(o, "damageDef", v));
        AoHelper.ApplyF<bool>(this, o, "enabled", nameof(enabled));

        ApplySentinel(o, "eqTNTkg", nameof(eqTNTkg));
        ApplySentinel(o, "blastFactor", nameof(blastFactor));
        ApplySentinel(o, "heatFactor", nameof(heatFactor));
        ApplySentinel(o, "effectRadiusFactor", nameof(effectRadiusFactor));
        ApplySentinel(o, "airMassFactor", nameof(airMassFactor));
        ApplySentinel(o, "coreRadius", nameof(coreRadius));
        ApplySentinel(o, "coreRadiusFactor", nameof(coreRadiusFactor));
        ApplySentinel(o, "supportRadius", nameof(supportRadius));
        ApplySentinel(o, "maxOverpressureKPa", nameof(maxOverpressureKPa));
        ApplySentinel(o, "maxRarefactionKPa", nameof(maxRarefactionKPa));
        ApplySentinel(o, "maxWindSpeed", nameof(maxWindSpeed));
        ApplySentinel(o, "acousticDamping", nameof(acousticDamping));
        ApplySentinel(o, "acousticWindGain", nameof(acousticWindGain));
        ApplySentinelInt(o, "acousticLifetimeTicks", nameof(acousticLifetimeTicks));
        ApplySentinelInt(o, "acousticHandoffDelayTicks", nameof(acousticHandoffDelayTicks));
        ApplySentinel(o, "acousticHandoffRatePerTick", nameof(acousticHandoffRatePerTick));
        ApplySentinel(o, "residualWindFraction", nameof(residualWindFraction));
        ApplySentinel(o, "maxTemperatureDelta", nameof(maxTemperatureDelta));
        ApplySentinel(o, "heatEnergyJoules", nameof(heatEnergyJoules));

        // Courant 单独走一遍：写完之后再夹取，超限时把生效值回写给字段，避免界面显示 1.0 而实际 0.707。
        ApplySentinel(o, "acousticCourant", nameof(acousticCourant));
        if (IsForceWriteField(nameof(acousticCourant)) || StateOf(nameof(acousticCourant)) != FieldState.Unset)
        {
            if (AoHelper.F(o, "acousticCourant") is float c && c > MaxAcousticCourant)
            {
                AoHelper.S(o, "acousticCourant", MaxAcousticCourant);
                acousticCourant = MaxAcousticCourant;
                if (!warnedCourant)
                {
                    warnedCourant = true;
                    Log.Warning($"[BalanceTweak] 爆炸剖面 {def.defName} 的 acousticCourant 超过 {MaxAcousticCourant}，"
                              + "已按上限写入：更大的值会让显式积分发散、地图物理失控。");
                }
            }
        }
    }

    /// <summary>
    /// 写回「-1 = 未设置」的 float 字段。
    /// Set / 重置 → 写字段值（null 即 -1）；Cleared → 写 -1；Unset → 不动。
    /// </summary>
    private void ApplySentinel(object target, string member, string field)
    {
        var mi = DataUtility.FindMember(target.GetType(), member);
        if (mi == null) return;
        var state = StateOf(field);
        if (IsForceWriteField(field) || state == FieldState.Set)
        {
            DataUtility.SetMember(target, mi, (RawVal(field) as float?) ?? -1f);
        }
        else if (state == FieldState.Cleared)
        {
            // 清空 = 还原原值。原值本来就是 null（XML 里写的是 -1）时无处可取，直接写回哨兵值。
            DataUtility.SetMember(target, mi,
                TweakDatabase.GetOriginal(id) is ExplosionProfileData orig && orig.RawVal(field) is float v ? v : -1f);
        }
    }

    private void ApplySentinelInt(object target, string member, string field)
    {
        var mi = DataUtility.FindMember(target.GetType(), member);
        if (mi == null) return;
        var state = StateOf(field);
        if (IsForceWriteField(field) || state == FieldState.Set)
        {
            DataUtility.SetMember(target, mi, (RawVal(field) as int?) ?? -1);
        }
        else if (state == FieldState.Cleared)
        {
            DataUtility.SetMember(target, mi,
                TweakDatabase.GetOriginal(id) is ExplosionProfileData orig && orig.RawVal(field) is int v ? v : -1);
        }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = ["MST.ExplosionProfile"];

    public override int GetPropType() => 0;
}
