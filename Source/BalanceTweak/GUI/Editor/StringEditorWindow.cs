using System;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class StringEditorWindow : EditorWindowBase
{
    private string workingValue;
    private Action<string?>? onSaveCallback;

    private string textFieldControlName;

    private const float TextAreaPadding = 8f;

    // 初始窗口尺寸的上下界：既能把初始字符串完整显示出来，又不会因为默认尺寸过大而不美观。
    private const float MinWindowWidth = 340f;
    private const float MaxWindowWidth = 620f;
    private const float MinWindowHeight = 170f;
    private const float MaxWindowHeight = 560f;

    // 估算文本区高度时预留的余量，避免字体度量误差导致最后一行被裁掉。
    private const float HeightSafetyPadding = 6f;

    public StringEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        workingValue = (string?)config.GetRawValue(data) ?? string.Empty;
        textFieldControlName = "StringEditorTextField_" + config.id;

        absorbInputAroundWindow = true;
    }

    public StringEditorWindow(string title, string initialValue, Action<string?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        workingValue = initialValue ?? string.Empty;
        textFieldControlName = "StringEditorTextField_independent";

        absorbInputAroundWindow = true;
    }

    /// <summary>
    /// 依据初始字符串的内容动态计算初始窗口大小：短文本得到紧凑的小窗口（避免默认尺寸过大而不美观），
    /// 长文本则加宽/加高以完整显示（超长单行靠自动换行，宽度受上限约束）。
    /// </summary>
    public override Vector2 InitialSize
    {
        get
        {
            float width = MaxWindowWidth;
            float height = MinWindowHeight;
            try
            {
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Small;
                try
                {
                    GUIStyle style = Text.CurTextAreaStyle;
                    float stylePadX = style.padding.horizontal;
                    float stylePadY = style.padding.vertical;

                    // 宽度：容纳最长的一行（加上文本区与窗口的内边距），并限制在合理范围内。
                    float longestLine = MeasureLongestLineWidth(workingValue, MaxWindowWidth);
                    float desiredWidth = longestLine + stylePadX + TextAreaPadding * 2f + Margin * 2f;
                    width = Mathf.Clamp(desiredWidth, MinWindowWidth, MaxWindowWidth);

                    // 高度：容纳按当前宽度换行后的全部文本。
                    float wrapWidth = Mathf.Max(1f, width - Margin * 2f - TextAreaPadding * 2f - stylePadX);
                    float textHeight = Text.CalcHeight(workingValue, wrapWidth) + stylePadY;
                    float chromeHeight = Margin * 2f + TitleHeight + Spacing * 3f + BottomBarHeight + TextAreaPadding * 2f;
                    float maxHeight = Mathf.Max(MinWindowHeight, Mathf.Min(MaxWindowHeight, UI.screenHeight * 0.8f));
                    height = Mathf.Clamp(chromeHeight + textHeight + HeightSafetyPadding, MinWindowHeight, maxHeight);
                }
                finally
                {
                    Text.Font = prevFont;
                }
            }
            catch (Exception e)
            {
                // 度量失败（如字体尚未就绪）时退回一个折中的固定尺寸，保证窗口仍能正常打开。
                Log.WarningOnce("[BalanceTweak] Failed to measure string editor window size, falling back to default: " + e, 0x5B1D30);
                width = MaxWindowWidth;
                height = MinWindowHeight;
            }

            return new Vector2(width, height);
        }
    }

    /// <summary>返回文本中最长一行的像素宽度；一旦超过 stopAt 即提前返回，避免超长文本的度量开销。</summary>
    private static float MeasureLongestLineWidth(string text, float stopAt)
    {
        float width = 0f;
        int lineStart = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == '\n')
            {
                width = Mathf.Max(width, Text.CalcSize(text.Substring(lineStart, i - lineStart)).x);
                if (width >= stopAt)
                {
                    return width;
                }
                lineStart = i + 1;
            }
        }
        return width;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        // Title
        Text.Font = GameFont.Medium;
        string titleText = GetBaseTitle();
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), titleText);
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        // Text editing area
        float textAreaHeight = inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing - Spacing;
        Rect textAreaRect = new(inRect.x, curY, inRect.width, textAreaHeight);

        GUI.BeginGroup(textAreaRect);
        DrawTextArea(textAreaRect.AtZero());
        GUI.EndGroup();
        curY = textAreaRect.yMax + Spacing;

        // Bottom bar (Save / Cancel)
        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);

        // Also save on Enter key (but allow Shift+Enter for newline)
        if (Event.current.isKey && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && !Event.current.shift)
        {
            Event.current.Use();
            SaveAndClose();
        }
    }

    private void DrawTextArea(Rect rect)
    {
        // Draw background
        Widgets.DrawMenuSection(rect);

        Rect innerRect = new(
            rect.x + TextAreaPadding,
            rect.y + TextAreaPadding,
            rect.width - TextAreaPadding * 2,
            rect.height - TextAreaPadding * 2
        );

        GUI.SetNextControlName(textFieldControlName);
        string newText = Widgets.TextArea(innerRect, workingValue);
        if (newText != workingValue)
        {
            workingValue = newText;
        }

        // Focus the text field when window opens
        if (Event.current.type == EventType.Layout)
        {
            GUI.FocusControl(textFieldControlName);
        }
    }

    protected override string SaveButtonLabel => "MST.Save".Translate().RawText;
    protected override string CancelButtonLabel => "MST.Cancel".Translate().RawText;

    protected override string? GetCopyData() => workingValue ?? "";

    protected override bool OnPasteData(string data)
    {
        workingValue = data;
        return true;
    }

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    private void SaveAndClose()
    {
        if (onSaveCallback != null)
        {
            onSaveCallback(string.IsNullOrEmpty(workingValue) ? null : workingValue);
        }
        else
        {
            // 严格判据：文本没被编辑过就不留下「已修改」痕迹（打开即保存的 null 字段尤甚）。
            var current = (string?)config!.GetRawValue(data!);
            bool unchanged = string.IsNullOrEmpty(workingValue)
                ? string.IsNullOrEmpty(current)
                : workingValue == current;
            if (!unchanged) config!.ApplyValue(data!, workingValue);
        }
        Close();
    }
}