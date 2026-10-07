using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class BiomeAnimalListEditorWindow : ListEditorWindow<BiomeAnimalRecord>
{
    private List<Def>? animalKinds;

    public BiomeAnimalListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<BiomeAnimalRecord>?)config.GetRawValue(data) ?? new List<BiomeAnimalRecord>();
        workingList = originalList.Select(s => new BiomeAnimalRecord { animal = s.animal, commonality = s.commonality }).ToList();
    }

    public BiomeAnimalListEditorWindow(string title, List<BiomeAnimalRecord> initialList, Action<List<BiomeAnimalRecord>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new BiomeAnimalRecord { animal = s.animal, commonality = s.commonality }).ToList() ?? new List<BiomeAnimalRecord>();
        workingList = originalList.Select(s => new BiomeAnimalRecord { animal = s.animal, commonality = s.commonality }).ToList();
    }

    private List<Def> AnimalKinds => animalKinds ??= DefDatabase<PawnKindDef>.AllDefsListForReading
        .Where(k => k.race != null && k.RaceProps.Animal)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddWildAnimal".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new BiomeAnimalRecord { commonality = 1f });
    protected override bool IsItemValid(BiomeAnimalRecord item) => item.animal != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float animalButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect animalButtonRect = new(curX, rect.y, animalButtonWidth, rect.height);
        string animalLabel = entry.animal == null ? "MST.SelectAnimal".Translate().RawText : $"{entry.animal.LabelCap}({entry.animal.defName})";
        if (Widgets.ButtonText(animalButtonRect, animalLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                AnimalKinds,
                entry.animal,
                (selectedDef) =>
                {
                    workingList[index].animal = (PawnKindDef?)selectedDef;
                }));
        }
        curX += animalButtonWidth + Spacing;

        Rect valueFieldRect = new(curX, rect.y, valueFieldWidth, rect.height);
        string valueText = entry.commonality.ToString();
        string newText = Widgets.TextField(valueFieldRect, valueText);
        if (float.TryParse(newText, out float newVal))
        {
            entry.commonality = newVal;
        }
        curX += valueFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeBiomeAnimalList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeBiomeAnimalList(data);
        if (list != null) workingList = list;
    }
}
