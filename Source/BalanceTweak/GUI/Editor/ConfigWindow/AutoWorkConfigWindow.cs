using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 自动工作台（<c>ModExtension_AutoWorkTable</c>）的配置窗口。
///
/// <para>
/// DMS 的反应室 / 烧结炉 / 机械打印机 / 自动磨床 / CE 弹药打印机都只挂这个扩展；
/// 表格里只留一个只读触发列，字段本身 <c>[TweakField(NotAColumn = true)]</c>。
/// 没有扩展的建筑整格留白，这里也不做增删。
/// </para>
///
/// <para>
/// <b>字段语义</b>：<c>workTime</c> 是工人每次启停的等待时间；<c>workAmountPerStage</c> 是
/// 无人值守的单段上限（越小工人来得越勤）；<c>skills</c> 是机器的名义技能（能否自动执行
/// 有技能要求的配方 + 产出品质）；抽料 / 弹出控制自动产线的上下料。
/// 表现层的 effecter / mote / northOnly 不在此开放。
/// </para>
/// </summary>
public class AutoWorkConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        ConfigSection.Of("MST.autoWorkTimingSection", null,
            ConfigRow.Int(nameof(BuildingData.autoWorkTime)),
            ConfigRow.Int(nameof(BuildingData.autoWorkAmountPerStage), 1),
            ConfigRow.Info("MST.autoWorkStageInfo", StageInfo)),

        ConfigSection.Of("MST.autoWorkSkillSection", null,
                ConfigRow.List(nameof(BuildingData.autoWorkSkills)))
            .Note("MST.autoWorkSkillNote"),

        ConfigSection.Of("MST.autoWorkFeedSection", null,
                ConfigRow.Bool(nameof(BuildingData.autoWorkPullFromLinkedStorage)),
                ConfigRow.Int(nameof(BuildingData.autoWorkPullCheckIntervalTicks), 1),
                ConfigRow.Bool(nameof(BuildingData.autoWorkAutoEjectProducts)),
                ConfigRow.Enum(nameof(BuildingData.autoWorkEjectPlaceMode)))
            .Note("MST.autoWorkFeedNote"),
    };

    public AutoWorkConfigWindow(TweakData data)
        : base(data, "MST.autoWorkConfig", "MST.autoWorkConfigIntro", Sections, 560f, "MST.autoWorkConfigNote", Validate)
    {
    }

    /// <summary>单段在效率 1× 下的无人值守时长（秒）。账单总工作量还受 WorkTableWorkSpeedFactor 影响。</summary>
    private static string StageInfo(TweakData data)
    {
        int amount = Int(data, nameof(BuildingData.autoWorkAmountPerStage)) ?? 60000;
        float seconds = Mathf.Max(1, amount) / 60f;
        return string.Format("MST.autoWorkStageInfoFormat".Translate().RawText, seconds.ToString("0.#"));
    }

    private static IReadOnlyList<ConfigIssue> Validate(TweakData data)
    {
        var issues = new List<ConfigIssue>();

        int workTime = Int(data, nameof(BuildingData.autoWorkTime)) ?? 300;
        int amount = Int(data, nameof(BuildingData.autoWorkAmountPerStage)) ?? 60000;
        int interval = Int(data, nameof(BuildingData.autoWorkPullCheckIntervalTicks)) ?? 120;

        if (workTime < 0)
            issues.Add(ConfigIssue.Warning("MST.autoWorkWarnWorkTime".Translate().RawText));

        // 运行时用 Mathf.Max(1, …) 兜底，<=0 会退化成「每 tick 换一段」，等于机器不停叫工人。
        if (amount < 1)
            issues.Add(ConfigIssue.Error("MST.autoWorkErrStageAmount".Translate().RawText));

        if (interval < 1)
            issues.Add(ConfigIssue.Warning("MST.autoWorkWarnPullInterval".Translate().RawText));

        // 品质生成会把技能夹在 0–20；超出范围的部分对品质没有意义（技能要求判定仍按原值）。
        var skills = data.GetFieldConfig(nameof(BuildingData.autoWorkSkills))?.GetRawValue(data) as List<SkillGain>;
        if (skills != null)
        {
            foreach (var gain in skills)
            {
                if (gain?.skill == null || (gain.amount >= 0 && gain.amount <= 20)) continue;
                issues.Add(ConfigIssue.Warning("MST.autoWorkWarnSkillRange".Translate().RawText));
                break;
            }
        }

        return issues;
    }

    private static int? Int(TweakData data, string field)
        => data.GetFieldConfig(field)?.GetRawValue(data) as int?;
}
