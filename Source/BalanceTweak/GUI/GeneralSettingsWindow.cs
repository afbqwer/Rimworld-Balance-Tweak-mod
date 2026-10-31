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

    // 高度按内容估的：多了「主按钮」一节后 600 会把最下面的「移除所有数据」按钮切掉
    public override Vector2 InitialSize => new(480f, 670f);

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

        // === 底部主按钮 ===
        DrawSectionHeader(ref curY, inRect.width, "MST.MainButtonSection".Translate().RawText);
        Rect mainButtonRect = new(10f, curY, inRect.width - 20f, RowHeight);
        // 不需要额外「应用」：主按钮每帧按这个开关重新算可见性
        Widgets.CheckboxLabeled(mainButtonRect, "MST.ShowMainButton".Translate().RawText, ref showMainButton);
        TooltipHandler.TipRegion(mainButtonRect, "MST.ShowMainButtonTooltip".Translate().RawText);
        curY += RowHeight;

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

        // 按钮/说明文案按「点下去会不会改数值」分成两档：
        //   Def 缺失 / 父项缺失 ⇒ 读档时就被跳过，从未写过游戏数据 ⇒ 只是删记录；
        //   内部引用解析失败 ⇒ 已把能解析的字段写进 Def ⇒ 必须先回滚再删。
        // 以前四个按钮共用一个「移除」口径，用户没法判断后果（见 ItemsToRemoveWindow 的类注释）。
        string noopNotice = "MST.CleanNoticeNoop".Translate().RawText;
        string rollbackNotice = "MST.CleanNoticeRollback".Translate().RawText;
        string discardLabel = "MST.ConfirmDiscard".Translate().RawText;

        // 清除失效记录：Def 找不到，条目在读档时就被跳过，游戏数值从未被改过
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedDirectItems.Select(d => (d, "MST.FailDefNotFound")).ToList(),
            "MST.RemoveFailedDirect".Translate().RawText,
            TweakUtility.RemoveFailedDirectItems,
            noopNotice);

        // 清除失效记录：父项找不到 / 父项结构已变，同上，只删记录
        // 原因逐条给：同一个列表里混着两种成因，用一个键糊过去就会把"父项还在"说成"父项已不存在"。
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedDelayedItems.Select(p => (p.data, DelayedReasonKey(p.cause))).ToList(),
            "MST.RemoveFailedDelayed".Translate().RawText,
            TweakUtility.RemoveFailedDelayedItems,
            noopNotice);

        // 放弃修改并回滚：这类条目已部分生效，清理前必须把 Def 写回原值
        curY += DrawCleanupButton(ref curY, inRect.width,
            TweakDatabase.failedResolveItems.Select(d => (d, "MST.FailResolveDefs")).ToList(),
            "MST.RemoveFailedResolve".Translate().RawText,
            TweakUtility.RemoveFailedResolveItems,
            rollbackNotice,
            noticeIsDestructive: true,
            confirmLabel: discardLabel);

        // 清理全部错误项：三个列表合并，说明行按实际构成选
        curY += DrawCleanupButtonForAllFailed(ref curY, inRect.width, noopNotice, rollbackNotice, discardLabel);

        // 分隔线
        Rect lineRect = new(10f, curY, inRect.width - 20f, 1f);
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(lineRect.x, lineRect.y, lineRect.width);
        GUI.color = Color.white;
        curY += 10f;

        // 重置所有数据（在列表窗口中二次确认）
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

    /// <summary>延迟项的失败原因 → 文案键。原因在数据侧（<see cref="TweakDatabase.DelayedFailCause"/>），键只放 UI 侧。</summary>
    private static string DelayedReasonKey(TweakDatabase.DelayedFailCause cause) => cause switch
    {
        TweakDatabase.DelayedFailCause.ParentMismatch => "MST.FailParentMismatch",
        _ => "MST.FailParentNotFound",
    };

    /// <summary>
    /// 清理按钮：直接打开列表窗口，二次确认（含后果说明）在列表窗口中处理。
    /// </summary>
    /// <param name="items">待清理条目与各自的失败原因键（原因逐条给，见 <see cref="DelayedReasonKey"/>）。</param>
    /// <param name="notice">列表上方的后果说明（已翻译）；见 <see cref="ItemsToRemoveWindow"/>。</param>
    private float DrawCleanupButton(ref float curY, float width,
        List<(TweakData data, string reason)> items,
        string buttonLabel,
        System.Action cleanupAction,
        string? notice = null,
        bool noticeIsDestructive = false,
        string? confirmLabel = null)
    {
        Rect btnRect = new(10f, curY, width - 20f, ButtonHeight);
        int count = items.Count;

        // 构建按钮文字："移除xxx (N个)"
        string label = $"{buttonLabel} ({count})";

        if (Widgets.ButtonText(btnRect, label))
        {
            OpenChildWindow(new ItemsToRemoveWindow(items, buttonLabel, cleanupAction,
                notice, noticeIsDestructive, confirmLabel));
        }

        return ButtonHeight + 4f;
    }

    /// <summary>
    /// 合并清理按钮：显示总数，点击时合并三个列表。
    /// <para>
    /// 三个列表的后果不同，所以说明行按实际构成选：全是"没生效过"就说不改数值；
    /// 混了"已部分生效"就必须说清楚哪几条会被回滚 —— 合并按钮最容易让人以为只是清记录。
    /// </para>
    /// </summary>
    private float DrawCleanupButtonForAllFailed(ref float curY, float width,
        string noopNotice, string rollbackNotice, string discardLabel)
    {
        int noopCount = TweakDatabase.failedDirectItems.Count + TweakDatabase.failedDelayedItems.Count;
        int rollbackCount = TweakDatabase.failedResolveItems.Count;
        int totalFailed = noopCount + rollbackCount;

        Rect btnRect = new(10f, curY, width - 20f, ButtonHeight);
        string label = "MST.RemoveAllFailed".Translate().RawText + $" ({totalFailed})";

        if (Widgets.ButtonText(btnRect, label))
        {
            if (totalFailed > 0)
            {
                var itemList = new List<(TweakData, string)>();
                foreach (var data in TweakDatabase.failedDirectItems)
                    itemList.Add((data, "MST.FailDefNotFound"));
                foreach (var (data, cause) in TweakDatabase.failedDelayedItems)
                    itemList.Add((data, DelayedReasonKey(cause)));
                foreach (var data in TweakDatabase.failedResolveItems)
                    itemList.Add((data, "MST.FailResolveDefs"));

                string notice;
                if (rollbackCount == 0)
                    notice = noopNotice;
                else if (noopCount == 0)
                    notice = rollbackNotice;
                else
                    notice = "MST.CleanNoticeMixed".Translate(totalFailed, noopCount, rollbackCount).RawText;

                OpenChildWindow(new ItemsToRemoveWindow(itemList,
                    "MST.RemoveAllFailed".Translate().RawText, TweakUtility.RemoveAllFailedItems,
                    notice, noticeIsDestructive: rollbackCount > 0,
                    confirmLabel: rollbackCount > 0 ? discardLabel : null));
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
                // 这条是"把所有修改写回原值"，跟清理错误项是两回事 —— 说明与确认文案都按重置口径给。
                OpenChildWindow(new ItemsToRemoveWindow(itemList,
                    "MST.RemoveAllData".Translate().RawText, TweakUtility.ApplyFullReset,
                    "MST.CleanNoticeResetAll".Translate().RawText,
                    noticeIsDestructive: true,
                    confirmLabel: "MST.ConfirmReset".Translate().RawText));
            }
        }

        curY += ButtonHeight;
    }
}