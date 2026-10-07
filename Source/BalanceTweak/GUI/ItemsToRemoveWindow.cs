using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class ItemsToRemoveWindow : Window
{
    private readonly List<(TweakData data, string reason)> items;
    private readonly Action onConfirm;
    private Vector2 scrollPosition = Vector2.zero;

    private const float RowHeight = 28f;
    private const float ButtonBarHeight = 40f;

    public ItemsToRemoveWindow(
        List<(TweakData data, string reason)> items,
        string title,
        Action onConfirm)
    {
        this.items = items;
        this.items.SortBy(t=>t.data.id.defName);
        this.onConfirm = onConfirm;
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
        // 列表区域
        Rect listRect = new(0, 0, inRect.width, inRect.height - ButtonBarHeight - 10f);
        DrawItemList(listRect);

        // 按钮区域
        Rect buttonRect = new(0, inRect.height - ButtonBarHeight, inRect.width, ButtonBarHeight);
        DrawButtons(buttonRect);
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
        if (Widgets.ButtonText(confirmRect, "MST.ConfirmRemove".Translate().RawText))
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