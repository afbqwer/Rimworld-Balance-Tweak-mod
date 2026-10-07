using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

/// <summary>
/// 通用的只读关联窗口：列出给定的一组 <see cref="TweakID"/> 链接，点击某一条跳转到对应数据。
/// 用于 BodyPartData / BodyData 的“种族关联”“身体关联”等反向查询。
/// 行数据在构造时一次性预计算，绘制阶段不查 Def（配合虚拟滚动）。
/// </summary>
public class TweakLinksWindow : EditorWindowBase
{
    private const float RowHeight = 28f;

    /// <summary>预计算好的行数据，避免绘制时每帧重复查询 Def / TweakData。</summary>
    private readonly struct Row
    {
        public readonly TweakID id;
        public readonly string text;
        public readonly string? tip;

        public Row(TweakID id, string text, string? tip)
        {
            this.id = id;
            this.text = text;
            this.tip = tip;
        }
    }

    private readonly List<Row> rows = new();
    private Vector2 scroll;

    /// <param name="titleKey">窗口副标题的本地化 key（如 MST.raceLinks）。</param>
    /// <param name="tipSelector">可选：为每条链接生成 tooltip（仅构造时调用一次）。</param>
    public TweakLinksWindow(TweakData data, string titleKey, List<TweakID> links, Func<TweakID, string?>? tipSelector = null)
    {
        this.data = data;
        pickerTitle = $"{data.label} - {titleKey.Translate().RawText}";
        foreach (var id in links)
        {
            var target = TweakData.GetData(id);
            string label = target?.label ?? id.defName;
            rows.Add(new Row(id, $"{label} ({id.defName})", tipSelector?.Invoke(id)));
        }
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(440f, 500f);

    protected override string SaveButtonLabel => "MST.Close".Translate().RawText;
    protected override bool ShowCancelButton => false;

    protected override void OnSave() => Close();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect listRect = new(inRect.x, curY, inRect.width, inRect.height - (curY - inRect.y) - BottomBarHeight - Spacing);
        DrawList(listRect);

        Rect bottomRect = new(inRect.x, listRect.yMax + Spacing, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    private void DrawList(Rect rect)
    {
        if (rows.Count == 0)
        {
            Widgets.Label(rect, "MST.Empty".Translate().RawText);
            return;
        }

        // 行高固定，直接按滚动位置计算可见区间，只绘制可见行（虚拟滚动）。
        const float pitch = RowHeight + Spacing;
        Rect viewRect = new(0, 0, rect.width - 16f, rows.Count * pitch);
        Widgets.BeginScrollView(rect, ref scroll, viewRect);

        int start = Mathf.Max(0, Mathf.FloorToInt(scroll.y / pitch));
        int end = Mathf.Min(rows.Count - 1, Mathf.FloorToInt((scroll.y + rect.height) / pitch));
        for (int i = start; i <= end; i++)
        {
            var rowData = rows[i];
            Rect row = new(0, i * pitch, viewRect.width, RowHeight);

            if (Widgets.ButtonText(row, rowData.text))
            {
                // 关闭本窗口，否则会盖在主窗口之上，看不到跳转结果。
                TweakUtility.JumpToData(rowData.id);
                Close();
            }

            if (rowData.tip != null)
                TooltipHandler.TipRegion(row, rowData.tip);
        }

        Widgets.EndScrollView();
    }
}