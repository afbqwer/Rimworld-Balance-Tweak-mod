using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 原版投射物护盾（<c>CompProperties_ProjectileInterceptor</c>）的配置窗口。
///
/// <para>
/// 只负责声明分节与组合校验；输入缓冲、三态写入、关窗提交都在 <see cref="ConfigWindow"/>。
/// 没有该 Comp 的种族不会显示触发列，这里也不做增删。
/// </para>
/// </summary>
public class ShieldConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        ConfigSection.Of("MST.shieldCoreSection", null,
            ConfigRow.Float(nameof(RaceData.projectileInterceptorRadius)),
            ConfigRow.Int(nameof(RaceData.projectileInterceptorHitPoints), -1),
            ConfigRow.Bool(nameof(RaceData.startWithMaxHitPoints)),
            ConfigRow.Int(nameof(RaceData.projectileInterceptorChargeDurationTicks)),
            ConfigRow.Int(nameof(RaceData.projectileInterceptorRechargeHitPointsIntervalTicks), 1),
            ConfigRow.Bool(nameof(RaceData.hitPointsRestoreInstantlyAfterCharge)),
            ConfigRow.Int(nameof(RaceData.cooldownTicks)),
            ConfigRow.Int(nameof(RaceData.chargeIntervalTicks)),
            ConfigRow.Int(nameof(RaceData.projectileInterceptorDisarmedByEmpForTicks))),

        ConfigSection.Of("MST.shieldInterceptSection", null,
            ConfigRow.Bool(nameof(RaceData.interceptGroundProjectiles)),
            ConfigRow.Bool(nameof(RaceData.interceptAirProjectiles)),
            ConfigRow.Bool(nameof(RaceData.interceptNonHostileProjectiles)),
            ConfigRow.Bool(nameof(RaceData.interceptOutgoingProjectiles))),

        ConfigSection.Of("MST.shieldActiveSection", null,
            ConfigRow.Bool(nameof(RaceData.activated)),
            ConfigRow.Int(nameof(RaceData.activeDuration))),
    };

    public ShieldConfigWindow(TweakData data)
        : base(data, "MST.shieldConfig", "MST.shieldConfigIntro", Sections, 720f, "MST.shieldConfigNote", Validate)
    {
    }

    /// <summary>
    /// 组合校验。第一组对齐原版 <c>ConfigErrors</c>（HP 池与充能周期互斥），
    /// 其余是原版不会检查、但会让护盾永远无法生效 / 每 tick 除以零的组合。
    /// </summary>
    private static IReadOnlyList<ConfigIssue> Validate(TweakData data)
    {
        var issues = new List<ConfigIssue>();

        int hp = Int(data, nameof(RaceData.projectileInterceptorHitPoints)) ?? -1;
        int recharge = Int(data, nameof(RaceData.projectileInterceptorRechargeHitPointsIntervalTicks)) ?? 240;
        int chargeInterval = Int(data, nameof(RaceData.chargeIntervalTicks)) ?? 0;
        int chargeDuration = Int(data, nameof(RaceData.projectileInterceptorChargeDurationTicks)) ?? 0;
        int activeDuration = Int(data, nameof(RaceData.activeDuration)) ?? 0;
        bool activated = Bool(data, nameof(RaceData.activated));
        bool instantRefill = Bool(data, nameof(RaceData.hitPointsRestoreInstantlyAfterCharge));
        bool ground = Bool(data, nameof(RaceData.interceptGroundProjectiles));
        bool air = Bool(data, nameof(RaceData.interceptAirProjectiles));

        if (hp > 0 && chargeInterval > 0)
            issues.Add(ConfigIssue.Error("MST.shieldErrHpAndChargeInterval".Translate().RawText));
        if (hp > 0 && recharge <= 0)
            issues.Add(ConfigIssue.Error("MST.shieldErrRechargeInterval".Translate().RawText));
        if (hp < -1)
            issues.Add(ConfigIssue.Error("MST.shieldErrHitPointsTooLow".Translate().RawText));

        if (chargeInterval > 0 && chargeDuration <= 0)
            issues.Add(ConfigIssue.Error("MST.shieldErrChargeDurationZero".Translate().RawText));
        else if (chargeInterval > 0 && chargeInterval < chargeDuration)
            issues.Add(ConfigIssue.Error("MST.shieldErrChargeIntervalShorter".Translate().RawText));

        if (activated && activeDuration <= 0)
            issues.Add(ConfigIssue.Error("MST.shieldErrActiveDurationZero".Translate().RawText));
        else if (activated)
            issues.Add(ConfigIssue.Warning("MST.shieldWarnActivatedNeedsExternal".Translate().RawText));
        if (!activated && activeDuration > 0)
            issues.Add(ConfigIssue.Warning("MST.shieldWarnActiveDurationIgnored".Translate().RawText));
        if (hp <= 0 && instantRefill)
            issues.Add(ConfigIssue.Warning("MST.shieldWarnInstantRefillNoHp".Translate().RawText));
        if (!ground && !air)
            issues.Add(ConfigIssue.Warning("MST.shieldWarnNoInterceptType".Translate().RawText));

        return issues;
    }

    private static int? Int(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as int?;

    private static bool Bool(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as bool? ?? false;
}
