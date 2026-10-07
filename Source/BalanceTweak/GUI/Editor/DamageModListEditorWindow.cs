using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class DamageModListEditorWindow : ListEditorWindow<DamageFactor>
{

    public DamageModListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<DamageFactor>?)config.GetRawValue(data) ?? new List<DamageFactor>();
        workingList = originalList.Select(d => new DamageFactor { damageDef = d.damageDef, factor = d.factor }).ToList();
    }

    public DamageModListEditorWindow(string title, List<DamageFactor> initialList, Action<List<DamageFactor>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(d => new DamageFactor { damageDef = d.damageDef, factor = d.factor }).ToList() ?? new List<DamageFactor>();
        workingList = originalList.Select(d => new DamageFactor { damageDef = d.damageDef, factor = d.factor }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddDamageFactor".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new DamageFactor { damageDef = null, factor = 1f });
    protected override bool IsItemValid(DamageFactor item) => item.damageDef != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 3);
        float defButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect defButtonRect = new(curX, rect.y, defButtonWidth, rect.height);
        string defLabel = entry.damageDef == null ? "MST.SelectDamageDef".Translate().RawText : $"{entry.damageDef.LabelCap}({entry.damageDef.defName})";
        if (Widgets.ButtonText(defButtonRect, defLabel))
        {
            var allDamageDefs = DefDatabase<DamageDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allDamageDefs,
                entry.damageDef,
                (selectedDef) =>
                {
                    workingList[index].damageDef = (DamageDef?)selectedDef;
                }));
        }
        if (entry.damageDef != null && !entry.damageDef.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(defButtonRect, entry.damageDef.description);
        }
        curX += defButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, 44f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Factor".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;
        if (entry.damageDef != null && !entry.damageDef.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(defButtonRect, entry.damageDef.description);
        }
        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.factor.ToString("F2");
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal))
        {
            entry.factor = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeDamageFactorList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeDamageFactorList(data);
        if (list != null) workingList = list;
    }
}