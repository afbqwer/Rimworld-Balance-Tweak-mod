using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 机械武器过滤（<c>MechWeaponExtension</c>）的统一编辑窗口。
///
/// <para>
/// <b>为什么是一个窗口而不是几列：</b>这几个字段互相配合（总开关决定其余三组过滤是否生效，
/// 科技 / 类别开关各自决定对应列表是否参与），拆成列既没有批量编辑价值，也看不出组合语义。
/// 所以表格里只留一个触发列，字段本身标 <c>[TweakField(NotAColumn = true)]</c>：
/// 照常存档 / 参与三态 / 重置，但不进入表格。
/// </para>
///
/// <para>
/// 每次改动都走 <see cref="StatColumnConfig.ApplyValue"/>，与表格编辑同一条路径：
/// 标记三态 → 调父行 <c>Apply</c> → 写回扩展 → 登记修改表。关窗口不需要额外保存。
/// </para>
/// </summary>
public class MechWeaponFilterWindow : EditorWindowBase
{
    private const float LineHeight = 24f;
    private const float SectionGap = 10f;
    private const float EditButtonWidth = 110f;
    private const float ViewHeight = 560f;

    private Vector2 scroll;

    public MechWeaponFilterWindow(TweakData data)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {"MST.mechWeaponFilter".Translate().RawText}";
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(620f, 660f);

    protected override string SaveButtonLabel => "MST.Close".Translate().RawText;
    protected override bool ShowCancelButton => false;
    protected override void OnSave() => Close();

