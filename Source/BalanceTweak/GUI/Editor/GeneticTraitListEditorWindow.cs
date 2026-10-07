using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class GeneticTraitListEditorWindow : ListEditorWindow<GeneticTraitData>
{

    public GeneticTraitListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<GeneticTraitData>?)config.GetRawValue(data) ?? new List<GeneticTraitData>();
        workingList = originalList.Select(gtd => new GeneticTraitData { def = gtd.def, degree = gtd.degree }).ToList();
    }

    public GeneticTraitListEditorWindow(string title, List<GeneticTraitData> initialList, Action<List<GeneticTraitData>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(gtd => new GeneticTraitData { def = gtd.def, degree = gtd.degree }).ToList() ?? new List<GeneticTraitData>();
        workingList = originalList.Select(gtd => new GeneticTraitData { def = gtd.def, degree = gtd.degree }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddTrait".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new GeneticTraitData());
    protected override bool IsItemValid(GeneticTraitData item) => item.def != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 3);
        float traitButtonWidth = availableWidth * 0.6f;
        float degreeFieldWidth = availableWidth * 0.4f;

        Rect traitButtonRect = new(curX, rect.y, traitButtonWidth, rect.height);
        string traitLabel = entry.def == null ? "MST.SelectTrait".Translate().RawText : $"{entry.def.LabelCap}({entry.def.defName})";
        if (Widgets.ButtonText(traitButtonRect, traitLabel))
        {
            var allTraits = DefDatabase<TraitDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allTraits,
                entry.def,
                (selectedDef) =>
                {
                    workingList[index].def = (TraitDef?)selectedDef;
                }));
        }
        if (entry.def != null && !entry.def.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(traitButtonRect, entry.def.description);
        }
        curX += traitButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, 44f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Degree".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;

        Rect degreeFieldRect = new(curX, rect.y, degreeFieldWidth, rect.height);
        string degreeLabel = GetDegreeLabel(entry);
        if (entry.def != null)
        {
            if (Widgets.ButtonText(degreeFieldRect, degreeLabel))
            {
                var options = new List<FloatMenuOption>();
                foreach (var dd in entry.def.degreeDatas)
                {
                    var degreeData = dd;
                    options.Add(new FloatMenuOption(degreeData.LabelCap, () =>
                    {
                        workingList[index].degree = degreeData.degree;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        else
        {
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(degreeFieldRect, degreeLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        curX += degreeFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeGeneticTraitDataList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeGeneticTraitDataList(data);
        if (list != null) workingList = list;
    }

    private static string GetDegreeLabel(GeneticTraitData entry)
    {
        if (entry.def == null) return "-";
        var dd = entry.def.degreeDatas.FirstOrDefault(d => d.degree == entry.degree);
        return dd != null ? (dd.LabelCap.NullOrEmpty() ? $"Degree {entry.degree}" : dd.LabelCap) : $"Degree {entry.degree}";
    }
}