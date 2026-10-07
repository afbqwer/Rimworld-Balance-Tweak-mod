using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
#pragma warning disable 8618
namespace BalanceTweak;

public class IngredientFilterEditorWindow : EditorWindowBase
{
    public override Vector2 InitialSize => new(700f, 600f);
    private Action<ThingFilter?>? onSelectedCallback;

    // Working data for sub-fields
    private List<ThingCategoryDef> workingCategories;
    private List<ThingCategoryDef> workingDisallowedCategories;
    private List<SpecialThingFilterDef> workingSpecialFiltersToAllow;
    private List<SpecialThingFilterDef> workingSpecialFiltersToDisallow;
    private List<ThingDef> workingThingDefs;
    private List<StuffCategoryDef> workingStuffCategoriesToAllow;

    // New fields
    private List<string> workingTradeTagsToAllow;
    private List<string> workingTradeTagsToDisallow;
    private List<string> workingThingSetMakerTagsToAllow;
    private List<string> workingThingSetMakerTagsToDisallow;
    private List<ThingDef> workingDisallowedThingDefs;

    // Extra diff lists: capture ThingDef allow/disallow states not representable by field lists alone
    private List<ThingDef> workingExtraAllowedDefs;
    private List<ThingDef> workingExtraDisallowedDefs;

    // Original filter (preserved to keep uneditable fields via CopyAllowancesFrom)
    private ThingFilter? originalFilter;

    // Preview filter (built when "Test" is clicked)
    private ThingFilter? previewFilter;

    // Left panel scroll
    private Vector2 leftScrollPosition;

    // Right panel virtual scrolling state
    private Vector2 rightScrollPosition;
    private QuickSearchWidget rightQuickSearch = new();
    private bool rightNeedRebuildList = true;
    private List<RightListItem> rightFlattenedItems = new();
    private bool showOnlyAllowed;

    // Highlight state: when >0, all allowed items get highlighted with fade-out
    private int highlightFramesRemaining;
    // Scroll target: first allowed entry index, updated on rebuild (for auto-scroll only)
    private int firstAllowedScrollIndex = -1;

    // Layout constants
    private const float FieldEntryHeight = 30f;
    private const float TestButtonHeight = 30f;
    private const float LabelWidth = 180f;
    private const float EditBtnWidth = 60f;
    private const float CountLabelWidth = 60f;
    private const float LeftPanelRatio = 0.50f;

    // Right panel layout constants
    private const float SearchBarHeight = 30f;
    private const float EntryHeight = 28f;
    private const float CategoryHeaderHeight = 24f;
    private const float IconSize = 24f;

    public IngredientFilterEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;

