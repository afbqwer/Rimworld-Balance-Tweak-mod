using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class XenotypeChanceListEditorWindow : ListEditorWindow<XenotypeChance>
{
    private List<Def>? xenotypeDefs;

    public XenotypeChanceListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<XenotypeChance>?)config.GetRawValue(data) ?? new List<XenotypeChance>();
        workingList = originalList.Select(s => new XenotypeChance { xenotype = s.xenotype, chance = s.chance }).ToList();
    }

    public XenotypeChanceListEditorWindow(string title, List<XenotypeChance> initialList, Action<List<XenotypeChance>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new XenotypeChance { xenotype = s.xenotype, chance = s.chance }).ToList() ?? new List<XenotypeChance>();
        workingList = originalList.Select(s => new XenotypeChance { xenotype = s.xenotype, chance = s.chance }).ToList();
    }

    // 原版 XenotypeDef.ConfigErrors 要求候选均为可继承异种人，这里只列可继承项；
    // 既有条目即使不可继承也照常显示、可编辑。
    private List<Def> XenotypeDefs => xenotypeDefs ??= DefDatabase<XenotypeDef>.AllDefsListForReading
        .Where(x => x.inheritable)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddXenotype".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new XenotypeChance { chance = 0.1f });
    protected override bool IsItemValid(XenotypeChance item) => item.xenotype != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float xenoButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect xenoButtonRect = new(curX, rect.y, xenoButtonWidth, rect.height);
        string xenoLabel = entry.xenotype == null ? "MST.SelectXenotype".Translate().RawText : $"{entry.xenotype.LabelCap}({entry.xenotype.defName})";
        if (Widgets.ButtonText(xenoButtonRect, xenoLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                XenotypeDefs,
                entry.xenotype,
                (selectedDef) =>
                {
                    workingList[index].xenotype = (XenotypeDef?)selectedDef;
                }));
        }
        if (entry.xenotype != null && !entry.xenotype.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(xenoButtonRect, entry.xenotype.description);
        }
        curX += xenoButtonWidth + Spacing;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.chance.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal))
        {
            entry.chance = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }
}
