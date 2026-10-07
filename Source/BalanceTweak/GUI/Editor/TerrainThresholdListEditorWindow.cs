using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class TerrainThresholdListEditorWindow : ListEditorWindow<TerrainThreshold>
{
    private List<Def>? terrainDefs;

    public TerrainThresholdListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<TerrainThreshold>?)config.GetRawValue(data) ?? new List<TerrainThreshold>();
        workingList = originalList.Select(s => new TerrainThreshold { terrain = s.terrain, min = s.min, max = s.max }).ToList();
    }

    public TerrainThresholdListEditorWindow(string title, List<TerrainThreshold> initialList, Action<List<TerrainThreshold>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new TerrainThreshold { terrain = s.terrain, min = s.min, max = s.max }).ToList() ?? new List<TerrainThreshold>();
        workingList = originalList.Select(s => new TerrainThreshold { terrain = s.terrain, min = s.min, max = s.max }).ToList();
    }

    private List<Def> TerrainDefs => terrainDefs ??= DefDatabase<TerrainDef>.AllDefsListForReading
        .Where(t => t.natural)
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddTerrainBand".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new TerrainThreshold { min = -999f, max = 999f });
    protected override bool IsItemValid(TerrainThreshold item) => item.terrain != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float terrainButtonWidth = availableWidth * 0.55f;
        float minFieldWidth = availableWidth * 0.225f;
        float maxFieldWidth = availableWidth * 0.225f;

        Rect terrainButtonRect = new(curX, rect.y, terrainButtonWidth, rect.height);
        string terrainLabel = entry.terrain == null ? "MST.SelectTerrain".Translate().RawText : $"{entry.terrain.LabelCap}({entry.terrain.defName})";
        if (Widgets.ButtonText(terrainButtonRect, terrainLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                TerrainDefs,
                entry.terrain,
                (selectedDef) =>
                {
                    workingList[index].terrain = (TerrainDef?)selectedDef;
                }));
        }
        if (entry.terrain != null && !entry.terrain.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(terrainButtonRect, entry.terrain.description);
        }
        curX += terrainButtonWidth + Spacing;

        Rect minFieldRect = new(curX, rect.y, minFieldWidth, rect.height);
        string minText = entry.min.ToString();
        string newMinText = Widgets.TextField(minFieldRect, minText);
        if (float.TryParse(newMinText, out float newMin))
        {
            entry.min = newMin;
        }
        curX += minFieldWidth + Spacing;

        Rect maxFieldRect = new(curX, rect.y, maxFieldWidth, rect.height);
        string maxText = entry.max.ToString();
        string newMaxText = Widgets.TextField(maxFieldRect, maxText);
        if (float.TryParse(newMaxText, out float newMax))
        {
            entry.max = newMax;
        }
        curX += maxFieldWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }

    protected override string? SerializeList() => SerializationHelper.SerializeTerrainThresholdList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeTerrainThresholdList(data);
        if (list != null) workingList = list;
    }
}
