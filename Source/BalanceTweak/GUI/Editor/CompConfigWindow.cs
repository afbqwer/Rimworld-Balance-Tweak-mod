using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 一组标量字段（数字 / 开关）的通用配置窗口。
///
/// <para>
/// 护盾与 APS 的字段都挂在别人的 CompProperties 上，逐列展示会挤占表格；因此在宿主行上只留一个
/// 只读触发列，字段本身 <c>[TweakField(NotAColumn = true)]</c>，由这个窗口统一编辑。
/// 子类只需要声明字段顺序与范围，不再重复输入缓冲 / 三态写入逻辑。
/// </para>
///
/// <para>
/// 数字输入带编辑缓冲：只有回车或失焦才提交，避免每敲一个字符就走一次 <c>Apply</c>。
/// 所有写入都走 <see cref="StatColumnConfig.ApplyValue"/>，与表格编辑同一条三态 / 重置路径。
/// </para>
/// </summary>
public class CompConfigWindow : EditorWindowBase
{
    private const float LineHeight = 24f;
    private const float SectionGap = 10f;
    private const float FieldWidth = 140f;
    private const string BufferPrefix = "comp.";

    private readonly string introKey;
    private readonly string? noteKey;
    private readonly IReadOnlyList<CompConfigRow> rows;
    private readonly float viewHeight;
    private readonly Func<TweakData, IReadOnlyList<CompConfigIssue>>? validate;

    private readonly Dictionary<string, string> buffers = new();
    private string? focusedField;
    private Vector2 scroll;

    protected CompConfigWindow(TweakData data, string titleKey, string introKey, string? noteKey,
        IReadOnlyList<CompConfigRow> rows, float viewHeight,
        Func<TweakData, IReadOnlyList<CompConfigIssue>>? validate = null)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {titleKey.Translate().RawText}";
        this.introKey = introKey;
        this.noteKey = noteKey;
        this.rows = rows;
        this.viewHeight = viewHeight;
        this.validate = validate;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(640f, Mathf.Min(760f, viewHeight + 100f));

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
        Rect view = new(0f, 0f, rect.width - 16f, viewHeight);
        Widgets.BeginScrollView(rect, ref scroll, view);

        float x = 0f;
        float width = view.width;
        float y = 0f;

        DrawWrappedText(ref y, x, width, introKey, GameFont.Tiny, Color.gray);
        y += SectionGap;

        foreach (var row in rows)
        {
            switch (row.kind)
            {
                case CompConfigRow.Kind.Section:
                    DrawSectionTitle(ref y, x, width, row.labelKey);
                    break;
                case CompConfigRow.Kind.Bool:
                    DrawBoolRow(x, ref y, width, row);
                    break;
                default:
                    DrawNumberRow(x, ref y, width, row);
                    break;
            }
        }

        if (validate != null && data != null)
        {
            y += SectionGap;
            DrawValidation(ref y, x, width);
        }

        if (!noteKey.NullOrEmpty())
        {
            y += SectionGap;
            DrawWrappedText(ref y, x, width, noteKey!, GameFont.Tiny, Color.gray);
        }

