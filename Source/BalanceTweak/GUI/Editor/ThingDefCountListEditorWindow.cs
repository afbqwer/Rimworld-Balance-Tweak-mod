using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class ThingDefCountListEditorWindow : ListEditorWindow<ThingDefCountClass>
{
    public override Vector2 InitialSize => new(660f, 500f);
    public ThingDefCountListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<ThingDefCountClass>?)config.GetRawValue(data) ?? new List<ThingDefCountClass>();
        workingList = originalList.Select(tdc => new ThingDefCountClass
        {
            thingDef = tdc.thingDef,
            stuff = tdc.stuff,
            count = tdc.count
        }).ToList();
    }

    public ThingDefCountListEditorWindow(string title, List<ThingDefCountClass> initialList, Action<List<ThingDefCountClass>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(tdc => new ThingDefCountClass
        {
            thingDef = tdc.thingDef,
            stuff = tdc.stuff,
            count = tdc.count
        }).ToList() ?? new List<ThingDefCountClass>();
        workingList = originalList.Select(tdc => new ThingDefCountClass
        {
            thingDef = tdc.thingDef,
            stuff = tdc.stuff,
            count = tdc.count
        }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddThingDefCount".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new ThingDefCountClass());
    protected override bool IsItemValid(ThingDefCountClass item) => item.thingDef != null;

    protected override void DrawListArea(Rect rect)
    {
        const float HeaderHeight = 24f;

        // Draw header row (fixed, not scrolling)
        Rect headerRect = new(rect.x, rect.y, rect.width, HeaderHeight);
        DrawHeader(headerRect);

        // Scrollable list starts below header
        float listY = rect.y + HeaderHeight + Spacing;
        float listHeight = rect.height - HeaderHeight - Spacing;
        Rect listRect = new(rect.x, listY, rect.width, listHeight);

        float addButtonHeight = HasAddButton ? ButtonHeight + Spacing : 0f;
        float totalHeight = workingList.Count * (EntryHeight + Spacing) + addButtonHeight;

        Rect viewRect = new(listRect.x, listRect.y, listRect.width, totalHeight);
        Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);

        float curY = viewRect.y;
        for (int i = 0; i < workingList.Count; i++)
        {
            Rect entryRect = new(viewRect.x, curY, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
            curY += EntryHeight + Spacing;
        }

        if (HasAddButton)
        {
            Rect addButtonRect = new(viewRect.x, curY, viewRect.width, ButtonHeight);
            if (Widgets.ButtonText(addButtonRect, AddButtonLabel))
            {
                OnAddItem();
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawHeader(Rect rect)
    {
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 3);
        float thingDefWidth = availableWidth * 0.4f;
        float stuffWidth = availableWidth * 0.4f;
        float countWidth = availableWidth * 0.2f;

        // Background
        Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.18f, 0.18f));

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Tiny;

        Widgets.Label(new Rect(curX, rect.y, thingDefWidth, rect.height), "MST.ThingDef".Translate().RawText);
        curX += thingDefWidth + Spacing;

        Widgets.Label(new Rect(curX, rect.y, stuffWidth, rect.height), "MST.Stuff".Translate().RawText);
        curX += stuffWidth + Spacing;

        Widgets.Label(new Rect(curX, rect.y, countWidth, rect.height), "MST.Count".Translate().RawText);

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 3);
        float thingDefWidth = availableWidth * 0.4f;
        float stuffWidth = availableWidth * 0.4f;
        float countWidth = availableWidth * 0.2f;

        Rect thingDefButtonRect = new(curX, rect.y, thingDefWidth, rect.height);
        string thingDefLabel = entry.thingDef == null ? "MST.SelectThingDef".Translate().RawText : $"{entry.thingDef.LabelCap}({entry.thingDef.defName})";
        if (Widgets.ButtonText(thingDefButtonRect, thingDefLabel))
        {
            var allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allThingDefs,
                entry.thingDef,
                (selectedDef) =>
                {
                    workingList[index].thingDef = (ThingDef?)selectedDef;
                }));
        }
        if (entry.thingDef != null && !entry.thingDef.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(thingDefButtonRect, entry.thingDef.description);
        }
        curX += thingDefWidth + Spacing;

        Rect stuffButtonRect = new(curX, rect.y, stuffWidth, rect.height);
        string stuffLabel = entry.stuff == null ? "MST.None".Translate().RawText : $"{entry.stuff.LabelCap}({entry.stuff.defName})";
        if (Widgets.ButtonText(stuffButtonRect, stuffLabel))
        {
            var allStuff = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(td => td.IsStuff)
                .Cast<Def>()
                .ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allStuff,
                entry.stuff,
                (selectedDef) =>
                {
                    workingList[index].stuff = (ThingDef?)selectedDef;
                }));
        }
        if (entry.stuff != null && !entry.stuff.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(stuffButtonRect, entry.stuff.description);
        }
        curX += stuffWidth + Spacing;

        Rect countFieldRect = new(curX, rect.y, countWidth, rect.height);
        string countText = entry.count.ToString();
        string newText = Widgets.TextField(countFieldRect, countText);
        if (int.TryParse(newText, out int newVal) && newVal >= 0)
        {
            entry.count = newVal;
        }
        curX += countWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeThingDefCountClassList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeThingDefCountClassList(data);
        if (list != null) workingList = list;
    }
}