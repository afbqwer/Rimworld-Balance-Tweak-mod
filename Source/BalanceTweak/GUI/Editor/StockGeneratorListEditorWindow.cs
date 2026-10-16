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
/// 非原版子类（其它 Mod 新增的生成器，只可能来自既有 Def 数据）：行上只显示类型名，编辑时也只暴露
/// 它继承的原版字段 —— 见 <see cref="StockGeneratorCodec.IsVanilla"/> 与
/// <see cref="StockGeneratorCodec.FieldsFor"/>。这类条目<b>不克隆</b>（它的自有字段不在字段表里，
/// 克隆只会丢数据），因此它的基类字段是就地改在 Def 的活对象上的。
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
        Find.WindowStack.Add(new FloatMenu(options));
    }

    protected override void DrawEntry(Rect rect, int index)
    {
        var gen = workingList[index];

        float available = rect.width - (EditButtonWidth + MoveButtonWidth * 2 + DeleteButtonWidth + Spacing * 4);
        float curX = rect.x;

        // 编辑
        if (Widgets.ButtonText(new Rect(curX, rect.y, EditButtonWidth, rect.height), "MST.Edit".Translate().RawText))
            OpenGeneratorEditor(gen);
        curX += EditButtonWidth + Spacing;

        // 类型 + 摘要
        var labelRect = new Rect(curX, rect.y, Mathf.Max(available, 1f), rect.height);
        DrawGeneratorLabel(labelRect, gen);
        curX += labelRect.width + Spacing;

        // 上移 / 下移
        bool prevEnabled = GUI.enabled;
        GUI.enabled = index > 0;
        if (Widgets.ButtonText(new Rect(curX, rect.y, MoveButtonWidth, rect.height), "▲")) Swap(index, index - 1);
        GUI.enabled = prevEnabled;
        curX += MoveButtonWidth + Spacing;

        prevEnabled = GUI.enabled;
        GUI.enabled = index < workingList.Count - 1;
        if (Widgets.ButtonText(new Rect(curX, rect.y, MoveButtonWidth, rect.height), "▼")) Swap(index, index + 1);
        GUI.enabled = prevEnabled;
        curX += MoveButtonWidth + Spacing;

        // 删除
        if (Widgets.ButtonText(new Rect(curX, rect.y, DeleteButtonWidth, rect.height), "X"))
            workingList.RemoveAt(index);
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
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;

        // 非原版类型：只显示类型名（Mod 字段既无翻译也无从判断语义），悬停说明为什么表单里字段很少。
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
