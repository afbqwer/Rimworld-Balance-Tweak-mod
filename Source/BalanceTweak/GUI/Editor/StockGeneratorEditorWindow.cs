using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 单个库存生成器的字段编辑窗口：按字段表声明的编辑方式渲染表单（见 <see cref="StockGeneratorFieldUi"/>），
/// 控件直接读写传入的那个 <see cref="StockGenerator"/> 对象 —— 没有中间副本、没有文本格式。
///
/// <para>
/// 传入的对象是 <see cref="StockGeneratorListEditorWindow"/> 的克隆副本，本窗口的"确定"只关闭自己，
/// 真正的保存 / 取消由列表窗口统一承担；字段级的修改标记也由外层 <c>ApplyL&lt;StockGenerator&gt;</c> 打上。
/// </para>
/// </summary>
public class StockGeneratorEditorWindow : EditorWindowBase
{
    private const float LabelWidth = 170f;
    private const float RowStride = StockGeneratorFieldUi.RowHeight + Spacing;

    private readonly StockGenerator gen;
    private readonly IReadOnlyList<StockGeneratorCodec.SgField> fields;
    private Vector2 scrollPosition;

    public StockGeneratorEditorWindow(string title, StockGenerator gen)
    {
        pickerTitle = title;
        this.gen = gen;
        fields = StockGeneratorCodec.FieldsFor(gen.GetType());
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(640f, 600f);

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
        // 首行（类型）+ 字段：行数固定，按行高算总高即可。
        int rowCount = 1 + fields.Count;
        Rect viewRect = new(0f, 0f, rect.width - 16f, rowCount * RowStride + Spacing);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float y = 0f;

        // 实际类型（= 原版 XML 里的 Class），只读展示
        DrawLabel(new Rect(0f, y, LabelWidth, StockGeneratorFieldUi.RowHeight), "MST.StockGenType");
        var typeRect = new Rect(LabelWidth, y, viewRect.width - LabelWidth, StockGeneratorFieldUi.RowHeight);
        DrawLabelText(typeRect, StockGeneratorCodec.TypeLabel(gen.GetType()));
        // 非原版（其它 Mod 新增）类型：下面只列出它继承的原版字段，悬停解释原因。
        if (!StockGeneratorCodec.IsVanilla(gen.GetType()))
            TooltipHandler.TipRegion(typeRect, "MST.StockGenNonVanilla".Translate().RawText);
        y += RowStride;

        // 生成器自身字段
        foreach (var field in fields)
        {
            var labelRect = new Rect(0f, y, LabelWidth, StockGeneratorFieldUi.RowHeight);
            DrawLabelText(labelRect, StockGeneratorFieldUi.LabelOf(field));
            // 悬停标签显示 C# 字段名（= 原版 XML 的元素名），便于对照 Def XML 手工排查
            TooltipHandler.TipRegion(labelRect, field.Name);

            var fieldRect = new Rect(LabelWidth, y, viewRect.width - LabelWidth, StockGeneratorFieldUi.RowHeight);
            StockGeneratorFieldUi.DrawField(fieldRect, field, gen, this);

            y += RowStride;
        }

        Widgets.EndScrollView();
    }

    private void DrawLabel(Rect rect, string labelKey)
    {
        DrawLabelText(rect, labelKey.TryTranslate(out var text) ? text : labelKey);

        if ((labelKey + "Comment").TryTranslate(out var comment))
            TooltipHandler.TipRegion(rect, comment);
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
