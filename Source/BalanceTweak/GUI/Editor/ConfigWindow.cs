using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 「一组字段塞进一个窗口」的通用编辑器 —— <see cref="StatColumnConfig.ColumnStyle.Config"/> 触发列的目标窗口。
///
/// <para>
/// <b>为什么要有它</b>：一个 Def 上可能有十几到几十个相关字段（AO 的建筑有 9 个 Comp + 1 个扩展、56 个数值），
/// 逐列展示会把表格挤爆，而且绝大多数行只有其中两三个有意义。做法是表格里只留一个只读触发列，
/// 字段本身 <c>[TweakField(NotAColumn = true)]</c>，点开这个窗口统一编辑。
/// </para>
///
/// <para>
/// <b>用法就是声明一份 <see cref="ConfigSection"/> 数组</b>：分节、节的出现条件、每行的控件类型与取值范围
/// 全在静态字段里写完，输入缓冲 / 三态写入 / 关窗提交 / 校验渲染都在基类。
/// </para>
///
/// <code>
/// [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
///     Editor = typeof(MyConfigWindow), MayRequire = TweakDatabase.AO)]
/// public TweakID? myConfig = null;
/// </code>
///
/// <para>
/// <b>写入路径</b>：每一行都走 <see cref="StatColumnConfig.ApplyValue"/> / <see cref="StatColumnConfig.CheckAndApply"/>，
/// 与表格单元格编辑完全同一条三态 / 重置 / 缓存失效路径，没有第二套写入逻辑。
/// </para>
/// </summary>
public class ConfigWindow : EditorWindowBase
{
    private const float LineHeight = 24f;
    private const float SectionGap = 10f;
    private const float FieldWidth = 140f;
    private const float ButtonWidth = 160f;
    private const string BufferPrefix = "cfg.";

    private readonly string introKey;
    private readonly string? noteKey;
    private readonly IReadOnlyList<ConfigSection> sections;
    private readonly float preferredHeight;
    private readonly Func<TweakData, IReadOnlyList<ConfigIssue>>? validate;

    private readonly Dictionary<string, string> buffers = new();
    private string? focusedField;
    private Vector2 scroll;

    /// <summary>本节校验结果。每次重绘算一次，测量与绘制复用同一份，避免同一帧跑两遍。</summary>
    private List<ConfigIssue>? issues;

    protected ConfigWindow(TweakData data,
        string titleKey,
        string introKey,
        IReadOnlyList<ConfigSection> sections,
        float preferredHeight = 720f,
        string? noteKey = null,
        Func<TweakData, IReadOnlyList<ConfigIssue>>? validate = null)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {titleKey.Translate().RawText}";
        this.introKey = introKey;
        this.sections = sections;
        this.preferredHeight = preferredHeight;
        this.noteKey = noteKey;
        this.validate = validate;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(660f, Mathf.Clamp(preferredHeight + 100f, 260f, 860f));

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
        float width = rect.width - 16f;

        // 校验只跑一次：测量与绘制共用同一份结果。
        issues = validate != null && data != null ? new List<ConfigIssue>(validate(data)) : null;

        // 先量高度再开滚动区。原先几个窗口各自传一个写死的 viewHeight，字段一多就滚不到底，
        // 而"到底了没"在 IMGUI 里没有可靠回调 —— 直接把高度算出来最省心。
        Rect view = new(0f, 0f, width, Math.Max(rect.height, MeasureHeight(width)));
        Widgets.BeginScrollView(rect, ref scroll, view);

        float x = 0f;
        float y = 0f;

        DrawWrappedText(ref y, x, width, introKey, GameFont.Tiny, Color.gray);
        y += SectionGap;

        foreach (var section in sections)
        {
            if (!IsVisible(section)) continue;
            DrawSection(ref y, x, width, section);
        }

        if (issues != null) DrawValidation(ref y, x, width);

        if (!noteKey.NullOrEmpty())
        {
            y += SectionGap;
            DrawWrappedText(ref y, x, width, noteKey!, GameFont.Tiny, Color.gray);
        }

