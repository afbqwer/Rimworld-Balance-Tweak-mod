using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <c>List&lt;MutatorChance&gt;</c> 的列表编辑器，同时服务
/// <see cref="LandmarkDef.mutatorChances"/> 与 <see cref="LandmarkDef.comboLandmarkMutators"/>。
///
/// <para>
/// 元素是「地块突变器 + 触发几率 + 是否必需」，对应原版 XML 里
/// <c>&lt;Oasis Required="True"/&gt;</c> 或 <c>&lt;Stockpile&gt;0.05&lt;/Stockpile&gt;</c>。
/// 两个地标列表形状完全相同，只是语义不同（前者逐条独立判定，后者命中一条即停），
/// 所以共用一个窗口，由列标题区分。
/// </para>
/// <para>
/// 复制 / 粘贴 / 拖拽涂抹走 <see cref="ListEditorWindow{T}"/> 的默认实现（<see cref="MutatorChance"/>
/// 已在字段表里，文本形如 <c>mutator=Fertile|chance=0.05|required=true</c>），本类刻意不覆写
/// <c>SerializeList</c> / <c>DeserializeList</c>。
/// </para>
/// </summary>
public class MutatorChanceListEditorWindow : ListEditorWindow<MutatorChance>
{
    private List<Def>? mutators;

    public MutatorChanceListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = CloneAll((List<MutatorChance>?)config.GetRawValue(data));
        workingList = CloneAll(originalList);
    }

    public MutatorChanceListEditorWindow(string title, List<MutatorChance> initialList, Action<List<MutatorChance>?> onSave) : base(title, onSave)
    {
        originalList = CloneAll(initialList);
        workingList = CloneAll(originalList);
    }

    /// <summary>
    /// 逐字段复制成新实例。直接引用字段里的对象 ⇒ 窗口里改到一半点取消，数据已经被改脏；
    /// 而且 <see cref="LandmarkData.SetDef"/> 的 <c>??=</c> 让字段与 Def 的活列表共用元素，
    /// 不复制就会顺着窗口改到 Def 上。
    /// </summary>
    private static List<MutatorChance> CloneAll(List<MutatorChance>? source)
        => source == null
            ? new List<MutatorChance>()
            : source.Where(m => m != null)
                .Select(m => new MutatorChance { mutator = m.mutator, chance = m.chance, required = m.required })
                .ToList();

    private List<Def> Mutators => mutators ??= DefDatabase<TileMutatorDef>.AllDefsListForReading
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddMutator".Translate().RawText;

    protected override void OnAddItem() => workingList.Add(new MutatorChance { chance = 1f });

    protected override bool IsItemValid(MutatorChance item) => item?.mutator != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 3);
        float mutatorButtonWidth = availableWidth * 0.5f;
        float chanceFieldWidth = availableWidth * 0.22f;
        float requiredWidth = availableWidth * 0.28f;

        Rect mutatorButtonRect = new(curX, rect.y, mutatorButtonWidth, rect.height);
        string mutatorLabel = entry.mutator == null
            ? "MST.SelectMutator".Translate().RawText
            : $"{entry.mutator.LabelCap}({entry.mutator.defName})";
        if (Widgets.ButtonText(mutatorButtonRect, mutatorLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                Mutators,
                entry.mutator,
                selectedDef => workingList[index].mutator = (TileMutatorDef?)selectedDef));
        }
        if (entry.mutator != null && !entry.mutator.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(mutatorButtonRect, entry.mutator.description);
        }
        curX += mutatorButtonWidth + Spacing;

        // 几率：原版是 0~1 的小数（>1 等价于"必定"），负数没有意义，输入非法时保持原值。
        Rect chanceFieldRect = new(curX, rect.y, chanceFieldWidth, rect.height);
        string newChanceText = Widgets.TextField(chanceFieldRect, entry.chance.ToString());
        if (float.TryParse(newChanceText, out float newChance))
        {
            entry.chance = Mathf.Max(0f, newChance);
        }
        // 这一格没有列内标签（宽度不够），靠 tooltip 说明它是什么 —— 同时也是 MST.Chance 的引用点。
        TooltipHandler.TipRegion(chanceFieldRect,
            "MST.Chance".Translate().RawText + "\n" + "MST.ChanceComment".Translate().RawText);
        curX += chanceFieldWidth + Spacing;

        Rect requiredRect = new(curX, rect.y, requiredWidth, rect.height);
        bool required = entry.required;
        Widgets.CheckboxLabeled(requiredRect, "MST.Required".Translate().RawText, ref required);
        entry.required = required;
        curX += requiredWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            RemoveAtDeferred(index);
        }
    }
}
