using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public abstract class ListEditorWindow<T> : EditorWindowBase
{
    protected List<T> workingList = null!;
    protected List<T> originalList = null!;
    protected Action<List<T>?>? onSaveCallback;
    protected Vector2 scrollPosition;

    protected const float EntryHeight = 30f;
    protected const float DeleteButtonWidth = 30f;
    protected const float ButtonHeight = 30f;

    /// <summary>行内固定标签（Degree / Count 这类短词）占的宽度。按中英双语里较长的那一侧留的。</summary>
    protected const float StyleLabelWidth = 44f;

    protected ListEditorWindow() { }

    protected ListEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
    }

    protected ListEditorWindow(string title, Action<List<T>?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
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

        Rect listRect = new(inRect.x, curY, inRect.width,
            inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing);
        DrawListArea(listRect);
        curY = listRect.yMax + Spacing;

        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    protected virtual bool HasAddButton => true;

    protected virtual void DrawListArea(Rect rect)
    {
        float addButtonHeight = HasAddButton ? ButtonHeight + Spacing : 0f;
        float totalHeight = workingList.Count * (EntryHeight + Spacing) + addButtonHeight;

        Rect viewRect = new(0f, 0f, rect.width - 16f, totalHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        // 虚拟滚动：只绘制可见范围内的条目（上下各多留一行余量）
        float rowStride = EntryHeight + Spacing;
        int firstVisible = Mathf.Max(0, Mathf.FloorToInt((scrollPosition.y - EntryHeight) / rowStride));
        int lastVisible = Mathf.Min(workingList.Count - 1, Mathf.CeilToInt((scrollPosition.y + rect.height + EntryHeight) / rowStride));

        for (int i = firstVisible; i <= lastVisible; i++)
        {
            Rect entryRect = new(0f, i * rowStride, viewRect.width, EntryHeight);
            DrawEntry(entryRect, i);
        }

        if (HasAddButton)
        {
            float addButtonY = workingList.Count * rowStride;
            bool addButtonVisible = addButtonY + ButtonHeight >= scrollPosition.y && addButtonY <= scrollPosition.y + rect.height;
            if (addButtonVisible && Widgets.ButtonText(new Rect(0f, addButtonY, viewRect.width, ButtonHeight), AddButtonLabel))
            {
                OnAddItem();
            }
        }

        Widgets.EndScrollView();
    }

    protected abstract string AddButtonLabel { get; }
    protected abstract void OnAddItem();
    protected abstract void DrawEntry(Rect rect, int index);

    protected virtual bool IsItemValid(T item) => true;

    protected virtual void SaveAndClose()
    {
        var finalList = workingList.Where(IsItemValid).ToList();
        var result = finalList.Count > 0 ? finalList : null;
        if (onSaveCallback != null)
        {
            onSaveCallback(result);
        }
        else
        {
            config!.ApplyValue(data!, result);
        }
        Close();
    }

    /// <summary>
    /// 列表 → 多行文本（剪贴板复制 / 拖拽涂抹取值）。
    ///
    /// <para>
    /// <b>默认走字段表通用编解码</b>：只要 <c>T</c> 已进 <see cref="PocoCodec"/> 的字段表，
    /// 这里就是完整实现 —— 17 个子类因此不必再各写一遍转发。仍然需要覆写的只有三类：
    /// 元素类型不在字段表里（<c>int</c> / <c>IngredientCount</c>）、格式特殊（<c>string</c> /
    /// <c>StockGenerator</c>）、以及刻意不支持剪贴板的（两个 giver，覆写成返回 <c>null</c>）。
    /// </para>
    /// <para>
    /// <b>别把 <c>virtual</c> 读成"随便"</b>：元素类型未声明、子类又没覆写时，默认实现返回
    /// <c>null</c>（而不是静默算出一串垃圾）—— 那是**可见**的失效（复制/涂抹没反应），不是数据损坏。
    /// 契约由游戏外测试 <c>ListEditorWindowContractTests</c> 枚举全部子类钉住。
    /// </para>
    /// </summary>
    protected virtual string? SerializeList()
    {
        TweakData.FieldCodecs.EnsureBuilt();   // 见该方法注释：不碰这一下，IsDeclared 全判 false
        return PocoCodec.EncodeList(workingList);
    }

    /// <summary>
    /// 多行文本 → 列表。任一行解析失败 ⇒ 保持原列表不动（<see cref="PocoCodec.DecodeList(Type, string)"/>
    /// 返回 <c>null</c>），绝不写入半截数据。
    /// </summary>
    protected virtual void DeserializeList(string data)
    {
        TweakData.FieldCodecs.EnsureBuilt();
        if (PocoCodec.DecodeList(typeof(T), data) is List<T> list) workingList = list;
    }

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    protected override string? GetCopyData() => SerializeList();
    protected override bool OnPasteData(string data)
    {
        try
        {
            DeserializeList(data);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] Paste failed: {ex.Message}");
            return false;
        }
    }
}