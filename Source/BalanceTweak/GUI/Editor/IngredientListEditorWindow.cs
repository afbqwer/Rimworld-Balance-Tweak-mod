using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class IngredientListEditorWindow : ListEditorWindow<IngredientCount>
{

    public IngredientListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<IngredientCount>?)config.GetRawValue(data) ?? new List<IngredientCount>();
        workingList = originalList.Select(ic =>
        {
            var copy = new IngredientCount();
            copy.SetBaseCount(ic.GetBaseCount());
            copy.filter.CopyAllowancesFrom(ic.filter);
            return copy;
        }).ToList();
    }

    public IngredientListEditorWindow(string title, List<IngredientCount> initialList, Action<List<IngredientCount>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(ic =>
        {
            var copy = new IngredientCount();
            copy.SetBaseCount(ic.GetBaseCount());
            copy.filter.CopyAllowancesFrom(ic.filter);
            return copy;
        }).ToList() ?? new List<IngredientCount>();
        workingList = originalList.Select(ic =>
        {
            var copy = new IngredientCount();
            copy.SetBaseCount(ic.GetBaseCount());
            copy.filter.CopyAllowancesFrom(ic.filter);
            return copy;
        }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddIngredient".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new IngredientCount());
    protected override bool IsItemValid(IngredientCount item) => item.filter.AllowedDefCount > 0;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 3);
        float filterButtonWidth = availableWidth * 0.65f;
        float valueFieldWidth = availableWidth * 0.35f;

        Rect filterButtonRect = new(curX, rect.y, filterButtonWidth, rect.height);
        string filterLabel;
        if (entry.IsFixedIngredient)
        {
            filterLabel = entry.FixedIngredient.LabelCap.NullOrEmpty() ? entry.FixedIngredient.defName : entry.FixedIngredient.LabelCap;
        }
        else
        {
            var cats = SerializationHelper.GetThingFilterCategories(entry.filter);
            var stuff = SerializationHelper.GetThingFilterStuffCategories(entry.filter);
            var catsCount = cats?.Count ?? -1;
            var stuffCount = stuff?.Count ?? -1;
            if (catsCount > 0)
                filterLabel = $"@{cats![0]}{(catsCount>1?"+"+(catsCount-1):"")}";
            else if(stuffCount > 0)
                filterLabel = $"#{stuff![0].label}{(stuffCount>1?"+"+(stuffCount-1):"")}";
            else 
                filterLabel = "MST.SelectIngredientFilter".Translate(entry.filter.AllowedDefCount);
        }
        if (Widgets.ButtonText(filterButtonRect, filterLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new IngredientFilterEditorWindow(
                title,
                entry.filter,
                (resultFilter) =>
                {
                    if (resultFilter != null)
                    {
                        entry.filter.CopyAllowancesFrom(resultFilter);
                    }
                }));
        }
        curX += filterButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, 44f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Count".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.GetBaseCount().ToString("F2");
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal) && newVal > 0f)
        {
            entry.SetBaseCount(newVal);
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeIngredientCountList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeIngredientCountList(data);
        if (list != null) workingList = list;
    }
}