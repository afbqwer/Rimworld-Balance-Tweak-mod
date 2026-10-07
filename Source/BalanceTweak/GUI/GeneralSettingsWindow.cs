using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

public class GeneralSettingsWindow : Window
{
    private const float SectionPadding = 10f;
    private const float RowHeight = 30f;
    private const float ButtonHeight = 28f;
    private const float LabelWidth = 120f;

    public List<Window> childWindows = new();

    public GeneralSettingsWindow()
    {
        doCloseX = true;
        forcePause = false;
        draggable = true;
        resizeable = true;
        closeOnClickedOutside = true;
        optionalTitle = "MST.GeneralSettings".Translate().RawText;
    }

    private void OpenChildWindow(Window window)
    {
        childWindows.Add(window);
        Find.WindowStack.Add(window);
    }

    public override void Notify_ClickOutsideWindow()
    {
        childWindows.RemoveAll(w => !Find.WindowStack.IsOpen(w));
        if (childWindows.Count > 0)
            return;
        childWindows.Clear();
        Close();
    }

    public override Vector2 InitialSize => new(480f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        float curY = 0f;

        // === 列宽度设置 ===
        DrawSectionHeader(ref curY, inRect.width, "MST.Name".Translate().RawText + " / " + "MST.Value".Translate().RawText + " Width");
        DrawWidthSlider(ref curY, inRect.width, "MST.NameColWidth".Translate().RawText, ref NameColWidth, 100, 400);
        DrawWidthSlider(ref curY, inRect.width, "MST.DataColWidth".Translate().RawText, ref DataColWidth, 50, 100);

        curY += SectionPadding;

        // === 主窗口大小设置 ===
        DrawSectionHeader(ref curY, inRect.width, "MST.MainWindowSize".Translate().RawText);
        int prevWidth = MainWindowWidth;
        int prevHeight = MainWindowHeight;
        DrawWidthSlider(ref curY, inRect.width, "MST.MainWindowWidth".Translate().RawText, ref MainWindowWidth, 800, 1920, 5);
        DrawWidthSlider(ref curY, inRect.width, "MST.MainWindowHeight".Translate().RawText, ref MainWindowHeight, 480, 1080, 5);
        if (MainWindowWidth != prevWidth || MainWindowHeight != prevHeight)
        {
            BalanceTweakMod.RecenterWindow();
        }

        curY += SectionPadding;

        // === 打开配置文件目录 ===
        DrawSectionHeader(ref curY, inRect.width, "MST.OpenconfigfolderButton".Translate().RawText);
        Rect colCfgBtnRect = new(10f, curY, inRect.width - 20f, ButtonHeight);
        if (Widgets.ButtonText(colCfgBtnRect, "MST.Openconfigfolder".Translate().RawText))
        {
            Application.OpenURL(GenFilePaths.ConfigFolderPath);
        }
        curY += ButtonHeight + SectionPadding;

        // === 数据清理 ===
        DrawSectionHeader(ref curY, inRect.width, "MST.DataCleanup".Translate().RawText);

        // 移除解析失败的直接项
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedDirectItems,
            "MST.RemoveFailedDirect".Translate().RawText,
            "MST.FailDefNotFound",
            TweakUtility.RemoveFailedDirectItems);

