using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public abstract class ListEditorWindow<T> : EditorWindowBase
{
    protected List<T> workingList = null!;
    protected List<T> originalList = null!;
    protected Action<List<T>?>? onSaveCallback;
    protected Vector2 scrollPosition;

    protected const float EntryHeight = 30f;
    protected const float DeleteButtonWidth = 30f;
    protected const float ButtonHeight = 30f;

    protected ListEditorWindow() { }

    protected ListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
    }

    protected ListEditorWindow(string title, Action<List<T>?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect listRect = new(inRect.x, curY, inRect.width,
            inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing);
        DrawListArea(listRect);
        curY = listRect.yMax + Spacing;

        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    protected virtual bool HasAddButton => true;

    protected virtual void DrawListArea(Rect rect)
    {
        float addButtonHeight = HasAddButton ? ButtonHeight + Spacing : 0f;
        float totalHeight = workingList.Count * (EntryHeight + Spacing) + addButtonHeight;

        Rect viewRect = new(0f, 0f, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        // 虚拟滚动：只绘制可见范围内的条目（上下各多留一行余量）
        float rowStride = EntryHeight + Spacing;
        int firstVisible = Mathf.Max(0, Mathf.FloorToInt((scrollPosition.y - EntryHeight) / rowStride));
        int lastVisible = Mathf.Min(workingList.Count - 1, Mathf.CeilToInt((scrollPosition.y + rect.height + EntryHeight) / rowStride));

        for (int i = firstVisible; i <= lastVisible; i++)
        {
            Rect entryRect = new(0f, i * rowStride, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
        }

        if (HasAddButton)
        {
            float addButtonY = workingList.Count * rowStride;
            bool addButtonVisible = addButtonY + ButtonHeight >= scrollPosition.y && addButtonY <= scrollPosition.y + rect.height;
            if (addButtonVisible && Widgets.ButtonText(new Rect(0f, addButtonY, viewRect.width, ButtonHeight), AddButtonLabel))
            {
                OnAddItem();
            }
        }

        Widgets.EndScrollView();
    }

    protected abstract string AddButtonLabel { get; }
    protected abstract void OnAddItem();
    protected abstract void DrawEntry(Rect rect, int index);

    protected virtual bool IsItemValid(T item) => true;

    protected virtual void SaveAndClose()
    {
        var finalList = workingList.Where(IsItemValid).ToList();
        var result = finalList.Count > 0 ? finalList : null;
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

    protected abstract string? SerializeList();
    protected abstract void DeserializeList(string data);

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    protected override string? GetCopyData() => SerializeList();
    protected override bool OnPasteData(string data)
    {
        try
        {
            DeserializeList(data);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] Paste failed: {ex.Message}");
            return false;
        }
    }
}