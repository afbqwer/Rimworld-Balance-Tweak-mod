using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <c>List&lt;PossessionThingDefCountClass&gt;</c>（随身物品 + 数量区间）的列表编辑器，
/// 服务 <see cref="BackstoryDef.possessions"/>。
///
/// <para>
/// 元素是「物品 Def + <see cref="IntRange"/>」：与原版 XML 里
/// <c>&lt;Steel&gt;2~5&lt;/Steal&gt;</c> 同义，所以数量给两个输入框（min / max），
/// 而不是单个 count —— 后者表达不了区间。
/// </para>
/// <para>
/// 复制 / 粘贴 / 拖拽涂抹走 <see cref="ListEditorWindow{T}"/> 的默认实现（元素类型已在字段表里，
/// 文本形如 <c>key=Steel|value=2~5</c>），本类刻意不覆写 <c>SerializeList</c> / <c>DeserializeList</c>。
/// </para>
/// </summary>
public class PossessionListEditorWindow : ListEditorWindow<PossessionThingDefCountClass>
{
    private List<Def>? thingDefs;

    public PossessionListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = CloneAll((List<PossessionThingDefCountClass>?)config.GetRawValue(data));
        workingList = CloneAll(originalList);
    }

    public PossessionListEditorWindow(string title, List<PossessionThingDefCountClass> initialList, Action<List<PossessionThingDefCountClass>?> onSave) : base(title, onSave)
    {
        originalList = CloneAll(initialList);
        workingList = CloneAll(originalList);
    }

    /// <summary>逐字段复制（<see cref="IntRange"/> 是值类型，直接拷即可）。</summary>
    private static List<PossessionThingDefCountClass> CloneAll(List<PossessionThingDefCountClass>? source)
        => source == null
            ? new List<PossessionThingDefCountClass>()
            : source.Where(p => p != null)
                .Select(p => new PossessionThingDefCountClass { key = p.key, value = p.value })
                .ToList();

    /// <summary>只列可搬动的物品：建筑/地形这类放进背包没有意义，且原版生成时会直接丢弃。</summary>
    private List<Def> ThingDefs => thingDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
        .Where(t => t.category == ThingCategory.Item)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddPossession".Translate().RawText;

    protected override void OnAddItem() => workingList.Add(new PossessionThingDefCountClass());

    protected override bool IsItemValid(PossessionThingDefCountClass item) => item.key != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (StyleLabelWidth + DeleteButtonWidth + Spacing * 4);
        float thingButtonWidth = availableWidth * 0.5f;
        float minFieldWidth = availableWidth * 0.25f;
        float maxFieldWidth = availableWidth * 0.25f;

        Rect thingButtonRect = new(curX, rect.y, thingButtonWidth, rect.height);
        string thingLabel = entry.key == null
            ? "MST.SelectThingDef".Translate().RawText
            : $"{entry.key.LabelCap}({entry.key.defName})";
        if (Widgets.ButtonText(thingButtonRect, thingLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                ThingDefs,
                entry.key,
                selectedDef => workingList[index].key = (ThingDef?)selectedDef));
        }
        if (entry.key != null && !entry.key.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(thingButtonRect, entry.key.description);
        }
        curX += thingButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, StyleLabelWidth, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Count".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += StyleLabelWidth + Spacing;

        Rect minFieldRect = new(curX, rect.y, minFieldWidth, rect.height);
        string newMinText = Widgets.TextField(minFieldRect, entry.value.min.ToString());
        if (int.TryParse(newMinText, out int newMin))
        {
            entry.value = new IntRange(newMin, entry.value.max);
        }
        curX += minFieldWidth + Spacing;

        Rect maxFieldRect = new(curX, rect.y, maxFieldWidth, rect.height);
        string newMaxText = Widgets.TextField(maxFieldRect, entry.value.max.ToString());
        if (int.TryParse(newMaxText, out int newMax))
        {
            entry.value = new IntRange(entry.value.min, newMax);
        }
        curX += maxFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }
}
