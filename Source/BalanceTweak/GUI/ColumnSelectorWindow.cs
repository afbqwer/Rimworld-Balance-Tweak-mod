using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings.TweakUtility;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

public partial class BalanceTweakSettings
{
    public class ColumnSelectorWindow : Window
    {
        private Vector2 scrollPosition;

        private const float RowHeight = 28f;
        private const float HeaderHeight = 40f;

        // 拖动状态
        private StatColumnConfig? draggingColumn;
        private int dragTargetIndex = -1;

        // 重置确认状态
        private bool resetSortConfirm = false;

        public ColumnSelectorWindow()
        {
            doCloseX = true;
            forcePause = false;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = true;
            optionalTitle = "MST.ColumnSelectorTitle".Translate().RawText;
        }

        public override Vector2 InitialSize => new(500f, 650f);

        public override void Notify_ClickOutsideWindow()
        {
            Close();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 顶部功能按钮区域
            Rect headerRect = new(0, 0, inRect.width, HeaderHeight);
            DrawHeaderButtons(headerRect);

            // 列表区域
            Rect listRect = new(0, HeaderHeight + 10f, inRect.width, inRect.height - HeaderHeight - 10f);
            DrawColumnList(listRect);
        }

        private void DrawHeaderButtons(Rect rect)
        {
            float curX = rect.x;
            float btnWidth = 110f;
            float gap = 5f;

            // 1. 显示全部
            if (Widgets.ButtonText(new Rect(curX, rect.y, btnWidth, rect.height - 2f), "MST.ShowAll".Translate().RawText))
            {
                CurrentHiddenColumns.Clear();
                SyncVisibleColumns();
                resetSortConfirm = false; // 操作后重置确认状态
            }
            curX += btnWidth + gap;

            // 2. 隐藏全部
            if (Widgets.ButtonText(new Rect(curX, rect.y, btnWidth, rect.height - 2f), "MST.HideAll".Translate().RawText))
            {
                CurrentHiddenColumns.Clear();
                foreach (var col in allColumnSource)
                {
                    CurrentHiddenColumns.Add(col.id);
                }
                SyncVisibleColumns();
                resetSortConfirm = false;
            }
            curX += btnWidth + gap;

            // 3. 重置排序
            Rect resetBtnRect = new(curX, rect.y, btnWidth, rect.height - 2f);
            string resetLabel = resetSortConfirm ? "MST.ClickConfirm".Translate().RawText : "MST.ResetSort".Translate().RawText;

            // 如果等待确认，改变背景色提示
            if (resetSortConfirm)
            {
                GUI.color = Color.yellow;
            }

            if (Widgets.ButtonText(resetBtnRect, resetLabel))
            {
                if (resetSortConfirm)
                {
                    // 执行重置（当前类型）
                    columnOrders.Remove(curType);
                    SyncVisibleColumns();
                    resetSortConfirm = false;
                }
                else
                {
                    // 进入确认状态
                    resetSortConfirm = true;
                }
            }

            // 恢复颜色
            if (resetSortConfirm)
            {
                GUI.color = Color.white;
            }

            // 如果鼠标移开按钮，取消确认状态
            if (resetSortConfirm && !Mouse.IsOver(resetBtnRect))
            {
                resetSortConfirm = false;
            }

            curX += btnWidth + gap;

            // 4. 按类型操作 (下拉菜单)
            Rect typeBtnRect = new(curX, rect.y, btnWidth, rect.height - 2f);
            if (Widgets.ButtonText(typeBtnRect, "MST.ByType".Translate().RawText))
            {
                List<FloatMenuOption> menuOptions = new();
                foreach (ColumnDataType dataType in System.Enum.GetValues(typeof(ColumnDataType)))
                {
                    menuOptions.Add(new FloatMenuOption($"{"MST.ShowAll".Translate().RawText} ({("MST." + dataType.ToString()).Translate().RawText})", () =>
                    {
                        SetTypeVisibility(dataType, true);
                        resetSortConfirm = false;
                    }));
                    menuOptions.Add(new FloatMenuOption($"{"MST.HideAll".Translate().RawText} ({("MST." + dataType.ToString()).Translate().RawText})", () =>
                    {
                        SetTypeVisibility(dataType, false);
                        resetSortConfirm = false;
                    }));
                }
                menuOptions.Add(new FloatMenuOption($"{"MST.ShowAll".Translate().RawText} ({("MST.Stat").Translate().RawText})", () =>
                {
                    SetStatVisibility(true);
                    resetSortConfirm = false;
                }));
                menuOptions.Add(new FloatMenuOption($"{"MST.HideAll".Translate().RawText} ({("MST.Stat").Translate().RawText})", () =>
                {
                    SetStatVisibility(false);
                    resetSortConfirm = false;
                }));
                Find.WindowStack.Add(new FloatMenu(menuOptions));
            }
        }
        private void SetStatVisibility(bool visible)
        {
            var targetIds = allColumnSource
                .Where(c => c.stat != null)
                .Select(c => c.id)
                .ToList();
            if (visible)
            {
                CurrentHiddenColumns.RemoveAll(id => targetIds.Contains(id));
            }
            else
            {
                foreach (var id in targetIds)
                {
                    if (!CurrentHiddenColumns.Contains(id))
                    {
                        CurrentHiddenColumns.Add(id);
                    }
                }
            }
            SyncVisibleColumns();
        }

