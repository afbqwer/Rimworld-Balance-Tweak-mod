using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <see cref="FactionDef.pawnGroupMakers"/> 的列表编辑窗口：每行一个兵种池（<see cref="PawnGroupMaker"/>），
/// 可添加（在弹出的菜单里选 <see cref="PawnGroupKindDef"/>）、编辑（打开
/// <see cref="PawnGroupMakerEditorWindow"/>）、上下移动、删除。
///
/// <para>
/// 字段值本身就是兵种池对象，窗口打开时按字段表深克隆一份副本（<see cref="PawnGroupMakerCodec.CloneAll"/>），
/// 编辑都发生在副本上，保存时整表写回字段 —— 中间不经过任何文本格式。取消 = 副本直接丢弃，
/// Def 上的活对象一个字节都没动过。
/// </para>
///
/// <para>
/// 非原版子类（其它 Mod 给 <see cref="PawnGroupMaker"/> 写的子类，只可能来自既有 Def 数据）：<b>只读</b> ——
/// 行上只显示类型名（灰字），编辑 / 上下移动 / 删除都禁用。本工具不读取也不修改它的字段：存档只留一个
/// 类型名占位标记，保存时按类型名把 Def 里那个原实例原样放回原位置（见
/// <see cref="PawnGroupMakerCodec.MergeWithDef"/>），既不会误改其它 Mod 的数据，也不会把它的字段弄丢。
/// </para>
/// </summary>
public class PawnGroupMakerListEditorWindow : ListEditorWindow<PawnGroupMaker>
{
    private const float EditButtonWidth = 50f;
    private const float MoveButtonWidth = 26f;

    public PawnGroupMakerListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        // 深克隆：窗口里改的是副本，取消/关闭不会碰到字段，更不会碰到 Def 的活对象。
        originalList = PawnGroupMakerCodec.CloneAll((List<PawnGroupMaker>?)config.GetRawValue(data))
                       ?? new List<PawnGroupMaker>();
        workingList = originalList.ToList();
    }

    public override Vector2 InitialSize => new(780f, 620f);

    protected override string AddButtonLabel => "MST.AddPawnGroupMaker".Translate().RawText;
    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;
    protected override bool ShowCopyButton => true;
    protected override bool ShowPasteButton => true;

    protected override void OnAddItem()
    {
        var options = new List<FloatMenuOption>();
        foreach (var kind in DefDatabase<PawnGroupKindDef>.AllDefsListForReading
                     .OrderBy(k => k.defName, StringComparer.Ordinal))
        {
            var captured = kind;
            string label = captured.LabelCap.Resolve();
            if (label.NullOrEmpty()) label = captured.defName;
            options.Add(new FloatMenuOption(label, () => workingList.Add(new PawnGroupMaker { kindDef = captured })));
        }

        // 必须走 OpenChildWindow：FloatMenu 也是 Window，点击它内部任意位置会触发
        // Find.WindowStack.NotifyOutsideClicks → 本窗口的 Notify_ClickOutsideWindow。
        // 直接 WindowStack.Add 的话 childWindows 为空 ⇒ 鼠标一按下本窗口就被关掉。
        OpenChildWindow(new FloatMenu(options));
    }

    /// <summary>
    /// 没有 <c>kindDef</c> 的兵种池不可能工作：原版 <see cref="PawnGroupMaker.CanGenerateFrom"/> 直接解引用
    /// <c>kindDef.Worker</c>，留着它 = 袭击生成期 NRE。保存时丢弃，行上另有红字提示（见 <see cref="DrawEntry"/>）。
    /// </summary>
    protected override bool IsItemValid(PawnGroupMaker item) => item != null && item.kindDef != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var maker = workingList[index];
        // 非原版（其它 Mod 新增的子类）：只读展示，编辑 / 移动 / 删除一律禁用 —— 位置也必须跟它在
        // Def 里的原位置对齐（MergeWithDef 按"类型 + 出现次序"找回原实例）。
        bool readOnly = !PawnGroupMakerCodec.IsVanilla(maker.GetType());

        float available = rect.width - (EditButtonWidth + MoveButtonWidth * 2 + DeleteButtonWidth + Spacing * 4);
        float curX = rect.x;
        bool prevEnabled = GUI.enabled;

        // 编辑
        GUI.enabled = !readOnly;
        if (Widgets.ButtonText(new Rect(curX, rect.y, EditButtonWidth, rect.height), "MST.Edit".Translate().RawText))
            OpenMakerEditor(maker);
        GUI.enabled = prevEnabled;
        curX += EditButtonWidth + Spacing;

        // 类型 + 摘要
        var labelRect = new Rect(curX, rect.y, Mathf.Max(available, 1f), rect.height);
        DrawMakerLabel(labelRect, maker);
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
            RemoveAtDeferred(index);
        GUI.enabled = prevEnabled;
    }

    private static void DrawMakerLabel(Rect rect, PawnGroupMaker maker)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        bool vanilla = PawnGroupMakerCodec.IsVanilla(maker.GetType());

        Color prevColor = GUI.color;
        string label;
        if (!vanilla)
        {
            // 非原版子类：只显示类型名（字段名无从翻译），灰字提示"这一行是只读的"
            label = PawnGroupMakerCodec.TypeLabel(maker.GetType());
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
        }
        else
        {
            label = PawnGroupMakerCodec.KindLabel(maker);
            if (label.Length == 0)
            {
                // 缺 kindDef：保存时会被丢弃，先在这里说清楚（否则用户只看到行"凭空消失"）
                label = "MST.PawnGroupMakerNoKind".Translate().RawText;
                GUI.color = new Color(1f, 0.45f, 0.45f);
            }
            else
            {
                label += "   " + PawnGroupMakerCodec.Summarize(maker);
            }
        }

        Widgets.Label(rect, label);
        GUI.color = prevColor;
        Text.Anchor = TextAnchor.UpperLeft;

        if (!vanilla)
            TooltipHandler.TipRegion(rect, "MST.PocoNonVanilla".Translate().RawText);
    }

    private void OpenMakerEditor(PawnGroupMaker maker)
    {
        string title = $"{PawnGroupMakerCodec.KindLabel(maker)} - {GetBaseTitle()}";
        OpenChildWindow(new PawnGroupMakerEditorWindow(title, maker));
    }

    private void Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= workingList.Count || b >= workingList.Count) return;
        (workingList[a], workingList[b]) = (workingList[b], workingList[a]);
    }

    /// <summary>
    /// 保存前把"将被丢弃的无效条目"说出来。基类只负责过滤（<c>Where(IsItemValid)</c>），
    /// 静默丢行会让用户以为"改坏了但没提示"。
    /// </summary>
    protected override void SaveAndClose()
    {
        int dropped = workingList.Count(m => !IsItemValid(m));
        if (dropped > 0)
            Messages.Message("MST.PawnGroupMakerDropped".Translate(dropped).RawText, MessageTypeDefOf.NegativeEvent, false);
        base.SaveAndClose();
    }
}
