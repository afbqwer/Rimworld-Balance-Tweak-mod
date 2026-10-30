using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <see cref="TraderKindDef.stockGenerators"/> 的列表编辑窗口：每行一个生成器（类型名 + 关键字段摘要），
/// 可添加（在弹出的类型菜单里选<b>原版</b> <see cref="StockGenerator"/> 子类）、编辑（打开
/// <see cref="StockGeneratorEditorWindow"/>）、上下移动、删除。
///
/// <para>
/// 字段值本身就是生成器对象，窗口打开时按字段表克隆一份副本（<see cref="StockGeneratorCodec.Clone"/>），
/// 编辑都发生在副本上，保存时整表写回字段 —— 中间不经过任何文本格式。
/// </para>
///
/// <para>
/// 非原版子类（其它 Mod 新增的生成器，只可能来自既有 Def 数据）：<b>只读</b> —— 行上只显示类型名（灰字），
/// 编辑 / 上下移动 / 删除都禁用。本工具不读取也不修改它的字段：存档只留一个类型名的占位标记，保存时按
/// 类型名把 Def 里那个原实例原样放回原位置（见 <see cref="StockGeneratorCodec.MergeWithDef"/>），
/// 所以既不会误改其它 Mod 的数据，也不会把它的字段弄丢。
/// </para>
/// </summary>
public class StockGeneratorListEditorWindow : ListEditorWindow<StockGenerator>
{
    private const float EditButtonWidth = 50f;
    private const float MoveButtonWidth = 26f;

