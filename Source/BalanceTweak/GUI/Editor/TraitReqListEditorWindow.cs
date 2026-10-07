using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class TraitReqListEditorWindow : ListEditorWindow<TraitRequirement>
{

    public TraitReqListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<TraitRequirement>?)config.GetRawValue(data) ?? new List<TraitRequirement>();
        workingList = originalList.Select(t => new TraitRequirement { def = t.def, degree = t.degree }).ToList();
    }

    public TraitReqListEditorWindow(string title, List<TraitRequirement> initialList, Action<List<TraitRequirement>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(t => new TraitRequirement { def = t.def, degree = t.degree }).ToList() ?? new List<TraitRequirement>();
        workingList = originalList.Select(t => new TraitRequirement { def = t.def, degree = t.degree }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddTraitReq".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new TraitRequirement());
    protected override bool IsItemValid(TraitRequirement item) => item.def != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (36f + DeleteButtonWidth + Spacing * 2);
        float traitButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

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

        Rect valueLabelRect = new(curX, rect.y, 36f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "Deg");
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 36f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.degree.HasValue ? entry.degree.Value.ToString() : "";
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (newText.NullOrEmpty())
        {
            entry.degree = null;
        }
        else if (int.TryParse(newText, out int newVal))
        {
            entry.degree = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeTraitRequirementList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeTraitRequirementList(data);
        if (list != null) workingList = list;
    }
}
