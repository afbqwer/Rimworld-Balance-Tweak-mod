using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 多炮塔配置窗口：主炮（<c>CompProperties_VehicleWeapon</c>）与每个副炮槽
/// （<c>CompPropertiesMultipleTurretGun.subTurrets</c>）分节编辑。
///
/// <para>
/// 表格里只留一个只读触发列（<see cref="ColumnStyle.Config"/>）；槽位数据以
/// <see cref="TurretSlotRecord"/> 列表落盘（NotAColumn），写回时按槽位 ID 原地合并，
/// 保留 Def 上的渲染配置与已存在的 <c>SubTurret</c> 引用。
/// </para>
///
/// <para>
/// 数字输入带编辑缓冲：回车 / 失焦 / 关窗才提交。每次提交都会把整份槽位列表克隆后
/// <see cref="StatColumnConfig.ApplyValue"/> 一次，避免窗口工作副本与存档实例共享元素对象。
/// </para>
/// </summary>
public class TurretConfigWindow : EditorWindowBase
{
    private const float LineHeight = 24f;
    private const float SectionGap = 10f;
    private const float EditButtonWidth = 160f;
    private const float FieldWidth = 120f;
    private const float ViewHeight = 900f;

    private readonly List<TurretSlotRecord> slots;
    private readonly Dictionary<string, string> buffers = new();
    private readonly Dictionary<string, (Action<float> Set, float Value)> numberRows = new();
    private string? focusedField;

    private Vector2 scroll;

    public TurretConfigWindow(TweakData data)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {"MST.turretConfig".Translate().RawText}";
        doCloseX = true;

