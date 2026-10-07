using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class FlagsEditorWindow : EditorWindowBase
{
    public override Vector2 InitialSize => new(360f, 500f);
    private long workingValue;
    private Type enumType;
    private List<Enum> allFlags;
    private Action<object?>? onSaveCallback;

    private Vector2 scrollPosition;

    private const float EntryHeight = 30f;

    public FlagsEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        this.enumType = config.EnumType!;

        var rawValue = config.GetRawValue(data);
        workingValue = rawValue != null ? Convert.ToInt64(rawValue) : 0L;

        allFlags = Enum.GetValues(enumType).Cast<Enum>().ToList();
    }

    public FlagsEditorWindow(string title, Enum initialValue, Action<object?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        enumType = initialValue?.GetType() ?? throw new ArgumentNullException(nameof(initialValue));
        workingValue = Convert.ToInt64(initialValue);
        allFlags = Enum.GetValues(enumType).Cast<Enum>().ToList();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        Text.Font = GameFont.Medium;
        string titleText = GetBaseTitle();
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), titleText);
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect listRect = new(inRect.x, curY, inRect.width, inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing);
        DrawListArea(listRect);
        curY = listRect.yMax + Spacing;

        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    private void DrawListArea(Rect rect)
    {
        float totalHeight = allFlags.Count * (EntryHeight + Spacing);

        Rect viewRect = new(rect.x, rect.y, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float curY = viewRect.y;

        for (int i = 0; i < allFlags.Count; i++)
        {
            Rect entryRect = new(viewRect.x, curY, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
            curY += EntryHeight + Spacing;
        }

        Widgets.EndScrollView();
    }

    private void DrawEntry(Rect rect, int index)
    {
        var flag = allFlags[index];
        long flagValue = Convert.ToInt64(flag);
        string flagName = flag.ToString();

        bool isSet = flagValue == 0
            ? workingValue == 0
            : (workingValue & flagValue) == flagValue;

        bool isNone = flagValue == 0;

        Widgets.DrawHighlightIfMouseover(rect);
        Rect checkboxRect = new(rect.x + 10f, rect.y + (rect.height - 24f) / 2f, 24f, 24f);
        bool newIsSet = isSet;
        Widgets.Checkbox(checkboxRect.position, ref newIsSet, 24f, false, true);

        if (newIsSet != isSet)
        {
            if (isNone)
            {
                workingValue = newIsSet ? 0L : workingValue;
            }
            else
            {
                if (newIsSet)
                {
                    workingValue |= flagValue;
                }
                else
                {
                    workingValue &= ~flagValue;
                }
            }
        }

        float labelX = checkboxRect.xMax + 10f;
        Rect labelRect = new(labelX, rect.y, rect.width - labelX, rect.height);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, flagName);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

    protected override string? GetCopyData() => workingValue.ToString();

    protected override bool OnPasteData(string data)
    {
        if (long.TryParse(data, out long val))
        {
            workingValue = val;
            return true;
        }
        return false;
    }

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    private void SaveAndClose()
    {
        var result = Enum.ToObject(enumType, workingValue);
        if (onSaveCallback != null)
        {
            onSaveCallback(result);
        }
        else
        {
            config!.ApplyValue(data!, result);
        }
        Close();
    }
}
