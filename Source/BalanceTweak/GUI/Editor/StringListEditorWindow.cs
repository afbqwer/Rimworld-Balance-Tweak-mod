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

    public StringListEditorWindow(string title, List<string> initialList, Action<List<string>?> onSave)
        : base(title, onSave)
    {
        workingList = initialList.ToList();
        originalList = initialList.ToList();
    }

    protected override string AddButtonLabel => "MST.AddString".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override void OnAddItem() => workingList.Add("");
    protected override bool IsItemValid(string item) => !item.NullOrEmpty();

    protected override void SaveAndClose()
    {
        var finalList = workingList
            .Select(t => t?.Trim() ?? "")
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

        // 只是打开后原样保存：不产生任何状态变化。
        if (finalList.SequenceEqual(originalList))
        {
            Close();
            return;
        }

        // 删光 = 清空（显式 Cleared ⇒ Def 写空列表）。这条对所有字符串列表列一视同仁 ——
        // 早先只有一个 clearWhenEmpty 开关（thingSetMakerTags 专用）走清空，其余列删光后
        // 会被判 Unset 静默跳过、Def 上的旧值一个不少，正是"清空 = 移除全部项"没落到核心的后果。
        CommitResult(finalList.Count > 0 ? finalList : null);
        Close();
    }

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