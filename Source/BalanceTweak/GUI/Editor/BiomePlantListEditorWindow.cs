using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class BiomePlantListEditorWindow : ListEditorWindow<BiomePlantRecord>
{
    private List<Def>? plantDefs;

    public BiomePlantListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<BiomePlantRecord>?)config.GetRawValue(data) ?? new List<BiomePlantRecord>();
        workingList = originalList.Select(s => new BiomePlantRecord { plant = s.plant, commonality = s.commonality }).ToList();
    }

    public BiomePlantListEditorWindow(string title, List<BiomePlantRecord> initialList, Action<List<BiomePlantRecord>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new BiomePlantRecord { plant = s.plant, commonality = s.commonality }).ToList() ?? new List<BiomePlantRecord>();
        workingList = originalList.Select(s => new BiomePlantRecord { plant = s.plant, commonality = s.commonality }).ToList();
    }

    private List<Def> PlantDefs => plantDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Where(t => t.plant != null)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddWildPlant".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new BiomePlantRecord { commonality = 1f });
    protected override bool IsItemValid(BiomePlantRecord item) => item.plant != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float plantButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect plantButtonRect = new(curX, rect.y, plantButtonWidth, rect.height);
        string plantLabel = entry.plant == null ? "MST.SelectPlant".Translate().RawText : $"{entry.plant.LabelCap}({entry.plant.defName})";
        if (Widgets.ButtonText(plantButtonRect, plantLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                PlantDefs,
                entry.plant,
                (selectedDef) =>
                {
                    workingList[index].plant = (ThingDef?)selectedDef;
                }));
        }
        if (entry.plant != null && !entry.plant.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(plantButtonRect, entry.plant.description);
        }
        curX += plantButtonWidth + Spacing;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.commonality.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal))
        {
            entry.commonality = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeBiomePlantList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeBiomePlantList(data);
        if (list != null) workingList = list;
    }
}
