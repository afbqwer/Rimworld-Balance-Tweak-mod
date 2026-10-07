using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class DefSelectionWindow : EditorWindowBase
{
    private Def? selectedDef;
    private Def? originalDef;
    private List<Def> allDefs;

    private Action<Def?>? onSelectedCallback;

    private Vector2 scrollPosition;

    private const float SearchBarHeight = 30f;
    private const float EntryHeight = 28f;
    private const float CategoryHeaderHeight = 24f;

    private struct ListItem
    {
        public enum ItemType { Header, Entry }
        public ItemType type;
        public string label;
        public float yOffset;
        public float height;
        public Def? def;
    }

    private QuickSearchWidget quickSearchWidget = new();
    private List<ListItem> flattenedItems = new();
    private bool needRebuildList = true;

    public DefSelectionWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        this.allDefs = config.GetAllDefsForSelector();
        this.originalDef = (Def?)config.GetRawValue(data);
        this.selectedDef = originalDef;
        this.doCloseX = true;
    }

    public DefSelectionWindow(string title, List<Def> allDefs, Def? initiallySelected, Action<Def?> onSelected)
    {
        this.pickerTitle = title;
        this.allDefs = allDefs;
        this.selectedDef = initiallySelected;
        this.originalDef = initiallySelected;
        this.onSelectedCallback = onSelected;
        this.doCloseX = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect searchRect = new(inRect.x, curY, inRect.width, SearchBarHeight);
        DrawSearchBar(searchRect);
        curY += SearchBarHeight + Spacing;

        Rect listRect = new(inRect.x, curY, inRect.width, inRect.height - TitleHeight - Spacing - SearchBarHeight - Spacing - BottomBarHeight - Spacing);
        DrawListArea(listRect);
        curY = listRect.yMax + Spacing;

        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    private void DrawSearchBar(Rect rect)
    {
        quickSearchWidget.OnGUI(rect, Notify_SearchChanged);
    }

    private void Notify_SearchChanged()
    {
        scrollPosition.y = 0;
        needRebuildList = true;
    }

    private string GetGroupKey(Def def)
    {
        if (def is StatDef statDef && statDef.category != null)
        {
            return statDef.category.LabelCap.Resolve();
        }
        return def.modContentPack?.Name ?? "MST.Misc".Translate().RawText;
    }

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

    private void RebuildFlattenedItems()
    {
        flattenedItems.Clear();

        var filtered = allDefs.Where(s =>
            quickSearchWidget.filter.Matches(s.defName) ||
            quickSearchWidget.filter.Matches(GetDefDisplayLabel(s))).ToList();

        var grouped = filtered.GroupBy(s => GetGroupKey(s))
            .OrderBy(g => g.Key)
            .ToList();

        quickSearchWidget.noResultsMatched = filtered.Count == 0;

        float y = 0;
        foreach (var group in grouped)
        {
            flattenedItems.Add(new ListItem
            {
                type = ListItem.ItemType.Header,
                label = group.Key,
                yOffset = y,
                height = CategoryHeaderHeight,
                def = null
            });
            y += CategoryHeaderHeight;

            foreach (var def in group)
            {
                flattenedItems.Add(new ListItem
                {
                    type = ListItem.ItemType.Entry,
                    label = $"{GetDefDisplayLabel(def)} ({def.defName})",
                    yOffset = y,
                    height = EntryHeight,
                    def = def
                });
                y += EntryHeight + Spacing;
            }
        }

        needRebuildList = false;
    }

    private void DrawListArea(Rect rect)
    {
        if (needRebuildList)
        {
            RebuildFlattenedItems();
        }

        if (flattenedItems.Count == 0) return;

        float totalHeight = flattenedItems[^1].yOffset + flattenedItems[^1].height;
        float viewWidth = rect.width - 16f;
        Rect viewRect = new(0, 0, viewWidth, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        int startIndex = FindFirstVisibleItem(scrollPosition.y);
        int endIndex = FindLastVisibleItem(scrollPosition.y + rect.height);

        for (int i = startIndex; i <= endIndex; i++)
        {
            DrawListItem(i, viewWidth);
        }

        Widgets.EndScrollView();
    }

    private int FindFirstVisibleItem(float scrollY)
    {
        for (int i = 0; i < flattenedItems.Count; i++)
        {
            if (flattenedItems[i].yOffset + flattenedItems[i].height > scrollY)
                return i;
        }
        return 0;
    }

    private int FindLastVisibleItem(float viewBottom)
    {
        for (int i = flattenedItems.Count - 1; i >= 0; i--)
        {
            if (flattenedItems[i].yOffset < viewBottom)
                return i;
        }
        return flattenedItems.Count - 1;
    }

    private void DrawListItem(int index, float viewWidth)
    {
        var item = flattenedItems[index];

        if (item.type == ListItem.ItemType.Header)
        {
            Rect headerRect = new(0, item.yOffset, viewWidth, CategoryHeaderHeight);
            Widgets.Label(headerRect, item.label);
        }
        else
        {
            Rect entryRect = new(10f, item.yOffset, viewWidth - 10f, EntryHeight);

            bool isSelected = item.def == selectedDef;
            if (isSelected)
            {
                GUI.color = new Color(0.3f, 0.5f, 0.8f, 0.3f);
                Widgets.DrawBox(entryRect);
                GUI.color = Color.white;
            }

            if (Widgets.ButtonText(entryRect, item.label))
            {
                selectedDef = item.def;
                SaveAndClose();
            }

            if (item.def != null && !item.def.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(entryRect, item.def.description);
            }
        }
    }

    protected override string? GetCopyData() => selectedDef?.defName;

    protected override bool OnPasteData(string data)
    {
        var matchedDef = allDefs.FirstOrDefault(d => d.defName == data);
        if (matchedDef != null)
        {
            selectedDef = matchedDef;
            SaveAndClose();
            return true;
        }
        return false;
    }

    protected override void DrawMiddleContent(ref float curX, Rect rect)
    {
        string currentLabel = selectedDef != null
            ? $"{GetDefDisplayLabel(selectedDef)} ({selectedDef.defName})"
            : "MST.None".Translate().RawText;
        string displayText = "MST.CurrentDef".Translate().RawText + " " + currentLabel;

        float clearBtnWidth = 80f;
        float labelWidth = rect.xMax - curX - clearBtnWidth - Spacing;

        Rect labelRect = new(curX, rect.y, labelWidth, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, displayText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += labelWidth + Spacing;

        Rect clearBtnRect = new(curX, rect.y, clearBtnWidth, rect.height);
        if (Widgets.ButtonText(clearBtnRect, "MST.Clear".Translate().RawText))
        {
            selectedDef = null;
            SaveAndClose();
        }
        curX += clearBtnWidth;
    }

    private void SaveAndClose()
    {
        if (onSelectedCallback != null)
        {
            onSelectedCallback(selectedDef);
        }
        else
        {
            config!.ApplyValue(data!, selectedDef);
        }
        Close();
    }
}