using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class AptitudeListEditorWindow : ListEditorWindow<Aptitude>
{
    public AptitudeListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<Aptitude>?)config.GetRawValue(data) ?? new List<Aptitude>();
        workingList = originalList.Select(a => new Aptitude(a.skill, a.level)).ToList();
    }

    public AptitudeListEditorWindow(string title, List<Aptitude> initialList, Action<List<Aptitude>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(a => new Aptitude(a.skill, a.level)).ToList() ?? new List<Aptitude>();
        workingList = originalList.Select(a => new Aptitude(a.skill, a.level)).ToList();
    }

    protected override string AddButtonLabel => "MST.AddAptitude".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new Aptitude(null, 0));
    protected override bool IsItemValid(Aptitude item) => item.skill != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (40f + DeleteButtonWidth + Spacing * 3);
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
        Widgets.Label(valueLabelRect, "MST.Level".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += 44f;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.level.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (int.TryParse(newText, out int newVal))
        {
            entry.level = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeAptitudeList(workingList);
    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeAptitudeList(data);
        if (list != null) workingList = list;
    }
}