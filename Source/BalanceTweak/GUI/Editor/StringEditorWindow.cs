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
            config!.ApplyValue(data!, workingValue);
        }
        Close();
    }
}