using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class StringListEditorWindow : ListEditorWindow<string>
{

    public StringListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<string>?)config.GetRawValue(data) ?? new List<string>();
        workingList = originalList.ToList();
    }

    public StringListEditorWindow(string title, List<string> initialList, Action<List<string>?> onSave) : base(title, onSave)
    {
        workingList = initialList.ToList();
        originalList = initialList.ToList();
    }

    protected override string AddButtonLabel => "MST.AddString".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override void OnAddItem() => workingList.Add("");
    protected override bool IsItemValid(string item) => !item.NullOrEmpty();

    protected override void DrawEntry(Rect rect, int index)
    {
        float curX = rect.x;
        float availableWidth = rect.width - (DeleteButtonWidth + Spacing);

        Rect textFieldRect = new(curX, rect.y, availableWidth, rect.height);
        string labelText = workingList[index].NullOrEmpty() ? "MST.EmptyString".Translate().RawText : workingList[index];
        string newText = Widgets.TextField(textFieldRect, labelText);
        if (newText != labelText)
        {
            workingList[index] = newText;
        }
        curX += availableWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => workingList.Count > 0 ? string.Join("\n", workingList) : null;

    protected override void DeserializeList(string data)
    {
        workingList = data.Split('\n').ToList();
    }
}