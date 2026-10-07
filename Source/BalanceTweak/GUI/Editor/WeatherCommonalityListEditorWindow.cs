using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class WeatherCommonalityListEditorWindow : ListEditorWindow<WeatherCommonalityRecord>
{
    private List<Def>? weatherDefs;

    public WeatherCommonalityListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = (List<WeatherCommonalityRecord>?)config.GetRawValue(data) ?? new List<WeatherCommonalityRecord>();
        workingList = originalList.Select(s => new WeatherCommonalityRecord { weather = s.weather, commonality = s.commonality }).ToList();
    }

    public WeatherCommonalityListEditorWindow(string title, List<WeatherCommonalityRecord> initialList, Action<List<WeatherCommonalityRecord>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(s => new WeatherCommonalityRecord { weather = s.weather, commonality = s.commonality }).ToList() ?? new List<WeatherCommonalityRecord>();
        workingList = originalList.Select(s => new WeatherCommonalityRecord { weather = s.weather, commonality = s.commonality }).ToList();
    }

    private List<Def> WeatherDefs => weatherDefs ??= DefDatabase<WeatherDef>.AllDefsListForReading
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddWeather".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new WeatherCommonalityRecord { commonality = 1f });
    protected override bool IsItemValid(WeatherCommonalityRecord item) => item.weather != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float weatherButtonWidth = availableWidth * 0.75f;
        float valueFieldWidth = availableWidth * 0.25f;

        Rect weatherButtonRect = new(curX, rect.y, weatherButtonWidth, rect.height);
        string weatherLabel = entry.weather == null ? "MST.SelectWeather".Translate().RawText : $"{entry.weather.LabelCap}({entry.weather.defName})";
        if (Widgets.ButtonText(weatherButtonRect, weatherLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                WeatherDefs,
                entry.weather,
                (selectedDef) =>
                {
                    workingList[index].weather = (WeatherDef?)selectedDef;
                }));
        }
        if (entry.weather != null && !entry.weather.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(weatherButtonRect, entry.weather.description);
        }
        curX += weatherButtonWidth + Spacing;

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

    protected override string? SerializeList() => SerializationHelper.SerializeWeatherCommonalityList(workingList);

    protected override void DeserializeList(string data)
    {
        var list = SerializationHelper.DeserializeWeatherCommonalityList(data);
        if (list != null) workingList = list;
    }
}
