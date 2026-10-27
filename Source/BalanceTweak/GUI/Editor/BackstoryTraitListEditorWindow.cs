using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <c>List&lt;BackstoryTrait&gt;</c>（特性 + 程度）的列表编辑器，服务
/// <see cref="BackstoryDef.forcedTraits"/> / <see cref="BackstoryDef.disallowedTraits"/>。
///
/// <para>
/// <see cref="BackstoryTrait"/> 与 <see cref="TraitRequirement"/> 字段形状相同但**是两个不同类型**
/// （前者在角色背景上，后者在派系/商店等需求上），所以不能复用
/// <see cref="TraitReqListEditorWindow"/>；这里只换了元素类型与文案。
/// </para>
/// <para>
/// 复制 / 粘贴 / 拖拽涂抹走 <see cref="ListEditorWindow{T}"/> 的默认实现（元素类型已在字段表里），
/// 本类刻意不覆写 <c>SerializeList</c> / <c>DeserializeList</c>。
/// </para>
/// </summary>
public class BackstoryTraitListEditorWindow : ListEditorWindow<BackstoryTrait>
{
    public BackstoryTraitListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = CloneAll((List<BackstoryTrait>?)config.GetRawValue(data));
        workingList = CloneAll(originalList);
    }

    public BackstoryTraitListEditorWindow(string title, List<BackstoryTrait> initialList, Action<List<BackstoryTrait>?> onSave) : base(title, onSave)
    {
        originalList = CloneAll(initialList);
        workingList = CloneAll(originalList);
    }

    /// <summary>逐字段复制：窗口里改到一半点取消，不能已经改脏了字段里的对象。</summary>
    private static List<BackstoryTrait> CloneAll(List<BackstoryTrait>? source)
        => source == null
            ? new List<BackstoryTrait>()
            : source.Where(t => t != null)
                .Select(t => new BackstoryTrait { def = t.def, degree = t.degree })
                .ToList();

    protected override string AddButtonLabel => "MST.AddBackstoryTrait".Translate().RawText;

    protected override void OnAddItem() => workingList.Add(new BackstoryTrait());

    /// <summary>没选特性的行在保存时被丢掉（<c>def</c> 为 null 的条目会让原版 <c>ConfigErrors</c> 抛异常）。</summary>
    protected override bool IsItemValid(BackstoryTrait item) => item.def != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (StyleLabelWidth + DeleteButtonWidth + Spacing * 3);
        float traitButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect traitButtonRect = new(curX, rect.y, traitButtonWidth, rect.height);
        string traitLabel = entry.def == null
            ? "MST.SelectTrait".Translate().RawText
            : $"{entry.def.LabelCap}({entry.def.defName})";
        if (Widgets.ButtonText(traitButtonRect, traitLabel))
        {
            var allTraits = DefDatabase<TraitDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allTraits,
                entry.def,
                selectedDef => workingList[index].def = (TraitDef?)selectedDef));
        }
        if (entry.def != null && !entry.def.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(traitButtonRect, entry.def.description);
        }
        curX += traitButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, StyleLabelWidth, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Degree".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += StyleLabelWidth + Spacing;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string newText = Widgets.TextField(valueFieldRect, entry.degree.ToString());
        if (newText.NullOrEmpty())
        {
            entry.degree = 0;
        }
        else if (int.TryParse(newText, out int newVal))
        {
            entry.degree = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }
}
