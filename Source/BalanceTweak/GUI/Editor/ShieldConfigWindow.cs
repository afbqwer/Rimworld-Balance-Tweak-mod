namespace BalanceTweak;

/// <summary>
/// 原版投射物护盾（<c>CompProperties_ProjectileInterceptor</c>）的配置窗口。
///
/// <para>
/// 只负责声明字段顺序与取值范围；输入缓冲、三态写入、关窗提交都在
/// <see cref="CompConfigWindow"/>。没有该 Comp 的种族不会显示触发列，这里也不做增删。
/// </para>
/// </summary>
public class ShieldConfigWindow : CompConfigWindow
{
    private static readonly CompConfigRow[] Rows =
    {
        CompConfigRow.Float(nameof(RaceData.projectileInterceptorRadius),
            "MST.projectileInterceptorRadius", "MST.projectileInterceptorRadiusComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorHitPoints),
            "MST.projectileInterceptorHitPoints", "MST.projectileInterceptorHitPointsComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorChargeDurationTicks),
            "MST.projectileInterceptorChargeDurationTicks", "MST.projectileInterceptorChargeDurationTicksComment"),
        CompConfigRow.Int(nameof(RaceData.projectileInterceptorRechargeHitPointsIntervalTicks),
            "MST.projectileInterceptorRechargeHitPointsIntervalTicks",
            "MST.projectileInterceptorRechargeHitPointsIntervalTicksComment"),
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
        CompConfigRow.Bool(nameof(RaceData.hitPointsRestoreInstantlyAfterCharge),
            "MST.hitPointsRestoreInstantlyAfterCharge", "MST.hitPointsRestoreInstantlyAfterChargeComment"),
    };

    public ShieldConfigWindow(TweakData data)
        : base(data, "MST.shieldConfig", "MST.shieldConfigIntro", "MST.shieldConfigNote", Rows, 620f)
    {
    }
}
