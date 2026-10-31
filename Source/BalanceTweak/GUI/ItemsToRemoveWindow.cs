using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 清理确认窗：列出将要被丢弃的条目，逐条给出失败原因。
/// <para>
/// <b>为什么要有 <c>notice</c> 这一行</b>：三类错误项的语义完全不同 ——
/// 「Def 已不存在」「父项已不存在」这两类在读档时就被跳过了，从来没写过游戏数据，清理只是删存档里的记录；
/// 「内部引用解析失败」那一类则已经把它能解析的字段写进了 Def，清理等于放弃这条修改、把数据恢复成原值。
/// 两者的后果差一个量级，共用一个「移除」按钮而不说明，用户没法判断点下去会不会改数值。
/// 所以后果说明放在列表上方，确认按钮按场景换文案（<c>confirmLabel</c>）。
/// </para>
/// </summary>
public class ItemsToRemoveWindow : Window
{
    private readonly List<(TweakData data, string reason)> items;
    private readonly Action onConfirm;
    private readonly string? notice;
    private readonly bool noticeIsDestructive;
    private readonly string? confirmLabel;
    private Vector2 scrollPosition = Vector2.zero;

    private const float RowHeight = 28f;
    private const float ButtonBarHeight = 40f;
    private const float NoticePadding = 8f;

    /// <param name="notice">列表上方的后果说明（已翻译）。空 = 不显示。</param>
    /// <param name="noticeIsDestructive">说明是否为"会改动游戏数据"，决定文字颜色。</param>
    /// <param name="confirmLabel">确认按钮文案（已翻译）。空 = 用默认的「确认移除」。</param>
    public ItemsToRemoveWindow(
        List<(TweakData data, string reason)> items,
        string title,
        Action onConfirm,
        string? notice = null,
        bool noticeIsDestructive = false,
        string? confirmLabel = null)
    {
        this.items = items;
        this.items.SortBy(t => t.data.id.defName);
        this.onConfirm = onConfirm;
        this.notice = notice;
        this.noticeIsDestructive = noticeIsDestructive;
        this.confirmLabel = confirmLabel;
        doCloseX = true;
        forcePause = false;
        draggable = true;
        resizeable = true;
        optionalTitle = title;
        closeOnClickedOutside = true;
    }

    public override Vector2 InitialSize => new(800f, 600f);

    public override void Notify_ClickOutsideWindow()
    {
        Close();
    }

    public override void DoWindowContents(Rect inRect)
    {
        float top = DrawNotice(inRect);

        // 列表区域（窗口可缩放：拖得比说明还矮时不能给 BeginScrollView 负高度）
        Rect listRect = new(0, top, inRect.width,
            Mathf.Max(0f, inRect.height - ButtonBarHeight - 10f - top));
        DrawItemList(listRect);

        // 按钮区域
        Rect buttonRect = new(0, inRect.height - ButtonBarHeight, inRect.width, ButtonBarHeight);
        DrawButtons(buttonRect);
    }

    /// <summary>画后果说明（有内容才占高度），返回列表的起始 y。</summary>
    private float DrawNotice(Rect inRect)
    {
        if (string.IsNullOrEmpty(notice)) return 0f;

        Text.Font = GameFont.Small;
        float width = inRect.width - NoticePadding * 2f;
        float height = Text.CalcHeight(notice, width);
        Rect rect = new(NoticePadding, 0f, width, height);

        Color prev = GUI.color;
        // 会改数据的那一档用暖色：屏幕上先要看到"这条点下去数值会变"。
        GUI.color = noticeIsDestructive ? new Color(1f, 0.72f, 0.45f) : new Color(0.72f, 0.72f, 0.72f);
        Widgets.Label(rect, notice);
        GUI.color = prev;

        return height + NoticePadding;
    }

    private void DrawItemList(Rect rect)
    {
        Text.Font = GameFont.Small;
        float viewWidth = rect.width - 16f;
        float viewHeight = items.Count * RowHeight;
        Rect viewRect = new(0, 0, viewWidth, viewHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float y = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var (data, reason) = items[i];
            Rect rowRect = new(0, y, viewWidth, RowHeight);

            if (i % 2 == 0)
                Widgets.DrawAltRect(rowRect);

            // settingType (左侧)
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Rect labelRect = new(10f, y, viewWidth * 0.15f, RowHeight);
            GUI.color = Color.white;
            Widgets.Label(labelRect, data.id.settingType.ToString());

            // defName (中间)
            Rect idRect = new(viewWidth * 0.15f + 10f, y, viewWidth * 0.45f, RowHeight);

            Widgets.Label(idRect, data.id.defName);


            // reason (右侧)
            Rect reasonRect = new(viewWidth * 0.65f + 10f, y, viewWidth * 0.35f - 20f, RowHeight);
            GUI.color = new Color(1f, 0.6f, 0.6f);
            Widgets.Label(reasonRect, reason.Translate().RawText);
            GUI.color = Color.white;

            y += RowHeight;
        }

        Widgets.EndScrollView();
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawButtons(Rect rect)
    {
        float btnWidth = 120f;
        float gap = 10f;
        float totalWidth = btnWidth * 2 + gap;
        float startX = rect.x + (rect.width - totalWidth) / 2f;

        // 确认按钮
        Rect confirmRect = new(startX, rect.y + 5f, btnWidth, rect.height - 10f);
        if (Widgets.ButtonText(confirmRect, confirmLabel ?? "MST.ConfirmRemove".Translate().RawText))
        {
            onConfirm?.Invoke();
            Close();
        }

        // 取消按钮
        Rect cancelRect = new(startX + btnWidth + gap, rect.y + 5f, btnWidth, rect.height - 10f);
        if (Widgets.ButtonText(cancelRect, "MST.CancelRemove".Translate().RawText))
        {
            Close();
        }
    }
}
