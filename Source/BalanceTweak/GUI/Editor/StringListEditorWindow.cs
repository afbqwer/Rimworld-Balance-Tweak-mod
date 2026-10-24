using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class StringListEditorWindow : ListEditorWindow<string>
{
    /// <summary>
    /// 删到空列表并保存时，是否视为一次显式清空（<see cref="TweakData.MarkFieldCleared"/>）。
    /// <para>
    /// 普通字符串列表保持旧行为：空列表不写回 Def。任务标签（thingSetMakerTags）必须打开这个开关，
    /// 否则空列表会被判成 <see cref="FieldState.Unset"/>，<c>ApplyL</c> 静默跳过，旧标签删不干净。
    /// </para>
    /// </summary>
    private readonly bool clearWhenEmpty;

    public StringListEditorWindow(StatColumnConfig config, TweakData data, bool clearWhenEmpty = false) : base(config, data)
    {
        originalList = (List<string>?)config.GetRawValue(data) ?? new List<string>();
        workingList = originalList.ToList();
        this.clearWhenEmpty = clearWhenEmpty;
    }

    public StringListEditorWindow(string title, List<string> initialList, Action<List<string>?> onSave
        , bool clearWhenEmpty = false) : base(title, onSave)
    {
        workingList = initialList.ToList();
        originalList = initialList.ToList();
        this.clearWhenEmpty = clearWhenEmpty;
    }

    protected override string AddButtonLabel => "MST.AddString".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override void OnAddItem() => workingList.Add("");
    protected override bool IsItemValid(string item) => !item.NullOrEmpty();

    protected override void SaveAndClose()
    {
        if (!clearWhenEmpty)
        {
            base.SaveAndClose();
            return;
        }

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

        if (onSaveCallback != null)
        {
            onSaveCallback(finalList.Count > 0 ? finalList : null);
        }
        else if (finalList.Count == 0)
        {
            // 与 StatColumnConfig.CheckAndApply 的显式清空路径保持一致：
            // 先清字段值，再标记 Cleared，最后 Apply 才会走 ApplyL 的 set(null) 分支。
            config!.ApplyValue(data!, null, isModified: false);
            data!.MarkFieldCleared(config.fieldName);
            data.Apply();
        }
        else
        {
            config!.ApplyValue(data!, finalList);
        }

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