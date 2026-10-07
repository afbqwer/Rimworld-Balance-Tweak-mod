using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class IntListEditorWindow : ListEditorWindow<int>
{

    public IntListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<int>?)config.GetRawValue(data) ?? new List<int>();
        workingList = originalList.ToList();
    }

    public IntListEditorWindow(string title, List<int> initialList, Action<List<int>?> onSave) : base(title, onSave)
    {
        workingList = initialList.ToList();
        originalList = initialList.ToList();
    }

    protected override string AddButtonLabel => "MST.AddInt".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(0);

    protected override void DrawEntry(Rect rect, int index)
    {
        float curX = rect.x;
        float availableWidth = rect.width - (DeleteButtonWidth + Spacing);

        Rect textFieldRect = new(curX, rect.y, availableWidth, rect.height);
        string text = workingList[index].ToString();
        string newText = Widgets.TextField(textFieldRect, text);
        if (int.TryParse(newText, out int newVal) && newVal != workingList[index])
        {
            workingList[index] = newVal;
        }
        curX += availableWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeIntList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeIntList(data);
        if (list != null) workingList = list;
    }
}