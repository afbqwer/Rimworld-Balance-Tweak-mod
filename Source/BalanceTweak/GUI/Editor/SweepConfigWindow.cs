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
public class SweepConfigWindow : CompConfigWindow
{
    private static readonly CompConfigRow[] Rows =
    {
        CompConfigRow.Section("MST.sweepSection"),
        CompConfigRow.Float(nameof(WeaponData.sprayWidth),
            "MST.sprayWidth", "MST.sprayWidthComment"),
        CompConfigRow.Float(nameof(WeaponData.sprayArching),
            "MST.sprayArching", "MST.sprayArchingComment"),
        CompConfigRow.Int(nameof(WeaponData.sprayThicknessCells),
            "MST.sprayThicknessCells", "MST.sprayThicknessCellsComment", 1),
        CompConfigRow.Int(nameof(WeaponData.sprayNumExtraCells),
            "MST.sprayNumExtraCells", "MST.sprayNumExtraCellsComment"),
    };

    public SweepConfigWindow(TweakData data)
        : base(data, "MST.sweepConfig", "MST.sweepConfigIntro", "MST.sweepConfigNote", Rows, 420f, Validate)
    {
    }

    /// <summary>
    /// 扫射路径先追加 <c>sprayNumExtraCells</c> 个弹着点，再追加目标格；每次射击按
    /// <c>burstShotsLeft</c> 索引这条路径。因此额外格不足时可能越界。
    /// </summary>
    private static IReadOnlyList<CompConfigIssue> Validate(TweakData data)
    {
        var issues = new List<CompConfigIssue>();
        int extra = Int(data, nameof(WeaponData.sprayNumExtraCells)) ?? 0;
        int burst = Int(data, nameof(WeaponData.burstShotCount)) ?? 1;

        if (burst > 1)
        {
            if (extra + 1 < burst)
                issues.Add(CompConfigIssue.Error("MST.sweepErrPathTooShort".Translate().RawText));
            else if (extra < burst)
                issues.Add(CompConfigIssue.Warning("MST.sweepWarnExtraCellsLessThanBurst".Translate().RawText));
        }

        return issues;
    }

    private static int? Int(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as int?;
}