    public override void DoWindowContents(Rect inRect)
    {
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

        DrawWrappedText(ref y, x, width, "MST.mechWeaponFilterIntro", GameFont.Tiny, Color.gray);
        y += SectionGap;

        // ── 总开关 ──
        bool filter = Bool("enableWeaponFilter");
        if (DrawCheckbox(x, y, width, "MST.enableWeaponFilter", filter, out bool nextFilter))
            Apply("enableWeaponFilter", nextFilter);
        y += LineHeight;
        DrawWrappedText(ref y, x, width,
            filter ? "MST.enableWeaponFilterOnComment" : "MST.enableWeaponFilterOffComment",
            GameFont.Tiny, Color.gray);
        y += SectionGap;

        // ── 武器标签 ──
        var tags = ListOf<string>("usableWeaponTags");
        DrawEditRow(x, y, width, "MST.usableWeaponTags", tags.Count, () =>
        {
            OpenChildWindow(new StringListEditorWindow(
                $"{data?.label} - {"MST.usableWeaponTags".Translate().RawText}",
                new List<string>(tags),
                list => Apply("usableWeaponTags", list ?? new List<string>())));
        });
        y += LineHeight;

        // ── 科技等级 ──
        bool techFilter = Bool("enableTechLevelFilter");
        if (DrawCheckbox(x, y, width, "MST.enableTechLevelFilter", techFilter, out bool nextTechFilter))
            Apply("enableTechLevelFilter", nextTechFilter);
        y += LineHeight;

        DrawTechLevelToggles(ref y, x, width);
        DrawWrappedText(ref y, x, width, "MST.usableTechLevelsComment", GameFont.Tiny, Color.gray);
        y += SectionGap;

        // ── 武器类别 ──
        bool classFilter = Bool("enableClassFilter");
        if (DrawCheckbox(x, y, width, "MST.enableClassFilter", classFilter, out bool nextClassFilter))
            Apply("enableClassFilter", nextClassFilter);
        y += LineHeight;

        var classes = ListOf<WeaponClassDef>("usableWeaponClasses");
        DrawEditRow(x, y, width, "MST.usableWeaponClasses", classes.Count, () =>
        {
            OpenChildWindow(new DefListEditorWindow(
                $"{data?.label} - {"MST.usableWeaponClasses".Translate().RawText}",
                new List<WeaponClassDef>(classes),
                typeof(WeaponClassDef),
                list => Apply("usableWeaponClasses",
                    list?.OfType<WeaponClassDef>().ToList() ?? new List<WeaponClassDef>())));
        });
        y += LineHeight;
        DrawWrappedText(ref y, x, width, "MST.usableWeaponClassesComment", GameFont.Tiny, Color.gray);
        y += SectionGap;

        // ── 豁免武器 ──
        var bypassNames = ListOf<string>("bypassUsableWeapons");
        DrawEditRow(x, y, width, "MST.bypassUsableWeapons", bypassNames.Count, () =>
        {
            // 扩展存的是武器 defName；界面上给武器选择器，保存时再转回名字。
            var current = bypassNames
                .Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n))
                .Where(d => d != null)
                .Cast<ThingDef>()
                .ToList();
            OpenChildWindow(new DefListEditorWindow(
                $"{data?.label} - {"MST.bypassUsableWeapons".Translate().RawText}",
                current,
                typeof(ThingDef),
                list =>
                {
                    var names = new List<string>();
                    if (list != null)
                        foreach (var item in list)
                            if (item is ThingDef td) names.Add(td.defName);
                    Apply("bypassUsableWeapons", names);
                }));
        });
        y += LineHeight;
        DrawWrappedText(ref y, x, width, "MST.bypassUsableWeaponsComment", GameFont.Tiny, Color.gray);
        y += SectionGap * 2;

        // ── 状态 ──
        DrawStatus(ref y, x, width);

        Widgets.EndScrollView();
    }

    private void DrawTechLevelToggles(ref float y, float x, float width)
    {
        var levels = ListOf<int>("usableTechLevels");
        var values = (TechLevel[])Enum.GetValues(typeof(TechLevel));
        const int perRow = 4;
        float cellWidth = width / perRow;

        for (int i = 0; i < values.Length; i++)
        {
            var level = values[i];
            int col = i % perRow;
            int row = i / perRow;
            Rect cell = new(x + col * cellWidth, y + row * LineHeight, cellWidth, LineHeight);

            bool on = levels.Contains((int)level);
            Widgets.CheckboxLabeled(cell, TechLevelLabel(level), ref on);
            if (on == levels.Contains((int)level)) continue;

            var next = new List<int>(levels);
            if (on) next.Add((int)level);
            else next.Remove((int)level);
            next.Sort();
            Apply("usableTechLevels", next);
            levels = next;
        }

        y += ((values.Length + perRow - 1) / perRow) * LineHeight;
    }

    private void DrawStatus(ref float y, float x, float width)
    {
        bool filter = Bool("enableWeaponFilter");
        var tags = ListOf<string>("usableWeaponTags");

        if (filter)
        {
            DrawWrappedText(ref y, x, width, "MST.mechWeaponFilterStatusOn", GameFont.Small, Color.white);
            if (tags.Count == 0)
                DrawWrappedText(ref y, x, width, "MST.mechWeaponFilterWarningEmptyTags", GameFont.Small, Color.yellow);
        }
        else
        {
            DrawWrappedText(ref y, x, width, "MST.mechWeaponFilterStatusOff", GameFont.Small, Color.white);
        }

        if (data?.def is ThingDef { race: not null } race)
        {
            string text = "MST.mechWeaponFilterBodySize".Translate(race.race.baseBodySize.ToString("0.##")).RawText;
            DrawWrappedText(ref y, x, width, text, GameFont.Tiny, Color.gray);
        }
    }

    // ── 小工具 ──

    private bool DrawCheckbox(float x, float y, float width, string labelKey, bool value, out bool next)
    {
        next = value;
        Widgets.CheckboxLabeled(new Rect(x, y, width, LineHeight), labelKey.Translate().RawText, ref next);
        return next != value;
    }

    private static void DrawEditRow(float x, float y, float width, string labelKey, int count, Action onClick)
    {
        var labelRect = new Rect(x, y, width - EditButtonWidth - Spacing, LineHeight);
        var buttonRect = new Rect(labelRect.xMax + Spacing, y, EditButtonWidth, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        string text = $"{("MST.mechWeaponFilterEdit".Translate().RawText)} ({count})";
        if (Widgets.ButtonText(buttonRect, text)) onClick();
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

    private static string TechLevelLabel(TechLevel level)
        => level == TechLevel.Undefined ? "Undefined" : ("TechLevel_" + level).Translate().RawText;

    private T? Raw<T>(string fieldName) where T : class
        => data?.GetFieldConfig(fieldName)?.GetRawValue(data) as T;

    private bool Bool(string fieldName)
        => data?.GetFieldConfig(fieldName)?.GetRawValue(data) as bool? ?? false;

    private List<T> ListOf<T>(string fieldName)
        => Raw<List<T>>(fieldName) ?? new List<T>();

    private void Apply(string fieldName, object? value)
    {
        if (data == null) return;
        data.GetFieldConfig(fieldName)?.ApplyValue(data, value);
    }
}
