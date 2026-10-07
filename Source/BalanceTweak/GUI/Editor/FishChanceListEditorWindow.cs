using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class FishChanceListEditorWindow : ListEditorWindow<FishChance>
{
    private List<Def>? fishDefs;

    public FishChanceListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<FishChance>?)config.GetRawValue(data) ?? new List<FishChance>();
        workingList = originalList.Select(s => new FishChance { fishDef = s.fishDef, chance = s.chance }).ToList();
    }

    public FishChanceListEditorWindow(string title, List<FishChance> initialList, Action<List<FishChance>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new FishChance { fishDef = s.fishDef, chance = s.chance }).ToList() ?? new List<FishChance>();
        workingList = originalList.Select(s => new FishChance { fishDef = s.fishDef, chance = s.chance }).ToList();
    }

    private List<Def> FishDefs => fishDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Where(t => t.race != null && t.race.canFishForFood)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddFish".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new FishChance { chance = 1f });
    protected override bool IsItemValid(FishChance item) => item.fishDef != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float fishButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect fishButtonRect = new(curX, rect.y, fishButtonWidth, rect.height);
        string fishLabel = entry.fishDef == null ? "MST.SelectFish".Translate().RawText : $"{entry.fishDef.LabelCap}({entry.fishDef.defName})";
        if (Widgets.ButtonText(fishButtonRect, fishLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                FishDefs,
                entry.fishDef,
                (selectedDef) =>
                {
                    workingList[index].fishDef = (ThingDef?)selectedDef;
                }));
        }
        if (entry.fishDef != null && !entry.fishDef.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(fishButtonRect, entry.fishDef.description);
        }
        curX += fishButtonWidth + Spacing;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.chance.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal))
        {
            entry.chance = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeFishChanceList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeFishChanceList(data);
        if (list != null) workingList = list;
    }
}
