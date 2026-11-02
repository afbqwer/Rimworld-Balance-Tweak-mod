using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 中继指挥配置的统一编辑窗口。
///
/// <para>
/// 一个 Def 可能同时挂 <c>CommandRelay</c> / <c>SubRelay</c> / <c>Overseer</c> /
/// <c>OverseerFacility</c> 中的一到四种，字段之间没有批量编辑价值，所以表格里只留一个
/// <see cref="StatColumnConfig.ColumnStyle.RelayConfig"/> 触发列；字段本身标
/// <c>[TweakField(NotAColumn = true)]</c>：照常落盘 / 参与三态 / 重置，但不进入表格。
/// 窗口按 Def 实际拥有的 Comp 分节，没有的节不显示。
/// </para>
///
/// <para>
/// 数字输入带编辑缓冲：只有回车或失焦才提交，避免每敲一个字符就走一次 <c>Apply</c>
/// （那会顺带触发一轮中继缓存刷新）。
/// </para>
/// </summary>
public class RelayConfigWindow : EditorWindowBase
{
    private const float LineHeight = 24f;
    private const float SectionGap = 10f;
    private const float EditButtonWidth = 110f;
    private const float FieldWidth = 120f;
    private const float ViewHeight = 780f;
    private const string BufferPrefix = "relay.";

    private Vector2 scroll;
    private readonly Dictionary<string, string> buffers = new();
    private string? focusedField;

    public RelayConfigWindow(TweakData data)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {"MST.relayConfig".Translate().RawText}";
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(620f, 720f);

    protected override string SaveButtonLabel => "MST.Close".Translate().RawText;
    protected override bool ShowCancelButton => false;
    protected override void OnSave() => Close();

    /// <summary>点 X / 点窗口外关闭时也要把还没回车的数字提交掉。</summary>
    public override void PreClose()
    {
        CommitAllBuffers();
        base.PreClose();
    }

    public override void DoWindowContents(Rect inRect)
    {
        CommitOnFocusChange();

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect bottom = new(inRect.x, inRect.yMax - BottomBarHeight, inRect.width, BottomBarHeight);
        Rect body = new(inRect.x, curY, inRect.width, Math.Max(0f, bottom.y - curY - Spacing));

        DrawBody(body);
        DrawBottomBar(bottom);
    }

    private void DrawBody(Rect rect)
    {
        Rect view = new(0f, 0f, rect.width - 16f, ViewHeight);
        Widgets.BeginScrollView(rect, ref scroll, view);

        float x = 0f;
        float width = view.width;
        float y = 0f;

        DrawWrappedText(ref y, x, width, "MST.relayConfigIntro", GameFont.Tiny, Color.gray);
        y += SectionGap;

        var def = data?.def;
        if (FortifiedRelay.FindCommandRelay(def) != null && HasField(nameof(RaceData.commandRelayMaxWorldMapRadius)))
            DrawCommandRelay(ref y, x, width);

        if (FortifiedRelay.FindSubRelay(def) != null && HasField(nameof(BuildingData.subRelayRange)))
            DrawSubRelay(ref y, x, width);

        if (FortifiedRelay.FindOverseer(def) != null && HasField(nameof(RaceData.overseerCanRepair)))
            DrawOverseer(ref y, x, width);

        if (FortifiedRelay.FindOverseerFacility(def) != null && HasField(nameof(BuildingData.facilityMaxSimultaneous)))
            DrawFacility(ref y, x, width);

        DrawStatus(ref y, x, width);

        Widgets.EndScrollView();
    }

    private void DrawCommandRelay(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.relayCommandSection");
        DrawNumberRow(x, ref y, width, nameof(RaceData.commandRelayMaxRelayRadius),
            "MST.commandRelayMaxRelayRadius", "MST.commandRelayMaxRelayRadiusComment");
        DrawNumberRow(x, ref y, width, nameof(RaceData.commandRelayMinRelayRadius),
            "MST.commandRelayMinRelayRadius", "MST.commandRelayMinRelayRadiusComment");
        DrawNumberRow(x, ref y, width, nameof(RaceData.commandRelayMaxWorldMapRadius),
            "MST.commandRelayMaxWorldMapRadius", "MST.commandRelayMaxWorldMapRadiusComment");
        DrawBoolRow(x, ref y, width, nameof(RaceData.commandRelayCoverWholeMap),
            "MST.commandRelayCoverWholeMap", "MST.commandRelayCoverWholeMapComment");
        y += SectionGap;
    }