        // 移除解析失败的延迟项
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedDelayedItems,
            "MST.RemoveFailedDelayed".Translate().RawText,
            "MST.FailParentNotFound",
            TweakUtility.RemoveFailedDelayedItems);

        // 移除内部解析失败的数据
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedResolveItems,
            "MST.RemoveFailedResolve".Translate().RawText,
            "MST.FailResolveDefs",
            TweakUtility.RemoveFailedResolveItems);

        // 移除所有错误项
        int totalFailed = TweakDatabase.failedDirectItems.Count
                        + TweakDatabase.failedDelayedItems.Count
                        + TweakDatabase.failedResolveItems.Count;
        curY += DrawCleanupButtonForAllFailed(ref curY, inRect.width, totalFailed);

        // 分隔线
        Rect lineRect = new(10f, curY, inRect.width - 20f, 1f);
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(lineRect.x, lineRect.y, lineRect.width);
        GUI.color = Color.white;
        curY += 10f;

        // 移除所有数据（在列表窗口中二次确认）
        DrawRemoveAllButton(ref curY, inRect.width);
    }

    private void DrawSectionHeader(ref float curY, float width, string label)
    {
        Text.Font = GameFont.Medium;
        GUI.color = Color.cyan;
        Widgets.Label(new Rect(10f, curY, width - 20f, RowHeight), label);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        curY += RowHeight;
    }

    private static void DrawWidthSlider(ref float curY, float width, string label, ref int target, int min, int max, int step = 1)
    {
        Rect labelRect = new(10f, curY, LabelWidth, RowHeight);
        Widgets.Label(labelRect, label);

        float val = target;
        Rect sliderRect = new(LabelWidth + 20f, curY, width - LabelWidth - 80f, RowHeight);
        val = Widgets.HorizontalSlider(sliderRect, val, min, max, true);
        target = Mathf.RoundToInt(val / step) * step;

        Rect valueRect = new(width - 60f, curY, 50f, RowHeight);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(valueRect, target.ToString());
        Text.Anchor = TextAnchor.UpperLeft;

        curY += RowHeight;
    }

    /// <summary>直接打开列表窗口，二次确认在列表窗口中处理</summary>
    private float DrawCleanupButton(ref float curY, float width,
        List<TweakData> items,
        string buttonLabel,
        string reasonKey,
        System.Action cleanupAction)
    {
        Rect btnRect = new(10f, curY, width - 20f, ButtonHeight);
        int count = items.Count;

        // 构建按钮文字："移除xxx (N个)"
        string label = $"{buttonLabel} ({count})";

        if (Widgets.ButtonText(btnRect, label))
        {
            var itemList = items.Select(data => (data, reasonKey)).ToList();
            OpenChildWindow(new ItemsToRemoveWindow(itemList, buttonLabel, cleanupAction));
        }

        return ButtonHeight + 4f;
    }

    /// <summary>合并清理按钮：显示总数，点击时合并三个列表</summary>
    private float DrawCleanupButtonForAllFailed(ref float curY, float width, int totalFailed)
    {
        Rect btnRect = new(10f, curY, width - 20f, ButtonHeight);
        string label = "MST.RemoveAllFailed".Translate().RawText + $" ({totalFailed})";

        if (Widgets.ButtonText(btnRect, label))
        {
            if (totalFailed > 0)
            {
                var itemList = new List<(TweakData, string)>();
                foreach (var data in TweakDatabase.failedDirectItems)
                    itemList.Add((data, "MST.FailDefNotFound"));
                foreach (var data in TweakDatabase.failedDelayedItems)
                    itemList.Add((data, "MST.FailParentNotFound"));
                foreach (var data in TweakDatabase.failedResolveItems)
                    itemList.Add((data, "MST.FailResolveDefs"));
                OpenChildWindow(new ItemsToRemoveWindow(itemList, "MST.RemoveAllFailed".Translate().RawText, TweakUtility.RemoveAllFailedItems));
            }
        }

        return ButtonHeight + 4f;
    }

    private void DrawRemoveAllButton(ref float curY, float width)
    {
        Rect btnRect = new(10f, curY, width - 20f, ButtonHeight);
        int count = tweakDatas.Count;
        string label = "MST.RemoveAllData".Translate().RawText + $" ({count})";

        if (Widgets.ButtonText(btnRect, label))
        {
            if (count > 0)
            {
                var allModifiedItems = tweakDatas.Values.ToList();
                var itemList = allModifiedItems.Select(data => (data, "MST.AllDataWillBeRemoved")).ToList();
                OpenChildWindow(new ItemsToRemoveWindow(itemList, "MST.RemoveAllData".Translate().RawText, TweakUtility.ApplyFullReset));
            }
        }

        curY += ButtonHeight;
    }
}