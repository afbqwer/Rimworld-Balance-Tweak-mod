using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class ProcessResultListEditorWindow : ListEditorWindow<ProcessResultItem>
{

    public ProcessResultListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<ProcessResultItem>?)config.GetRawValue(data) ?? new List<ProcessResultItem>();
        workingList = originalList.Select(r => new ProcessResultItem
        {
            thing = r.thing,
            count = r.count,
        }).ToList();
    }

    public ProcessResultListEditorWindow(string title, List<ProcessResultItem> initialList, Action<List<ProcessResultItem>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(r => new ProcessResultItem
        {
            thing = r.thing,
            count = r.count,
        }).ToList() ?? new List<ProcessResultItem>();
        workingList = originalList.Select(r => new ProcessResultItem
        {
            thing = r.thing,
            count = r.count,
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
        float thingDefWidth = availableWidth * 0.6f;
        float countWidth = availableWidth * 0.4f;

        // ThingDef selector
        Rect thingRect = new(curX, rect.y, thingDefWidth, rect.height);
        string thingLabel = entry.thing == null ? "MST.SelectThingDef".Translate().RawText : $"{entry.thing.LabelCap}({entry.thing.defName})";
        if (Widgets.ButtonText(thingRect, thingLabel))
        {
            var allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle() + " - Thing",
                allThingDefs,
                entry.thing,
                (selectedDef) => { workingList[index].thing = (ThingDef?)selectedDef; }));
        }
        if (entry.thing != null && !entry.thing.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(thingRect, entry.thing.description);
        }
        curX += thingDefWidth + Spacing;

        // count
        Rect countRect = new(curX, rect.y, countWidth, rect.height);
        string countText = entry.count.ToString();
        string newCount = Widgets.TextField(countRect, countText);
        if (int.TryParse(newCount, out int countVal) && countVal >= 0)
            entry.count = countVal;
    }

    protected override void SaveAndClose()
    {
        var resultList = workingList.Select(src => new ProcessResultItem
        {
            thing = src.thing,
            count = src.count,
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

    protected override string? SerializeList() => SerializationHelper.SerializeProcessResultItemList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeProcessResultItemList(data);
        if (list != null) workingList = list;
    }
}