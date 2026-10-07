using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class MentalStateGiverListEditorWindow : ListEditorWindow<MentalStateGiver>
{
    protected override bool HasAddButton => false;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

    public MentalStateGiverListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        this.originalList = ((List<MentalStateGiver>?)config.GetRawValue(data))?.Select(m => new MentalStateGiver
        {
            mentalState = m.mentalState,
            mtbDays = m.mtbDays
        }).ToList() ?? new List<MentalStateGiver>();
        this.workingList = originalList.Select(m => new MentalStateGiver
        {
            mentalState = m.mentalState,
            mtbDays = m.mtbDays
        }).ToList();
    }

    public MentalStateGiverListEditorWindow(string title, List<MentalStateGiver> initialList, Action<List<MentalStateGiver>?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        originalList = initialList?.Select(m => new MentalStateGiver
        {
            mentalState = m.mentalState,
            mtbDays = m.mtbDays
        }).ToList() ?? new List<MentalStateGiver>();
        workingList = originalList.Select(m => new MentalStateGiver
        {
            mentalState = m.mentalState,
            mtbDays = m.mtbDays
        }).ToList();
    }

    protected override string AddButtonLabel => "";
    protected override void OnAddItem() { }

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float mtbLabelWidth = 80f;
        float mtbFieldWidth = 80f;
        float availableWidth = rect.width - (mtbLabelWidth + mtbFieldWidth + DeleteButtonWidth + Spacing * 3);

        Rect mentalStateRect = new(curX, rect.y, availableWidth, rect.height);
        string msLabel = entry.mentalState == null ? "MST.None".Translate().RawText : $"{entry.mentalState.LabelCap} ({entry.mentalState.defName})";
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(mentalStateRect, msLabel);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += availableWidth + Spacing;

        Rect mtbLabelRect = new(curX, rect.y, mtbLabelWidth, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(mtbLabelRect, "MST.MtbDays".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += mtbLabelWidth + Spacing;

        Rect mtbFieldRect = new(curX, rect.y, mtbFieldWidth, rect.height);
        string mtbText = entry.mtbDays.ToString("F2");
        string newText = Widgets.TextField(mtbFieldRect, mtbText);
        if (float.TryParse(newText, out float newMtb))
        {
            entry.mtbDays = newMtb;
        }
        curX += mtbFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList()
    {
        return null;
    }

    protected override void DeserializeList(string data)
    {
        return;
    }

    protected override void SaveAndClose()
    {
        var result = workingList.Count > 0 ? workingList : null;
        if (onSaveCallback != null)
        {
            onSaveCallback(result);
        }
        else
        {
            config!.ApplyValue(data!, result);
        }
        Close();
    }
}