using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// "小对象列表"编辑窗口：元素只有几个字段（如 <see cref="CountChance"/> 的 count / chance、
/// <see cref="ThingDefCountRangeClass"/> 的 thingDef / countRange），每行把子字段等宽铺开，可增删。
///
/// <para>
/// 元素以装箱形式保存（<c>List&lt;object&gt;</c>）：<see cref="CountChance"/> 是 struct，直接改列表里的
/// 副本不会生效，而反射 <c>FieldInfo.SetValue</c> 作用在装箱对象上能就地改写这个箱子，因此统一用装箱保存，
/// 保存时再回收成 <c>List&lt;元素类型&gt;</c>。
/// </para>
/// </summary>
public class SGComplexListEditorWindow : EditorWindowBase
{
    private const float RowHeight = 46f;
    private const float RowLabelHeight = 16f;
    private const float DeleteButtonWidth = 30f;

    private readonly Type elementType;
    private readonly List<object> items = new();
    private readonly IReadOnlyList<PocoCodec.Field> fields;
    private readonly Action<IList?> onSave;
    private Vector2 scrollPosition;

    public SGComplexListEditorWindow(string title, IList? initial, Type elementType, Action<IList?> onSave)
    {
        pickerTitle = title;
        this.elementType = elementType;
        this.onSave = onSave;
        fields = StockGeneratorCodec.FieldsFor(elementType);
        if (initial != null)
            foreach (var item in initial)
                if (item != null) items.Add(item);
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(620f, 480f);

    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

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

        Rect listRect = new(inRect.x, curY, inRect.width,
            inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing);
        DrawList(listRect);

        DrawBottomBar(new Rect(inRect.x, listRect.yMax + Spacing, inRect.width, BottomBarHeight));
    }

    private void DrawList(Rect rect)
    {
        float stride = RowHeight + Spacing;
        float totalHeight = items.Count * stride + 30f + Spacing;

        Rect viewRect = new(0f, 0f, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        for (int i = 0; i < items.Count; i++)
            DrawRow(new Rect(0f, i * stride, viewRect.width, RowHeight), i);

        if (Widgets.ButtonText(new Rect(0f, items.Count * stride, viewRect.width, 30f),
                "MST.AddEntry".Translate().RawText))
        {
            try { items.Add(Activator.CreateInstance(elementType)!); }
            catch (Exception e) { Log.Error($"[BalanceTweak] 无法创建 {elementType.FullName}：{e.Message}"); }
        }

        Widgets.EndScrollView();
    }

    private void DrawRow(Rect rect, int index)
    {
        var item = items[index];
        int count = fields.Count;

        float available = rect.width - DeleteButtonWidth - Spacing;
        float slot = count > 0 ? (available - Spacing * (count - 1)) / count : available;

        float x = rect.x;
        foreach (var field in fields)
        {
            var labelRect = new Rect(x, rect.y + 2f, slot, RowLabelHeight);
            var fieldRect = new Rect(x, rect.y + RowLabelHeight + 2f, slot, RowHeight - RowLabelHeight - 4f);

            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, StockGeneratorFieldUi.LabelOf(field));
            Text.Font = prevFont;
            Text.Anchor = TextAnchor.UpperLeft;

            StockGeneratorFieldUi.DrawField(fieldRect, field, item, this);
            x += slot + Spacing;
        }

        var deleteRect = new Rect(rect.x + available + Spacing, rect.y + (rect.height - 30f) / 2f,
            DeleteButtonWidth, 30f);
        if (Widgets.ButtonText(deleteRect, "X")) items.RemoveAt(index);
    }

    protected override void OnSave()
    {
        var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items)
            if (item != null) result.Add(item);
        onSave(result.Count > 0 ? result : null);
        Close();
    }

    protected override void OnCancel() => Close();
}
