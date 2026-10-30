using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class CurveEditorWindow : EditorWindowBase
{
    private SimpleCurve curve;
    private SimpleCurveDrawerStyle style;
    private Action<SimpleCurve?>? onSaveCallback;

    private int? selectedPointIndex = null;
    private bool dragging = false;
    private bool panning = false;
    private Vector2 lastMousePosition = Vector2.zero;
    private Vector2 scrollPosition = Vector2.zero;

    private float newPointX = 0f;
    private float newPointY = 0f;

    private const float PointRadius = 8f;
    private const float LeftPanelWidth = 300f;
    private const float ButtonHeight = 30f;
    private const float FieldHeight = 24f;
    private const float MeasureLeftPadding = 60f;
    private const float MeasureBottomPadding = 30f;
    private const float ZoomResetButtonWidth = 110f;

    public override Vector2 InitialSize => new(1100f, 700f);

    protected override string SaveButtonLabel => "MST.Save".Translate().RawText;
    protected override string CancelButtonLabel => "MST.Cancel".Translate().RawText;
    protected override bool ShowCopyButton => true;
    protected override bool ShowPasteButton => true;

    /// <summary>确定：写回曲线并关闭窗口。</summary>
    protected override void OnSave()
    {
        SaveCurve();
        Close();
    }

    /// <summary>取消：直接关闭，不写回（等价于放弃本次编辑）。</summary>
    protected override void OnCancel() => Close();

    /// <summary>底栏中段：缩放重置（只影响视图，不改动曲线数据）。</summary>
    protected override void DrawMiddleContent(ref float curX, Rect rect)
    {
        Rect zoomResetRect = new(curX, rect.y, ZoomResetButtonWidth, rect.height);
        if (Widgets.ButtonText(zoomResetRect, "MST.ResetZoom".Translate().RawText))
        {
            ResetZoom();
        }
        TooltipHandler.TipRegion(zoomResetRect, "MST.ResetZoomTooltip".Translate().RawText);
        curX += ZoomResetButtonWidth + Spacing;
    }

    public CurveEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        var raw = (SimpleCurve?)config.GetRawValue(data);
        this.curve = raw != null ? CloneCurve(raw) : new SimpleCurve();

        style = new SimpleCurveDrawerStyle
        {
            DrawBackground = true,
            DrawBackgroundLines = true,
            DrawMeasures = true,
            DrawPoints = true,
            DrawCurveMousePoint = true,
            UseAntiAliasedLines = true,
            MeasureLabelsXCount = 8,
            MeasureLabelsYCount = 6,
            LabelX = "X"
        };

        forcePause = true;
        doCloseX = true;
        draggable = false;
    }

    public CurveEditorWindow(string title, SimpleCurve initialCurve, Action<SimpleCurve?> onSave)
    {
        pickerTitle = title;
        onSaveCallback = onSave;
        curve = initialCurve != null ? CloneCurve(initialCurve) : new SimpleCurve();

        style = new SimpleCurveDrawerStyle
        {
            DrawBackground = true,
            DrawBackgroundLines = true,
            DrawMeasures = true,
            DrawPoints = true,
            DrawCurveMousePoint = true,
            UseAntiAliasedLines = true,
            MeasureLabelsXCount = 8,
            MeasureLabelsYCount = 6,
            LabelX = "X"
        };

        forcePause = true;
        doCloseX = true;
        draggable = false;
    }

    private SimpleCurve CloneCurve(SimpleCurve source)
    {
        SimpleCurve clone = new();
        foreach (CurvePoint point in source.Points)
        {
            clone.Add(point.x, point.y);
        }
        return clone;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        float titleHeight = 32f;
        Rect titleRect = new(0f, 0f, inRect.width, titleHeight);
        string titleText = GetBaseTitle();
        Widgets.Label(titleRect, titleText);

        float contentHeight = inRect.height - titleHeight - Spacing - BottomBarHeight - Spacing;
        Rect contentRect = new(0f, titleHeight + Spacing, inRect.width, contentHeight);

        Rect leftPanelRect = new(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
        Rect curveRect = new(leftPanelRect.xMax + Spacing, contentRect.y, contentRect.width - LeftPanelWidth - Spacing, contentRect.height);

        DrawLeftPanel(leftPanelRect);
        DrawCurveArea(curveRect);

        HandleInput(curveRect);

        Rect bottomRect = new(inRect.x, contentRect.yMax + Spacing, inRect.width, BottomBarHeight);
        DrawBottomBar(bottomRect);
    }

    private void DrawLeftPanel(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        float curY = rect.y + Spacing;
        float innerWidth = rect.width - Spacing * 2;

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;

        Rect pointsLabelRect = new(rect.x + Spacing, curY, innerWidth, 24f);
        Widgets.Label(pointsLabelRect, "MST.Points".Translate().RawText);
        curY += pointsLabelRect.height + Spacing;

        float addSectionHeight = FieldHeight * 2 + Spacing * 2 + ButtonHeight;
        float listHeight = rect.height - (curY - rect.y) - addSectionHeight - Spacing * 2;

        Rect listRect = new(rect.x + Spacing, curY, innerWidth, listHeight);
        DrawPointsList(listRect);
        curY = listRect.yMax + Spacing;

        Rect addSectionRect = new(rect.x + Spacing, curY, innerWidth, addSectionHeight);
        DrawAddPointSection(addSectionRect);
    }

    private void DrawPointsList(Rect rect)
    {
        Rect viewRect = new(0f, 0f, rect.width - 16f, curve.PointsCount * 28f + 4f);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

        float curY = 4f;
        for (int i = 0; i < curve.PointsCount; i++)
        {
            CurvePoint point = curve[i];
            Rect rowRect = new(0f, curY, viewRect.width, 26f);

            bool isSelected = selectedPointIndex == i;
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rowRect);
            }

            string pointText = $"({point.x:F2}, {point.y:F2})";
            Rect labelRect = new(rowRect.x + 4f, rowRect.y + 3f, rowRect.width - 50f, 20f);
            Widgets.Label(labelRect, pointText);

            Rect deleteRect = new(rowRect.xMax - 26f, rowRect.y + 3f, 22f, 20f);
            if (Widgets.ButtonText(deleteRect, "X"))
            {
                RemovePoint(i);
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                selectedPointIndex = i;
                newPointX = point.x;
                newPointY = point.y;
            }

            curY += 28f;
        }

        Widgets.EndScrollView();
    }

    private void DrawAddPointSection(Rect rect)
    {
        float curY = rect.y;

        Rect xLabelRect = new(rect.x, curY, 20f, FieldHeight);
        Widgets.Label(xLabelRect, "X:");
        Rect xFieldRect = new(rect.x + 22f, curY, rect.width - 22f, FieldHeight);
        newPointX = Widgets.TextField(xFieldRect, newPointX.ToString("F2")).ParseFloatOr(newPointX);
        curY += FieldHeight + Spacing;

        Rect yLabelRect = new(rect.x, curY, 20f, FieldHeight);
        Widgets.Label(yLabelRect, "Y:");
        Rect yFieldRect = new(rect.x + 22f, curY, rect.width - 22f, FieldHeight);
        newPointY = Widgets.TextField(yFieldRect, newPointY.ToString("F2")).ParseFloatOr(newPointY);
        curY += FieldHeight + Spacing;

        Rect addButtonRect = new(rect.x, curY, rect.width, ButtonHeight);
        if (Widgets.ButtonText(addButtonRect, "MST.AddPoint".Translate().RawText))
        {
            AddPoint(newPointX, newPointY);
        }
    }

    private void DrawCurveArea(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.18f));

        SimpleCurveDrawer.DrawCurve(rect, curve, style);

        if (selectedPointIndex.HasValue && selectedPointIndex.Value < curve.PointsCount)
        {
            DrawSelectedPointHighlight(rect, selectedPointIndex.Value);
        }
    }

    private Rect GetActualGraphRect(Rect curveRect)
    {
        Rect graphRect = curveRect;
        if (style.DrawMeasures)
        {
            graphRect.xMin += MeasureLeftPadding;
            graphRect.yMax -= MeasureBottomPadding;
        }
        return graphRect;
    }

    private void DrawSelectedPointHighlight(Rect curveRect, int index)
    {
        if (index < 0 || index >= curve.PointsCount) return;

        CurvePoint point = curve[index];
        Rect viewRect = curve.View.rect;
        Rect graphRect = GetActualGraphRect(curveRect);

        Vector2 screenPos = SimpleCurveDrawer.CurveToScreenCoordsInsideScreenRect(graphRect, viewRect, point.Loc);
        screenPos.x += graphRect.x;
        screenPos.y += graphRect.y;

        float highlightSize = PointRadius * 2.5f;
        Rect highlightRect = new(screenPos.x - highlightSize / 2f, screenPos.y - highlightSize / 2f, highlightSize, highlightSize);

        // 点被缩放/平移移到视野之外时，高亮只保留落在绘图区内的部分，不再溢出到左侧面板或测量轴
        Rect visibleRect = Rect.MinMaxRect(
            Mathf.Max(highlightRect.xMin, graphRect.xMin),
            Mathf.Max(highlightRect.yMin, graphRect.yMin),
            Mathf.Min(highlightRect.xMax, graphRect.xMax),
            Mathf.Min(highlightRect.yMax, graphRect.yMax));
        if (visibleRect.width <= 0f || visibleRect.height <= 0f) return;

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 0f, 0.5f);
        GUI.DrawTexture(visibleRect, BaseContent.WhiteTex);
        GUI.color = prevColor;
    }

    private void HandleInput(Rect curveRect)
    {
        if (!Mouse.IsOver(curveRect)) return;

        Event current = Event.current;
        Rect graphRect = GetActualGraphRect(curveRect);

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 localMousePos = new(mousePos.x - graphRect.x, mousePos.y - graphRect.y);

            int clickedPointIndex = GetPointAtScreenPosition(localMousePos, graphRect);

            if (clickedPointIndex >= 0)
            {
                selectedPointIndex = clickedPointIndex;
                dragging = true;
                CurvePoint point = curve[clickedPointIndex];
                newPointX = point.x;
                newPointY = point.y;
            }
            else
            {
                selectedPointIndex = null;
            }

            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0)
        {
            dragging = false;
        }
        else if (current.type == EventType.MouseDrag && dragging && selectedPointIndex.HasValue)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 localMousePos = new(mousePos.x - graphRect.x, mousePos.y - graphRect.y);

            CurvePoint newPoint = new(SimpleCurveDrawer.ScreenToCurveCoords(graphRect, curve.View.rect, localMousePos));

            curve[selectedPointIndex.Value] = new CurvePoint(newPoint.x, newPoint.y);
            curve.SortPoints();

            newPointX = newPoint.x;
            newPointY = newPoint.y;

            current.Use();
        }
        else if (current.type == EventType.MouseDown && current.button == 1)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2 localMousePos = new(mousePos.x - graphRect.x, mousePos.y - graphRect.y);

            int clickedPointIndex = GetPointAtScreenPosition(localMousePos, graphRect);

            if (clickedPointIndex >= 0)
            {
                List<FloatMenuOption> options = new()
                {
                    new FloatMenuOption("MST.DeletePoint".Translate().RawText, () => RemovePoint(clickedPointIndex)),
                    new FloatMenuOption("MST.DuplicatePoint".Translate().RawText, () => AddPoint(curve[clickedPointIndex].x + 0.1f, curve[clickedPointIndex].y))
                };
                // 子窗口登记：否则点菜单项的鼠标按下会经 NotifyOutsideClicks 关掉本窗口。
                OpenChildWindow(new FloatMenu(options));
                current.Use();
            }
            else if (Mouse.IsOver(graphRect))
            {
                CurvePoint newPoint = new(SimpleCurveDrawer.ScreenToCurveCoords(graphRect, curve.View.rect, localMousePos));
                AddPoint(newPoint.x, newPoint.y);
                current.Use();
            }
        }
        else if (current.type == EventType.MouseDown && current.button == 2)
        {
            panning = true;
            lastMousePosition = current.mousePosition;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 2)
        {
            panning = false;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && panning)
        {
            Vector2 currentMousePosition = current.mousePosition;
            Vector2 delta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            HandlePan(graphRect, delta);

            current.Use();
        }
        else if (current.type == EventType.ScrollWheel)
        {
            HandleZoom(graphRect, current.delta.y);
            current.Use();
        }
    }

    private void HandlePan(Rect graphRect, Vector2 screenDelta)
    {
        Rect viewRect = curve.View.rect;

        float curveDeltaX = -screenDelta.x * viewRect.width / graphRect.width;
        float curveDeltaY = screenDelta.y * viewRect.height / graphRect.height;

        viewRect.x += curveDeltaX;
        viewRect.y += curveDeltaY;

        curve.View.rect = viewRect;
    }

    private int GetPointAtScreenPosition(Vector2 localScreenPos, Rect graphRect)
    {
        Rect viewRect = curve.View.rect;

        for (int i = 0; i < curve.PointsCount; i++)
        {
            Vector2 pointScreenPos = SimpleCurveDrawer.CurveToScreenCoordsInsideScreenRect(graphRect, viewRect, curve[i].Loc);
            float distance = Vector2.Distance(localScreenPos, pointScreenPos);
            if (distance <= PointRadius)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleZoom(Rect curveRect, float scrollDelta)
    {
        if (curve.PointsCount == 0) return;

        Rect viewRect = curve.View.rect;
        float zoomFactor = scrollDelta > 0 ? 1.1f : 0.9f;

        Vector2 center = new(viewRect.center.x, viewRect.center.y);

        float newWidth = viewRect.width * zoomFactor;
        float newHeight = viewRect.height * zoomFactor;

        // newWidth = Mathf.Clamp(newWidth, 0.1f, 10000f);
        // newHeight = Mathf.Clamp(newHeight, 0.1f, 10000f);

        curve.View.rect = new Rect(center.x - newWidth / 2f, center.y - newHeight / 2f, newWidth, newHeight);
    }

    private void AddPoint(float x, float y)
    {
        curve.Add(x, y);
        selectedPointIndex = curve.PointsCount - 1;
        curve.View.SetViewRectAround(curve);
    }

    private void RemovePoint(int index)
    {
        if (index >= 0 && index < curve.PointsCount)
        {
            curve.Points.RemoveAt(index);
            if (selectedPointIndex == index)
            {
                selectedPointIndex = null;
            }
            else if (selectedPointIndex > index)
            {
                selectedPointIndex--;
            }
            if (curve.PointsCount > 0)
            {
                curve.View.SetViewRectAround(curve);
            }
        }
    }

    private void SaveCurve()
    {
        if (onSaveCallback != null)
        {
            onSaveCallback(curve.PointsCount > 0 ? curve : null);
        }
        else
        {
            config!.ApplyValue(data!, curve);
        }
    }

    /// <summary>把视图的缩放与平移恢复到自动适配状态（不影响曲线数据）。</summary>
    private void ResetZoom()
    {
        curve.View.SetViewRectAround(curve);
    }

    protected override string? GetCopyData() => SerializationHelper.SerializeSimpleCurve(curve);

    protected override bool OnPasteData(string data)
    {
        var newCurve = SerializationHelper.DeserializeSimpleCurve(data);
        if (newCurve != null)
        {
            curve = newCurve;
            selectedPointIndex = null;
            if (curve.PointsCount > 0)
                curve.View.SetViewRectAround(curve);
            return true;
        }
        return false;
    }

    protected override string GetBaseTitle()
    {
        if (pickerTitle != null) return pickerTitle;
        if (config != null)
            return $"{"MST.EditCurve".Translate().RawText} - {config.label.Translate().RawText}";
        return "";
    }
}

public static class StringExtensions
{
    public static float ParseFloatOr(this string str, float defaultValue)
    {
        if (float.TryParse(str, out float result))
        {
            return result;
        }
        return defaultValue;
    }
}