        Widgets.EndScrollView();
    }

    private bool IsVisible(ConfigSection section) => section.visible?.Invoke(data!) ?? true;

    // ── 高度测量（与 DrawSection 一一对应，改一边记得改另一边） ──

    private float MeasureHeight(float width)
    {
        float h = CalcTextHeight(introKey, width, GameFont.Tiny) + 2f + SectionGap;

        foreach (var section in sections)
        {
            if (!IsVisible(section)) continue;
            h += MeasureSection(section, width);
        }

        if (issues != null)
        {
            h += LineHeight + 4f;   // 「配置检查」节标题
            if (issues.Count == 0)
                h += CalcTextHeight("MST.configValidationOk", width, GameFont.Small) + 2f;
            else
                foreach (var issue in issues)
                    h += CalcTextHeight(issue.text, width, GameFont.Small) + 2f;
        }

        if (!noteKey.NullOrEmpty())
            h += SectionGap + CalcTextHeight(noteKey!, width, GameFont.Tiny) + 2f;

        return h + SectionGap;
    }

    private static float MeasureSection(ConfigSection section, float width)
    {
        float h = 0f;
        if (!section.titleKey.NullOrEmpty()) h += LineHeight + 4f;
        h += section.rows.Count * LineHeight;
        if (!section.noteKey.NullOrEmpty())
            h += CalcTextHeight(section.noteKey!, width, GameFont.Tiny) + 2f;
        return h + SectionGap;
    }

    // ── 绘制 ──

    private void DrawSection(ref float y, float x, float width, ConfigSection section)
    {
        if (!section.titleKey.NullOrEmpty()) DrawSectionTitle(ref y, x, width, section.titleKey!);

        foreach (var row in section.rows)
        {
            var cfg = data?.GetFieldConfig(row.field);
            if (cfg == null)
            {
                // 字段不存在（改了名字 / 漏了 NotAColumn 声明）：跳过整行，别画一个永远读不出值的空框。
                y += LineHeight;
                continue;
            }

            switch (row.kind)
            {
                case ConfigRow.RowKind.Bool: DrawBoolRow(x, ref y, width, cfg, row); break;
                case ConfigRow.RowKind.Enum: DrawEnumRow(x, ref y, width, cfg, row); break;
                case ConfigRow.RowKind.Def:  DrawButtonRow(x, ref y, width, cfg, row, OpenDefPicker); break;
                case ConfigRow.RowKind.List: DrawButtonRow(x, ref y, width, cfg, row, OpenListEditor); break;
                default:                     DrawInputRow(x, ref y, width, cfg, row); break;
            }
        }

        if (!section.noteKey.NullOrEmpty())
            DrawWrappedText(ref y, x, width, section.noteKey!, GameFont.Tiny, Color.gray);
        y += SectionGap;
    }

    /// <summary>
    /// 内联文本框行。数字 / 百分比走"解析后 ApplyValue"，字符串整段交给 CheckAndApply
    /// （它只在文本与当前显示值不同才落盘）。两者都只在回车 / 失焦 / 关窗时提交，
    /// 不做每敲一个字符一次 Apply —— 那会顺带触发一轮缓存刷新与整行重算。
    /// </summary>
    private void DrawInputRow(float x, ref float y, float width, StatColumnConfig cfg, ConfigRow row)
    {
        string id = BufferPrefix + cfg.fieldName;
        string current = row.kind == ConfigRow.RowKind.String
            ? cfg.GetRawValue(data!) as string ?? ""
            : FormatValue(cfg.GetRawValue(data!), row);

        if (!buffers.TryGetValue(id, out var buffer))
            buffers[id] = buffer = current;

        DrawLabel(x, y, width - FieldWidth - Spacing, row.labelKey);
        var fieldRect = new Rect(x + width - FieldWidth, y, FieldWidth, LineHeight);

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
        Tip(fieldRect, row.commentKey);
        y += LineHeight;
    }

    private void DrawBoolRow(float x, ref float y, float width, StatColumnConfig cfg, ConfigRow row)
    {
        bool current = cfg.GetRawValue(data!) as bool? ?? false;
        bool next = current;
        var rowRect = new Rect(x, y, width, LineHeight);
        Widgets.CheckboxLabeled(rowRect, row.labelKey.Translate().RawText, ref next);
        if (next != current) cfg.ApplyValue(data!, next);
        Tip(rowRect, row.commentKey);
        y += LineHeight;
    }

    /// <summary>
    /// 下拉行。选项来源优先 <c>OptionsList</c>（跨 Mod 枚举只能用字符串列表），否则取 <c>EnumType</c> 的成员名。
    /// 写回统一走 <see cref="StatColumnConfig.CheckAndApply"/> —— 它已经处理了"字段是 string 还是 enum"两条路。
    /// </summary>
    private void DrawEnumRow(float x, ref float y, float width, StatColumnConfig cfg, ConfigRow row)
    {
        var options = cfg.GetOptions() ?? (cfg.EnumType != null ? Enum.GetNames(cfg.EnumType).ToList() : null);

        DrawLabel(x, y, width - ButtonWidth - Spacing, row.labelKey);
        var buttonRect = new Rect(x + width - ButtonWidth, y, ButtonWidth, LineHeight);
        string label = cfg.GetString(data!) is { Length: > 0 } s ? s : "MST.None".Translate().RawText;

        if (Widgets.ButtonText(buttonRect, label) && options != null && options.Count > 0)
        {
            var menu = options
                .Select(o => new FloatMenuOption(o, () => cfg.CheckAndApply(data!, o)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(menu));
        }
        else if (options == null || options.Count == 0)
        {
            TooltipHandler.TipRegion(buttonRect, "MST.configNoOptions".Translate());
        }

        Tip(buttonRect, row.commentKey);
        y += LineHeight;
    }

    /// <summary>Def 引用 / 列表这类"点开另一个窗口"的行。文字与目标窗口都由列配置自己决定。</summary>
    private void DrawButtonRow(float x, ref float y, float width, StatColumnConfig cfg, ConfigRow row,
        Action<StatColumnConfig, ConfigRow> onClick)
    {
        string text = row.kind == ConfigRow.RowKind.Def
            ? cfg.GetString(data!) is { Length: > 0 } s ? s : "MST.None".Translate().RawText
            : $"{"MST.Edit".Translate().RawText} ({(cfg.GetRawValue(data!) as System.Collections.IList)?.Count ?? 0})";

        DrawLabel(x, y, width - ButtonWidth - Spacing, row.labelKey);
        var buttonRect = new Rect(x + width - ButtonWidth, y, ButtonWidth, LineHeight);
        if (Widgets.ButtonText(buttonRect, text)) onClick(cfg, row);

        Tip(buttonRect, row.commentKey);
        y += LineHeight;
    }

    private void OpenDefPicker(StatColumnConfig cfg, ConfigRow row)
        => OpenChildWindow(new DefSelectionWindow(cfg, data!));

    private void OpenListEditor(StatColumnConfig cfg, ConfigRow row)
    {
        var window = BalanceTweakSettings.CreateEditor(cfg, data!);
        if (window != null) OpenChildWindow(window);
    }

    /// <summary>配置检查：错误红色、提示黄色；没有问题时显示一行绿色状态。</summary>
    private void DrawValidation(ref float y, float x, float width)
    {
        DrawSectionTitle(ref y, x, width, "MST.configValidationSection");
        if (issues!.Count == 0)
        {
            DrawWrappedText(ref y, x, width, "MST.configValidationOk", GameFont.Small, Color.green);
            return;
        }

        foreach (var issue in issues)
            DrawWrappedText(ref y, x, width, issue.text, GameFont.Small,
                issue.isError ? Color.red : Color.yellow);
    }

    private static void DrawLabel(float x, float y, float width, string labelKey)
    {
        var prevAnchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, y, width, LineHeight), labelKey.Translate().RawText);
        Text.Anchor = prevAnchor;
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

    /// <summary>只量不画。字体 / 换行状态用完必须还原（tooltip 会沿用调用时的全局值，见 Readme §4.7）。</summary>
    private static float CalcTextHeight(string textOrKey, float width, GameFont font)
    {
        string text = textOrKey.TryTranslate(out var translated) ? translated : textOrKey;
        var prevFont = Text.Font;
        bool prevWrap = Text.WordWrap;
        Text.Font = font;
        Text.WordWrap = true;
        float h = Text.CalcHeight(text, width);
        Text.WordWrap = prevWrap;
        Text.Font = prevFont;
        return h;
    }

    private static void Tip(Rect rect, string? commentKey)
    {
        if (!commentKey.NullOrEmpty() && commentKey!.TryTranslate(out var tip))
            TooltipHandler.TipRegion(rect, tip);
    }

    // ── 输入缓冲 ──

    /// <summary>焦点从某个输入框移走时提交它的缓冲（Enter 已在各行处理）。</summary>
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

        // 字符串列不做数值解析，整段交给 CheckAndApply（它只在文本与当前显示值不同才落盘）。
        if (row.kind == ConfigRow.RowKind.String)
        {
            cfg.CheckAndApply(data, text);
            buffers[id] = cfg.GetRawValue(data) as string ?? "";
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
        buffers[id] = FormatValue(value, row);
    }

    private ConfigRow? FindRow(string field)
    {
        foreach (var section in sections)
            foreach (var row in section.rows)
                if (row.field == field) return row;
        return null;
    }

    private static bool TryParse(ConfigRow row, string text, out object value)
    {
        if (row.kind == ConfigRow.RowKind.Int)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                value = 0;
                return false;
            }
            value = Mathf.Clamp(i, (int)row.min, (int)row.max);
            return true;
        }

        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            value = 0f;
            return false;
        }

        // 百分比列：界面按 0~100 收，字段仍存 0~1（与 Prec 列同一约定）。
        value = Mathf.Clamp(f, row.min, row.max);
        if (row.kind == ConfigRow.RowKind.Percent) value = (float)value / 100f;
        return true;
    }

    private static string FormatValue(object? value, ConfigRow row)
    {
        if (row.kind == ConfigRow.RowKind.Int)
            return value is int i ? i.ToString(CultureInfo.InvariantCulture) : "";
        if (row.kind == ConfigRow.RowKind.Percent)
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

/// <summary>
/// <see cref="ConfigWindow"/> 的一节：标题 + 出现条件 + 若干行 + 节尾说明。
/// <para>
/// 节的 <see cref="visible"/> 就是"这个 Def 有没有对应 Comp / 扩展"的判据。判据照着写
/// <c>static bool Foo(TweakData d) =&gt; …</c> 再传方法组 —— <b>不要</b>在 lambda 里捕获可选 Mod 的类型，
/// 那会编出 typeref（见 <c>AssemblyReferenceTests</c>）。
/// </para>
/// </summary>
public sealed class ConfigSection
{
    public readonly string? titleKey;
    public readonly Func<TweakData, bool>? visible;
    public readonly IReadOnlyList<ConfigRow> rows;
    public string? noteKey { get; private set; }

    private ConfigSection(string? titleKey, Func<TweakData, bool>? visible, ConfigRow[] rows)
    {
        this.titleKey = titleKey;
        this.visible = visible;
        this.rows = rows;
    }

    public static ConfigSection Of(string? titleKey, Func<TweakData, bool>? visible, params ConfigRow[] rows)
        => new(titleKey, visible, rows ?? Array.Empty<ConfigRow>());

    /// <summary>节尾的灰色说明（key 或已翻译文本都行，绘制时先试翻译）。</summary>
    public ConfigSection Note(string noteKey)
    {
        this.noteKey = noteKey;
        return this;
    }
}

/// <summary>
/// 配置窗口里的一行。默认 labelKey = <c>MST.{字段名}</c>、commentKey = <c>MST.{字段名}Comment</c>
/// —— 与表格列标题同一套约定，<c>CheckLoc</c> 会自动把这两个键算作"代码引用"。
/// </summary>
public sealed class ConfigRow
{
    public enum RowKind { Int, Float, Percent, Bool, Enum, Def, String, List }

    public readonly RowKind kind;
    public readonly string field;
    public readonly string labelKey;
    public readonly string? commentKey;
    public readonly float min;
    public readonly float max;

    private ConfigRow(RowKind kind, string field, string? labelKey, string? commentKey, float min, float max)
    {
        this.kind = kind;
        this.field = field;
        this.labelKey = labelKey ?? "MST." + field;
        // null = 明说"这一行没有说明"；不传则按约定找 MST.{字段名}Comment。
        this.commentKey = commentKey ?? "MST." + field + "Comment";
        this.min = min;
        this.max = max;
    }

    public static ConfigRow Int(string field, int min = 0, int max = int.MaxValue,
        string? labelKey = null, string? commentKey = null)
        => new(RowKind.Int, field, labelKey, commentKey, min, max);

    public static ConfigRow Float(string field, float min = 0f, float max = float.MaxValue,
        string? labelKey = null, string? commentKey = null)
        => new(RowKind.Float, field, labelKey, commentKey, min, max);

    /// <summary>界面按 0~100 显示，字段仍存 0~1（与 <c>ColumnStyle.Prec</c> 同一约定）。</summary>
    public static ConfigRow Percent(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.Percent, field, labelKey, commentKey, 0f, 100f);

    public static ConfigRow Bool(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.Bool, field, labelKey, commentKey, 0f, 0f);

    /// <summary>下拉：选项来自字段的 <c>OptionsList</c>，没有则用 <c>EnumType</c> 的成员名。</summary>
    public static ConfigRow Enum(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.Enum, field, labelKey, commentKey, 0f, 0f);

    public static ConfigRow Def(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.Def, field, labelKey, commentKey, 0f, 0f);

    public static ConfigRow String(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.String, field, labelKey, commentKey, 0f, 0f);

    /// <summary>
    /// 列表字段：点按钮打开该列样式对应的编辑器窗口（<see cref="BalanceTweakSettings.CreateEditor"/>）。
    /// 字段本身仍需按 <c>ColumnStyle.StringList</c> / <c>StatModList</c> 等声明。
    /// </summary>
    public static ConfigRow List(string field, string? labelKey = null, string? commentKey = null)
        => new(RowKind.List, field, labelKey, commentKey, 0f, 0f);
}

/// <summary>一条配置检查结果。<see cref="isError"/> = 错误（红），否则是提示（黄）。</summary>
public sealed class ConfigIssue
{
    public readonly bool isError;
    public readonly string text;

    private ConfigIssue(bool isError, string text)
    {
        this.isError = isError;
        this.text = text;
    }

    public static ConfigIssue Error(string text) => new(true, text);
    public static ConfigIssue Warning(string text) => new(false, text);
}
