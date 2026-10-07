using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class SkillGainListEditorWindow : ListEditorWindow<SkillGain>
{

    public SkillGainListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<SkillGain>?)config.GetRawValue(data) ?? new List<SkillGain>();
        workingList = originalList.Select(s => new SkillGain { skill = s.skill, amount = s.amount }).ToList();
    }

    public SkillGainListEditorWindow(string title, List<SkillGain> initialList, Action<List<SkillGain>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new SkillGain { skill = s.skill, amount = s.amount }).ToList() ?? new List<SkillGain>();
        workingList = originalList.Select(s => new SkillGain { skill = s.skill, amount = s.amount }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddSkillGain".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new SkillGain { skill = null, amount = 0 });
    protected override bool IsItemValid(SkillGain item) => item.skill != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 2);
        float skillButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect skillButtonRect = new(curX, rect.y, skillButtonWidth, rect.height);
        string skillLabel = entry.skill == null ? "MST.SelectSkill".Translate().RawText : $"{entry.skill.LabelCap}({entry.skill.defName})";
        if (Widgets.ButtonText(skillButtonRect, skillLabel))
        {
            var allSkills = DefDatabase<SkillDef>.AllDefsListForReading.Cast<Def>().ToList();
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                allSkills,
                entry.skill,
                (selectedDef) =>
                {
                    workingList[index].skill = (SkillDef?)selectedDef;
                }));
        }
        if (entry.skill != null && !entry.skill.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(skillButtonRect, entry.skill.description);
        }
        curX += skillButtonWidth + Spacing;

        Rect valueLabelRect = new(curX, rect.y, 44f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "MST.Amount".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.amount.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (int.TryParse(newText, out int newVal))
        {
            entry.amount = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeSkillGainList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeSkillGainList(data);
        if (list != null) workingList = list;
    }
}