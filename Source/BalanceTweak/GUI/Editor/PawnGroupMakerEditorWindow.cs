using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 单个兵种池（<see cref="PawnGroupMaker"/>）的字段编辑窗口：按字段表声明的编辑方式渲染表单
/// （见 <see cref="PocoFieldUi"/>），控件直接读写传入的那个对象 —— 没有中间副本、没有文本格式。
///
/// <para>
/// 传入的对象是 <see cref="PawnGroupMakerListEditorWindow"/> 的克隆副本，本窗口的"确定"只关闭自己，
/// 真正的保存 / 取消由列表窗口统一承担；字段级的修改标记也由外层 <c>ApplyL&lt;PawnGroupMaker&gt;</c> 打上。
/// </para>
///
/// <para>
/// <b>四个子列表不是等价的</b>：<c>options</c> 供 <see cref="PawnGroupKindWorker_Normal"/>（及继承它的
/// Shamblers）使用，<c>traders</c> / <c>carriers</c> / <c>guards</c> 只有
/// <see cref="PawnGroupKindWorker_Trader"/> 读。所以头部把当前的 <c>workerClass</c> 显示出来 ——
/// 改了一个"当前 worker 根本不会读"的列表是最常见的白忙。这里<b>不按 worker 隐藏字段</b>：
/// 其它 Mod 的 worker 可以按自己的规则读它们。
/// </para>
/// </summary>
public class PawnGroupMakerEditorWindow : EditorWindowBase
{
    private const float LabelWidth = 170f;
    private const float RowStride = PocoFieldUi.RowHeight + Spacing;

    private readonly PawnGroupMaker maker;
    private readonly IReadOnlyList<PocoCodec.Field> fields;
    private Vector2 scrollPosition;

    public PawnGroupMakerEditorWindow(string title, PawnGroupMaker maker)
    {
        pickerTitle = title;
        this.maker = maker;
        fields = PawnGroupMakerCodec.FieldsFor(typeof(PawnGroupMaker));
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(700f, 560f);

    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;

    // 编辑是就地写回副本对象的（没有中间副本），所以没有"取消"可言：这里只给"确定"，
    // 真正的撤销由列表窗口的"取消"承担。
    protected override bool ShowCancelButton => false;

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        float curY = inRect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        Rect bodyRect = new(inRect.x, curY, inRect.width,
            inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing);
        DrawBody(bodyRect);

        DrawBottomBar(new Rect(inRect.x, bodyRect.yMax + Spacing, inRect.width, BottomBarHeight));
    }

    private void DrawBody(Rect rect)
    {
        // 头部 2 行（worker / 空 options 提示）+ 字段。窗口标题里已经有 kindDef 的标签，
        // 所以这里不再重复一行"类型"——kindDef 本身在字段表里，下面可以改。
        Rect viewRect = new(0f, 0f, rect.width - 16f, (2 + fields.Count) * RowStride + Spacing);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float y = 0f;

        // workerClass：决定四个子列表里哪些真的会被读（见类注释）
        DrawLabelText(new Rect(0f, y, LabelWidth, PocoFieldUi.RowHeight), "MST.PawnGroupMakerWorker".Translate().RawText);
        var workerType = maker.kindDef?.workerClass;
        DrawLabelText(new Rect(LabelWidth, y, viewRect.width - LabelWidth, PocoFieldUi.RowHeight),
            workerType?.Name ?? "-");
        y += RowStride;

        // options 为空 ⇒ 这个兵种池凑不出任何人。新增的条目一开始就是空的，必须提示。
        if (maker.options.NullOrEmpty())
        {
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.4f);
            DrawLabelText(new Rect(0f, y, viewRect.width, PocoFieldUi.RowHeight),
                "MST.PawnGroupMakerNoOptions".Translate().RawText);
            GUI.color = prev;
        }
        y += RowStride;

        // 字段表：kindDef / commonality / maxTotalPoints / disallowedStrategies / options / traders / carriers / guards
        foreach (var field in fields)
        {
            var labelRect = new Rect(0f, y, LabelWidth, PocoFieldUi.RowHeight);
            DrawLabelText(labelRect, PocoFieldUi.LabelOf(field));
            // 有说明就显示说明；没有则显示 C# 字段名（= 原版 XML 的元素名），便于对照 Def XML 手工排查
            TooltipHandler.TipRegion(labelRect, PocoFieldUi.CommentOf(field) ?? field.Name);

            var fieldRect = new Rect(LabelWidth, y, viewRect.width - LabelWidth, PocoFieldUi.RowHeight);
            PocoFieldUi.DrawField(fieldRect, field, maker, this);

            y += RowStride;
        }

        Widgets.EndScrollView();
    }

    private static void DrawLabelText(Rect rect, string label)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected override void OnSave() => Close();

    protected override void OnCancel() => Close();
}
