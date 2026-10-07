using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class HediffGiverListEditorWindow : ListEditorWindow<HediffGiver>
{
    protected override bool HasAddButton => false;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

    public HediffGiverListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        this.originalList = ((List<HediffGiver>?)config.GetRawValue(data))?.ToList() ?? new List<HediffGiver>();
        this.workingList = originalList.ToList();
    }

    public HediffGiverListEditorWindow(string title, List<HediffGiver> initialList, Action<List<HediffGiver>?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        originalList = initialList?.ToList() ?? new List<HediffGiver>();
        workingList = originalList.ToList();
    }

    protected override string AddButtonLabel => "";
    protected override void OnAddItem() { }

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing);

        Rect hediffLabelRect = new(curX, rect.y, availableWidth, rect.height);
        string hediffLabel = entry.hediff == null ? "MST.None".Translate().RawText : $"{entry.hediff.LabelCap} ({entry.hediff.defName})";
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(hediffLabelRect, hediffLabel);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += availableWidth + Spacing;

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