    private void DrawSubRelay(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.relaySubSection");
        DrawNumberRow(x, ref y, width, nameof(BuildingData.subRelayRange),
            "MST.subRelayRange", "MST.subRelayRangeComment");
        DrawBoolRow(x, ref y, width, nameof(BuildingData.subRelayCoverWholeMap),
            "MST.subRelayCoverWholeMap", "MST.subRelayCoverWholeMapComment");
        y += SectionGap;
    }

    private void DrawOverseer(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.relayOverseerSection");
        DrawBoolRow(x, ref y, width, nameof(RaceData.overseerCanRepair),
            "MST.overseerCanRepair", "MST.overseerCanRepairComment");
        DrawNumberRow(x, ref y, width, nameof(RaceData.overseerTicksPerHeal),
            "MST.overseerTicksPerHeal", "MST.overseerTicksPerHealComment");
        DrawBoolRow(x, ref y, width, nameof(RaceData.overseerInstantControl),
            "MST.overseerInstantControl", "MST.overseerInstantControlComment");
        y += SectionGap;
    }

    private void DrawFacility(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.relayFacilitySection");
        DrawNumberRow(x, ref y, width, nameof(BuildingData.facilityMaxSimultaneous),
            "MST.facilityMaxSimultaneous", "MST.facilityMaxSimultaneousComment");
        DrawNumberRow(x, ref y, width, nameof(BuildingData.facilityMaxDistance),
            "MST.facilityMaxDistance", "MST.facilityMaxDistanceComment");
        DrawBoolRow(x, ref y, width, nameof(BuildingData.facilityRequiresLOS),
            "MST.facilityRequiresLOS", "MST.facilityRequiresLOSComment");
        DrawBoolRow(x, ref y, width, nameof(BuildingData.facilityMustBePlacedAdjacent),
            "MST.facilityMustBePlacedAdjacent", "MST.facilityMustBePlacedAdjacentComment");
        DrawListRow(x, ref y, width, nameof(BuildingData.facilityStatOffsets),
            "MST.facilityStatOffsets", "MST.facilityStatOffsetsComment");
        y += SectionGap;
    }

    private void DrawStatus(ref float y, float x, float width)
    {
        if (data?.def is not ThingDef td) return;
        // 只有真正的代理机械师宿主才显示这些 stat；设施行自己的 statBases 不代表它给宿主的加成。
        if (FortifiedRelay.FindOverseer(td) == null) return;

        var bandwidth = DefDatabase<StatDef>.GetNamedSilentFail("MechBandwidth");
        var groups = DefDatabase<StatDef>.GetNamedSilentFail("MechControlGroups");
        var range = DefDatabase<StatDef>.GetNamedSilentFail("FFF_MechCommandRange");

        string format = "MST.relayEffectiveStats".Translate().RawText;
        string text = string.Format(CultureInfo.InvariantCulture, format,
            StatText(td, bandwidth), StatText(td, groups), StatText(td, range));
        DrawWrappedText(ref y, x, width, text, GameFont.Small, Color.white);

        if (FortifiedRelay.FindOverseerFacility(td) != null)
            DrawWrappedText(ref y, x, width, "MST.relayFacilityNote", GameFont.Tiny, Color.gray);
    }

    private static string StatText(ThingDef def, StatDef? stat)
        => stat == null ? "-" : def.GetStatValueAbstract(stat).ToString("0.##", CultureInfo.InvariantCulture);

    // ── 行绘制 ──

