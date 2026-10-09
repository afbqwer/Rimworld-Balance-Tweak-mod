using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class CapModListEditorWindow : ListEditorWindow<PawnCapacityModifier>
{
    public override Vector2 InitialSize => new(800f, 500f);
    protected override float BottomBarHeight => 35f;
    protected override string CancelButtonLabel => "MST.CancelStatMods".Translate().RawText;

    public CapModListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        this.originalList = (List<PawnCapacityModifier>?)config.GetRawValue(data) ?? new List<PawnCapacityModifier>();
        this.workingList = originalList.Select(c => new PawnCapacityModifier
        {
            capacity = c.capacity,
            offset = c.offset,
            setMax = c.setMax,
            postFactor = c.postFactor,
            statFactorMod = c.statFactorMod,
            setMaxCurveEvaluateStat = c.setMaxCurveEvaluateStat
        }).ToList();
    }

    public CapModListEditorWindow(string title, List<PawnCapacityModifier> initialList, Action<List<PawnCapacityModifier>?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        originalList = initialList?.Select(c => new PawnCapacityModifier
        {
            capacity = c.capacity,
            offset = c.offset,
            setMax = c.setMax,
            postFactor = c.postFactor,
            statFactorMod = c.statFactorMod,
            setMaxCurveEvaluateStat = c.setMaxCurveEvaluateStat
        }).ToList() ?? new List<PawnCapacityModifier>();
        workingList = originalList.Select(c => new PawnCapacityModifier
        {
            capacity = c.capacity,
            offset = c.offset,
            setMax = c.setMax,
            postFactor = c.postFactor,
            statFactorMod = c.statFactorMod,
            setMaxCurveEvaluateStat = c.setMaxCurveEvaluateStat
        }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddCapMod".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new PawnCapacityModifier
    {
        capacity = null,
        offset = 0f,
        setMax = 999f,
        postFactor = 1f,
        statFactorMod = null,
        setMaxCurveEvaluateStat = null
    });

    protected override void DrawListArea(Rect rect)
    {
        float headerHeight = 24f;
        float addButtonHeight = HasAddButton ? ButtonHeight + Spacing : 0f;
        float totalHeight = headerHeight + Spacing + workingList.Count * (EntryHeight + Spacing) + addButtonHeight;

        Rect viewRect = new(0f, 0f, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        // Draw header（随内容滚动，滚出视野后跳过绘制）
        float contentTop = headerHeight + Spacing;
        bool headerVisible = headerHeight >= scrollPosition.y;
        if (headerVisible)
        {
            Rect headerRect = new(0f, 0f, viewRect.width, headerHeight);
            DrawHeader(headerRect);
        }

        // 虚拟滚动：只绘制可见范围内的条目（上下各多留一行余量）
        float rowStride = EntryHeight + Spacing;
        int firstVisible = Mathf.Max(0, Mathf.FloorToInt((scrollPosition.y - contentTop - EntryHeight) / rowStride));
        int lastVisible = Mathf.Min(workingList.Count - 1, Mathf.CeilToInt((scrollPosition.y + rect.height + EntryHeight - contentTop) / rowStride));

        // Draw entries
        for (int i = firstVisible; i <= lastVisible; i++)
        {
            Rect entryRect = new(0f, contentTop + i * rowStride, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
        }

        if (HasAddButton)
        {
            float addButtonY = contentTop + workingList.Count * rowStride;
            bool addButtonVisible = addButtonY + ButtonHeight >= scrollPosition.y && addButtonY <= scrollPosition.y + rect.height;
            if (addButtonVisible && Widgets.ButtonText(new Rect(0f, addButtonY, viewRect.width, ButtonHeight), AddButtonLabel))
            {
                OnAddItem();
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawHeader(Rect rect)
    {
        var cols = GetColumnRects(rect);

        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        Widgets.DrawBox(rect);
        GUI.color = Color.white;

        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;

        Widgets.Label(cols.capacity, "MST.CapModCapacity".Translate().RawText);
        Widgets.Label(cols.offset, "MST.CapModOffset".Translate().RawText);
        Widgets.Label(cols.setMax, "MST.CapModSetMax".Translate().RawText);
        Widgets.Label(cols.postFactor, "MST.CapModPostFactor".Translate().RawText);
        Widgets.Label(cols.statFactor, "MST.CapModStatFactor".Translate().RawText);
        Widgets.Label(cols.evalStat, "MST.CapModEvalStat".Translate().RawText);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private struct ColRects
    {
        public Rect capacity, offset, setMax, postFactor, statFactor, evalStat, delete;
    }

    private ColRects GetColumnRects(Rect rowRect)
    {
        float curX = rowRect.x;
        float w = rowRect.width;

        float extraLabelWidth = 10f + 22f + 10f;
        float availableWidth = w - (DeleteButtonWidth + Spacing * 6 + extraLabelWidth);
        float capacityWidth = availableWidth * 0.28f;
        float offsetWidth = availableWidth * 0.13f + 10f;
        float setMaxWidth = availableWidth * 0.13f + 22f;
        float postFactorWidth = availableWidth * 0.13f + 10f;
        float statFactorWidth = availableWidth * 0.165f;
        float evalStatWidth = availableWidth * 0.165f;

        var cols = new ColRects();
        cols.capacity = new Rect(curX, rowRect.y, capacityWidth, rowRect.height);
        curX += capacityWidth + Spacing;
        cols.offset = new Rect(curX, rowRect.y, offsetWidth, rowRect.height);
        curX += offsetWidth + Spacing;
        cols.setMax = new Rect(curX, rowRect.y, setMaxWidth, rowRect.height);
        curX += setMaxWidth + Spacing;
        cols.postFactor = new Rect(curX, rowRect.y, postFactorWidth, rowRect.height);
        curX += postFactorWidth + Spacing;
        cols.statFactor = new Rect(curX, rowRect.y, statFactorWidth, rowRect.height);
        curX += statFactorWidth + Spacing;
        cols.evalStat = new Rect(curX, rowRect.y, evalStatWidth, rowRect.height);
        curX += evalStatWidth + Spacing;
        cols.delete = new Rect(curX, rowRect.y, DeleteButtonWidth, rowRect.height);
        return cols;
    }

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        var cols = GetColumnRects(rect);

        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;

        // Capacity selector
        string capacityLabel = entry.capacity == null ? "MST.None".Translate().RawText : entry.capacity.LabelCap.NullOrEmpty() ? entry.capacity.defName : entry.capacity.LabelCap;
        if (Widgets.ButtonText(cols.capacity, capacityLabel))
        {
            var allCapacities = DefDatabase<PawnCapacityDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle(), allCapacities, entry.capacity,
                (selectedDef) => { workingList[index].capacity = (PawnCapacityDef?)selectedDef; }));
        }
        if (entry.capacity != null && !entry.capacity.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(cols.capacity, entry.capacity.description);
        }
        // offset
        string offsetStr = entry.offset.ToString("F2");
        string newOffset = Widgets.TextField(cols.offset, offsetStr);
        if (float.TryParse(newOffset, out float offsetVal))
            entry.offset = offsetVal;

        // setMax
        string setMaxStr = entry.setMax.ToString("F2");
        string newSetMax = Widgets.TextField(cols.setMax, setMaxStr);
        if (float.TryParse(newSetMax, out float setMaxVal))
            entry.setMax = setMaxVal;

        // postFactor
        string postFactorStr = entry.postFactor.ToString("F2");
        string newPostFactor = Widgets.TextField(cols.postFactor, postFactorStr);
        if (float.TryParse(newPostFactor, out float postFactorVal))
            entry.postFactor = postFactorVal;

        // statFactor selector
        string statFactorLabel = entry.statFactorMod == null ? "MST.None".Translate().RawText : $"{entry.statFactorMod.LabelCap}";
        if (Widgets.ButtonText(cols.statFactor, statFactorLabel))
        {
            var allStats = DefDatabase<StatDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle(), allStats, entry.statFactorMod,
                (selectedDef) => { workingList[index].statFactorMod = (StatDef?)selectedDef; }));
        }
        if (entry.statFactorMod != null && !entry.statFactorMod.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(cols.statFactor, entry.statFactorMod.description);
        }
        // evalStat selector
        string evalStatLabel = entry.setMaxCurveEvaluateStat == null ? "MST.None".Translate().RawText : $"{entry.setMaxCurveEvaluateStat.LabelCap}";
        if (Widgets.ButtonText(cols.evalStat, evalStatLabel))
        {
            var allStats = DefDatabase<StatDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle(), allStats, entry.setMaxCurveEvaluateStat,
                (selectedDef) => { workingList[index].setMaxCurveEvaluateStat = (StatDef?)selectedDef; }));
        }
        if (entry.setMaxCurveEvaluateStat != null && !entry.setMaxCurveEvaluateStat.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(cols.evalStat, entry.setMaxCurveEvaluateStat.description);
        }
        // Delete button
        if (Widgets.ButtonText(cols.delete, "X"))
        {
            workingList.RemoveAt(index);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected override string? SerializeList() => SerializationHelper.SerializePawnCapacityModifierList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializePawnCapacityModifierList(data);
        if (list != null) workingList = list;
    }

    protected override void SaveAndClose()
    {
        var finalList = workingList.Where(c => c.capacity != null).ToList();
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
}