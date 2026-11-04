using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 武器扫射（<c>Verb_Spray</c> / <c>Verb_ArcSpray</c>，DMS 的 GAU 系列）配置窗口。
///
/// <para>
/// 四个 <c>spray*</c> 字段都是原版 <see cref="VerbProperties"/> 的字段，挂在武器动词上；
/// 表格里只留一个只读触发列，字段本身 <c>[TweakField(NotAColumn = true)]</c>。
/// 只有带扫射 Verb 的武器才显示触发列，其他武器整格留白。
/// </para>
/// </summary>
public class SweepConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        ConfigSection.Of("MST.sweepSection", null,
            ConfigRow.Float(nameof(WeaponData.sprayWidth)),
            ConfigRow.Float(nameof(WeaponData.sprayArching)),
            ConfigRow.Int(nameof(WeaponData.sprayThicknessCells), 1),
            ConfigRow.Int(nameof(WeaponData.sprayNumExtraCells))),
    };

    public SweepConfigWindow(TweakData data)
        : base(data, "MST.sweepConfig", "MST.sweepConfigIntro", Sections, 420f, "MST.sweepConfigNote", Validate)
    {
    }

    /// <summary>
    /// 扫射路径先追加 <c>sprayNumExtraCells</c> 个弹着点，再追加目标格；每次射击按
    /// <c>burstShotsLeft</c> 索引这条路径。因此额外格不足时可能越界。
    /// </summary>
    private static IReadOnlyList<ConfigIssue> Validate(TweakData data)
    {
        var issues = new List<ConfigIssue>();
        int extra = Int(data, nameof(WeaponData.sprayNumExtraCells)) ?? 0;
        int burst = Int(data, nameof(WeaponData.burstShotCount)) ?? 1;

        if (burst > 1)
        {
            if (extra + 1 < burst)
                issues.Add(ConfigIssue.Error("MST.sweepErrPathTooShort".Translate().RawText));
            else if (extra < burst)
                issues.Add(ConfigIssue.Warning("MST.sweepWarnExtraCellsLessThanBurst".Translate().RawText));
        }

        return issues;
    }

    private static int? Int(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as int?;
}