    private void DrawNumberRow(float x, ref float y, float width, string field, string labelKey, string commentKey)
    {
        var cfg = data?.GetFieldConfig(field);
        if (cfg == null) return;

        var labelRect = new Rect(x, y, width - FieldWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var fieldRect = new Rect(x + width - FieldWidth, y, FieldWidth, LineHeight);
        DrawNumberField(fieldRect, cfg);
        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(fieldRect, tip);
        y += LineHeight;
    }

    private void DrawBoolRow(float x, ref float y, float width, string field, string labelKey, string commentKey)
    {
        var cfg = data?.GetFieldConfig(field);
        if (cfg == null) return;

        bool current = cfg.GetRawValue(data!) as bool? ?? false;
        bool next = current;
        var rowRect = new Rect(x, y, width, LineHeight);
        Widgets.CheckboxLabeled(rowRect, labelKey.Translate().RawText, ref next);
        if (next != current) cfg.ApplyValue(data!, next);

        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(rowRect, tip);
        y += LineHeight;
    }

    private void DrawListRow(float x, ref float y, float width, string field, string labelKey, string commentKey)
    {
        var cfg = data?.GetFieldConfig(field);
        if (cfg == null) return;

        var list = cfg.GetRawValue(data!) as List<StatModifier>;
        int count = list?.Count ?? 0;

        var labelRect = new Rect(x, y, width - EditButtonWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var buttonRect = new Rect(labelRect.xMax + Spacing, y, EditButtonWidth, LineHeight);
        string buttonText = $"{("MST.relayConfigEdit".Translate().RawText)} ({count})";
        if (Widgets.ButtonText(buttonRect, buttonText))
        {
            // 窗口里改到一半点取消，不能把真实字段改脏 —— 先克隆一份。
            var initial = list == null
                ? new List<StatModifier>()
                : list.Where(s => s != null)
                    .Select(s => new StatModifier { stat = s.stat, value = s.value })
                    .ToList();
            OpenChildWindow(new StatModListEditorWindow(
                $"{data?.label} - {labelKey.Translate().RawText}",
                initial,
                next => cfg.ApplyValue(data!, next ?? new List<StatModifier>())));
        }

        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(buttonRect, tip);
        y += LineHeight;
    }

    private static void DrawSectionTitle(ref float y, float x, float width, string key)
    {
        Widgets.DrawLineHorizontal(x, y, width);
        y += 4f;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(x, y, width, LineHeight), key.Translate().RawText);
        y += LineHeight;
    }

    private static void DrawWrappedText(ref float y, float x, float width, string textOrKey, GameFont font, Color color)
    {
        string text = textOrKey.TryTranslate(out var translated) ? translated : textOrKey;

        var prevFont = Text.Font;
        var prevColor = GUI.color;
        bool prevWrap = Text.WordWrap;

        Text.Font = font;
        Text.WordWrap = true;
        GUI.color = color;
        float height = Text.CalcHeight(text, width);
        Widgets.Label(new Rect(x, y, width, height), text);
        GUI.color = prevColor;
        Text.WordWrap = prevWrap;
        Text.Font = prevFont;
        y += height + 2f;
    }

    private bool HasField(string field) => data?.GetFieldConfig(field) != null;

    // ── 数字输入缓冲 ──

    private void DrawNumberField(Rect rect, StatColumnConfig cfg)
    {
        string id = BufferPrefix + cfg.fieldName;
        bool integer = IsIntField(cfg);
        string current = FormatValue(cfg.GetRawValue(data!), integer);
        if (!buffers.TryGetValue(id, out var buffer))
            buffers[id] = buffer = current;

        GUI.SetNextControlName(id);
        string text = Widgets.TextField(rect, buffer);
        if (text != buffer) buffers[id] = text;

        bool enter = Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == id;
        if (!enter) return;

        Commit(id);
        GUI.FocusControl(null);
        Event.current.Use();
    }

    /// <summary>焦点从某个输入框移走时提交它的缓冲（Enter 已在 <see cref="DrawNumberField"/> 处理）。</summary>
    private void CommitOnFocusChange()
    {
        string focused = GUI.GetNameOfFocusedControl();
        if (focusedField != null && focused != focusedField)
            Commit(focusedField);
        focusedField = focused;
    }

    private void CommitAllBuffers()
    {
        if (buffers.Count == 0 || data == null) return;
        foreach (var id in new List<string>(buffers.Keys))
            Commit(id);
        buffers.Clear();
    }

    private void Commit(string id)
    {
        if (data == null || !buffers.TryGetValue(id, out var text)) return;
        string field = id.Substring(BufferPrefix.Length);
        var cfg = data.GetFieldConfig(field);
        if (cfg == null)
        {
            buffers.Remove(id);
            return;
        }

        if (!TryParse(cfg, text, out var value))
        {
            // 非法 / 半截输入：丢掉缓冲，下次按当前值重新生成，别把坏文本留在界面上。
            buffers.Remove(id);
            return;
        }

        if (!ValueEquals(cfg.GetRawValue(data), value))
            cfg.ApplyValue(data, value);
        buffers[id] = FormatValue(value, IsIntField(cfg));
    }

    private static bool IsIntField(StatColumnConfig cfg)
    {
        var type = cfg.field?.FieldType;
        if (type == null) return false;
        return (Nullable.GetUnderlyingType(type) ?? type) == typeof(int);
    }

    private static bool TryParse(StatColumnConfig cfg, string text, out object value)
    {
        if (IsIntField(cfg))
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = 0;
                return false;
            }
            i = cfg.fieldName == nameof(BuildingData.facilityMaxSimultaneous) ? Math.Max(1, i) : Math.Max(0, i);
            value = i;
            return true;
        }

        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            value = 0f;
            return false;
        }
        value = Mathf.Max(0f, f);
        return true;
    }

    private static string FormatValue(object? value, bool integer) => value switch
    {
        int i => i.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(integer ? "0" : "0.##", CultureInfo.InvariantCulture),
        _ => "",
    };

    private static bool ValueEquals(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai == bi;
        if (a is float af && b is float bf) return Mathf.Approximately(af, bf);
        return Equals(a, b);
    }
}