        var raw = (ThingFilter?)config.GetRawValue(data);
        InitFromFilter(raw ?? new ThingFilter());
        RequestHighlight();
    }

    public IngredientFilterEditorWindow(string title, ThingFilter? initialFilter, Action<ThingFilter?> onSelected)
    {
        this.pickerTitle = title;
        this.onSelectedCallback = onSelected;

        InitFromFilter(initialFilter ?? new ThingFilter());
        RequestHighlight();
    }

    private void RequestHighlight()
    {
        highlightFramesRemaining = 90;
        rightNeedRebuildList = true;
    }

    private void InitFromFilter(ThingFilter filter)
    {
        originalFilter = filter;
        workingCategories = ((SerializationHelper.TfCategoriesField?.GetValue(filter) as List<string>) ?? new List<string>())
            .Select(s => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(s))
            .Where(d => d != null)
            .Cast<ThingCategoryDef>()
            .ToList();
        workingDisallowedCategories = ((SerializationHelper.TfDisallowedCategoriesField?.GetValue(filter) as List<string>) ?? new List<string>())
            .Select(s => DefDatabase<ThingCategoryDef>.GetNamedSilentFail(s))
            .Where(d => d != null)
            .Cast<ThingCategoryDef>()
            .ToList();
        workingSpecialFiltersToAllow = ((SerializationHelper.TfSpecialFiltersToAllowField?.GetValue(filter) as List<string>) ?? new List<string>())
            .Select(s => DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(s))
            .Where(d => d != null)
            .Cast<SpecialThingFilterDef>()
            .ToList();
        workingSpecialFiltersToDisallow = ((SerializationHelper.TfSpecialFiltersToDisallowField?.GetValue(filter) as List<string>) ?? new List<string>())
            .Select(s => DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail(s))
            .Where(d => d != null)
            .Cast<SpecialThingFilterDef>()
            .ToList();
        workingThingDefs = (SerializationHelper.TfThingDefsField?.GetValue(filter) as List<ThingDef>)?.ToList() ?? new List<ThingDef>();
        workingStuffCategoriesToAllow = (SerializationHelper.TfStuffCategoriesToAllowField?.GetValue(filter) as List<StuffCategoryDef>)?.ToList() ?? new List<StuffCategoryDef>();

        workingTradeTagsToAllow = (SerializationHelper.TfTradeTagsToAllowField?.GetValue(filter) as List<string>)?.ToList() ?? new List<string>();
        workingTradeTagsToDisallow = (SerializationHelper.TfTradeTagsToDisallowField?.GetValue(filter) as List<string>)?.ToList() ?? new List<string>();
        workingThingSetMakerTagsToAllow = (SerializationHelper.TfThingSetMakerTagsToAllowField?.GetValue(filter) as List<string>)?.ToList() ?? new List<string>();
        workingThingSetMakerTagsToDisallow = (SerializationHelper.TfThingSetMakerTagsToDisallowField?.GetValue(filter) as List<string>)?.ToList() ?? new List<string>();
        workingDisallowedThingDefs = (SerializationHelper.TfDisallowedThingDefsField?.GetValue(filter) as List<ThingDef>)?.ToList() ?? new List<ThingDef>();

        ComputeExtraDiff(originalFilter, out workingExtraAllowedDefs, out workingExtraDisallowedDefs);

        previewFilter = BuildFilterFromOriginalWithEdits();
    }

    /// <summary>
    /// Computes the diff between what the field lists produce and the original filter's allowedDefs.
    /// This captures ThingDef allow/disallow states that aren't representable by the field lists alone.
    /// </summary>
    private static void ComputeExtraDiff(ThingFilter original, out List<ThingDef> extraAllowed, out List<ThingDef> extraDisallowed)
    {
        extraAllowed = new List<ThingDef>();
        extraDisallowed = new List<ThingDef>();

        // Build a filter from only the field lists (no CopyAllowancesFrom pre-population)
        var fieldOnly = new ThingFilter();
        SerializationHelper.CopyFieldsTo(original, fieldOnly);
        fieldOnly.ResolveReferences();

        var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
        for (int i = 0; i < allDefs.Count; i++)
        {
            var def = allDefs[i];
            bool fieldAllows = fieldOnly.Allows(def);
            bool originalAllows = original.Allows(def);
            if (originalAllows && !fieldAllows)
                extraAllowed.Add(def);
            else if (fieldAllows && !originalAllows)
                extraDisallowed.Add(def);
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float curY = inRect.y;

        // Title (full width)
        Text.Font = GameFont.Medium;
        string titleText = GetBaseTitle();
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), titleText);
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        // Split into left and right panels
        float leftWidth = inRect.width * LeftPanelRatio;
        float rightWidth = inRect.width - leftWidth - Spacing;
        float contentHeight = inRect.height - TitleHeight - Spacing - BottomBarHeight - Spacing;

        Rect leftRect = new(inRect.x, curY, leftWidth, contentHeight);
        Rect rightRect = new(inRect.x + leftWidth + Spacing, curY, rightWidth, contentHeight);

        DrawLeftPanel(leftRect);
        DrawRightPanel(rightRect);

        curY = leftRect.yMax + Spacing;

        // Bottom bar
        Rect bottomRect = new(inRect.x, curY, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    private void DrawLeftPanel(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        float innerX = rect.x + Spacing;
        float innerY = rect.y + Spacing;
        float innerWidth = rect.width - Spacing;
        float innerHeight = rect.height - Spacing;

        const int totalFieldCount = 14;
        float totalFieldHeight = totalFieldCount * FieldEntryHeight;
        float totalContentHeight = totalFieldHeight + Spacing + TestButtonHeight;

        Rect viewRect = new(0f, 0f, innerWidth - 16f, totalContentHeight);
        Widgets.BeginScrollView(new Rect(innerX, innerY, innerWidth, innerHeight), ref leftScrollPosition, viewRect);

        float curY = 0f;

        // Categories
        curY = DrawFieldEntry(curY, viewRect.width, "MST.Categories".Translate().RawText, workingCategories.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.Categories".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingCategories.ToList(), typeof(ThingCategoryDef), (result) =>
            {
                workingCategories = result?.Cast<ThingCategoryDef>().ToList() ?? new List<ThingCategoryDef>();
            }));
        });

        // Disallowed Categories
        curY = DrawFieldEntry(curY, viewRect.width, "MST.DisallowedCategories".Translate().RawText, workingDisallowedCategories.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.DisallowedCategories".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingDisallowedCategories.ToList(), typeof(ThingCategoryDef), (result) =>
            {
                workingDisallowedCategories = result?.Cast<ThingCategoryDef>().ToList() ?? new List<ThingCategoryDef>();
            }));
        });

        // Special Filters To Allow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.SpecialFiltersToAllow".Translate().RawText, workingSpecialFiltersToAllow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.SpecialFiltersToAllow".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingSpecialFiltersToAllow.ToList(), typeof(SpecialThingFilterDef), (result) =>
            {
                workingSpecialFiltersToAllow = result?.Cast<SpecialThingFilterDef>().ToList() ?? new List<SpecialThingFilterDef>();
            }));
        });

        // Special Filters To Disallow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.SpecialFiltersToDisallow".Translate().RawText, workingSpecialFiltersToDisallow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.SpecialFiltersToDisallow".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingSpecialFiltersToDisallow.ToList(), typeof(SpecialThingFilterDef), (result) =>
            {
                workingSpecialFiltersToDisallow = result?.Cast<SpecialThingFilterDef>().ToList() ?? new List<SpecialThingFilterDef>();
            }));
        });

        // ThingDefs
        curY = DrawFieldEntry(curY, viewRect.width, "MST.ThingDefs".Translate().RawText, workingThingDefs.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.ThingDefs".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingThingDefs.ToList(), typeof(ThingDef), (result) =>
            {
                workingThingDefs = result?.Cast<ThingDef>().ToList() ?? new List<ThingDef>();
            }));
        });

        // Stuff Categories To Allow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.StuffCategoriesToAllow".Translate().RawText, workingStuffCategoriesToAllow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.StuffCategoriesToAllow".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingStuffCategoriesToAllow.ToList(), typeof(StuffCategoryDef), (result) =>
            {
                workingStuffCategoriesToAllow = result?.Cast<StuffCategoryDef>().ToList() ?? new List<StuffCategoryDef>();
            }));
        });

        // --- New fields ---

        // Trade Tags To Allow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.TradeTagsToAllow".Translate().RawText, workingTradeTagsToAllow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.TradeTagsToAllow".Translate().RawText}";
            OpenChildWindow(new StringListEditorWindow(title, workingTradeTagsToAllow.ToList(), (result) =>
            {
                workingTradeTagsToAllow = result ?? new List<string>();
            }));
        });

        // Trade Tags To Disallow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.TradeTagsToDisallow".Translate().RawText, workingTradeTagsToDisallow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.TradeTagsToDisallow".Translate().RawText}";
            OpenChildWindow(new StringListEditorWindow(title, workingTradeTagsToDisallow.ToList(), (result) =>
            {
                workingTradeTagsToDisallow = result ?? new List<string>();
            }));
        });

        // Thing Set Maker Tags To Allow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.ThingSetMakerTagsToAllow".Translate().RawText, workingThingSetMakerTagsToAllow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.ThingSetMakerTagsToAllow".Translate().RawText}";
            OpenChildWindow(new StringListEditorWindow(title, workingThingSetMakerTagsToAllow.ToList(), (result) =>
            {
                workingThingSetMakerTagsToAllow = result ?? new List<string>();
            }));
        });

        // Thing Set Maker Tags To Disallow
        curY = DrawFieldEntry(curY, viewRect.width, "MST.ThingSetMakerTagsToDisallow".Translate().RawText, workingThingSetMakerTagsToDisallow.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.ThingSetMakerTagsToDisallow".Translate().RawText}";
            OpenChildWindow(new StringListEditorWindow(title, workingThingSetMakerTagsToDisallow.ToList(), (result) =>
            {
                workingThingSetMakerTagsToDisallow = result ?? new List<string>();
            }));
        });

        // Disallowed ThingDefs
        curY = DrawFieldEntry(curY, viewRect.width, "MST.DisallowedThingDefs".Translate().RawText, workingDisallowedThingDefs.Count, () =>
        {
            string title = $"{GetBaseTitle()} - {"MST.DisallowedThingDefs".Translate().RawText}";
            OpenChildWindow(new DefListEditorWindow(title, workingDisallowedThingDefs.ToList(), typeof(ThingDef), (result) =>
            {
                workingDisallowedThingDefs = result?.Cast<ThingDef>().ToList() ?? new List<ThingDef>();
            }));
        });

        // Extra Allowed Defs (read-only count display, modified via right panel)
        curY = DrawFieldEntry(curY, viewRect.width, "MST.ExtraAllowedDefs".Translate().RawText, workingExtraAllowedDefs.Count, null);

        // Extra Disallowed Defs (read-only count display, modified via right panel)
        curY = DrawFieldEntry(curY, viewRect.width, "MST.ExtraDisallowedDefs".Translate().RawText, workingExtraDisallowedDefs.Count, null);

        // Show only allowed toggle
        curY += Spacing;
        Rect toggleRect = new(viewRect.x, curY, viewRect.width, FieldEntryHeight);
        var showOnlyAllowedP = showOnlyAllowed;
        Widgets.CheckboxLabeled(toggleRect, "MST.ShowOnlyAllowed".Translate().RawText, ref showOnlyAllowed);
        if(showOnlyAllowedP != showOnlyAllowed){
            previewFilter = BuildFilterFromOriginalWithEdits();
            rightNeedRebuildList = true;
            RequestHighlight();
        }
        curY += FieldEntryHeight;

        // Test button
        curY += Spacing;
        Rect testBtnRect = new(viewRect.x, curY, viewRect.width, TestButtonHeight);
        if (Widgets.ButtonText(testBtnRect, "MST.TestFilter".Translate().RawText))
        {
            previewFilter = BuildFilterFromOriginalWithEdits();
            RequestHighlight();
        }

        Widgets.EndScrollView();
    }

    private float DrawFieldEntry(float curY, float viewWidth, string label, int count, Action? onEdit)
    {
        float curX = 0f;

        // Field label
        Rect labelRect = new(curX, curY, LabelWidth, FieldEntryHeight);
        Widgets.Label(labelRect, label);
        curX += LabelWidth + Spacing;

        // Edit button (only if onEdit is provided)
        if (onEdit != null)
        {
            Rect editBtnRect = new(curX, curY, EditBtnWidth, FieldEntryHeight);
            if (Widgets.ButtonText(editBtnRect, "MST.Edit".Translate().RawText))
            {
                onEdit();
            }
        }
        curX += EditBtnWidth + Spacing;

        // Count label
        string countText = $"({count})";
        Rect countRect = new(curX, curY, CountLabelWidth, FieldEntryHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(countRect, countText);
        Text.Anchor = TextAnchor.UpperLeft;

        return curY + FieldEntryHeight;
    }

    #region Right Panel - Virtual Scrolled ThingDef List

    private struct RightListItem
    {
        public enum ItemType { Header, Entry }
        public ItemType type;
        public string label;
        public float yOffset;
        public float height;
        public ThingDef? def;
        public bool allowed;
    }

    private void Notify_RightSearchChanged()
    {
        rightScrollPosition.y = 0;
        rightNeedRebuildList = true;
        // Search should never trigger highlight
        highlightFramesRemaining = 0;
    }

    private string GetCategoryKey(ThingDef def)
    {
        var cat = def.FirstThingCategory;
        if (cat != null) return cat.LabelCap.Resolve();
        return "MST.Misc".Translate().RawText;
    }

    private void RebuildRightFlattenedItems()
    {
        rightFlattenedItems.Clear();
        if (previewFilter == null) return;

        var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
        var filtered = allDefs.Where(def => rightQuickSearch.filter.Matches(def.label)).ToList();

        var grouped = filtered
            .GroupBy(def => GetCategoryKey(def))
            .OrderBy(g => g.Key)
            .ToList();

        rightQuickSearch.noResultsMatched = filtered.Count == 0;

        // Track first allowed entry index for auto-scroll
        int firstAllowedIndex = -1;
        float y = 0;
        foreach (var group in grouped)
        {
            // Collect entries for this category
            var entries = group.OrderBy(d => d.LabelCap.Resolve()).ToList();
            if (showOnlyAllowed)
            {
                entries = entries.Where(d => previewFilter.Allows(d)).ToList();
            }
            if (entries.Count == 0) continue;

            // Category header
            rightFlattenedItems.Add(new RightListItem
            {
                type = RightListItem.ItemType.Header,
                label = group.Key,
                yOffset = y,
                height = CategoryHeaderHeight,
                def = null,
                allowed = false
            });
            y += CategoryHeaderHeight;

            // Entries (compact, no spacing)
            foreach (var def in entries)
            {
                bool allowed = previewFilter.Allows(def);
                if (firstAllowedIndex < 0 && allowed)
                    firstAllowedIndex = rightFlattenedItems.Count;
                rightFlattenedItems.Add(new RightListItem
                {
                    type = RightListItem.ItemType.Entry,
                    label = def.LabelCap.Resolve(),
                    yOffset = y,
                    height = EntryHeight,
                    def = def,
                    allowed = allowed
                });
                y += EntryHeight;
            }
        }

        firstAllowedScrollIndex = firstAllowedIndex;
        rightNeedRebuildList = false;
    }

    private void DrawRightPanel(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        float innerX = rect.x + Spacing;
        float innerY = rect.y + Spacing;
        float innerWidth = rect.width - Spacing * 2;
        float innerHeight = rect.height - Spacing * 2;

        if (previewFilter == null)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(innerX, innerY, innerWidth, innerHeight), "MST.ClickTestToPreview".Translate().RawText);
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        // Search bar
        float curY = innerY;
        Rect searchRect = new(innerX, curY, innerWidth, SearchBarHeight);
        rightQuickSearch.OnGUI(searchRect, Notify_RightSearchChanged);
        curY += SearchBarHeight + Spacing;

        // List area
        float listHeight = innerHeight - SearchBarHeight - Spacing;
        Rect listRect = new(innerX, curY, innerWidth, listHeight);

        if (rightNeedRebuildList)
        {
            RebuildRightFlattenedItems();

            // Auto-scroll to first allowed item (header-aware)
            if (firstAllowedScrollIndex >= 0)
            {
                int scrollTargetIndex = firstAllowedScrollIndex;
                int searchStart = Math.Max(0, firstAllowedScrollIndex - 16);
                for (int i = searchStart; i < firstAllowedScrollIndex; i++)
                {
                    if (rightFlattenedItems[i].type == RightListItem.ItemType.Header)
                    {
                        scrollTargetIndex = i;
                        break;
                    }
                }
                rightScrollPosition.y = rightFlattenedItems[scrollTargetIndex].yOffset;
            }
        }

        if (rightFlattenedItems.Count == 0)
        {
            return;
        }

        // Virtual scroll: calculate total height and view rect
        float totalHeight = rightFlattenedItems[^1].yOffset + rightFlattenedItems[^1].height + 10f;
        float viewWidth = listRect.width - 16f;
        Rect viewRect = new(0f, 0f, viewWidth, totalHeight);
        Widgets.BeginScrollView(listRect, ref rightScrollPosition, viewRect);

        // Only draw visible items
        int startIndex = FindFirstVisibleRightItem(rightScrollPosition.y);
        int endIndex = FindLastVisibleRightItem(rightScrollPosition.y + listRect.height);

        for (int i = startIndex; i <= endIndex; i++)
        {
            DrawRightListItem(i, viewWidth);
        }

        Widgets.EndScrollView();

        // Decrement highlight frame counter each frame
        if (highlightFramesRemaining > 0)
            highlightFramesRemaining--;
    }

    private int FindFirstVisibleRightItem(float scrollY)
    {
        for (int i = 0; i < rightFlattenedItems.Count; i++)
        {
            if (rightFlattenedItems[i].yOffset + rightFlattenedItems[i].height > scrollY)
                return i;
        }
        return 0;
    }

    private int FindLastVisibleRightItem(float viewBottom)
    {
        for (int i = rightFlattenedItems.Count - 1; i >= 0; i--)
        {
            if (rightFlattenedItems[i].yOffset < viewBottom)
                return i;
        }
        return rightFlattenedItems.Count - 1;
    }

    private void DrawRightListItem(int index, float viewWidth)
    {
        var item = rightFlattenedItems[index];

        if (item.type == RightListItem.ItemType.Header)
        {
            // Draw category header
            Rect headerRect = new(0f, item.yOffset, viewWidth, CategoryHeaderHeight);
            Widgets.Label(headerRect, item.label);
        }
        else
        {
            // Highlight all allowed items with fade-out over 90 frames
            if (item.allowed && highlightFramesRemaining > 0)
            {
                float alpha = highlightFramesRemaining / 90f * 0.6f;
                Rect highlightRect = new(0f, item.yOffset, viewWidth, EntryHeight);
                Widgets.DrawRectFast(highlightRect, new Color(1f, 0.84f, 0f, alpha));
            }

            // Draw entry with icon, label, and non-editable toggle
            float entryX = 10f;
            float entryWidth = viewWidth - 10f;
            float toggleSize = 20f;

            float curX = entryX + 4f;

            // Icon
            if (item.def != null)
            {
                Rect iconRect = new(curX, item.yOffset + (EntryHeight - IconSize) / 2f, IconSize, IconSize);
                try
                {
                    Widgets.DefIcon(iconRect, item.def);
                }
                catch
                {
                    // Icon rendering may fail for some defs; skip silently
                }
                curX += IconSize + 6f;
            }

            // Label
            Rect labelRect = new(curX, item.yOffset, entryWidth - (curX - entryX) - toggleSize - 6f, EntryHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = item.allowed ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(labelRect, item.label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Tooltip with def description
            if (item.def != null && !item.def.description.NullOrEmpty())
            {
                Rect tooltipRect = new(0f, item.yOffset, viewWidth, EntryHeight);
                TooltipHandler.TipRegion(tooltipRect, item.def.description);
            }

            // Editable toggle for allowed state (modifies extra lists)
            float toggleX = entryX + entryWidth - toggleSize - 4f;
            Rect toggleRect = new(toggleX, item.yOffset + (EntryHeight - toggleSize) / 2f, toggleSize, toggleSize);
            bool current = item.allowed;
            Widgets.Checkbox(new Vector2(toggleRect.x, toggleRect.y), ref current, toggleSize, disabled: false);
            if (current != item.allowed)
            {
                var def = item.def;
                if (def != null)
                {
                    if (current)
                    {
                        // Was disallowed, now allowed → add to extra allowed, remove from extra disallowed
                        if (!workingExtraAllowedDefs.Contains(def))
                            workingExtraAllowedDefs.Add(def);
                        workingExtraDisallowedDefs.Remove(def);
                    }
                    else
                    {
                        // Was allowed, now disallowed → add to extra disallowed, remove from extra allowed
                        if (!workingExtraDisallowedDefs.Contains(def))
                            workingExtraDisallowedDefs.Add(def);
                        workingExtraAllowedDefs.Remove(def);
                    }
                    item.allowed = current;
                    rightFlattenedItems[index] = item;
                    previewFilter = BuildFilterFromOriginalWithEdits();
                    rightNeedRebuildList = true;
                }
            }
        }
    }

    #endregion

    protected override string? GetCopyData()
    {
        var filter = BuildFilterFromOriginalWithEdits();
        return SerializationHelper.SerializeThingFilter(filter);
    }

    protected override bool OnPasteData(string data)
    {
        var newFilter = SerializationHelper.DeserializeThingFilter(data);
        if (newFilter != null)
        {
            InitFromFilter(newFilter);
            return true;
        }
        return false;
    }

    protected override void OnSave() => SaveAndClose();
    protected override void OnCancel() => Close();

    private ThingFilter BuildFilterFromOriginalWithEdits()
    {
        var filter = new ThingFilter();
        filter.CopyAllowancesFrom(originalFilter);
        // Write edited fields directly via reflection; ResolveReferences will
        // convert them into the allowedDefs/ disallowedSpecialFilters state.
        SerializationHelper.TfCategoriesField?.SetValue(filter, workingCategories.Select(d => d.defName).ToList());
        SerializationHelper.TfDisallowedCategoriesField?.SetValue(filter, workingDisallowedCategories.Select(d => d.defName).ToList());
        SerializationHelper.TfSpecialFiltersToAllowField?.SetValue(filter, workingSpecialFiltersToAllow.Select(d => d.defName).ToList());
        SerializationHelper.TfSpecialFiltersToDisallowField?.SetValue(filter, workingSpecialFiltersToDisallow.Select(d => d.defName).ToList());
        SerializationHelper.TfThingDefsField?.SetValue(filter, workingThingDefs.ToList());
        SerializationHelper.TfStuffCategoriesToAllowField?.SetValue(filter, workingStuffCategoriesToAllow.ToList());
        // New fields
        SerializationHelper.TfTradeTagsToAllowField?.SetValue(filter, workingTradeTagsToAllow.ToList());
        SerializationHelper.TfTradeTagsToDisallowField?.SetValue(filter, workingTradeTagsToDisallow.ToList());
        SerializationHelper.TfThingSetMakerTagsToAllowField?.SetValue(filter, workingThingSetMakerTagsToAllow.ToList());
        SerializationHelper.TfThingSetMakerTagsToDisallowField?.SetValue(filter, workingThingSetMakerTagsToDisallow.ToList());
        SerializationHelper.TfDisallowedThingDefsField?.SetValue(filter, workingDisallowedThingDefs.ToList());
        // Clear allowedDefs before ResolveReferences to prevent merging with
        // the original filter's allowedDefs
        SerializationHelper.TfAllowedDefsField?.SetValue(filter, new HashSet<ThingDef>());
        filter.ResolveReferences();

        // Apply extra diff lists to capture states not representable by field lists alone
        foreach (var def in workingExtraAllowedDefs)
            filter.SetAllow(def, true);
        foreach (var def in workingExtraDisallowedDefs)
            filter.SetAllow(def, false);

        return filter;
    }

    private void SaveAndClose()
    {
        var filter = BuildFilterFromOriginalWithEdits();
        if (onSelectedCallback != null)
        {
            onSelectedCallback?.Invoke(filter);
        }
        else
        {
            config!.ApplyValue(data!, filter);
        }
        Close();
    }
}
