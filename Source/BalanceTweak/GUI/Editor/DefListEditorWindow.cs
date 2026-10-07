using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class DefListEditorWindow : EditorWindowBase
{
    private IList workingList;
    private IList originalList;
    private IList? initialList;
    private Type defType = null!;
    private Action<IList?>? onSaveCallback;

    private Vector2 scrollPosition;

    private const float EntryHeight = 30f;
    private const float DeleteButtonWidth = 30f;
    private const float ButtonHeight = 30f;

    public DefListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;

        if (config.field != null)
        {
            defType = config.field.FieldType.GetGenericArguments()[0];
        }

        originalList = (IList?)config.GetRawValue(data) ?? (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(defType));
        workingList = CloneList(originalList);
    }

    public DefListEditorWindow(string title, IList initialList, Type defType, Action<IList?> onSave)
    {
        pickerTitle = title;
        this.initialList = initialList;
        onSaveCallback = onSave;
        this.defType = defType;
        workingList = CloneList(initialList);
        originalList = CloneList(initialList);
    }

    private IList CloneList(IList source)
    {
        var listType = typeof(List<>).MakeGenericType(defType);
        var clone = (IList)Activator.CreateInstance(listType);
        foreach (var item in source)
        {
            clone.Add(item);
        }
        return clone;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        string titleText = GetBaseTitle();
        Text.Font = GameFont.Medium;
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
        float totalHeight = workingList.Count * (EntryHeight + Spacing) + ButtonHeight + Spacing;

        Rect viewRect = new(rect.x, rect.y, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float curY = viewRect.y;

        for (int i = 0; i < workingList.Count; i++)
        {
            Rect entryRect = new(viewRect.x, curY, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
            curY += EntryHeight + Spacing;
        }

        Rect addButtonRect = new(viewRect.x, curY, viewRect.width, ButtonHeight);
        if (Widgets.ButtonText(addButtonRect, "MST.AddDef".Translate().RawText))
        {
            var allDefs = GenDefDatabase.GetAllDefsInDatabaseForDef(defType).ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allDefs,
                null,
                (selectedDef) =>
                {
                    if (selectedDef != null)
                        workingList.Add(selectedDef);
                }));
        }
        curY += ButtonHeight + Spacing;

        Widgets.EndScrollView();
    }

    private void DrawEntry(Rect rect, int index)
    {
        var entry = (Def?)workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing);

        Rect defButtonRect = new(curX, rect.y, availableWidth, rect.height);
        string defLabel = entry == null ? "MST.SelectDef".Translate().RawText : $"{GetDefDisplayLabel(entry)}({entry.defName})";
        if (Widgets.ButtonText(defButtonRect, defLabel))
        {
            var allDefs = GenDefDatabase.GetAllDefsInDatabaseForDef(defType).ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allDefs,
                entry,
                (selectedDef) =>
                {
                    workingList[index] = selectedDef;
                }));
        }
        if (entry != null && !entry.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(defButtonRect, entry.description);
        }
        curX += availableWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

    protected override string? GetCopyData()
    {
        var defNames = new List<string>();
        foreach (var item in workingList)
            if (item is Def def)
                defNames.Add(def.defName);
        return defNames.Count > 0 ? string.Join("\n", defNames) : null;
    }

    protected override bool OnPasteData(string data)
    {
        var defNames = data.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(defType));
        foreach (var defName in defNames)
        {
            var def = GenDefDatabase.GetDefSilentFail(defType, defName.Trim());
            if (def != null)
                newList.Add(def);
        }
        if (newList.Count > 0)
        {
            workingList = newList;
            return true;
        }
        return false;
    }

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    private static string GetDefDisplayLabel(Def def)
    {
        if (def is TraitDef traitDef && def.LabelCap.NullOrEmpty())
        {
            var firstDegree = traitDef.degreeDatas.FirstOrDefault();
            if (firstDegree != null)
                return firstDegree.LabelCap;
        }
        return def.LabelCap.Resolve();
    }

    private void SaveAndClose()
    {
        var listType = typeof(List<>).MakeGenericType(defType);
        var finalList = (IList)Activator.CreateInstance(listType);
        foreach (var item in workingList)
        {
            if (item != null)
                finalList.Add(item);
        }
        if (onSaveCallback != null)
        {
            onSaveCallback(finalList);
        }
        else
        {
            var result = finalList.Count > 0 ? finalList : null;
            config!.ApplyValue(data!, result);
        }
        Close();
    }
}