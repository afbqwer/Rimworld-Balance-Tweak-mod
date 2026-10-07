using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

public partial class BalanceTweakSettings : ModSettings
{
    #region 常量定义
    private const float LineHeight = 26f;
    private const float TabTopHeight = 60f;
    public static int NameColWidth = 230;
    public static int DataColWidth = 74;
    public static int MainWindowWidth = 1280;
    public static int MainWindowHeight = 720;
    private const float IconSize = 30f;
    private const float IconSizePadded = 35f;
    private const float ResetButtonWidth = 30f;
    private const float Contracted = 2f;
    #endregion

    #region 数据与状态
    // 静态数据
    public static Dictionary<TweakID, TweakData> tweakDatas = new();
    public static Dictionary<SettingType, Dictionary<TweakID, TweakData>> cachedData = new();
    public static HashSet<TweakID> pinnedIds = new();

    // 筛选与缓存
    private static List<TweakData> filteredData = new();
    private static List<StatColumnConfig> allColumnSource = new();
    private static List<StatColumnConfig> visibleColumns = new();
    public static Dictionary<SettingType, List<string>> hiddenColumnIds = new();
    public static List<string> CurrentHiddenColumns
    {
        get
        {
            if (!hiddenColumnIds.ContainsKey(curType))
                hiddenColumnIds[curType] = new();
            return hiddenColumnIds[curType];
        }
    }
    private static List<string> availableModPacks = new();
    private static bool filterDirty = true;
    private static bool cached = false;
    public static Dictionary<SettingType, Dictionary<string, int>> columnOrders = new();

    // UI 状态
    public static SortModeType curSortMode = SortModeType.NoSort;
    public static StatColumnConfig? selectedCol = null;
    /// <summary>当前排序的列（可通过右键单独设置，左键同时设置选中与排序）</summary>
    public static StatColumnConfig? sortCol = null;
    public static float batchFlat = 0f;
    public static float batchPrec = 1f;
    public static float batchLowerLimit = -9999999999f;
    public static float batchUpperLimit = 9999999999f;
    private static TweakData? needRemove = null;
    private static string? editingControlName = null;
    private static string? editingBuffer = null;
    private static TweakData? editingControlData = null;
    private static StatColumnConfig? editingControlConfig = null;
    private static bool dragging = false;
    // 表头列拖拽状态
    private static StatColumnConfig? draggingHeaderColumn;
    private static int dragHeaderTargetIndex = -1;
    private static bool headerDragActive = false;
    // 按下位置追踪（延迟拖拽启动）
    private static StatColumnConfig? dragMouseDownColumn;
    private static Vector2 dragStartMousePos;
    // 拖拽结束后禁止选中列的剩余帧数
    private static int dragEndFrameCount;
    public static bool showChanged = false;
    public static bool showAbstract = false;
    public static bool showDisplayMode = false;

    // 筛选状态
    public static SettingType curType = SettingType.Race;
    private static string filterModPack = "All";
    private static int filterPropType = 0;
    private static List<string> filterStr = [];

    private static QuickSearchFilter searchFilter = new();
    private static TweakData? jumpData = null;

    // 跳转高亮
    private static TweakData? jumpHighlightData = null;
    private static int jumpHighlightFrame = 0;

    // 滚动位置
    private static Vector2 dataScrollPosition = Vector2.zero;
    private static Vector2 headerScrollPosition = Vector2.zero;
    private static float jumpdataScrollY = -1;

    // 右键菜单状态
    private static Vector2 rightClickStartPos;
    private static bool rightClickDown;
    private static TweakData? rightClickData;
    private static StatColumnConfig? rightClickColumnConfig;

    // 每帧缓存的焦点控件名称
    private static string _focusedControlCache = "";
    // 面板移动时请求取消输入框聚焦，在当帧 Repaint 时机释放键盘焦点
    private static bool cancelFocusPending;

    #endregion
    #region 初始化
    private readonly static List<string> curSettingTypeStr = Enum.GetNames(typeof(SettingType)).Select(s => "MST." + s).ToList();
    /// <summary>
    /// 页签下拉的索引 → 枚举值映射。Enum.GetNames 与 Enum.GetValues 的排序一致，
    /// 所以第 i 项对应的就是这个列表的第 i 项 —— 不再依赖"枚举值必须从 0 起连续"这一隐含约束。
    /// </summary>
    private readonly static List<SettingType> curSettingTypeValues = Enum.GetValues(typeof(SettingType)).Cast<SettingType>().ToList();
    public enum SortModeType
    {
        NoSort,
        Ascending,
        Descending,
    }
    private readonly static List<string> sortModeTypeStr = Enum.GetNames(typeof(SortModeType)).Select(s => "MST." + s).ToList();
    static BalanceTweakSettings()
    {
        foreach (SettingType settingType in typeof(SettingType).GetEnumValues())
        {
            cachedData[settingType] = new Dictionary<TweakID, TweakData>();
        }
    }

    // 编辑器窗口工厂 - 将 ColumnStyle 映射到对应的编辑器窗口构造函数
    private static readonly Dictionary<ColumnStyle, Func<StatColumnConfig, TweakData, Window>> EditorWindowFactory = new()
    {
        // 按钮式编辑器
        { ColumnStyle.String, (c, d) => new StringEditorWindow(c, d) },
        { ColumnStyle.Curve, (c, d) => new CurveEditorWindow(c, d) },
        { ColumnStyle.Flags, (c, d) => new FlagsEditorWindow(c, d) },
        { ColumnStyle.DefSelector, (c, d) => new DefSelectionWindow(c, d) },
        { ColumnStyle.IngredientFilter, (c, d) => new IngredientFilterEditorWindow(c, d) },
        // 列表编辑器
        { ColumnStyle.StatModList, (c, d) => new StatModListEditorWindow(c, d) },
        { ColumnStyle.CapModList, (c, d) => new CapModListEditorWindow(c, d) },
        { ColumnStyle.DamageModList, (c, d) => new DamageModListEditorWindow(c, d) },
        { ColumnStyle.DefList, (c, d) => new DefListEditorWindow(c, d) },
        { ColumnStyle.HediffGiverList, (c, d) => new HediffGiverListEditorWindow(c, d) },
        { ColumnStyle.MentalStateGiverList, (c, d) => new MentalStateGiverListEditorWindow(c, d) },
        { ColumnStyle.SkillGainList, (c, d) => new SkillGainListEditorWindow(c, d) },
        { ColumnStyle.AptitudeList, (c, d) => new AptitudeListEditorWindow(c, d) },
        { ColumnStyle.GeneticTraitList, (c, d) => new GeneticTraitListEditorWindow(c, d) },
        { ColumnStyle.StringList, (c, d) => new StringListEditorWindow(c, d) },
        { ColumnStyle.IntList, (c, d) => new IntListEditorWindow(c, d) },
        { ColumnStyle.ThingDefCountList, (c, d) => new ThingDefCountListEditorWindow(c, d) },
        { ColumnStyle.IngredientList, (c, d) => new IngredientListEditorWindow(c, d) },
        { ColumnStyle.SkillReqList, (c, d) => new SkillReqListEditorWindow(c, d) },
        { ColumnStyle.TraitReqList, (c, d) => new TraitReqListEditorWindow(c, d) },
        { ColumnStyle.ProcessIngredientList, (c, d) => new ProcessIngredientListEditorWindow(c, d) },
        { ColumnStyle.ProcessResultList, (c, d) => new ProcessResultListEditorWindow(c, d) },
        { ColumnStyle.BodyPartTree, (c, d) => new BodyPartTreeEditorWindow(c, d) },
    };
    #endregion

