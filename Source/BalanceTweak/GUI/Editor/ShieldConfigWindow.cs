using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 原版投射物护盾（<c>CompProperties_ProjectileInterceptor</c>）的配置窗口。
///
/// <para>
/// 只负责声明字段顺序、取值范围与组合校验；输入缓冲、三态写入、关窗提交都在
/// <see cref="CompConfigWindow"/>。没有该 Comp 的种族不会显示触发列，这里也不做增删。
/// </para>
/// </summary>
public class ShieldConfigWindow : CompConfigWindow
{
    private static readonly CompConfigRow[] Rows =
    {
        CompConfigRow.Section("MST.shieldCoreSection"),

        CompConfigRow.Float(nameof(RaceData.projectileInterceptorRadius),
            "MST.projectileInterceptorRadius", "MST.projectileInterceptorRadiusComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorHitPoints),
            "MST.projectileInterceptorHitPoints", "MST.projectileInterceptorHitPointsComment", -1),
        CompConfigRow.Bool(nameof(RaceData.startWithMaxHitPoints),
            "MST.startWithMaxHitPoints", "MST.startWithMaxHitPointsComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorChargeDurationTicks),
            "MST.projectileInterceptorChargeDurationTicks", "MST.projectileInterceptorChargeDurationTicksComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorRechargeHitPointsIntervalTicks),
            "MST.projectileInterceptorRechargeHitPointsIntervalTicks",
            "MST.projectileInterceptorRechargeHitPointsIntervalTicksComment", 1),
        CompConfigRow.Bool(nameof(RaceData.hitPointsRestoreInstantlyAfterCharge),
            "MST.hitPointsRestoreInstantlyAfterCharge", "MST.hitPointsRestoreInstantlyAfterChargeComment"),
        CompConfigRow.Int(nameof(RaceData.cooldownTicks),
            "MST.cooldownTicks", "MST.cooldownTicksComment"),
        CompConfigRow.Int(nameof(RaceData.chargeIntervalTicks),
            "MST.chargeIntervalTicks", "MST.chargeIntervalTicksComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorDisarmedByEmpForTicks),
            "MST.projectileInterceptorDisarmedByEmpForTicks",
            "MST.projectileInterceptorDisarmedByEmpForTicksComment"),

        CompConfigRow.Section("MST.shieldInterceptSection"),

        CompConfigRow.Bool(nameof(RaceData.interceptGroundProjectiles),
            "MST.interceptGroundProjectiles", "MST.interceptGroundProjectilesComment"),
        CompConfigRow.Bool(nameof(RaceData.interceptAirProjectiles),
            "MST.interceptAirProjectiles", "MST.interceptAirProjectilesComment"),
        CompConfigRow.Bool(nameof(RaceData.interceptNonHostileProjectiles),
            "MST.interceptNonHostileProjectiles", "MST.interceptNonHostileProjectilesComment"),
        CompConfigRow.Bool(nameof(RaceData.interceptOutgoingProjectiles),
            "MST.interceptOutgoingProjectiles", "MST.interceptOutgoingProjectilesComment"),

        CompConfigRow.Section("MST.shieldActiveSection"),

        CompConfigRow.Bool(nameof(RaceData.activated),
            "MST.activated", "MST.activatedComment"),
        CompConfigRow.Int(nameof(RaceData.activeDuration),
            "MST.activeDuration", "MST.activeDurationComment"),
    };

    public ShieldConfigWindow(TweakData data)
        : base(data, "MST.shieldConfig", "MST.shieldConfigIntro", "MST.shieldConfigNote", Rows, 720f, Validate)
    {
    }

    /// <summary>
    /// 组合校验。第一组对齐原版 <c>ConfigErrors</c>（HP 池与充能周期互斥），
    /// 其余是原版不会检查、但会让护盾永远无法生效 / 每 tick 除以零的组合。
    /// </summary>
    private static IReadOnlyList<CompConfigIssue> Validate(TweakData data)
    {
        var issues = new List<CompConfigIssue>();

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
            issues.Add(CompConfigIssue.Error("MST.shieldErrHpAndChargeInterval".Translate().RawText));
        if (hp > 0 && recharge <= 0)
            issues.Add(CompConfigIssue.Error("MST.shieldErrRechargeInterval".Translate().RawText));
        if (hp < -1)
            issues.Add(CompConfigIssue.Error("MST.shieldErrHitPointsTooLow".Translate().RawText));

        if (chargeInterval > 0 && chargeDuration <= 0)
            issues.Add(CompConfigIssue.Error("MST.shieldErrChargeDurationZero".Translate().RawText));
        else if (chargeInterval > 0 && chargeInterval < chargeDuration)
            issues.Add(CompConfigIssue.Error("MST.shieldErrChargeIntervalShorter".Translate().RawText));

        if (activated && activeDuration <= 0)
            issues.Add(CompConfigIssue.Error("MST.shieldErrActiveDurationZero".Translate().RawText));
        else if (activated)
            issues.Add(CompConfigIssue.Warning("MST.shieldWarnActivatedNeedsExternal".Translate().RawText));
        if (!activated && activeDuration > 0)
            issues.Add(CompConfigIssue.Warning("MST.shieldWarnActiveDurationIgnored".Translate().RawText));
        if (hp <= 0 && instantRefill)
            issues.Add(CompConfigIssue.Warning("MST.shieldWarnInstantRefillNoHp".Translate().RawText));
        if (!ground && !air)
            issues.Add(CompConfigIssue.Warning("MST.shieldWarnNoInterceptType".Translate().RawText));

        return issues;
    }

    private static int? Int(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as int?;

    private static bool Bool(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as bool? ?? false;
}
