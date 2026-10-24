using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class FactionCurveListEditorWindow : ListEditorWindow<FactionCurve>
{
    // FactionCurve.selectionWeightPerPointsCurve 是 private 字段，只能反射读写
    //（与 PocoCodec 字段表声明的是同一个名字，改字段名时测试会先炸出来）。
    private static readonly FieldInfo? curveField = Reflect.Field(typeof(FactionCurve), "selectionWeightPerPointsCurve");

    private List<Def>? factionDefs;

    public FactionCurveListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        if (curveField == null)
            Log.Error("[BalanceTweak] FactionCurve.selectionWeightPerPointsCurve 反射失败（原版改了字段名？），曲线列无法读写。");
        originalList = ((List<FactionCurve>?)config.GetRawValue(data) ?? new List<FactionCurve>())
            .Select(CloneEntry).ToList();
        workingList = originalList.Select(CloneEntry).ToList();
    }

    public FactionCurveListEditorWindow(string title, List<FactionCurve> initialList, Action<List<FactionCurve>?> onSave) : base(title, onSave)
    {
        originalList = initialList?.Select(CloneEntry).ToList() ?? new List<FactionCurve>();
        workingList = originalList.Select(CloneEntry).ToList();
    }

    private static FactionCurve CloneEntry(FactionCurve source)
        => PawnsArrivalModeData.CloneFactionCurve(source) ?? new FactionCurve();

    private List<Def> FactionDefs => factionDefs ??= DefDatabase<FactionDef>.AllDefsListForReading
        .Cast<Def>()
        .OrderBy(d => d.label)
        .ToList();

    protected override string AddButtonLabel => "MST.AddFactionCurve".Translate().RawText;
    protected override void OnAddItem() => workingList.Add(new FactionCurve());
    protected override bool IsItemValid(FactionCurve item) =>
        item.faction != null && GetCurve(item) is { PointsCount: > 0 };

    private static SimpleCurve? GetCurve(FactionCurve entry) => curveField?.GetValue(entry) as SimpleCurve;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 2);
        float factionButtonWidth = availableWidth * 0.6f;
        float curveButtonWidth = availableWidth * 0.4f;

        Rect factionButtonRect = new(curX, rect.y, factionButtonWidth, rect.height);
        string factionLabel = entry.faction == null
            ? "MST.SelectFaction".Translate().RawText
            : $"{entry.faction.LabelCap}({entry.faction.defName})";
        if (Widgets.ButtonText(factionButtonRect, factionLabel))
        {
            string title = GetBaseTitle();
            OpenChildWindow(new DefSelectionWindow(
                title,
                FactionDefs,
                entry.faction,
                (selectedDef) =>
                {
                    workingList[index].faction = (FactionDef?)selectedDef;
                }));
        }
        curX += factionButtonWidth + Spacing;

        Rect curveButtonRect = new(curX, rect.y, curveButtonWidth, rect.height);
        var curve = GetCurve(entry);
        string curveText = curve == null
            ? "MST.EditCurve".Translate().RawText
            : $"{"MST.EditCurve".Translate().RawText} ({curve.PointsCount})";
        if (Widgets.ButtonText(curveButtonRect, curveText))
        {
            string title = entry.faction == null
                ? GetBaseTitle()
                : $"{GetBaseTitle()} - {entry.faction.LabelCap}";
            OpenChildWindow(new CurveEditorWindow(
                title,
                curve ?? new SimpleCurve(),
                (newCurve) =>
                {
                    if (curveField != null) curveField.SetValue(workingList[index], newCurve);
                }));
        }
        curX += curveButtonWidth + Spacing;

        Rect deleteRect = new(curX, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            workingList.RemoveAt(index);
        }
    }
}
