namespace BalanceTweak;

/// <summary>
/// Fortified 主动防御系统（<c>CompProperties_ActiveProtectionSystem</c>）的配置窗口。
///
/// <para>
/// APS 的 Comp 挂在 <c>AbilityDef.comps</c> 上，只有 4 个影响平衡的标量；其余字段是特效 /
/// 音效 / 目标名单，不在默认编辑范围内。没有该 Comp 的能力不会显示触发列。
/// </para>
/// </summary>
public class APSConfigWindow : CompConfigWindow
{
    private static readonly CompConfigRow[] Rows =
    {
        CompConfigRow.Int(nameof(AbilityData.apsRadius),
            "MST.apsRadius", "MST.apsRadiusComment"),
        CompConfigRow.Percent(nameof(AbilityData.apsChanceToFail),
            "MST.apsChanceToFail", "MST.apsChanceToFailComment"),
        CompConfigRow.Int(nameof(AbilityData.apsActiveTicks),
            "MST.apsActiveTicks", "MST.apsActiveTicksComment"),
        CompConfigRow.Int(nameof(AbilityData.apsInterceptCountMax),
            "MST.apsInterceptCountMax", "MST.apsInterceptCountMaxComment"),
    };

    public APSConfigWindow(TweakData data)
        : base(data, "MST.apsConfig", "MST.apsConfigIntro", "MST.apsConfigNote", Rows, 420f)
    {
    }
}