    public StockGeneratorListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        // 深克隆：窗口里改的是副本，取消/关闭不会碰到字段，更不会碰到 Def 的活对象。
        originalList = StockGeneratorCodec.CloneAll((List<StockGenerator>?)config.GetRawValue(data)) ?? new List<StockGenerator>();
        workingList = originalList.ToList();
    }

    public override Vector2 InitialSize => new(760f, 620f);

    protected override string AddButtonLabel => "MST.AddStockGenerator".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override bool ShowCopyButton => true;
    protected override bool ShowPasteButton => true;

    protected override void OnAddItem()
    {
        var options = new List<FloatMenuOption>();
        // 只列字段表里的原版类型：Mod 新增的 StockGenerator 子类不在表里、字段任意也没有翻译，
        // 本工具也不暴露其编辑（见 StockGeneratorCodec.FieldsFor），选进来只会做出游戏读不懂的库存。
        foreach (var type in StockGeneratorCodec.VanillaGeneratorTypes())
        {
            var captured = type;
            options.Add(new FloatMenuOption(StockGeneratorCodec.TypeLabel(captured), () =>
            {
                try
                {
                    if (Activator.CreateInstance(captured) is StockGenerator gen)
                        workingList.Add(gen);
                }
                catch (Exception e)
                {
                    Log.Error($"[BalanceTweak] 无法创建 {captured.FullName}：{e.Message}");
                }
            }));
        }
        // 必须走 OpenChildWindow：FloatMenu 也是 Window，点击它内部任意位置会触发
        // Find.WindowStack.NotifyOutsideClicks → 本窗口的 Notify_ClickOutsideWindow。
        // 直接 WindowStack.Add 的话 childWindows 为空 ⇒ 鼠标一按下本窗口就被关掉，
        // 选项的 action 落在一个已关闭窗口的 workingList 上 = 「点了没反应、无日志」。
        OpenChildWindow(new FloatMenu(options));
    }

    protected override void DrawEntry(Rect rect, int index)
    {
        var gen = workingList[index];
        // 非原版（其它 Mod 新增）条目：只读展示，编辑 / 移动 / 删除一律禁用 —— 本工具不读取也不修改它的
        // 字段（存档只留类型名，Apply 时按类型名把 Def 里那个原实例原样放回，见 StockGeneratorCodec.MergeWithDef），
        // 位置也必须跟它在 Def 里的原位置对齐。
        bool readOnly = !StockGeneratorCodec.IsVanilla(gen.GetType());

        float available = rect.width - (EditButtonWidth + MoveButtonWidth * 2 + DeleteButtonWidth + Spacing * 4);
        float curX = rect.x;
        bool prevEnabled = GUI.enabled;

        // 编辑
        GUI.enabled = !readOnly;
        if (Widgets.ButtonText(new Rect(curX, rect.y, EditButtonWidth, rect.height), "MST.Edit".Translate().RawText))
            OpenGeneratorEditor(gen);
        GUI.enabled = prevEnabled;
        curX += EditButtonWidth + Spacing;

        // 类型 + 摘要
        var labelRect = new Rect(curX, rect.y, Mathf.Max(available, 1f), rect.height);
        DrawGeneratorLabel(labelRect, gen);
        curX += labelRect.width + Spacing;

        // 上移 / 下移
        GUI.enabled = !readOnly && index > 0;
        if (Widgets.ButtonText(new Rect(curX, rect.y, MoveButtonWidth, rect.height), "▲")) Swap(index, index - 1);
        GUI.enabled = !readOnly && index < workingList.Count - 1;
        if (Widgets.ButtonText(new Rect(curX + MoveButtonWidth + Spacing, rect.y, MoveButtonWidth, rect.height), "▼")) Swap(index, index + 1);
        GUI.enabled = prevEnabled;
        curX += MoveButtonWidth * 2 + Spacing * 2;

        // 删除
        GUI.enabled = !readOnly;
        if (Widgets.ButtonText(new Rect(curX, rect.y, DeleteButtonWidth, rect.height), "X"))
            workingList.RemoveAt(index);
        GUI.enabled = prevEnabled;
    }

    private static void DrawGeneratorLabel(Rect rect, StockGenerator gen)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        string label = StockGeneratorCodec.TypeLabel(gen.GetType());
        bool vanilla = StockGeneratorCodec.IsVanilla(gen.GetType());
        if (vanilla)
        {
            string summary = StockGeneratorCodec.Summarize(gen);
            if (summary.Length > 0) label += "   " + summary;
        }

        // 非原版条目用灰字提示"这一行是只读的"
        Color prevColor = GUI.color;
        if (!vanilla) GUI.color = new Color(0.75f, 0.75f, 0.75f);
        Widgets.Label(rect, label);
        GUI.color = prevColor;
        Text.Anchor = TextAnchor.UpperLeft;

        // 非原版类型：只显示类型名（字段靠 Mod 自己解释、没有翻译），悬停说明本工具对它只读。
        if (!vanilla)
            TooltipHandler.TipRegion(rect, "MST.StockGenNonVanilla".Translate().RawText);
    }

    private void OpenGeneratorEditor(StockGenerator gen)
    {
        string title = $"{StockGeneratorCodec.TypeLabel(gen.GetType())} - {GetBaseTitle()}";
        OpenChildWindow(new StockGeneratorEditorWindow(title, gen));
    }

    private void Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= workingList.Count || b >= workingList.Count) return;
        (workingList[a], workingList[b]) = (workingList[b], workingList[a]);
    }

    /// <summary>复制用：整表 → "一行一条生成器文本"（见 <see cref="StockGeneratorCodec"/>）。</summary>
    protected override string? SerializeList()
    {
        var lines = StockGeneratorCodec.Serialize(workingList);
        return lines != null ? string.Join("\n", lines) : null;
    }

    /// <summary>粘贴走 <see cref="OnPasteData"/>（整体校验后整块替换），不做增量还原。</summary>
    protected override void DeserializeList(string data) { }

    protected override bool OnPasteData(string data)
    {
        var lines = data.Split('\n').Where(l => !l.NullOrEmpty()).ToList();
        if (lines.Count == 0) return false;
        // 整体校验：任一条无法解析就整块拒绝，避免把半截数据写进字段。
        var list = StockGeneratorCodec.Deserialize(lines);
        if (list == null) return false;
        workingList = list;
        return true;
    }
}