    #region 数据存储
    public override void ExposeData()
    {
        Scribe_Collections.Look(ref tweakDatas, "tweakDatas", LookMode.Deep);
        // 序列化 hiddenColumnIds (按 SettingType 分组)
        List<string>? hiddenColumnIdsFlat = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            hiddenColumnIdsFlat = new();
            foreach (var typeKvp in hiddenColumnIds)
            {
                string prefix = typeKvp.Key.ToString() + "|";
                foreach (var colId in typeKvp.Value)
                {
                    hiddenColumnIdsFlat.Add(prefix + colId);
                }
            }
        }
        Scribe_Collections.Look(ref hiddenColumnIdsFlat, "hiddenColumnIds", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars && hiddenColumnIdsFlat != null)
        {
            hiddenColumnIds.Clear();
            foreach (string entry in hiddenColumnIdsFlat)
            {
                int separatorIndex = entry.IndexOf('|');
                if (separatorIndex > 0)
                {
                    string typeStr = entry.Substring(0, separatorIndex);
                    string colId = entry.Substring(separatorIndex + 1);
                    if (Enum.TryParse<SettingType>(typeStr, out var settingType))
                    {
                        if (!hiddenColumnIds.ContainsKey(settingType))
                            hiddenColumnIds[settingType] = new();
                        hiddenColumnIds[settingType].Add(colId);
                    }
                }
            }
        }
        // 序列化 columnOrders (按 SettingType 分组)
        List<string>? columnOrdersFlatKeys = null;
        List<int>? columnOrdersFlatValues = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            columnOrdersFlatKeys = new();
            columnOrdersFlatValues = new();
            foreach (var typeKvp in columnOrders)
            {
                string prefix = typeKvp.Key.ToString() + "|";
                foreach (var colKvp in typeKvp.Value)
                {
                    columnOrdersFlatKeys.Add(prefix + colKvp.Key);
                    columnOrdersFlatValues.Add(colKvp.Value);
                }
            }
        }
        Scribe_Collections.Look(ref columnOrdersFlatKeys, "columnOrdersKeys", LookMode.Value);
        Scribe_Collections.Look(ref columnOrdersFlatValues, "columnOrdersValues", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars && columnOrdersFlatKeys != null)
        {
            columnOrders.Clear();
            for (int i = 0; i < columnOrdersFlatKeys.Count; i++)
            {
                string key = columnOrdersFlatKeys[i];
                int separatorIndex = key.IndexOf('|');
                if (separatorIndex > 0)
                {
                    string typeStr = key.Substring(0, separatorIndex);
                    string colId = key.Substring(separatorIndex + 1);
                    if (Enum.TryParse<SettingType>(typeStr, out var settingType))
                    {
                        if (!columnOrders.ContainsKey(settingType))
                            columnOrders[settingType] = new();
                        columnOrders[settingType][colId] = columnOrdersFlatValues[i];
                    }
                }
            }
        }
        if (tweakDatas == null) tweakDatas = new();
        // 序列化 pinnedIds (通过 List<TweakID> 中转)
        List<TweakID>? pinnedList = null;
        if (Scribe.mode == LoadSaveMode.Saving)
            pinnedList = pinnedIds.ToList();
        Scribe_Collections.Look(ref pinnedList, "pinnedIds", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.LoadingVars && pinnedList != null)
            pinnedIds = new HashSet<TweakID>(pinnedList);
        if (pinnedIds == null) pinnedIds = new();
        Scribe_Values.Look(ref NameColWidth, "nameColWidth", 230);
        Scribe_Values.Look(ref DataColWidth, "dataColWidth", 74);
        Scribe_Values.Look(ref MainWindowWidth, "mainWindowWidth", 1280);
        Scribe_Values.Look(ref MainWindowHeight, "mainWindowHeight", 720);
        base.ExposeData();
    }
    #endregion

    #region 主绘制逻辑
    // Unity 的 IMGUI在每一帧会为不同的事件类型（EventType）多次调用 OnGUI(DoSettingsWindowContents) 方法。
    public void DoSettingsWindowContents(Rect inRect)
    {
        TweakUtility.InitAndUpdateData();

        GUI.BeginGroup(inRect);

        // 1. 顶部控制栏
        Rect tabTopRect = new(0, 0, inRect.width, TabTopHeight);
        DrawTabTop(tabTopRect);

        // 2. 分隔线
        float headerStartY = TabTopHeight + 5f;
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(0, headerStartY, inRect.width);
        Widgets.DrawLineVertical(NameColWidth, headerStartY, inRect.height - TabTopHeight); // 垂直分隔线
        GUI.color = Color.white;
        // 3. 表头
        Rect headerRect = new(0, headerStartY, inRect.width, LineHeight);
        DrawHeader(headerRect);

        // 4. 数据区域
        Rect dataRect = new(0, headerStartY + LineHeight + 4f, inRect.width, inRect.height - (headerStartY + LineHeight + 4f));
        DrawData(dataRect);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.EndGroup();
    }
    #endregion

    #region UI 组件绘制

    private static void DrawTabTop(Rect rect)
    {
        // 分为上下两行
        Rect bottomrow = rect.BottomHalf();
        Rect toprow = rect.TopHalf();

        float curX = toprow.x;

        // 当前列信息

        Text.Anchor = TextAnchor.MiddleCenter;
        string colName = selectedCol != null ? selectedCol.label.Translate().RawText : "MST.empty".Translate().RawText;   // 修改点
        Rect colInfoRect = new Rect(curX, toprow.y, toprow.width * 0.16f + Contracted, toprow.height).ContractedBy(Contracted);
        Widgets.Label(colInfoRect, "MST.CurrentColumn".Translate().RawText + colName);
        curX += colInfoRect.width;
        // 批量操作区
        Rect FlatRect = new Rect(curX, toprow.y, toprow.width * 0.10f + Contracted, toprow.height).ContractedBy(Contracted);
        curX += DrawLabeledInput(FlatRect, "MST.FlatInput".Translate().RawText, ref batchFlat, false);
        Rect PrecRect = new Rect(curX, toprow.y, toprow.width * 0.10f + Contracted, toprow.height).ContractedBy(Contracted);
        curX += DrawLabeledInput(PrecRect, "MST.PrecInput".Translate().RawText, ref batchPrec, true) + 4f;
        // 下限输入框
        Rect LowerLimitRect = new Rect(curX, toprow.y, toprow.width * 0.12f + Contracted, toprow.height).ContractedBy(Contracted);
        curX += DrawLabeledInput(LowerLimitRect, "MST.LowerLimit".Translate().RawText, ref batchLowerLimit, false);
        // 上限输入框
        Rect UpperLimitRect = new Rect(curX, toprow.y, toprow.width * 0.12f + Contracted, toprow.height).ContractedBy(Contracted);
        curX += DrawLabeledInput(UpperLimitRect, "MST.UpperLimit".Translate().RawText, ref batchUpperLimit, false) + 4f;
        // 确保下限 ≤ 上限
        if (batchLowerLimit > batchUpperLimit)
        {
            batchLowerLimit = batchUpperLimit;
        }

        // 批量应用按钮
        Rect applyRect = new Rect(curX, toprow.y, toprow.width * 0.08f + Contracted, toprow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(applyRect, "MST.Apply".Translate().RawText))
        {
            TweakUtility.ApplyBatchOperation();
        }
        TooltipHandler.TipRegion(applyRect, "MST.ApplyToolTip".Translate().RawText);
        curX += applyRect.width;

        // 打开列配置窗口按钮
        Rect openFolderRect = new Rect(curX, toprow.y, toprow.width * 0.08f + Contracted, toprow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(openFolderRect, "MST.ColumnSelectorTitle".Translate().RawText))
        {
            Find.WindowStack.Add(new ColumnSelectorWindow());
        }
        TooltipHandler.TipRegion(openFolderRect, "MST.ColumnConfig".Translate().RawText);
        curX += openFolderRect.width;

        // 总体设置按钮
        Rect colConfigBtn = new Rect(curX, toprow.y, toprow.width * 0.08f + Contracted, toprow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(colConfigBtn, "MST.GeneralSettings".Translate().RawText))
        {
            Find.WindowStack.Add(new GeneralSettingsWindow());
        }
        curX += colConfigBtn.width;

        // 重置此列按钮
        Rect resetColRect = new Rect(curX, toprow.y, toprow.width * 0.08f + Contracted, toprow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(resetColRect, "MST.ResetCol".Translate().RawText))
        {
            TweakUtility.ResetSelectedColumn();
        }
        TooltipHandler.TipRegion(resetColRect, "MST.ResetColTooltip".Translate().RawText);
        curX += resetColRect.width;

        // 重置当前项按钮
        Rect resetCurRect = new Rect(curX, toprow.y, toprow.width * 0.08f + Contracted, toprow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(resetCurRect, "MST.ResetCur".Translate().RawText))
        {
            var tweakedItems = filteredData
                .Where(d => d.tweaked)
                .Select(d => (d, "MST.ResetCur"))
                .ToList();
            Find.WindowStack.Add(new ItemsToRemoveWindow(
                tweakedItems,
                "MST.ResetCur".Translate().RawText,
                TweakUtility.ApplyCurReset));
        }
        TooltipHandler.TipRegion(resetCurRect, "MST.ResetCurTooltip".Translate().RawText);
        curX += resetCurRect.width;

        DrawFilters(bottomrow);
    }

    private static void DrawFilters(Rect bottomrow)
    {
        float curX = bottomrow.x;
        // 搜索框
        Rect searchInputTexture = new Rect(curX, bottomrow.y, bottomrow.height, bottomrow.height).ContractedBy(Contracted);
        curX += searchInputTexture.width;
        Rect searchInputRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.23f + Contracted, bottomrow.height).ContractedBy(Contracted);

        const string SearchControlName = "SearchBox";
        GUI.DrawTexture(searchInputTexture, TexButton.Search);
        GUI.SetNextControlName(SearchControlName);
        string newSearch = Widgets.TextField(searchInputRect, searchFilter.Text);
        if (newSearch != searchFilter.Text)
        {
            searchFilter.Text = newSearch;
            filterDirty = true;
        }

        curX += searchInputRect.width;

        // 主要类型筛选
        Rect typeFilterRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.09f + Contracted, bottomrow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(typeFilterRect, ("MST." + curType).Translate().RawText))
        {
            OpenFilterMenu(curSettingTypeStr, selectedIndex =>
            {
                curType = curSettingTypeValues[selectedIndex];
                filterPropType = 0;
                selectedCol = null;
                sortCol = null;
                filterDirty = true;
            });
        }
        curX += typeFilterRect.width;

        //  次要类型筛选
        Rect propFilterRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.09f + Contracted, bottomrow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(propFilterRect, filterStr.Count > 0 ? filterStr[filterPropType].Translate().RawText : "MST.empty".Translate().RawText))
        {
            if (filterStr.Count > 0)
                OpenFilterMenu(filterStr, selectedIndex =>
                {
                    filterPropType = selectedIndex;
                    selectedCol = null;
                    sortCol = null;
                    filterDirty = true;
                });
        }
        curX += propFilterRect.width;

        // Mod 筛选
        Rect modFilterRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.23f + Contracted, bottomrow.height).ContractedBy(Contracted);
        if (Widgets.ButtonText(modFilterRect, filterModPack))
        {
            OpenFilterMenu(availableModPacks, selection =>
            {
                filterModPack = selection;
                filterDirty = true;
            });
        }
        curX += modFilterRect.width;

        //  排序选择
        Rect sortModeRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.08f + Contracted, bottomrow.height).ContractedBy(Contracted);
        bool sortC = sortCol != null;
        var s = sortModeTypeStr[(int)curSortMode].Translate().RawText;
        if (sortC)
        {
            if (Widgets.ButtonText(sortModeRect, s))
            {
                OpenFilterMenu(sortModeTypeStr, selectedIndex =>
                {
                    curSortMode = (SortModeType)selectedIndex;
                    filterDirty = true;
                });
            }
        }
        else
        {
            Widgets.Label(sortModeRect, s);
        }
        curX += sortModeRect.width;
        // 显示已修改
        Rect showChangedRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.09f + Contracted, bottomrow.height).ContractedBy(Contracted);
        TooltipHandler.TipRegion(showChangedRect, "MST.showChangedTooltip".Translate().RawText);

        Widgets.CheckboxLabeled(showChangedRect, "MST.showChanged".Translate().RawText, ref showChanged);
        curX += showChangedRect.width;

        // 显示抽象值
        Rect showAbstractRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.09f + Contracted, bottomrow.height).ContractedBy(Contracted);
        TooltipHandler.TipRegion(showAbstractRect, "MST.showAbstractTooltip".Translate().RawText);
        Widgets.CheckboxLabeled(showAbstractRect, "MST.showAbstract".Translate().RawText, ref showAbstract);
        curX += showAbstractRect.width;

        // 数据查看模式
        Rect showDisplayModeRect = new Rect(curX, bottomrow.y, bottomrow.width * 0.08f + Contracted, bottomrow.height).ContractedBy(Contracted);
        TooltipHandler.TipRegion(showDisplayModeRect, "MST.showDisplayModeTooltip".Translate().RawText);
        Widgets.CheckboxLabeled(showDisplayModeRect, "MST.showDisplayMode".Translate().RawText, ref showDisplayMode);
        curX += showDisplayModeRect.width;
    }
    /// <summary>绘制表头</summary>
    private static void DrawHeader(Rect rect)
    {
        float totalColWidth = DataColWidth * visibleColumns.Count;
        Text.Anchor = TextAnchor.MiddleLeft;
        // 固定列：名称
        Rect nameHeaderRect = new(rect.x + IconSizePadded, rect.y, NameColWidth - IconSizePadded, LineHeight);
        Widgets.Label(nameHeaderRect, $"<b>{"MST.Name".Translate().RawText}</b>");
        Text.Anchor = TextAnchor.MiddleCenter;
        // 滚动列：属性
        Rect scrollOutRect = new(rect.x + NameColWidth, rect.y, rect.width - NameColWidth, LineHeight);
        Rect scrollViewRect = new(0, 0, totalColWidth + 24f, LineHeight + 24f);

        // 如果拖拽标记存在但鼠标左键实际已松开，清理拖拽状态（处理鼠标在窗口外释放的情况）
        if (draggingHeaderColumn != null && !Input.GetMouseButton(0))
        {
            if (headerDragActive)
            {
                ApplyHeaderDragOrder(draggingHeaderColumn, dragHeaderTargetIndex);
                dragEndFrameCount = 2;
                if (Event.current.type == EventType.MouseUp)
                    Event.current.Use();
            }
            draggingHeaderColumn = null;
            dragHeaderTargetIndex = -1;
            headerDragActive = false;
            dragMouseDownColumn = null;
        }
        // 鼠标未移动就松开时清理按下记录（点击但不拖拽的情况）
        if (dragMouseDownColumn != null && !Input.GetMouseButton(0))
        {
            dragMouseDownColumn = null;
        }

        Vector2 scrollPos = new(headerScrollPosition.x, 0);
        Widgets.BeginScrollView(scrollOutRect, ref scrollPos, scrollViewRect, false);
        headerScrollPosition.x = scrollPos.x;

        float mouseContentX = Event.current.mousePosition.x;

        for (int i = 0; i < visibleColumns.Count; i++)
        {
            Rect headerCell = new(DataColWidth * i, 0, DataColWidth, LineHeight);

            // === 拖拽逻辑 ===
            bool isMouseOverCell = mouseContentX >= headerCell.x && mouseContentX < headerCell.xMax
                && Event.current.mousePosition.y >= headerCell.y && Event.current.mousePosition.y < headerCell.yMax;

            // 开始拖拽（仅记录按下位置，移动后才实际启动拖拽）
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && isMouseOverCell)
            {
                dragMouseDownColumn = visibleColumns[i];
                dragStartMousePos = Event.current.mousePosition;
                dragHeaderTargetIndex = i;
                headerDragActive = false;
                Event.current.Use();
            }

            // 拖拽过程中更新目标位置（实际拖拽已开始后）
            if (draggingHeaderColumn != null && Event.current.type == EventType.MouseDrag)
            {
                headerDragActive = true;
                int newTargetIndex = Mathf.Clamp((int)((mouseContentX + DataColWidth * 0.5f) / DataColWidth), 0, visibleColumns.Count);
                if (newTargetIndex != dragHeaderTargetIndex)
                {
                    dragHeaderTargetIndex = newTargetIndex;
                }
                Event.current.Use();
            }

            // 按下后移动达到阈值才启动拖拽
            if (dragMouseDownColumn != null && draggingHeaderColumn == null && Event.current.type == EventType.MouseDrag)
            {
                if (Vector2.Distance(Event.current.mousePosition, dragStartMousePos) > 2f)
                {
                    draggingHeaderColumn = dragMouseDownColumn;
                    headerDragActive = true;
                    int newTargetIndex = Mathf.Clamp((int)((mouseContentX + DataColWidth * 0.5f) / DataColWidth), 0, visibleColumns.Count);
                    dragHeaderTargetIndex = newTargetIndex;
                }
                Event.current.Use();
            }

            // 绘制拖动指示线
            if (draggingHeaderColumn != null && i == dragHeaderTargetIndex)
            {
                Widgets.DrawLine(new Vector2(DataColWidth * i, 0), new Vector2(DataColWidth * i, LineHeight), Color.yellow, 2f);
            }

            // 被拖动的列半透明
            if (draggingHeaderColumn == visibleColumns[i])
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
            }

            if (Mouse.IsOver(headerCell)) Widgets.DrawHighlight(headerCell);

            // 排序列：黄色背景高光
            if (visibleColumns[i] == sortCol)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.92f, 0.016f, 0.3f);
                GUI.DrawTexture(headerCell, BaseContent.WhiteTex);
                GUI.color = prevColor;
            }

            GUI.color = (visibleColumns[i] == selectedCol) ? Color.cyan : Color.gray;
            if (visibleColumns[i].comment != null) TooltipHandler.TipRegion(headerCell, visibleColumns[i].comment);
            Widgets.Label(headerCell, $"<b>{visibleColumns[i].label}</b>");
            GUI.color = Color.white;

            // 恢复被拖动列的颜色
            if (draggingHeaderColumn == visibleColumns[i])
            {
                GUI.color = Color.white;
            }

            // 点击逻辑（仅在非拖拽状态触发，且不在拖拽结束后的冷却帧内）
            if (!headerDragActive && Event.current.type == EventType.MouseUp && draggingHeaderColumn == null && dragEndFrameCount <= 0 && headerCell.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    // 左键：同时选中与排序
                    Event.current.Use();
                    if (selectedCol == sortCol)
                    {
                        selectedCol = visibleColumns[i];
                        sortCol = visibleColumns[i];
                        if (curSortMode != SortModeType.NoSort)
                        {
                            filterDirty = true;
                        }
                    }
                    else
                    {
                        selectedCol = visibleColumns[i];
                    }
                }
                else if (Event.current.button == 1)
                {
                    // 右键：仅排序，不改变选中
                    Event.current.Use();
                    sortCol = visibleColumns[i];
                    if (curSortMode != SortModeType.NoSort)
                    {
                        filterDirty = true;
                    }
                }
            }
        }

        Widgets.EndScrollView();

        // 处理鼠标松开 (拖拽结束) — 放在 EndScrollView 之外，确保鼠标在滚动区域外释放也能被捕获
        if (Event.current.type == EventType.MouseUp && draggingHeaderColumn != null)
        {
            if (headerDragActive)
            {
                ApplyHeaderDragOrder(draggingHeaderColumn, dragHeaderTargetIndex);
                dragEndFrameCount = 2;
                Event.current.Use();
            }
            draggingHeaderColumn = null;
            dragHeaderTargetIndex = -1;
            headerDragActive = false;
            dragMouseDownColumn = null;
        }

        // 处理鼠标松开（仅按下未拖拽时清理记录）
        if (Event.current.type == EventType.MouseUp && dragMouseDownColumn != null)
        {
            dragMouseDownColumn = null;
        }

        // 每帧递减拖拽结束冷却计数
        if (dragEndFrameCount > 0)
        {
            dragEndFrameCount--;
        }
    }

    private static void ApplyHeaderDragOrder(StatColumnConfig dragItem, int targetVisibleIndex)
    {
        if (targetVisibleIndex < 0 || targetVisibleIndex >= visibleColumns.Count) return;

        // 获取完整排序列表（包含隐藏列）
        var sortedAll = allColumnSource
            .OrderBy(c => TweakUtility.GetColumnSortOrder(c.id))
            .ToList();

        int originalIndex = sortedAll.IndexOf(dragItem);
        if (originalIndex < 0) return;
        if (originalIndex == targetVisibleIndex) return;

        // 找到目标位置的可见列
        var targetVisibleCol = visibleColumns[targetVisibleIndex];

        sortedAll.RemoveAt(originalIndex);

        // 在完整排序列表中定位到目标可见列之前插入
        int insertIndex = sortedAll.IndexOf(targetVisibleCol);
        if (insertIndex < 0) insertIndex = sortedAll.Count;

        sortedAll.Insert(insertIndex, dragItem);

        // 更新当前类型的排序顺序
        if (!columnOrders.ContainsKey(curType))
            columnOrders[curType] = new();
        var typeOrders = columnOrders[curType];
        typeOrders.Clear();
        for (int i = 0; i < sortedAll.Count; i++)
        {
            typeOrders[sortedAll[i].id] = i;
        }

        TweakUtility.SyncVisibleColumns();
    }

    /// <summary>面板移动时请求取消输入框聚焦：提交正在编辑的内容，并在本帧 Repaint 时机立即释放键盘焦点。</summary>
    private static void UpdateFocusCancel(bool panelMoved)
    {
        if (panelMoved && (editingControlName != null || _focusedControlCache.Length > 0))
        {
            cancelFocusPending = true;
            // 提交正在编辑的内容，避免单元格被移出可视区域后丢失编辑
            if (editingControlName != null)
            {
                if (editingControlData != null && editingControlConfig != null && editingBuffer != null)
                {
                    editingControlConfig.CheckAndApply(editingControlData, editingBuffer);
                }
                editingControlName = null;
                editingBuffer = null;
                editingControlData = null;
                editingControlConfig = null;
            }
        }
        // 在 Repaint 时机释放键盘焦点：既当帧立即生效（避免虚拟滚动导致聚焦单元格错乱），
        // 又不会打断鼠标拖动（拖动事件的输入处理已经结束）。
        // 注意：改变键盘焦点会顺带清空 hotControl，从而中断正在进行的拖动
        // （滚动条拖动、在输入框上开始的拖动）。因此保存并恢复 hotControl。
        if (cancelFocusPending && Event.current.type == EventType.Repaint)
        {
            int hotControl = GUIUtility.hotControl;
            // 用最底层的方式清空键盘焦点，避免 GUI.FocusControl 附带的焦点切换逻辑
            GUIUtility.keyboardControl = 0;
            if (hotControl != 0 && GUIUtility.hotControl != hotControl)
            {
                GUIUtility.hotControl = hotControl;
            }
            cancelFocusPending = false;
        }
    }

    public static void DrawData(Rect inRect)
    {
        float totalColWidth = DataColWidth * visibleColumns.Count + 24f;
        float totalHeight = filteredData.Count * LineHeight + 24f;
        // 滚轮滚动面板
        bool panelMoved = Event.current.type == EventType.ScrollWheel
            && (Event.current.delta.x != 0f || Event.current.delta.y != 0f)
            && inRect.Contains(Event.current.mousePosition);
        // 鼠标中键拖动滚动
        var b = Event.current.button;
        if (Event.current.type == EventType.MouseDrag && (b == 1 || b == 2))
        {
            if (inRect.Contains(Event.current.mousePosition) || dragging)
            {
                dragging = true;
                panelMoved = true;
                dataScrollPosition.y -= Event.current.delta.y;
                Rect rightRectCheck = new(inRect.x + NameColWidth, inRect.y, inRect.width - NameColWidth, inRect.height);
                if (rightRectCheck.Contains(Event.current.mousePosition) || dragging)
                {
                    headerScrollPosition.x -= Event.current.delta.x;
                }
                Event.current.Use();
            }
        }
        else
        {
            dragging = false;
        }
        if (jumpdataScrollY >= 0f) { dataScrollPosition.y = jumpdataScrollY; jumpdataScrollY = -1f; }
        float maxScrollX = Mathf.Max(0, totalColWidth - inRect.width + NameColWidth);
        float maxScrollY = Mathf.Max(0, totalHeight - inRect.height);
        headerScrollPosition.x = Mathf.Clamp(headerScrollPosition.x, 0, maxScrollX);
        dataScrollPosition.y = Mathf.Clamp(dataScrollPosition.y, 0, maxScrollY);

        // 计算可视区域行索引 (虚拟化)
        int startRow = Mathf.FloorToInt(dataScrollPosition.y / LineHeight);
        int endRow = Mathf.Min(filteredData.Count - 1, Mathf.CeilToInt((dataScrollPosition.y + inRect.height) / LineHeight));


        // 每帧仅调用一次 GetNameOfFocusedControl（IMGUI 内部遍历开销大）
        _focusedControlCache = GUI.GetNameOfFocusedControl();

        // 滚轮滚动或鼠标拖动使面板移动时，请求取消输入框的聚焦
        // （这里只是请求，真正的焦点释放会等到鼠标按键松开，避免打断拖动）
        bool focusCancelWasPending = cancelFocusPending;
        UpdateFocusCancel(panelMoved);
        // 面板移动或焦点正在释放（含本帧刚释放）时，屏蔽编辑焦点，避免单元格重新进入编辑
        if (panelMoved || focusCancelWasPending || cancelFocusPending) _focusedControlCache = "";

        // 记录滚动前的位置，用于检测滚动条拖动导致的移动
        float prevDataScrollY = dataScrollPosition.y;
        float prevHeaderScrollX = headerScrollPosition.x;

        // 左侧：名称列 (仅垂直滚动)
        Rect leftRect = new(inRect.x, inRect.y, NameColWidth, inRect.height);

        Vector2 leftScroll = new(0, dataScrollPosition.y);
        Widgets.BeginScrollView(leftRect, ref leftScroll, new Rect(0, 0, NameColWidth, totalHeight), false);
        dataScrollPosition.y = leftScroll.y;

        for (int i = startRow; i <= endRow; i++)
        {
            DrawNameRow(i, filteredData[i]);
        }
        Widgets.EndScrollView();

        // 右侧：数据列 (双向滚动)
        Rect rightRect = new(inRect.x + NameColWidth, inRect.y, inRect.width - NameColWidth, inRect.height);
        Vector2 rightScroll = new(headerScrollPosition.x, dataScrollPosition.y);
        Widgets.BeginScrollView(rightRect, ref rightScroll, new Rect(0, 0, totalColWidth, totalHeight));

        // 计算可见列范围 (列虚拟化)
        int startCol = Mathf.Max(Mathf.FloorToInt(rightScroll.x / DataColWidth - 1), 0);
        int endCol = Mathf.Min(visibleColumns.Count - 1, Mathf.CeilToInt((rightScroll.x + rightRect.width) / DataColWidth + 1));
        // 同步滚动位置
        headerScrollPosition.x = rightScroll.x;
        dataScrollPosition.y = rightScroll.y;

        for (int i = startRow; i <= endRow; i++)
        {
            DrawDataRows(i, filteredData[i], startCol, endCol);
        }
        Widgets.EndScrollView();

        // 滚动条拖动等导致的滚动位置变化同样视为面板移动
        if (Mathf.Abs(dataScrollPosition.y - prevDataScrollY) > 0.01f
            || Mathf.Abs(headerScrollPosition.x - prevHeaderScrollX) > 0.01f)
        {
            UpdateFocusCancel(true);
        }

        // 处理删除逻辑
        if (needRemove != null)
        {
            TweakUtility.RemoveData(needRemove);
            needRemove = null;
        }

        // 递减跳转高亮帧计数器
        if (jumpHighlightFrame > 0)
        {
            jumpHighlightFrame--;
            if (jumpHighlightFrame == 0)
                jumpHighlightData = null;
        }
    }

    private static void DrawNameRow(int index, TweakData data)
    {
        Text.Anchor = TextAnchor.UpperLeft;
        float y = index * LineHeight;
        Rect rowRect = new(0, y, NameColWidth, LineHeight);

        if (index % 2 == 0) Widgets.DrawLightHighlight(rowRect);

        if (data.CountFieldCorrupted() > 0)
        {
            // 有损坏字段：橙红色背景（优先于已修改的绿色）
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0f, 0.3f);
            GUI.DrawTexture(rowRect, BaseContent.WhiteTex);
            GUI.color = prevColor;
        }
        else if (data.CountFieldModified() > 0)
        {
            // 仅已修改：绿色背景
            var prevColor = GUI.color;
            GUI.color = new Color(0.7f, 1f, 0.7f, 0.15f);
            GUI.DrawTexture(rowRect, BaseContent.WhiteTex);
            GUI.color = prevColor;
        }

        // 跳转高亮：淡红色背景（淡出效果）
        if (jumpHighlightFrame > 0 && data == jumpHighlightData)
        {
            var prevColor = GUI.color;
            float fadeAlpha = 0.6f * (jumpHighlightFrame / 90f);
            GUI.color = new Color(1f, 1f, 0f, fadeAlpha);
            GUI.DrawTexture(rowRect, BaseContent.WhiteTex);
            GUI.color = prevColor;
        }

        // 图标
        if (data.uiIcon != null)
            GUI.DrawTexture(new Rect(0, y, IconSize, IconSize), data.uiIcon, ScaleMode.StretchToFill, true, 0f, data.uiIconColor, 0f, 0f);

        // 标签名
        Rect labelRect = new(IconSizePadded, y, NameColWidth - IconSizePadded - ResetButtonWidth, LineHeight);
        Text.WordWrap = false;
        if (IsPinned(data.id))
        {
            GUI.color = Color.yellow;
            Widgets.Label(labelRect, data.label);
            GUI.color = Color.white;
        }
        else
        {
            Widgets.Label(labelRect, data.label);
        }
        Text.WordWrap = true;
        if (data.modPackName != "Unknown") { TooltipHandler.TipRegion(rowRect, "MST.FromMod".Translate().RawText + data.modPackName); }
        // 悬浮提示（排除重置按钮）
        Rect nameClickRect = new(0, y, NameColWidth - ResetButtonWidth, LineHeight);
        if (data.parentTweakId != null)
        {
            TooltipHandler.TipRegion(nameClickRect, "MST.JumpToDesc".Translate().RawText);
        }
        TooltipHandler.TipRegion(rowRect, data.desc);
        if (Mouse.IsOver(nameClickRect))
        {
            Widgets.DrawHighlight(nameClickRect);
        }
        if (data.parentTweakId != null && Mouse.IsOver(nameClickRect) && Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            TweakUtility.JumpToData(data.parentTweakId);
            Event.current.Use();
        }
        else if (data.def != null && Current.Game != null && Mouse.IsOver(nameClickRect) && Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            Find.WindowStack.Add(new Dialog_InfoCard(data.def));
            Event.current.Use();
        }
        // 重置按钮
        Rect resetRect = new(NameColWidth - ResetButtonWidth, y, ResetButtonWidth, LineHeight);
        if (data.tweaked)
        {
            Widgets.DrawButtonGraphic(resetRect);
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && resetRect.Contains(Event.current.mousePosition))
            {
                needRemove = data;
                Event.current.Use();
            }
        }
        // 右键菜单检测（名称行）
        if (Input.GetMouseButtonDown(1) && nameClickRect.Contains(Event.current.mousePosition))
        {
            rightClickStartPos = Input.mousePosition;
            rightClickDown = true;
            rightClickData = data;
            rightClickColumnConfig = null;
        }
        if (Input.GetMouseButtonUp(1) && rightClickDown && rightClickData == data && nameClickRect.Contains(Event.current.mousePosition))
        {
            if (Vector2.Distance(Input.mousePosition, rightClickStartPos) < 2f)
            {
                ShowRightClickContextMenu(data, null);
                Event.current.Use();
            }
            rightClickDown = false;
            rightClickData = null;
        }
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(resetRect, (data.tweaked ? "R" : string.Empty).Translate().RawText);
    }

    private static void DrawDataRows(int line, TweakData data, int startCol, int endCol)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        float totalWidth = DataColWidth * visibleColumns.Count;
        if (line % 2 == 0)
            Widgets.DrawHighlight(new Rect(0, line * LineHeight, totalWidth, LineHeight));
        // 跳转高亮：黄色背景（淡出效果）
        if (jumpHighlightFrame > 0 && data == jumpHighlightData)
        {
            var prevColor = GUI.color;
            float fadeAlpha = 0.6f * (jumpHighlightFrame / 90f);
            GUI.color = new Color(1f, 1f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0, line * LineHeight, totalWidth, LineHeight), BaseContent.WhiteTex);
            GUI.color = prevColor;
        }
        for (int i = startCol; i <= endCol; i++)
        {
            DrawDataColumns(line, i, data);
        }
    }

    private static void DrawDataColumns(int line, int col, TweakData data)
    {
        Text.WordWrap = false;
        var config = visibleColumns[col];
        Rect cellBack = new(DataColWidth * col, line * LineHeight, DataColWidth, LineHeight);
        Rect cellRect = cellBack.ContractedBy(2f, 0f);

        string configFieldName = config.fieldName;

        // 损坏字段显示原始数据值
        string sourceText;
        if (data.IsFieldCorrupted(configFieldName))
        {
            TweakDatabase.TryGetOriginal(data.id, out var orig);
            sourceText = orig != null ? config.GetString(orig) : config.GetString(data);
        }
        else
        {
            sourceText = config.GetString(data);
        }

        if (config.columnDataType == ColumnDataType.Field && !showDisplayMode)
        {
            // controlName 必须每帧设置，否则 IMGUI 焦点系统无法正确注册点击
            string idTag = data.IdTag;
            string controlName = string.Concat("Tweak_", idTag, "_", col.ToString(), "_", line.ToString());
            string currentFocus = _focusedControlCache;
            bool isFocused = currentFocus == controlName;
            string? displayText;

            if (editingControlName == controlName)
            {
                // 情况A: 正在编辑此字段 -> 显示缓存内容
                displayText = editingBuffer;
                if (!isFocused && editingBuffer != null)
                {
                    config.CheckAndApply(data, editingBuffer);
                    editingControlName = null;
                    editingControlData = null;
                    editingControlConfig = null;
                    displayText = sourceText;
                }
            }
            else
            {
                // 情况B: 未处于编辑状态 -> 显示源数据
                displayText = sourceText;
                if (isFocused)
                {
                    // 先提交旧单元格的编辑（防止因迭代顺序导致旧单元格的修改被覆盖）
                    if (editingControlName != null && editingControlData != null && editingControlConfig != null && editingBuffer != null)
                    {
                        editingControlConfig.CheckAndApply(editingControlData, editingBuffer);
                    }
                    // Bool 字段使用复选框，修改已由 DrawBoolFieldCell 即时提交，无需进入编辑缓冲机制
                    if (config.columnType != ColumnStyle.Bool)
                    {
                        editingControlName = controlName;
                        editingBuffer = sourceText;
                        editingControlData = data;
                        editingControlConfig = config;
                    }
                }
            }

            // 绘制输入框
            string newText;
            GUI.SetNextControlName(controlName);
            var num = config.GetNumericValue(data);
            if (config.stat != null && num.HasValue && !config.isSPStat && (
                (num.Value > config.stat.maxValue) || (num.Value < config.stat.minValue)))
            {
                GUI.color = Color.red;
            }
            switch (config.columnType)
            {
                case ColumnStyle.Float or ColumnStyle.Int or ColumnStyle.Range:
                    newText = Widgets.TextField(cellRect, displayText);
                    break;
                case ColumnStyle.Prec:
                    cellRect.SplitVertically(cellRect.width * 0.8f, out Rect left, out Rect right);
                    Widgets.Label(right, "%");
                    newText = Widgets.TextField(left, displayText);
                    break;
                case ColumnStyle.Bool:
                    newText = DrawBoolFieldCell(cellRect, config, data, displayText);
                    break;
                case ColumnStyle.Enum:
                    newText = DrawEnumDropdownCell(cellRect, config, data, displayText);
                    break;
                default:
                    // 通过工厂统一调度按钮编辑器和列表编辑器
                    if (EditorWindowFactory.TryGetValue(config.columnType, out var factory))
                    {
                        newText = config.isListStyle
                            ? DrawListEditorCell(cellRect, config, data, () => factory(config, data))
                            : DrawEditorButtonCell(cellRect, config, data, () => factory(config, data));
                    }
                    else
                    {
                        newText = "";
                    }
                    break;
            }
            // 为损坏或已修改字段绘制背景（使用缓存的 configFieldName）
            if (configFieldName != null && data.IsFieldCorrupted(configFieldName))
            {
                // 已损坏：橙红色
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0f, 0.3f);
                GUI.DrawTexture(cellBack, BaseContent.WhiteTex);
                GUI.color = prevColor;
            }
            else if (configFieldName != null && data.IsFieldModified(configFieldName))
            {
                // 已修改：淡绿色
                var prevColor = GUI.color;
                GUI.color = new Color(0.7f, 1f, 0.7f, 0.15f);
                GUI.DrawTexture(cellBack, BaseContent.WhiteTex);
                GUI.color = prevColor;
            }
            GUI.color = Color.white;
            if (editingControlName == controlName && config.columnType is ColumnStyle.Float or ColumnStyle.Int or ColumnStyle.Prec or ColumnStyle.Range)
            {
                editingBuffer = newText;
            }
        }
        else
        {
            DrawDisplayCell(cellRect, config, data, sourceText);
        }
        // 右键菜单检测（数据单元格）
        if (Input.GetMouseButtonDown(1) && cellBack.Contains(Event.current.mousePosition))
        {
            rightClickStartPos = Input.mousePosition;
            rightClickDown = true;
            rightClickData = data;
            rightClickColumnConfig = config;
        }
        if (Input.GetMouseButtonUp(1) && rightClickDown && rightClickData == data && rightClickColumnConfig == config && cellBack.Contains(Event.current.mousePosition))
        {
            if (config.columnDataType == ColumnDataType.Field && Vector2.Distance(Input.mousePosition, rightClickStartPos) < 2f)
            {
                ShowRightClickContextMenu(data, config);
                //Event.current.Use();
            }
            rightClickDown = false;
            rightClickData = null;
            rightClickColumnConfig = null;
        }
        Text.WordWrap = true;
    }

    #region 字段绘制辅助方法

    /// <summary>绘制布尔值复选框字段（Field类型）</summary>
    private static string DrawBoolFieldCell(Rect cellRect, StatColumnConfig config, TweakData data, string? displayText)
    {
        bool currentBool = displayText?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
        bool newBool = currentBool;
        float checkboxSize = Mathf.Min(cellRect.height - 4f, 24f);
        float checkboxX = cellRect.x + (cellRect.width - checkboxSize) / 2f;
        float checkboxY = cellRect.y + (cellRect.height - checkboxSize) / 2f;
        Widgets.Checkbox(checkboxX, checkboxY, ref newBool, checkboxSize);
        if (newBool != currentBool)
        {
            string newText = newBool.ToString();
            config.CheckAndApply(data, newText);
            return newText;
        }
        return displayText ?? "";
    }

    /// <summary>绘制枚举下拉菜单字段，合并原 GetOptions 和 EnumType 两种路径</summary>
    private static string DrawEnumDropdownCell(Rect cellRect, StatColumnConfig config, TweakData data, string? displayText)
    {
        var options = config.GetOptions();
        List<string> enumOptions;

        if (options != null)
        {
            enumOptions = options;
        }
        else if (config.EnumType != null)
        {
            enumOptions = Enum.GetNames(config.EnumType).ToList();
        }
        else
        {
            return displayText ?? "";
        }

        if (Widgets.ButtonText(cellRect, displayText))
        {
            var menuOptions = new List<FloatMenuOption>();
            for (int i = 0; i < enumOptions.Count; i++)
            {
                int index = i;
                menuOptions.Add(new FloatMenuOption(enumOptions[index], () =>
                {
                    config.CheckAndApply(data, enumOptions[index]);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(menuOptions));
        }
        return displayText ?? "";
    }

    /// <summary>绘制只读显示单元格（Display类型）</summary>
    private static void DrawDisplayCell(Rect cellRect, StatColumnConfig config, TweakData data, string? sourceText)
    {
        sourceText ??= "";
        if (config.columnType == ColumnStyle.Bool)
        {
            bool currentBool = sourceText?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
            float checkboxSize = Mathf.Min(cellRect.height - 4f, 24f);
            float checkboxX = cellRect.x + (cellRect.width - checkboxSize) / 2f;
            float checkboxY = cellRect.y + (cellRect.height - checkboxSize) / 2f;
            Widgets.Checkbox(checkboxX, checkboxY, ref currentBool, checkboxSize, true);
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            var d = config.GetRawValue(data);
            if (config.columnType == ColumnStyle.Link && d != null)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.DrawButtonGraphic(cellRect);
                if (cellRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    if (d is TweakID id)
                    {
                        TweakUtility.JumpToData(id);
                    }
                    Event.current.Use();
                }
            }
            else if (config.columnType is ColumnStyle.RaceLinks or ColumnStyle.BodyLinks)
            {
                // 只读的关联入口：点击打开链接列表窗口。
                Text.Font = GameFont.Tiny;
                Widgets.DrawButtonGraphic(cellRect);
                Widgets.Label(cellRect, sourceText);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && cellRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    if (config.columnType == ColumnStyle.BodyLinks)
                    {
                        if (data.def is BodyPartDef bodyPart)
                            Find.WindowStack.Add(new TweakLinksWindow(data, "MST.bodyLinks", BodyPartData.FindBodyLinks(bodyPart)));
                    }
                    else if (data.def is BodyPartDef part)
                    {
                        Find.WindowStack.Add(new TweakLinksWindow(data, "MST.raceLinks", BodyPartData.FindRaceLinks(part), GetRaceBodyTip));
                    }
                    else if (data.def is BodyDef body)
                    {
                        Find.WindowStack.Add(new TweakLinksWindow(data, "MST.raceLinks", BodyData.FindRaceLinks(body), GetRaceBodyTip));
                    }
                }
                return;
            }
            else if (config.columnType == ColumnStyle.Prec)
            {
                if (d != null)
                {
                    Widgets.Label(cellRect.ContractedBy(4f), sourceText + "%");
                }
                return;
            }
            else if (config.columnType == ColumnStyle.String)
            {
                Text.Font = GameFont.Tiny;
            } else if (!(config.columnType is ColumnStyle.Int or ColumnStyle.Float or ColumnStyle.Prec) && sourceText.Length > 5)
            {
                Text.Font = GameFont.Tiny;
            }
            Widgets.Label(cellRect.ContractedBy(4f), sourceText);
            Text.Font = GameFont.Small;
            return;
        }
    }

    #endregion

    #endregion

    #region 辅助方法

    /// <summary>种族链接行的 tooltip：显示该种族使用的身体。</summary>
    private static string? GetRaceBodyTip(TweakData.TweakID id)
    {
        var race = DefDatabase<ThingDef>.GetNamed(id.defName, false);
        var body = race?.race?.body;
        return body != null ? $"{body.LabelCap.Resolve()} ({body.defName})" : null;
    }

    private static string DrawEditorButtonCell(Rect cellRect, StatColumnConfig config, TweakData data, Func<Window> createWindow)
    {
        string displayText = config.GetString(data);
        Widgets.DrawButtonGraphic(cellRect);
        Text.Anchor = TextAnchor.MiddleCenter;
        if (config.columnType is ColumnStyle.DefSelector or ColumnStyle.Flags)
        {
            Text.Font = GameFont.Tiny;
        }
        Widgets.Label(cellRect, displayText);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && cellRect.Contains(Event.current.mousePosition))
        {
            Event.current.Use();
            Find.WindowStack.Add(createWindow());
        }
        return displayText;
    }

    private static string DrawListEditorCell(Rect cellRect, StatColumnConfig config, TweakData data, Func<Window> createWindow)
    {
        var list = (System.Collections.IList?)config.GetRawValue(data);
        string text = list != null ? $"[{list.Count}]" : "MST.Empty".Translate().RawText;
        Widgets.DrawButtonGraphic(cellRect);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(cellRect, text);
        Text.Anchor = TextAnchor.UpperLeft;
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && cellRect.Contains(Event.current.mousePosition))
        {
            Event.current.Use();
            Find.WindowStack.Add(createWindow());
        }
        return text;
    }

    /// 辅助方法：绘制带标签的输入框
    private static float DrawLabeledInput(Rect rect, string label, ref float value, bool postive = false)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        float labelWidth = 40f;
        Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);

        Rect inputRect = new(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
        string text = Widgets.TextField(inputRect, value.ToString("F2"));

        if (float.TryParse(text, out float newVal))
        {
            value = postive ? Mathf.Max(newVal, 0) : newVal;
        }
        return rect.width;
    }

    public static bool IsPinned(TweakID id) => pinnedIds.Contains(id);

    public static void TogglePin(TweakID id)
    {
        if (pinnedIds.Contains(id))
            pinnedIds.Remove(id);
        else
            pinnedIds.Add(id);
        filterDirty = true;
    }

    /// 用于 Mod 包筛选 (返回选中的字符串)
    private static void OpenFilterMenu(List<string> options, Action<string> onSelect)
    {
        List<FloatMenuOption> menuOptions = new();
        foreach (var option in options)
        {
            string currentOpt = option;
            menuOptions.Add(new FloatMenuOption(currentOpt, () => onSelect(currentOpt)));
        }
        Find.WindowStack.Add(new FloatMenu(menuOptions));
    }

    /// 用于类型筛选 (返回选中的索引)
    private static void OpenFilterMenu(List<string> options, Action<int> onIndexSelected)
    {
        List<FloatMenuOption> menuOptions = [];
        for (int i = 0; i < options.Count; i++)
        {
            int tmp = i;
            menuOptions.Add(new FloatMenuOption(options[i].Translate().RawText, () => onIndexSelected(tmp)));
        }
        Find.WindowStack.Add(new FloatMenu(menuOptions));
    }

    /// <summary>显示右键上下文菜单</summary>
    private static void ShowRightClickContextMenu(TweakData data, StatColumnConfig? columnConfig)
    {
        var options = new List<FloatMenuOption>();

        if (columnConfig == null)
        {
            // Name column context menu
            if (IsPinned(data.id))
                options.Add(new FloatMenuOption("MST.Unpin".Translate().RawText, () => TogglePin(data.id)));
            else
                options.Add(new FloatMenuOption("MST.Pin".Translate().RawText, () => TogglePin(data.id)));
            options.Add(new FloatMenuOption("MST.Reset".Translate().RawText, () => needRemove = data));
            if (data.id.defName != null)
                options.Add(new FloatMenuOption("MST.CopyDefName".Translate().RawText, () =>
                {
                    GUIUtility.systemCopyBuffer = data.id.defName;
                    Messages.Message("MST.CopySuccess".Translate().RawText, MessageTypeDefOf.NeutralEvent, false);
                }));
        }
        else
        {
            // Data cell context menu

            options.Add(new FloatMenuOption("MST.Paste".Translate().RawText, () =>
            {
                // 先提交旧单元格的编辑
                if (editingControlName != null && editingControlData != null && editingControlConfig != null && editingBuffer != null)
                {
                    editingControlConfig.CheckAndApply(editingControlData, editingBuffer);
                }
                editingControlName = null;
                editingBuffer = null;
                editingControlData = null;
                editingControlConfig = null;
                var clipboardData = GUIUtility.systemCopyBuffer;
                if (!clipboardData.NullOrEmpty())
                {
                    if (!columnConfig.TryPasteData(data, clipboardData))
                        Messages.Message("MST.PasteFailed".Translate().RawText, MessageTypeDefOf.NegativeEvent, false);
                }
            }));
            options.Add(new FloatMenuOption("MST.Copy".Translate().RawText, () =>
            {
                var copyData = columnConfig.GetCopyData(data);
                if (copyData != null)
                {
                    GUIUtility.systemCopyBuffer = copyData;
                    Messages.Message("MST.CopySuccess".Translate().RawText, MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message("MST.CopyFailed".Translate().RawText, MessageTypeDefOf.NegativeEvent, false);
                }
            }));
            options.Add(new FloatMenuOption("MST.Reset".Translate().RawText, () =>
            {
                // 先提交旧单元格的编辑
                if (editingControlName != null && editingControlData != null && editingControlConfig != null && editingBuffer != null)
                {
                    editingControlConfig.CheckAndApply(editingControlData, editingBuffer);
                }
                editingControlName = null;
                editingBuffer = null;
                editingControlData = null;
                editingControlConfig = null;
                TweakUtility.ResetSingleItem(data, columnConfig);
            }));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }
    #endregion
}