        private void SetTypeVisibility(ColumnDataType dataType, bool visible)
        {
            var targetIds = allColumnSource
                .Where(c => c.columnDataType == dataType)
                .Select(c => c.id)
                .ToList();
            if (visible)
            {
                CurrentHiddenColumns.RemoveAll(id => targetIds.Contains(id));
            }
            else
            {
                foreach (var id in targetIds)
                {
                    if (!CurrentHiddenColumns.Contains(id))
                    {
                        CurrentHiddenColumns.Add(id);
                    }
                }
            }
            SyncVisibleColumns();
        }

        private void DrawColumnList(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;

            float viewWidth = rect.width - 16f;
            float viewHeight = allColumnSource.Count * RowHeight;

            Rect viewRect = new(0, 0, viewWidth, viewHeight);

            // 根据存储的顺序对源数据进行排序显示
            var sortedColumns = allColumnSource
                .OrderBy(c => GetColumnSortOrder(c.id))
                .ToList();

            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0;
            for (int i = 0; i < sortedColumns.Count; i++)
            {
                var col = sortedColumns[i];
                Rect rowRect = new(0, y, viewWidth, RowHeight);

                // 斑马纹背景
                if (i % 2 == 0) Widgets.DrawAltRect(rowRect);

                // === 拖拽逻辑 ===
                float mouseContentY = Event.current.mousePosition.y;

                // 检测鼠标是否在当前行区域内
                bool isMouseOverRow = mouseContentY >= y && mouseContentY < y + RowHeight;

                // 检测鼠标是否在 Checkbox 区域 (X < 30f)
                bool isOverCheckbox = Event.current.mousePosition.x < 30f;

                // 开始拖拽
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && isMouseOverRow && !isOverCheckbox)
                {
                    draggingColumn = col;
                    dragTargetIndex = i;
                    Event.current.Use();
                }

                // 拖拽过程中更新目标位置
                if (draggingColumn != null && Event.current.type == EventType.MouseDrag)
                {
                    int newTargetIndex = Mathf.Clamp((int)((mouseContentY + RowHeight * 0.5f) / RowHeight), 0, sortedColumns.Count);

                    if (newTargetIndex != dragTargetIndex)
                    {
                        dragTargetIndex = newTargetIndex;
                    }
                    Event.current.Use();
                }

                // 绘制拖动指示线
                if (draggingColumn != null && i == dragTargetIndex)
                {
                    Widgets.DrawLine(new Vector2(0, y), new Vector2(viewWidth, y), Color.yellow, 2f);
                }

                // 绘制行背景和内容
                // 如果当前行是被拖动的行，绘制半透明效果
                if (draggingColumn == col)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                }
                else
                {
                    GUI.color = Color.white;
                }

                if (col.comment != null) TooltipHandler.TipRegion(rowRect, col.comment);

                bool isVisible = !CurrentHiddenColumns.Contains(col.id);
                bool newVisible = isVisible;

                // 绘制 Checkbox
                Widgets.Checkbox(new Vector2(5f, y + 2f), ref newVisible, RowHeight - 4f);

                if (newVisible != isVisible)
                {
                    if (newVisible)
                        CurrentHiddenColumns.Remove(col.id);
                    else
                        CurrentHiddenColumns.Add(col.id);

                    SyncVisibleColumns();
                }

                string infoText = $"{col.label} ({col.columnDataTypeLabel}, {col.columnTypeLabel})";
                Rect labelRect = new(30f, y, viewWidth - 35f, RowHeight);

                if (!newVisible)
                {
                    GUI.color = new Color(0.6f, 0.6f, 0.6f, GUI.color.a);
                }

                Widgets.Label(labelRect, infoText);
                GUI.color = Color.white;

                y += RowHeight;
            }

            Widgets.EndScrollView();
            Text.Anchor = TextAnchor.UpperLeft;

            // 处理鼠标松开
            if (Event.current.type == EventType.MouseUp && draggingColumn != null)
            {
                ApplyDragOrder(sortedColumns, draggingColumn, dragTargetIndex);
                draggingColumn = null;
                dragTargetIndex = -1;
            }
        }

        private void ApplyDragOrder(List<StatColumnConfig> currentList, StatColumnConfig dragItem, int targetIndex)
        {
            if (targetIndex < 0) return;

            int originalIndex = currentList.IndexOf(dragItem);
            if (originalIndex == targetIndex) return; // 位置不变，无需操作

            currentList.RemoveAt(originalIndex);
            // 若原索引小于目标索引，移除后目标索引应减一
            if (originalIndex < targetIndex)
            {
                targetIndex--;
            }
            currentList.Insert(targetIndex, dragItem);

            // 更新当前类型的排序顺序
            if (!columnOrders.ContainsKey(curType))
                columnOrders[curType] = new();
            var typeOrders = columnOrders[curType];
            typeOrders.Clear();
            for (int i = 0; i < currentList.Count; i++)
            {
                typeOrders[currentList[i].id] = i;
            }

            SyncVisibleColumns();
        }
    }
}