        var config = data.GetFieldConfig(nameof(RaceData.turretSlots));
        slots = TurretSlotRecord.CloneAll(config?.GetRawValue(data) as List<TurretSlotRecord>);
    }

    public override Vector2 InitialSize => new(660f, 760f);

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

        DrawWrappedText(ref y, x, width, "MST.turretConfigIntro", GameFont.Tiny, Color.gray);
        y += SectionGap;

        var def = data?.def;
        if (FortifiedTurret.FindVehicleWeapon(def) != null && HasField(nameof(RaceData.vehicleDefaultWeapon)))
            DrawVehicleWeapon(ref y, x, width);

        if (FortifiedTurret.FindMultipleTurret(def) != null && HasField(nameof(RaceData.turretSlots)))
        {
            if (slots.Count == 0)
            {
                DrawWrappedText(ref y, x, width, "MST.turretNoSlots", GameFont.Small, Color.gray);
            }
            else
            {
                for (int i = 0; i < slots.Count; i++)
                    DrawSlot(ref y, x, width, i, slots[i]);
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawVehicleWeapon(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.turretMainSection");

        var config = data?.GetFieldConfig(nameof(RaceData.vehicleDefaultWeapon));
        if (config != null)
        {
            var current = config.GetRawValue(data!) as ThingDef;
            DrawDefRow(x, ref y, width, "MST.vehicleDefaultWeapon", "MST.vehicleDefaultWeaponComment", "MST.vehicleSelectWeapon",
                current, v => config.ApplyValue(data!, v));
        }

        // 旋转跟随 / 平滑时间 / 绘制尺寸属于表现层，不开放编辑。
        DrawWrappedText(ref y, x, width, "MST.turretMainNote", GameFont.Tiny, Color.gray);
        y += SectionGap;
    }

    private void DrawSlot(ref float y, float x, float width, int index, TurretSlotRecord slot)
    {
        DrawSectionTitle(ref y, x, width, "MST.turretSlotSection", slot.id);
        string prefix = $"slot{index}.";

        DrawDefRow(x, ref y, width, "MST.turret", "MST.turretComment", "MST.turretSelectWeapon",
            slot.turret, v => { slot.turret = v; CommitSlots(); });

        DrawFloatRow(x, ref y, width, prefix + "warmingTime", "MST.warmingTime", "MST.warmingTimeComment",
            slot.warmingTime, v => { slot.warmingTime = v; CommitSlots(); });

        // 瞄准 / 待机角度属于表现层：记录仍随 TurretSlotRecord 往返（不改原值），但不提供编辑。
        DrawBoolRow(x, ref y, width, "MST.autoAttack", "MST.autoAttackComment",
            slot.autoAttack, v => { slot.autoAttack = v; CommitSlots(); });

        DrawStringRow(x, ref y, width, slot);
        DrawStringListRow(x, ref y, width, slot);

        y += SectionGap;
    }

    private void DrawDefRow(float x, ref float y, float width, string labelKey, string commentKey,
        string selectTextKey, ThingDef? current, Action<ThingDef?> set)
    {
        var labelRect = new Rect(x, y, width - EditButtonWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var buttonRect = new Rect(labelRect.xMax + Spacing, y, EditButtonWidth, LineHeight);
        string text = current == null
            ? selectTextKey.Translate().RawText
            : $"{current.LabelCap}";
        if (Widgets.ButtonText(buttonRect, text))
        {
            var options = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(t => t.IsWeapon || ReferenceEquals(t, current))
                .Cast<Def>()
                .ToList();
            OpenChildWindow(new DefSelectionWindow(
                $"{data?.label} - {labelKey.Translate().RawText}",
                options,
                current,
                selected => set(selected as ThingDef)));
        }
        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(buttonRect, tip);
        y += LineHeight;
    }

    private void DrawStringRow(float x, ref float y, float width, TurretSlotRecord slot)
    {
        var labelRect = new Rect(x, y, width - EditButtonWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "MST.supportedWeaponTag".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var buttonRect = new Rect(labelRect.xMax + Spacing, y, EditButtonWidth, LineHeight);
        string text = slot.supportedWeaponTag.NullOrEmpty() ? "-" : slot.supportedWeaponTag;
        if (Widgets.ButtonText(buttonRect, text))
        {
            OpenChildWindow(new StringEditorWindow(
                $"{data?.label} - {slot.id} - {"MST.supportedWeaponTag".Translate().RawText}",
                slot.supportedWeaponTag,
                value =>
                {
                    slot.supportedWeaponTag = value ?? "";
                    CommitSlots();
                }));
        }
        if ("MST.supportedWeaponTagComment".TryTranslate(out var tip))
            TooltipHandler.TipRegion(buttonRect, tip);
        y += LineHeight;
    }

    private void DrawStringListRow(float x, ref float y, float width, TurretSlotRecord slot)
    {
        var labelRect = new Rect(x, y, width - EditButtonWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "MST.generateWithWeapons".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var buttonRect = new Rect(labelRect.xMax + Spacing, y, EditButtonWidth, LineHeight);
        int count = slot.generateWithWeapons?.Count ?? 0;
        string text = $"{("MST.turretConfigEdit".Translate().RawText)} ({count})";
        if (Widgets.ButtonText(buttonRect, text))
        {
            OpenChildWindow(new StringListEditorWindow(
                $"{data?.label} - {slot.id} - {"MST.generateWithWeapons".Translate().RawText}",
                new List<string>(slot.generateWithWeapons ?? new List<string>()),
                value =>
                {
                    slot.generateWithWeapons = value ?? new List<string>();
                    CommitSlots();
                }));
        }
        if ("MST.generateWithWeaponsComment".TryTranslate(out var tip))
            TooltipHandler.TipRegion(buttonRect, tip);
        y += LineHeight;
    }

    // ── 数字行 ──

    private void DrawFloatRow(float x, ref float y, float width, string id, string labelKey, string commentKey,
        float value, Action<float> set)
    {
        var labelRect = new Rect(x, y, width - FieldWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var fieldRect = new Rect(x + width - FieldWidth, y, FieldWidth, LineHeight);
        string current = value.ToString("0.###", CultureInfo.InvariantCulture);
        if (!buffers.TryGetValue(id, out var buffer))
            buffers[id] = buffer = current;
        numberRows[id] = (set, value);

        GUI.SetNextControlName(id);
        string text = Widgets.TextField(fieldRect, buffer);
        if (text != buffer) buffers[id] = text;

        bool enter = Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == id;
        if (enter)
        {
            Commit(id);
            GUI.FocusControl(null);
            Event.current.Use();
        }

        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(fieldRect, tip);
        y += LineHeight;
    }

    private void DrawBoolRow(float x, ref float y, float width, string labelKey, string commentKey,
        bool value, Action<bool> set)
    {
        bool next = value;
        var rowRect = new Rect(x, y, width, LineHeight);
        Widgets.CheckboxLabeled(rowRect, labelKey.Translate().RawText, ref next);
        if (next != value) set(next);
        if (commentKey.TryTranslate(out var tip))
            TooltipHandler.TipRegion(rowRect, tip);
        y += LineHeight;
    }

    // ── 提交 ──

    private void CommitSlots()
    {
        if (data == null) return;
        var config = data.GetFieldConfig(nameof(RaceData.turretSlots));
        if (config == null) return;
        // 交给字段的是克隆列表：窗口工作副本继续独立编辑，存档实例不与之共享元素。
        config.ApplyValue(data, TurretSlotRecord.CloneAll(slots));
    }

    private void CommitOnFocusChange()
    {
        string focused = GUI.GetNameOfFocusedControl();
        if (focusedField != null && focused != focusedField)
            Commit(focusedField);
        focusedField = focused;
    }

    private void CommitAllBuffers()
    {
        if (buffers.Count == 0) return;
        foreach (var id in new List<string>(buffers.Keys))
            Commit(id);
        buffers.Clear();
    }

    private void Commit(string id)
    {
        if (!buffers.TryGetValue(id, out var text) || !numberRows.TryGetValue(id, out var row)) return;
        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            // 半截输入：丢掉缓冲，下次按当前值重新生成。
            buffers.Remove(id);
            return;
        }

        parsed = Mathf.Max(0f, parsed);
        buffers[id] = parsed.ToString("0.###", CultureInfo.InvariantCulture);
        if (!Mathf.Approximately(parsed, row.Value)) row.Set(parsed);
    }

    // ── 小工具 ──

    private bool HasField(string field) => data?.GetFieldConfig(field) != null;

    private static void DrawSectionTitle(ref float y, float x, float width, string key, string? suffix = null)
    {
        Widgets.DrawLineHorizontal(x, y, width);
        y += 4f;
        string text = key.Translate().RawText;
        if (suffix != null)
            text = string.Format(CultureInfo.InvariantCulture, text, suffix);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(x, y, width, LineHeight), text);
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
}
