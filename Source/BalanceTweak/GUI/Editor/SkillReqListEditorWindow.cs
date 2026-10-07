using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class SkillReqListEditorWindow : ListEditorWindow<SkillRequirement>
{

    public SkillReqListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<SkillRequirement>?)config.GetRawValue(data) ?? new List<SkillRequirement>();
        workingList = originalList.Select(s => new SkillRequirement { skill = s.skill, minLevel = s.minLevel }).ToList();
    }

    public SkillReqListEditorWindow(string title, List<SkillRequirement> initialList, Action<List<SkillRequirement>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new SkillRequirement { skill = s.skill, minLevel = s.minLevel }).ToList() ?? new List<SkillRequirement>();
        workingList = originalList.Select(s => new SkillRequirement { skill = s.skill, minLevel = s.minLevel }).ToList();
    }

    protected override string AddButtonLabel => "MST.AddSkillReq".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new SkillRequirement());
    protected override bool IsItemValid(SkillRequirement item) => item.skill != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (36f + DeleteButtonWidth + Spacing * 2);
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

        Rect valueLabelRect = new(curX, rect.y, 36f, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(valueLabelRect, "Lv");
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 36f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.minLevel.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (int.TryParse(newText, out int newVal))
        {
            entry.minLevel = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeSkillRequirementList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeSkillRequirementList(data);
        if (list != null) workingList = list;
    }
}