        Widgets.EndScrollView();
    }

    /// <summary>配置检查：错误红色、提示黄色；没有问题时显示一行绿色状态。</summary>
    private void DrawValidation(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.configValidationSection");
        var issues = validate!(data!);
        if (issues.Count == 0)
        {
            DrawWrappedText(ref y, x, width, "MST.configValidationOk", GameFont.Small, Color.green);
            return;
        }

        foreach (var issue in issues)
            DrawWrappedText(ref y, x, width, issue.text, GameFont.Small,
                issue.isError ? Color.red : Color.yellow);
    }

    private void DrawNumberRow(float x, ref float y, float width, CompConfigRow row)
    {
        if (row.field == null) return;
        var cfg = data?.GetFieldConfig(row.field);
        if (cfg == null) return;

        var labelRect = new Rect(x, y, width - FieldWidth - Spacing, LineHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, row.labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        var fieldRect = new Rect(x + width - FieldWidth, y, FieldWidth, LineHeight);
        string id = BufferPrefix + row.field;
        string current = FormatValue(cfg.GetRawValue(data!), row.kind);
        if (!buffers.TryGetValue(id, out var buffer))
            buffers[id] = buffer = current;

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

        if (!row.commentKey.NullOrEmpty() && row.commentKey!.TryTranslate(out var tip))
            TooltipHandler.TipRegion(fieldRect, tip);
        y += LineHeight;
    }

    private void DrawBoolRow(float x, ref float y, float width, CompConfigRow row)
    {
        if (row.field == null) return;
        var cfg = data?.GetFieldConfig(row.field);
        if (cfg == null) return;

        bool current = cfg.GetRawValue(data!) as bool? ?? false;
        bool next = current;
        var rowRect = new Rect(x, y, width, LineHeight);
        Widgets.CheckboxLabeled(rowRect, row.labelKey.Translate().RawText, ref next);
        if (next != current) cfg.ApplyValue(data!, next);

        if (!row.commentKey.NullOrEmpty() && row.commentKey!.TryTranslate(out var tip))
            TooltipHandler.TipRegion(rowRect, tip);
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

    // ── 数字输入缓冲 ──

    /// <summary>焦点从某个输入框移走时提交它的缓冲（Enter 已在 <see cref="DrawNumberRow"/> 处理）。</summary>
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
        var row = FindRow(field);
        if (cfg == null || row == null)
        {
            buffers.Remove(id);
            return;
        }

        if (!TryParse(row, text, out var value))
        {
            // 非法 / 半截输入：丢掉缓冲，下次按当前值重新生成，别把坏文本留在界面上。
            buffers.Remove(id);
            return;
        }

        if (!ValueEquals(cfg.GetRawValue(data), value))
            cfg.ApplyValue(data, value);
        buffers[id] = FormatValue(value, row.kind);
    }

    private CompConfigRow? FindRow(string field)
    {
        foreach (var row in rows)
            if (row.field == field) return row;
        return null;
    }

    private static bool TryParse(CompConfigRow row, string text, out object value)
    {
        if (row.kind == CompConfigRow.Kind.Int)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = 0;
                return false;
            }
            value = Mathf.Clamp(i, row.intMin, row.intMax);
            return true;
        }

        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            value = 0f;
            return false;
        }

        // 百分比列：界面按 0~100 收，字段仍存 0~1（与 Prec 列同一约定）。
        value = Mathf.Clamp(f, row.floatMin, row.floatMax);
        if (row.kind == CompConfigRow.Kind.Percent) value = (float)value / 100f;
        return true;
    }

    private static string FormatValue(object? value, CompConfigRow.Kind kind)
    {
        if (kind == CompConfigRow.Kind.Int)
            return value is int i ? i.ToString(CultureInfo.InvariantCulture) : "";
        if (kind == CompConfigRow.Kind.Percent)
            return value is float pf ? (pf * 100f).ToString("0.##", CultureInfo.InvariantCulture) : "";
        return value is float f ? f.ToString("0.###", CultureInfo.InvariantCulture) : "";
    }

    private static bool ValueEquals(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai == bi;
        if (a is float af && b is float bf) return Mathf.Approximately(af, bf);
        return Equals(a, b);
    }
}

/// <summary><see cref="CompConfigWindow"/> 的一行：数字、开关或分节标题。</summary>
public sealed class CompConfigRow
{
    public enum Kind { Int, Float, Percent, Bool, Section }

    public readonly Kind kind;
    public readonly string? field;
    public readonly string labelKey;
    public readonly string? commentKey;
    public readonly int intMin;
    public readonly int intMax;
    public readonly float floatMin;
    public readonly float floatMax;

    private CompConfigRow(Kind kind, string? field, string labelKey, string? commentKey,
        int intMin, int intMax, float floatMin, float floatMax)
    {
        this.kind = kind;
        this.field = field;
        this.labelKey = labelKey;
        this.commentKey = commentKey;
        this.intMin = intMin;
        this.intMax = intMax;
        this.floatMin = floatMin;
        this.floatMax = floatMax;
    }

    public static CompConfigRow Int(string field, string labelKey, string? commentKey = null,
        int min = 0, int max = int.MaxValue)
        => new(Kind.Int, field, labelKey, commentKey, min, max, 0f, 0f);

    public static CompConfigRow Float(string field, string labelKey, string? commentKey = null,
        float min = 0f, float max = float.MaxValue)
        => new(Kind.Float, field, labelKey, commentKey, 0, int.MaxValue, min, max);

    /// <summary>界面按 0~100 显示，字段仍存 0~1（与 <c>ColumnStyle.Prec</c> 同一约定）。</summary>
    public static CompConfigRow Percent(string field, string labelKey, string? commentKey = null)
        => new(Kind.Percent, field, labelKey, commentKey, 0, int.MaxValue, 0f, 100f);

    public static CompConfigRow Bool(string field, string labelKey, string? commentKey = null)
        => new(Kind.Bool, field, labelKey, commentKey, 0, int.MaxValue, 0f, 0f);

    public static CompConfigRow Section(string labelKey)
        => new(Kind.Section, null, labelKey, null, 0, int.MaxValue, 0f, 0f);
}

/// <summary>一条配置检查结果。<see cref="isError"/> = 错误（红），否则是提示（黄）。</summary>
public sealed class CompConfigIssue
{
    public readonly bool isError;
    public readonly string text;

    private CompConfigIssue(bool isError, string text)
    {
        this.isError = isError;
        this.text = text;
    }

    public static CompConfigIssue Error(string text) => new(true, text);
    public static CompConfigIssue Warning(string text) => new(false, text);
}
