using System.Linq;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 中继指挥配置的统一编辑窗口。
///
/// <para>
/// 一个 Def 可能同时挂 <c>CommandRelay</c> / <c>SubRelay</c> / <c>Overseer</c> /
/// <c>OverseerFacility</c> 中的一到四种，字段之间没有批量编辑价值，所以表格里只留一个
/// <see cref="ColumnStyle.Config"/> 触发列；字段本身标 <c>[TweakField(NotAColumn = true)]</c>：
/// 照常落盘 / 参与三态 / 重置，但不进入表格。窗口按 Def 实际拥有的 Comp 分节，没有的节不显示。
/// </para>
///
/// <para>
/// 同一份声明服务三个宿主（种族 / 建筑 / 服装）：各宿主只声明了自己那几个字段，
/// 不存在的行由 <see cref="ConfigWindow"/> 自动跳过（不占高度）。
/// </para>
/// </summary>
public class RelayConfigWindow : ConfigWindow
{
    private static readonly ConfigSection[] Sections =
    {
        // ── Comp 增删开关：三个宿主的字段各不相同，不存在的行会自动跳过 ──
        ConfigSection.Of("MST.relayCompSection", HasAnyToggle,
                ConfigRow.Bool(nameof(RaceData.relayHasCommandRelay)),
                ConfigRow.Bool(nameof(BuildingData.relayHasSubRelay)),
                ConfigRow.Bool(nameof(RaceData.relayHasOverseer)),
                ConfigRow.Bool(nameof(BuildingData.relayHasOverseerFacility)))
            .Note("MST.relayCompToggleNote"),

        // ── 命令中继 ──
        ConfigSection.Of("MST.relayCommandSection",
                d => FortifiedRelay.FindCommandRelay(d.def) != null
                     && Has(d, nameof(RaceData.commandRelayMaxWorldMapRadius)),
                ConfigRow.Float(nameof(RaceData.commandRelayMaxRelayRadius)),
                ConfigRow.Float(nameof(RaceData.commandRelayMinRelayRadius)),
                ConfigRow.Int(nameof(RaceData.commandRelayMaxWorldMapRadius)),
                ConfigRow.Bool(nameof(RaceData.commandRelayCoverWholeMap))),

        // ── 副中继 ──
        ConfigSection.Of("MST.relaySubSection",
                d => FortifiedRelay.FindSubRelay(d.def) != null
                     && Has(d, nameof(BuildingData.subRelayRange)),
                ConfigRow.Float(nameof(BuildingData.subRelayRange)),
                ConfigRow.Bool(nameof(BuildingData.subRelayCoverWholeMap))),

        // ── 监督者 ──
        ConfigSection.Of("MST.relayOverseerSection",
                d => FortifiedRelay.FindOverseer(d.def) != null
                     && Has(d, nameof(RaceData.overseerCanRepair)),
                ConfigRow.Bool(nameof(RaceData.overseerCanRepair)),
                ConfigRow.Int(nameof(RaceData.overseerTicksPerHeal)),
                ConfigRow.Bool(nameof(RaceData.overseerInstantControl))),

        // ── 监督者设施：同一设施上限至少 1（0 会让设施永远无法服务）──
        ConfigSection.Of("MST.relayFacilitySection",
                d => FortifiedRelay.FindOverseerFacility(d.def) != null
                     && Has(d, nameof(BuildingData.facilityMaxSimultaneous)),
                ConfigRow.Int(nameof(BuildingData.facilityMaxSimultaneous), 1),
                ConfigRow.Float(nameof(BuildingData.facilityMaxDistance)),
                ConfigRow.Bool(nameof(BuildingData.facilityRequiresLOS)),
                ConfigRow.Bool(nameof(BuildingData.facilityMustBePlacedAdjacent)),
                ConfigRow.List(nameof(BuildingData.facilityStatOffsets)))
            .Note("MST.relayFacilityNote"),

        // ── 宿主 stat：只有真正的代理机械师宿主才显示（设施自己的 statBases 不是给宿主的加成）──
        ConfigSection.Of("MST.relayHostStatsSection",
                d => FortifiedRelay.FindOverseer(d.def) != null
                     && Has(d, nameof(RaceData.mechControlGroups)),
                ConfigRow.Float(nameof(RaceData.mechBandwidth), -1000f, 1000f),
                ConfigRow.Float(nameof(RaceData.mechControlGroups), -1000f, 1000f),
                ConfigRow.Float(nameof(RaceData.mechCommandRange), -1000f, 1000f))
            .Note("MST.relayHostStatsNote"),
    };

    public RelayConfigWindow(TweakData data)
        : base(data, "MST.relayConfig", "MST.relayConfigIntro", Sections, preferredHeight: 620f)
    {
    }

    /// <summary>这个宿主上有没有该字段（三个宿主各声明了一部分）。</summary>
    private static bool Has(TweakData data, string field) => data.GetFieldConfig(field) != null;

    /// <summary>任何一个宿主只要有一个 Comp 增删开关，这一节就要出现。</summary>
    private static bool HasAnyToggle(TweakData data)
        => Has(data, nameof(RaceData.relayHasCommandRelay))
        || Has(data, nameof(BuildingData.relayHasSubRelay))
        || Has(data, nameof(RaceData.relayHasOverseer))
        || Has(data, nameof(BuildingData.relayHasOverseerFacility))
        || Has(data, nameof(ApparelData.relayHasSubRelay));
}
