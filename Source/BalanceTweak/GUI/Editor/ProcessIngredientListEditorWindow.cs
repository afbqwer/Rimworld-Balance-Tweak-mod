using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class ProcessIngredientListEditorWindow : ListEditorWindow<ProcessIngredientItem>
{
    public override Vector2 InitialSize => new(760f, 500f);
    public ProcessIngredientListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<ProcessIngredientItem>?)config.GetRawValue(data) ?? new List<ProcessIngredientItem>();
        workingList = originalList.Select(i => new ProcessIngredientItem
        {
            thing = i.thing,
            thingCategory = i.thingCategory,
            disallowedThingDefs = i.disallowedThingDefs?.ToList(),
            countNeeded = i.countNeeded,
        }).ToList();
    }

    public ProcessIngredientListEditorWindow(string title, List<ProcessIngredientItem> initialList, Action<List<ProcessIngredientItem>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(i => new ProcessIngredientItem
        {
            thing = i.thing,
            thingCategory = i.thingCategory,
            disallowedThingDefs = i.disallowedThingDefs?.ToList(),
            countNeeded = i.countNeeded,
        }).ToList() ?? new List<ProcessIngredientItem>();
        workingList = originalList.Select(i => new ProcessIngredientItem
        {
            thing = i.thing,
            thingCategory = i.thingCategory,
            disallowedThingDefs = i.disallowedThingDefs?.ToList(),
            countNeeded = i.countNeeded,
        }).ToList();
    }

    protected override bool HasAddButton => false;
    protected override string AddButtonLabel => "";
    protected override void OnAddItem() { }

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width;
        int columns = 4;
        float colWidth = availableWidth / columns;

        // ThingDef selector
        Rect thingRect = new(curX, rect.y, colWidth, rect.height);
        string thingLabel = entry.thing == null ? "MST.None".Translate().RawText : entry.thing.LabelCap.NullOrEmpty() ? entry.thing.defName : entry.thing.LabelCap;
        if (Widgets.ButtonText(thingRect, thingLabel))
        {
            var allDefs = DefDatabase<ThingDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle() + " - Thing",
                allDefs,
                entry.thing,
                (selectedDef) => { workingList[index].thing = (ThingDef?)selectedDef; }));
        }
        if (entry.thing != null && !entry.thing.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(thingRect, entry.thing.description);
        }
        curX += colWidth + Spacing;

        // ThingCategoryDef selector
        Rect catRect = new(curX, rect.y, colWidth, rect.height);
        string catLabel = entry.thingCategory == null ? "MST.None".Translate().RawText : entry.thingCategory.LabelCap.NullOrEmpty() ? entry.thingCategory.defName : entry.thingCategory.LabelCap;
        if (Widgets.ButtonText(catRect, catLabel))
        {
            var allCats = DefDatabase<ThingCategoryDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle() + " - Category",
                allCats,
                entry.thingCategory,
                (selectedDef) => { workingList[index].thingCategory = (ThingCategoryDef?)selectedDef; }));
        }
        if (entry.thingCategory != null && !entry.thingCategory.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(catRect, entry.thingCategory.description);
        }
        curX += colWidth + Spacing;

        // countNeeded
        Rect countRect = new(curX, rect.y, colWidth, rect.height);
        string countText = entry.countNeeded.ToString("F1");
        string newCount = Widgets.TextField(countRect, countText);
        if (float.TryParse(newCount, out float countVal) && countVal >= 0)
            entry.countNeeded = countVal;
        curX += colWidth + Spacing;

        // disallowedThingDefs
        Rect disallowedRect = new(curX, rect.y, colWidth, rect.height);
        string disallowedLabel = entry.disallowedThingDefs is { Count: > 0 }
            ? $"[{entry.disallowedThingDefs.Count}]"
            : "MST.None".Translate().RawText;
        if (Widgets.ButtonText(disallowedRect, disallowedLabel))
        {
            var current = entry.disallowedThingDefs?.Cast<Def>().ToList() ?? new List<Def>();
            OpenChildWindow(new DefListEditorWindow(
                GetBaseTitle() + " - " + "MST.IngredientDisallowed".Translate().RawText,
                current,
                typeof(ThingDef),
                (selectedDefs) =>
                {
                    var list = selectedDefs?.Cast<ThingDef>().ToList();
                    workingList[index].disallowedThingDefs = list is { Count: > 0 } ? list : null;
                }));
        }
    }

    protected override void SaveAndClose()
    {
        var resultList = workingList.Select(src => new ProcessIngredientItem
        {
            thing = src.thing,
            thingCategory = src.thingCategory,
            disallowedThingDefs = src.disallowedThingDefs?.ToList(),
            countNeeded = src.countNeeded,
        }).ToList();

        if (onSaveCallback != null)
        {
            onSaveCallback(resultList.Count > 0 ? resultList : null);
        }
        else
        {
            originalList.Clear();
            foreach (var item in resultList)
            {
                originalList.Add(item);
            }
            config!.ApplyValue(data!, originalList.Count > 0 ? originalList : null);
        }
        Close();
    }

    protected override string? SerializeList() => SerializationHelper.SerializeProcessIngredientItemList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeProcessIngredientItemList(data);
        if (list != null) workingList = list;
    }
}