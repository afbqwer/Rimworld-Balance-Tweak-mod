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

    /// <summary>
    /// 提交编辑结果。**空列表 = 清空**（从集合里移除全部项），走显式 Cleared 路径；
    /// 非空 = 普通赋值。
    ///
    /// <para>
    /// <b>为什么空列表不能当普通赋值</b>：那样字段是"空列表 + 已修改"，而 <c>StateOf</c> 要求
    /// Set 必须非空（空集合按 Unset 处理）⇒ <c>ApplyL</c> 静默跳过，用户把行删光保存后
    /// Def 上的旧条目一个都没少，界面却显示已经空了。改成显式 Cleared 后，
    /// "删光 = 清空"才真的落到 Def 上（写空列表，不是 null）。
    /// </para>
    /// </summary>
    /// <param name="finalList"><c>null</c> 或空列表都表示清空；子窗口回调原样收到它本来的值。</param>
    protected void CommitResult(List<T>? finalList)
    {
        if (onSaveCallback != null)
        {
            // 子窗口（过滤器里的分类 / 标签等）自己决定 null 与空列表怎么存，维持原有约定。
            onSaveCallback(finalList);
            return;
        }
        if (finalList == null || finalList.Count == 0) config!.ClearValue(data!);
        else config!.ApplyValue(data!, finalList);
    }

    protected virtual void SaveAndClose()
    {
        var finalList = workingList.Where(IsItemValid).ToList();
        CommitResult(finalList.Count > 0 ? finalList : null);
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