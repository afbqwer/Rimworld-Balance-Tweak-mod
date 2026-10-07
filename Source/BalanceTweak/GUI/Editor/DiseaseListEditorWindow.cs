using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class DiseaseListEditorWindow : ListEditorWindow<BiomeDiseaseRecord>
{
    private List<Def>? diseaseIncidents;

    public DiseaseListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<BiomeDiseaseRecord>?)config.GetRawValue(data) ?? new List<BiomeDiseaseRecord>();
        workingList = originalList.Select(s => new BiomeDiseaseRecord { diseaseInc = s.diseaseInc, commonality = s.commonality }).ToList();
    }

    public DiseaseListEditorWindow(string title, List<BiomeDiseaseRecord> initialList, Action<List<BiomeDiseaseRecord>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new BiomeDiseaseRecord { diseaseInc = s.diseaseInc, commonality = s.commonality }).ToList() ?? new List<BiomeDiseaseRecord>();
        workingList = originalList.Select(s => new BiomeDiseaseRecord { diseaseInc = s.diseaseInc, commonality = s.commonality }).ToList();
    }

    private List<Def> DiseaseIncidents => diseaseIncidents ??= DefDatabase<IncidentDef>.AllDefsListForReading
        .Where(d => d.workerClass != null && d.workerClass.IsSubclassOf(typeof(IncidentWorker_Disease)))
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddDisease".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new BiomeDiseaseRecord { commonality = 100f });
    protected override bool IsItemValid(BiomeDiseaseRecord item) => item.diseaseInc != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float diseaseButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect diseaseButtonRect = new(curX, rect.y, diseaseButtonWidth, rect.height);
        string diseaseLabel = entry.diseaseInc == null ? "MST.SelectDisease".Translate().RawText : $"{entry.diseaseInc.LabelCap}({entry.diseaseInc.defName})";
        if (Widgets.ButtonText(diseaseButtonRect, diseaseLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                DiseaseIncidents,
                entry.diseaseInc,
                (selectedDef) =>
                {
                    workingList[index].diseaseInc = (IncidentDef?)selectedDef;
                }));
        }
        if (entry.diseaseInc != null && !entry.diseaseInc.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(diseaseButtonRect, entry.diseaseInc.description);
        }
        curX += diseaseButtonWidth + Spacing;

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

    protected override string? SerializeList() => SerializationHelper.SerializeDiseaseList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeDiseaseList(data);
        if (list != null) workingList = list;
    }
}
