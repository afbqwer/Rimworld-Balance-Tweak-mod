using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class StatModListEditorWindow : ListEditorWindow<StatModifier>
{

    public StatModListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<StatModifier>?)config.GetRawValue(data) ?? new List<StatModifier>();
        workingList = originalList.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList();
    }

    public StatModListEditorWindow(string title, List<StatModifier> initialList, Action<List<StatModifier>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList() ?? new List<StatModifier>();
        workingList = originalList.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddStatMod".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new StatModifier { stat = null, value = 0f });
    protected override bool IsItemValid(StatModifier item) => item.stat != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 3);
        float statButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect statButtonRect = new(curX, rect.y, statButtonWidth, rect.height);
        string statLabel = entry.stat == null ? "MST.SelectStat".Translate().RawText : $"{entry.stat.LabelCap}({entry.stat.defName})";
        if (Widgets.ButtonText(statButtonRect, statLabel))
        {
            var allStats = DefDatabase<StatDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allStats,
                entry.stat,
                (selectedDef) =>
                {
                    workingList[index].stat = (StatDef?)selectedDef;
                }));
        }
        if (entry.stat != null && !entry.stat.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(statButtonRect, entry.stat.description);
        }
        curX += statButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, 44f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Value".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        
        // 检查数值是否超出 StatDef 范围，超出则标红
        bool isOutOfRange = entry.stat != null &&
                            (entry.value < entry.stat.minValue || entry.value > entry.stat.maxValue);
        if (isOutOfRange)
        {
            GUI.backgroundColor = Color.red;
        }
        
        string valueText = entry.value.ToString("F2");
        string newText = Widgets.TextField(valueFieldRect, valueText);
        
        if (isOutOfRange)
        {
            GUI.backgroundColor = Color.white;
        }
        
        if (float.TryParse(newText, out float newVal))
        {
            entry.value = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeStatModifierList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeStatModifierList(data);
        if (list != null) workingList = list;
    }
}