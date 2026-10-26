using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <c>List&lt;SkillRange&gt;</c>（技能 + 等级区间）的列表编辑器。
///
/// <para>
/// <c>SkillRange</c> 的两个字段都是 <b>private</b>（对外只有只读属性 <c>Skill</c> / <c>Range</c>），
/// 所以读写统一走 <see cref="PocoCodec"/> 的字段表（<c>ReadField</c> / <c>WriteField</c>）——
/// 与存档编解码共用同一份声明，避免"窗口里能改、存档里丢"这类两套清单漂移。
/// </para>
/// <para>
/// 复制 / 粘贴 / 拖拽涂抹走 <see cref="ListEditorWindow{T}"/> 的默认实现（元素类型已在字段表里），
/// 本类刻意不覆写 <c>SerializeList</c> / <c>DeserializeList</c>。
/// </para>
/// </summary>
public class SkillRangeListEditorWindow : ListEditorWindow<SkillRange>
{
    private static PocoCodec.Field? skillField;
    private static PocoCodec.Field? rangeField;

    public SkillRangeListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = CloneAll((List<SkillRange>?)config.GetRawValue(data));
        workingList = CloneAll(originalList);
    }

    public SkillRangeListEditorWindow(string title, List<SkillRange> initialList, Action<List<SkillRange>?> onSave) : base(title, onSave)
    {
        originalList = CloneAll(initialList);
        workingList = CloneAll(originalList);
    }

    /// <summary>字段表的声明是懒建的；窗口任何一次读写前都得先确它建好（否则 <c>FieldsFor</c> 返回空表）。</summary>
    private static void EnsureFields()
    {
        if (skillField != null && rangeField != null) return;
        TweakData.FieldCodecs.EnsureBuilt();
        foreach (var f in PocoCodec.FieldsFor(typeof(SkillRange)))
        {
            if (f.Name == "skill") skillField = f;
            else if (f.Name == "range") rangeField = f;
        }
    }

    /// <summary>按字段表深拷贝（每次打开窗口都在副本上编辑，改到一半取消不会污染 Def）。</summary>
    private static List<SkillRange> CloneAll(List<SkillRange>? source)
    {
        if (source == null) return new List<SkillRange>();
        EnsureFields();
        var result = new List<SkillRange>(source.Count);
        foreach (var item in source)
        {
            if (item == null) continue;
            if (PocoCodec.CloneElement(item, typeof(SkillRange)) is SkillRange copy) result.Add(copy);
            // 拷不出来就不放进副本：宁可少一条可见的条目，也不把半成品交给用户去编辑
        }
        return result;
    }

    private static SkillDef? SkillOf(SkillRange item)
        => skillField == null ? null : PocoCodec.ReadField(skillField, item) as SkillDef;

    private static void SetSkill(SkillRange item, SkillDef? skill)
    {
        if (skillField != null) PocoCodec.WriteField(skillField, item, skill);
    }

    private static IntRange RangeOf(SkillRange item)
        => rangeField != null && PocoCodec.ReadField(rangeField, item) is IntRange r ? r : IntRange.One;

    private static void SetRange(SkillRange item, IntRange range)
    {
        if (rangeField != null) PocoCodec.WriteField(rangeField, item, range);
    }

    protected override string AddButtonLabel => "MST.AddSkillRange".Translate().RawText;

    protected override void OnAddItem()
    {
        EnsureFields();
        workingList.Add(new SkillRange());
    }

    protected override bool IsItemValid(SkillRange item) => SkillOf(item) != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        EnsureFields();
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float skillButtonWidth = availableWidth * 0.5f;
        float minFieldWidth = availableWidth * 0.25f;
        float maxFieldWidth = availableWidth * 0.25f;

        var skill = SkillOf(entry);
        Rect skillButtonRect = new(curX, rect.y, skillButtonWidth, rect.height);
        string skillLabel = skill == null ? "MST.SelectSkill".Translate().RawText : $"{skill.LabelCap}({skill.defName})";
        if (Widgets.ButtonText(skillButtonRect, skillLabel))
        {
            var allSkills = DefDatabase<SkillDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allSkills,
                skill,
                selectedDef => SetSkill(workingList[index], selectedDef as SkillDef)));
        }
        if (skill != null && !skill.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(skillButtonRect, skill.description);
        }
        curX += skillButtonWidth + Spacing;

        var range = RangeOf(entry);

        Rect minFieldRect = new(curX, rect.y, minFieldWidth, rect.height);
        string newMinText = Widgets.TextField(minFieldRect, range.min.ToString());
        if (int.TryParse(newMinText, out int newMin))
        {
            SetRange(entry, new IntRange(newMin, range.max));
            range = RangeOf(entry);
        }
        curX += minFieldWidth + Spacing;

        Rect maxFieldRect = new(curX, rect.y, maxFieldWidth, rect.height);
        string newMaxText = Widgets.TextField(maxFieldRect, range.max.ToString());
        if (int.TryParse(newMaxText, out int newMax))
        {
            SetRange(entry, new IntRange(range.min, newMax));
        }
        curX += maxFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }
}
