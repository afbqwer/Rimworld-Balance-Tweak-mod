using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public abstract class EditorWindowBase : Window
{
    protected StatColumnConfig? config;
    protected TweakData? data;
    protected string? pickerTitle;
    public override Vector2 InitialSize => new(550f, 650f);
    protected const float TitleHeight = 30f;
    protected const float Spacing = 4f;
    private const float CopyPasteButtonWidth = 60f;

    public List<Window> childWindows = new();

    public EditorWindowBase()
    {
        forcePause = false;
        draggable = true;
        resizeable = true;
        closeOnClickedOutside = true;
    }

    protected void OpenChildWindow(Window window)
    {
        childWindows.Add(window);
        Find.WindowStack.Add(window);
    }

    public override void Notify_ClickOutsideWindow()
    {
        childWindows.RemoveAll(w => !Find.WindowStack.IsOpen(w));
        if (childWindows.Count > 0)
            return;
        childWindows.Clear();
        Close();
    }

    protected virtual string GetBaseTitle()
    {
        if (pickerTitle != null) return pickerTitle;
        if (config != null && data != null)
            return $"{config.label.Translate().RawText} - {data.label}";
        return "";
    }

    // ===== Bottom Bar (Copy / Paste / Save / Cancel) =====

    protected virtual float BottomBarHeight => 35f;
    protected virtual string SaveButtonLabel => "MST.SaveStatMods".Translate().RawText;
    protected virtual string CancelButtonLabel => "MST.CancelStatMods".Translate().RawText;

    /// <summary>Returns the string to copy to clipboard. null = copy disabled.</summary>
    protected virtual string? GetCopyData() => null;

    /// <summary>Handles pasting clipboard content. Returns true if paste was successful.</summary>
    protected virtual bool OnPasteData(string data) { return false; }

    protected virtual bool ShowCopyButton => false;
    protected virtual bool ShowPasteButton => false;
    protected virtual bool ShowSaveButton => true;
    protected virtual bool ShowCancelButton => true;

    /// <summary>Called when the Save button is clicked.</summary>
    protected virtual void OnSave() => Close();

    /// <summary>Called when the Cancel button is clicked.</summary>
    protected virtual void OnCancel() => Close();

    /// <summary>Hook for subclasses to draw extra content between Paste and Save buttons.</summary>
    protected virtual void DrawMiddleContent(ref float curX, Rect rect) { }

    /// <summary>Draws the standard bottom bar with Copy, Paste, Save, Cancel buttons.</summary>
    protected virtual void DrawBottomBar(Rect rect)
    {
        float curX = rect.x;

        // Copy button
        if (ShowCopyButton)
            DrawButton(ref curX, rect, CopyPasteButtonWidth, "MST.Copy".Translate().RawText, () =>
            {
                var data = GetCopyData();
                if (data != null)
                {
                    GUIUtility.systemCopyBuffer = data;
                    Messages.Message("MST.CopySuccess".Translate().RawText, MessageTypeDefOf.NeutralEvent, false);
                }
                else
                    Messages.Message("MST.CopyFailed".Translate().RawText, MessageTypeDefOf.NegativeEvent, false);
            });

        // Paste button
        if (ShowPasteButton)
            DrawButton(ref curX, rect, CopyPasteButtonWidth, "MST.Paste".Translate().RawText, () =>
            {
                var clipboardData = GUIUtility.systemCopyBuffer;
                if (!clipboardData.NullOrEmpty())
                {
                    if (OnPasteData(clipboardData))
                        Messages.Message("MST.PasteSuccess".Translate().RawText, MessageTypeDefOf.NeutralEvent, false);
                    else
                        Messages.Message("MST.PasteFailed".Translate().RawText, MessageTypeDefOf.NegativeEvent, false);
                }
            });

        // Middle content hook
        DrawMiddleContent(ref curX, rect);

        // Save / Cancel share remaining width equally
        float remainingWidth = rect.xMax - curX;
        int rightButtonCount = (ShowSaveButton ? 1 : 0) + (ShowCancelButton ? 1 : 0);

        if (rightButtonCount > 0)
        {
            float btnWidth = (remainingWidth - (rightButtonCount - 1) * Spacing) / rightButtonCount;

            if (ShowSaveButton)
                DrawButton(ref curX, rect, btnWidth, SaveButtonLabel, OnSave);
            if (ShowCancelButton)
                DrawButton(ref curX, rect, btnWidth, CancelButtonLabel, OnCancel);
        }
    }

    private static void DrawButton(ref float curX, Rect rect, float width, string label, Action onClick)
    {
        Rect btnRect = new(curX, rect.y, width, rect.height);
        if (Widgets.ButtonText(btnRect, label))
            onClick();
        curX += width + Spacing;
    }
}