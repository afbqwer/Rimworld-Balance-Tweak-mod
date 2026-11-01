using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

/// <summary>
/// 只读的反查窗口：列出该研究项目解锁的全部 Def，点击可跳转到对应页签的那一行。
///
/// <para>
/// 行数据在<b>构造时一次性</b>算完（反查 + 载体归属 + 可跳转判定），绘制阶段不查 Def ——
/// 与 <see cref="TweakLinksWindow"/> 同一条规矩：这些信息随 Def 变动，缓存到下一帧就会和表格打架。
/// </para>
///
/// <para>
/// <b>未解锁目标没有数据行是常态，不是错误</b>：心能仪式没有对应的 Data 类，第三方 Mod 的 Def
/// 也可能不被任何规则收编。这类行照样列出来（漏掉它反而让人以为"没解锁"），只是不可点击，
/// 并在 tooltip 里说明原因。
/// </para>
/// </summary>
public class ResearchUnlockWindow : EditorWindowBase
{
    private const float RowHeight = 28f;
    private const float CarrierColumnRatio = 0.42f;

    private readonly struct Row
    {
        /// <summary>可跳转目标；null = 该 Def 没有对应的数据行。</summary>
        public readonly TweakID? target;
        public readonly string text;
        public readonly string? carrier;
        public readonly string tip;

        public Row(TweakID? target, string text, string? carrier, string tip)
        {
            this.target = target;
            this.text = text;
            this.carrier = carrier;
            this.tip = tip;
        }
    }

    private readonly List<Row> rows = new();
    private Vector2 scroll;

    public ResearchUnlockWindow(TweakData data, ResearchProjectDef project)
    {
        this.data = data;

        var entries = ResearchUnlocks.Get(project);

        // 同一产物可能由多条配方解锁：按显示 Def 归组，载体合并展示（与原版 Distinct 后的一条对应）。
        var order = new List<Def>();
        var carriers = new Dictionary<Def, List<Def>>();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!carriers.TryGetValue(entry.display, out var list))
            {
                carriers[entry.display] = list = new List<Def>();
                order.Add(entry.display);
            }
            if (!list.Contains(entry.carrier)) list.Add(entry.carrier);
        }
        order.Sort((a, b) => string.Compare(a.label ?? a.defName, b.label ?? b.defName, StringComparison.CurrentCulture));

        for (int i = 0; i < order.Count; i++)
        {
            var display = order[i];
            var list = carriers[display];

            // 载体就是它自己（建筑 / 地形 / 仪式的前置写在自身字段上）时不占一列，免得整列重复同一个名字。
            bool selfCarried = list.Count == 1 && ReferenceEquals(list[0], display);
            var carrierText = selfCarried ? null : string.Join(", ", list.ConvertAll(c => c.LabelCap.Resolve()));

            var target = FindRow(display);
            var tip = $"{display.LabelCap.Resolve()} ({display.defName})";
            if (!selfCarried)
                tip += "\n" + "MST.unlockCarrierTip".Translate(carrierText).RawText;
            tip += "\n\n" + (target.HasValue
                ? "MST.JumpToDesc".Translate().RawText
                : "MST.unlockNoData".Translate().RawText);

            rows.Add(new Row(target, $"{display.LabelCap.Resolve()} ({display.defName})", carrierText, tip));
        }

        pickerTitle = $"{data.label} - {"MST.unlockedDefs".Translate().RawText} ({rows.Count})";
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(560f, 560f);

    protected override string SaveButtonLabel => "MST.Close".Translate().RawText;
    protected override bool ShowCancelButton => false;

    protected override void OnSave() => Close();

    /// <summary>找出该 Def 在主规则下对应的数据行；没有就返回 null（点了也跳不过去，不如不给按钮）。</summary>
    private static TweakID? FindRow(Def def)
    {
        var rule = TweakData.FindRuleFor(def, includeFallback: true);
        if (rule == null) return null;
        var id = new TweakID(def.defName, rule.Tab);
        return TweakDatabase.GetOriginal(id) != null ? id : null;
    }

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

        // 行高固定，按滚动位置只绘制可见行（虚拟滚动）。
        const float pitch = RowHeight + Spacing;
        Rect viewRect = new(0, 0, rect.width - 16f, rows.Count * pitch);
        Widgets.BeginScrollView(rect, ref scroll, viewRect);

        int start = Mathf.Max(0, Mathf.FloorToInt(scroll.y / pitch));
        int end = Mathf.Min(rows.Count - 1, Mathf.FloorToInt((scroll.y + rect.height) / pitch));
        for (int i = start; i <= end; i++)
        {
            var row = rows[i];
            Rect rowRect = new(0, i * pitch, viewRect.width, RowHeight);

            float carrierWidth = row.carrier == null ? 0f : rowRect.width * CarrierColumnRatio;
            Rect mainRect = new(rowRect.x, rowRect.y, rowRect.width - carrierWidth - (carrierWidth > 0f ? Spacing : 0f), rowRect.height);
            Rect carrierRect = new(mainRect.xMax + Spacing, rowRect.y, carrierWidth, rowRect.height);

            if (row.target.HasValue)
            {
                if (Widgets.ButtonText(mainRect, row.text))
                {
                    // 关掉本窗口，否则会盖在主窗口之上，看不到跳转结果。
                    // 走 TweakData 重载而不是 TweakID 重载：后者直接索引 cachedData，键不存在会抛。
                    var target = TweakData.GetData(row.target.Value);
                    if (target != null) TweakUtility.JumpToData(target);
                    Close();
                }
            }
            else
            {
                // 不可跳转：画成灰字而不是禁用按钮 —— 禁用按钮在 IMGUI 里既无 hover 也无 tooltip。
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                Widgets.Label(mainRect.ContractedBy(6f, 0f), row.text);
                GUI.color = prevColor;
            }

            if (carrierWidth > 0f)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(carrierRect, row.carrier!);
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
            }

            TooltipHandler.TipRegion(rowRect, row.tip);
        }

        Widgets.EndScrollView();
    }
}
