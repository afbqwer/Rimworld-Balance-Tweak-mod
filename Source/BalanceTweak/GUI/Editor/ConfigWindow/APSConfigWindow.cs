namespace BalanceTweak;

/// <summary>
/// Fortified 主动防御系统（<c>CompProperties_ActiveProtectionSystem</c>）的配置窗口。
///
/// <para>
/// APS 的 Comp 挂在 <c>AbilityDef.comps</c> 上，只有 4 个影响平衡的标量；其余字段是特效 /
/// 音效 / 目标名单，不在默认编辑范围内。没有该 Comp 的能力不会显示触发列。
/// </para>
/// </summary>
public class APSConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        ConfigSection.Of(null, null,
            ConfigRow.Int(nameof(AbilityData.apsRadius)),
            ConfigRow.Percent(nameof(AbilityData.apsChanceToFail)),
            ConfigRow.Int(nameof(AbilityData.apsActiveTicks)),
            ConfigRow.Int(nameof(AbilityData.apsInterceptCountMax))),
    };

    public APSConfigWindow(TweakData data)
        : base(data, "MST.apsConfig", "MST.apsConfigIntro", Sections, 420f, "MST.apsConfigNote")
    {
    }
}
