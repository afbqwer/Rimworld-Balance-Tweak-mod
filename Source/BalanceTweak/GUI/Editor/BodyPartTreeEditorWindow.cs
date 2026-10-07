using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak;

/// <summary>
/// BodyDef 部位树的树形编辑窗口。
/// 左侧是可直接操作的部位树：右键节点弹出上下文菜单（增删改、复制、上下移动），
/// 拖拽节点可以调整同级顺序或改变父子层级，双击节点可内联重命名。
/// 右侧只负责编辑选中节点的属性字段（def / customLabel / coverage / height / depth / groups）。
/// 保存时把整棵树编码为 <c>List&lt;string&gt;</c> 写回 <see cref="BodyData.parts"/>。
/// </summary>
public class BodyPartTreeEditorWindow : EditorWindowBase
{
    private const float RowHeight = 28f;
    private const float IndentWidth = 16f;
    private const float RightPanelWidth = 340f;
    private const float FieldHeight = 28f;
    private const float FieldSpacing = 6f;
    private const float ToggleWidth = 24f;
    private const float LabelWidth = 92f;
    private const float DragThresholdSqr = 25f;
    private const string RenameControlName = "BodyPartTreeRenameField";

    // 显式字面量数组：与 BodyPartHeight / BodyPartDepth 的枚举序号对齐，且便于 CheckLoc 识别引用
    private static readonly string[] HeightLabels = { "MST.Undefined", "MST.Bottom", "MST.Middle", "MST.Top" };
    private static readonly string[] DepthLabels = { "MST.Undefined", "MST.Inside", "MST.Outside" };

    private static readonly Color SelectionColor = new(0.3f, 0.5f, 0.8f, 0.35f);
    private static readonly Color DropChildColor = new(0.2f, 0.85f, 0.3f, 0.25f);
    private static readonly Color DropLineColor = new(0.2f, 0.9f, 0.3f, 0.9f);

    private BodyPartNode? root;
    private BodyPartNode? selected;
    private Vector2 treeScroll;

    private readonly List<(BodyPartNode node, int depth)> visible = new();

    // 内联重命名
    private BodyPartNode? renaming;
    private string renameBuffer = "";
    private bool renameFocused;

    // 拖拽
    private BodyPartNode? dragNode;
    private Vector2 dragStartMouse;
    private bool dragging;
    private DropInfo? dropInfo;

    private enum DropZone { Above, Child, Below }

    /// <summary>一次拖拽落点的计算结果（<see cref="BodyPartNode"/> 插入到 parent.children 的 index 处）。</summary>
    private readonly struct DropInfo
    {
        public readonly BodyPartNode parent;
        public readonly int index;
        public readonly BodyPartNode hoverNode;
        public readonly int hoverIndex;
        public readonly int hoverDepth;
        public readonly DropZone zone;

        public DropInfo(BodyPartNode parent, int index, BodyPartNode hoverNode, int hoverIndex, int hoverDepth, DropZone zone)
        {
            this.parent = parent;
            this.index = index;
            this.hoverNode = hoverNode;
            this.hoverIndex = hoverIndex;
            this.hoverDepth = hoverDepth;
            this.zone = zone;
        }
    }

    public BodyPartTreeEditorWindow(StatColumnConfig config, TweakData data)
    {
        this.config = config;
        this.data = data;
        root = BodyPartTreeCodec.Decode((List<string>?)config.GetRawValue(data));
        if (root == null && data.def is BodyDef bd)
            root = BodyPartTreeCodec.FromRecord(bd.corePart);
        selected = root;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new(880f, 680f);

    protected override string SaveButtonLabel => "MST.SaveDefList".Translate().RawText;
    protected override string CancelButtonLabel => "MST.CancelDefList".Translate().RawText;

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        float curY = inRect.y;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, curY, inRect.width, TitleHeight), GetBaseTitle());
        Text.Font = GameFont.Small;
        curY += TitleHeight + Spacing;

        float bodyHeight = inRect.height - (curY - inRect.y) - BottomBarHeight - Spacing;
        Rect bodyRect = new(inRect.x, curY, inRect.width, bodyHeight);

        float treeWidth = bodyRect.width - RightPanelWidth - Spacing;
        Rect treeRect = new(bodyRect.x, bodyRect.y, treeWidth, bodyRect.height);
        Rect panelRect = new(bodyRect.xMax - RightPanelWidth, bodyRect.y, RightPanelWidth, bodyRect.height);

        DrawTree(treeRect);
        DrawPanel(panelRect);

        DrawBottomBar(new Rect(inRect.x, bodyRect.yMax + Spacing, inRect.width, BottomBarHeight));
    }

    #region 左树

    private void DrawTree(Rect rect)
    {
        visible.Clear();
        CollectVisible(root, 0);

        float totalHeight = Mathf.Max(visible.Count * RowHeight, 1f);
        Rect viewRect = new(0, 0, rect.width - 16f, totalHeight);

        // 进入 ScrollView 之前记录窗口坐标下的鼠标位置（用于判断是否在树区域内）
        Vector2 windowMouse = Event.current.mousePosition;

        Widgets.BeginScrollView(rect, ref treeScroll, viewRect);

        // 组内坐标已随滚动偏移，直接使用（与 local rect 同一坐标系）
        Vector2 mouse = Event.current.mousePosition;
        HandleTreeInput(rect, viewRect, windowMouse, mouse);

        bool mouseInTree = rect.Contains(windowMouse);

        for (int i = 0; i < visible.Count; i++)
        {
            var (node, depth) = visible[i];
            DrawRow(viewRect, i, node, depth, mouse, mouseInTree);
        }

        DrawDropIndicator(viewRect);

        Widgets.EndScrollView();
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawRow(Rect viewRect, int index, BodyPartNode node, int depth, Vector2 mouse, bool mouseInTree)
    {
        Rect row = new(0, index * RowHeight, viewRect.width, RowHeight);

        if (node == selected)
            Widgets.DrawBoxSolid(row, SelectionColor);
        else if (mouseInTree && !dragging && row.Contains(mouse))
            Widgets.DrawLightHighlight(row);

        if (dropInfo.HasValue && dropInfo.Value.hoverNode == node && dropInfo.Value.zone == DropZone.Child)
            Widgets.DrawBoxSolid(row, DropChildColor);

        float x = depth * IndentWidth;
        Rect toggleRect = new(x, row.y + 2f, ToggleWidth, RowHeight - 4f);
        if (node.children.Count > 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(toggleRect, node.expanded ? "▼" : "▶");
            Text.Anchor = TextAnchor.UpperLeft;
        }
        x += ToggleWidth + 2f;

        Rect labelRect = new(x, row.y, Mathf.Max(row.width - x, 1f), RowHeight);
        if (node == renaming)
        {
            DrawRenameField(labelRect);
            return;
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, BuildNodeLabel(node));
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawDropIndicator(Rect viewRect)
    {
        if (!dropInfo.HasValue || dropInfo.Value.zone == DropZone.Child) return;

        var d = dropInfo.Value;
        float indent = d.hoverDepth * IndentWidth + ToggleWidth + 2f;
        float y = d.zone == DropZone.Above ? d.hoverIndex * RowHeight : (d.hoverIndex + 1) * RowHeight;
        Rect line = new(indent, y - 1f, Mathf.Max(viewRect.width - indent, 1f), 2f);
        Widgets.DrawBoxSolid(line, DropLineColor);
    }

    private void CollectVisible(BodyPartNode? node, int depth)
    {
        if (node == null) return;
        visible.Add((node, depth));
        if (!node.expanded) return;
        foreach (var child in node.children)
            CollectVisible(child, depth + 1);
    }

    private static string GetNodeLabel(BodyPartNode node)
    {
        if (!string.IsNullOrEmpty(node.customLabel)) return node.customLabel!;
        if (node.def != null)
        {
            var cap = node.def.LabelCap.Resolve();
            return cap.NullOrEmpty() ? node.def.defName : cap;
        }
        return "?";
    }

    /// <summary>节点行文本：名称 + （覆盖率 / 部位定义的生命值）。</summary>
    private string BuildNodeLabel(BodyPartNode node)
    {
        if (node.def == null) return $"";
        return $"{GetNodeLabel(node)}  [C:{node.coverage:0.###}|H:{node.def.hitPoints}]";
    }

    #endregion

    #region 树输入（右键菜单 / 拖拽 / 重命名）

    private void HandleTreeInput(Rect rect, Rect viewRect, Vector2 windowMouse, Vector2 mouse)
    {
        var evt = Event.current;

        dropInfo = dragging && dragNode != null ? ComputeDrop(mouse, viewRect) : null;

        // 拖拽结束：落点有效则执行移动
        if (evt.type == EventType.MouseUp && evt.button == 0)
        {
            if (dragging && dragNode != null)
            {
                if (dropInfo.HasValue)
                    ApplyDrop(dragNode, dropInfo.Value.parent, dropInfo.Value.index);
                evt.Use();
            }
            dragNode = null;
            dragging = false;
            dropInfo = null;
            return;
        }

        // 拖拽过程中判定是否达到拖拽阈值
        if (evt.type == EventType.MouseDrag && evt.button == 0 && dragNode != null)
        {
            if (!dragging && (mouse - dragStartMouse).sqrMagnitude > DragThresholdSqr)
                dragging = true;
            return;
        }

        if (evt.type != EventType.MouseDown) return;
        if (!rect.Contains(windowMouse)) return;

        int rowIndex = GetRowIndexAt(mouse, viewRect);
        if (rowIndex < 0) return;

        var node = visible[rowIndex].node;

        // 正在重命名的节点：把事件交给输入框自己处理
        if (node == renaming) return;

        // 点击其他节点时提交当前重命名
        if (renaming != null) CommitRename();

        if (evt.button == 1)
        {
            selected = node;
            OpenRowMenu(node);
            evt.Use();
            return;
        }

        if (evt.button != 0) return;

        // 展开/折叠三角
        if (IsInToggle(mouse, rowIndex) && node.children.Count > 0)
        {
            node.expanded = !node.expanded;
            evt.Use();
            return;
        }

        // 双击内联重命名
        if (evt.clickCount == 2)
        {
            StartRename(node);
            evt.Use();
            return;
        }

        // 选中并准备拖拽
        selected = node;
        dragNode = node;
        dragStartMouse = mouse;
        dragging = false;
        evt.Use();
    }

    private int GetRowIndexAt(Vector2 mouse, Rect viewRect)
    {
        if (mouse.x < 0f || mouse.x > viewRect.width) return -1;
        int i = Mathf.FloorToInt(mouse.y / RowHeight);
        if (i < 0 || i >= visible.Count) return -1;
        return i;
    }

    private bool IsInToggle(Vector2 mouse, int rowIndex)
    {
        int depth = visible[rowIndex].depth;
        float x = depth * IndentWidth;
        float y = rowIndex * RowHeight;
        return mouse.x >= x && mouse.x <= x + ToggleWidth && mouse.y >= y && mouse.y <= y + RowHeight;
    }

    private DropInfo? ComputeDrop(Vector2 mouse, Rect viewRect)
    {
        if (dragNode == null || dragNode == root) return null;

        int idx = GetRowIndexAt(mouse, viewRect);
        if (idx < 0) return null;

        var (node, depth) = visible[idx];
        float localY = mouse.y - idx * RowHeight;
        DropZone zone = localY < RowHeight * 0.25f ? DropZone.Above
                      : localY > RowHeight * 0.75f ? DropZone.Below
                      : DropZone.Child;

        BodyPartNode parent;
        int index;

        if (node == root)
        {
            // 根节点没有同级：悬停其上方=插入到最前，其余=插入到末尾
            parent = node;
            index = zone == DropZone.Above ? 0 : node.children.Count;
            zone = DropZone.Child;
        }
        else if (zone == DropZone.Child)
        {
            parent = node;
            index = node.children.Count;
        }
        else
        {
            var p = FindParent(root!, node);
            if (p == null) return null;
            parent = p;
            index = p.children.IndexOf(node) + (zone == DropZone.Below ? 1 : 0);
        }

        // 不能把节点拖进自己或自己的子树
        if (parent == dragNode || IsDescendantOf(parent, dragNode)) return null;

        return new DropInfo(parent, index, node, idx, depth, zone);
    }

    private void ApplyDrop(BodyPartNode node, BodyPartNode parent, int index)
    {
        if (node == root || node == parent) return;
        if (IsDescendantOf(parent, node)) return;

        var oldParent = FindParent(root!, node);
        if (oldParent == null) return;

        int oldIndex = oldParent.children.IndexOf(node);
        // 原地放置（前后紧邻自身）不做无意义的重排
        if (oldParent == parent && (index == oldIndex || index == oldIndex + 1)) return;

        oldParent.children.RemoveAt(oldIndex);
        if (oldParent == parent && oldIndex < index) index--;
        index = Mathf.Clamp(index, 0, parent.children.Count);
        parent.children.Insert(index, node);
        parent.expanded = true;
        selected = node;
    }

    private static bool IsDescendantOf(BodyPartNode candidate, BodyPartNode ancestor)
    {
        foreach (var child in ancestor.children)
        {
            if (child == null) continue;
            if (child == candidate || IsDescendantOf(candidate, child)) return true;
        }
        return false;
    }

    #endregion

    #region 节点操作（右键菜单）

    private void OpenRowMenu(BodyPartNode node)
    {
        bool isRoot = node == root;
        var siblings = isRoot ? null : FindParent(root!, node)?.children;
        int sibIndex = siblings?.IndexOf(node) ?? -1;

        Action? addSibling = isRoot ? null : () => AddSiblingOf(node);
        Action? moveUp = isRoot || sibIndex <= 0 ? null : () => MoveNode(node, -1);
        Action? moveDown = isRoot || siblings == null || sibIndex >= siblings.Count - 1 ? null : () => MoveNode(node, 1);
        Action? delete = isRoot ? null : () => DeleteNode(node);

        var options = new List<FloatMenuOption>
        {
            new FloatMenuOption("MST.ChangePartDef".Translate().RawText, () => OpenDefSelector(node.def, def => node.def = def)),
            new FloatMenuOption("MST.AddChildPart".Translate().RawText, () => AddChildTo(node)),
            new FloatMenuOption("MST.AddSiblingPart".Translate().RawText, addSibling),
            new FloatMenuOption("MST.DuplicatePart".Translate().RawText, () => DuplicateNode(node)),
            new FloatMenuOption("MST.RenamePart".Translate().RawText, () => StartRename(node)),
            new FloatMenuOption("MST.MovePartUp".Translate().RawText, moveUp),
            new FloatMenuOption("MST.MovePartDown".Translate().RawText, moveDown),
            new FloatMenuOption("MST.DeletePart".Translate().RawText, delete),
        };
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void AddChildTo(BodyPartNode parent)
    {
        OpenDefSelector(null, def =>
        {
            var node = new BodyPartNode { def = def };
            parent.children.Add(node);
            parent.expanded = true;
            selected = node;
        });
    }

    private void AddSiblingOf(BodyPartNode node)
    {
        if (node == root) return;
        var parent = FindParent(root!, node);
        if (parent == null) return;
        OpenDefSelector(null, def =>
        {
            var sibling = new BodyPartNode { def = def };
            int i = parent.children.IndexOf(node);
            parent.children.Insert(i < 0 ? parent.children.Count : i + 1, sibling);
            parent.expanded = true;
            selected = sibling;
        });
    }

    private void DuplicateNode(BodyPartNode node)
    {
        var clone = CloneNode(node);
        if (node == root)
        {
            root!.children.Add(clone);
            root.expanded = true;
            selected = clone;
            return;
        }

        var parent = FindParent(root!, node);
        if (parent == null) return;
        int i = parent.children.IndexOf(node);
        parent.children.Insert(i < 0 ? parent.children.Count : i + 1, clone);
        parent.expanded = true;
        selected = clone;
    }

    private void DeleteNode(BodyPartNode node)
    {
        if (node == root) return;
        var parent = FindParent(root!, node);
        if (parent == null) return;
        parent.children.Remove(node);
        selected = parent;
    }

    private void MoveNode(BodyPartNode node, int delta)
    {
        if (node == root) return;
        var parent = FindParent(root!, node);
        if (parent == null) return;
        int i = parent.children.IndexOf(node);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= parent.children.Count) return;
        (parent.children[i], parent.children[j]) = (parent.children[j], parent.children[i]);
    }

    private static BodyPartNode CloneNode(BodyPartNode src)
    {
        var clone = new BodyPartNode
        {
            def = src.def,
            customLabel = src.customLabel,
            coverage = src.coverage,
            height = src.height,
            depth = src.depth,
            groups = src.groups != null ? new List<BodyPartGroupDef>(src.groups) : new List<BodyPartGroupDef>(),
            expanded = src.expanded,
        };
        if (src.children != null)
        {
            foreach (var child in src.children)
                if (child != null) clone.children.Add(CloneNode(child));
        }
        return clone;
    }

    private static BodyPartNode? FindParent(BodyPartNode current, BodyPartNode target)
    {
        foreach (var child in current.children)
        {
            if (child == target) return current;
            var found = FindParent(child, target);
            if (found != null) return found;
        }
        return null;
    }

    #endregion

    #region 内联重命名

    private void StartRename(BodyPartNode node)
    {
        renaming = node;
        renameBuffer = node.customLabel ?? "";
        renameFocused = false;
    }

    private void DrawRenameField(Rect rect)
    {
        GUI.SetNextControlName(RenameControlName);
        renameBuffer = Widgets.TextField(rect, renameBuffer);
        if (!renameFocused)
        {
            GUI.FocusControl(RenameControlName);
            renameFocused = true;
        }

        var evt = Event.current;
        if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
        {
            CommitRename();
            evt.Use();
        }
        else if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        {
            CancelRename();
            evt.Use();
        }
        else if (evt.type == EventType.MouseDown && !rect.Contains(evt.mousePosition))
        {
            CommitRename();
        }
    }

    private void CommitRename()
    {
        if (renaming != null)
            renaming.customLabel = string.IsNullOrEmpty(renameBuffer) ? null : renameBuffer;
        CancelRename();
    }

    private void CancelRename()
    {
        renaming = null;
        renameBuffer = "";
        renameFocused = false;
    }

    #endregion

    #region 右侧属性面板

    private void DrawPanel(Rect rect)
    {
        if (selected == null)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, FieldHeight), "MST.Empty".Translate().RawText);
            return;
        }

        float y = rect.y;

        DrawDefRow(rect, ref y);
        DrawLabeledField(rect, ref y, "MST.customLabel", fieldRect =>
        {
            selected!.customLabel = Widgets.TextField(fieldRect, selected.customLabel ?? "");
        });
        DrawLabeledField(rect, ref y, "MST.coverage", fieldRect =>
        {
            string text = Widgets.TextField(fieldRect, selected!.coverage.ToString());
            if (float.TryParse(text, out float v)) selected.coverage = v;
        });
        DrawLabeledField(rect, ref y, "MST.height", fieldRect =>
        {
            if (Widgets.ButtonText(fieldRect, HeightLabels[(int)selected!.height].Translate().RawText))
                OpenEnumMenu(HeightLabels, i => selected!.height = (BodyPartHeight)i);
        });
        DrawLabeledField(rect, ref y, "MST.depth", fieldRect =>
        {
            if (Widgets.ButtonText(fieldRect, DepthLabels[(int)selected!.depth].Translate().RawText))
                OpenEnumMenu(DepthLabels, i => selected!.depth = (BodyPartDepth)i);
        });
        DrawLabeledField(rect, ref y, "MST.groups", fieldRect =>
        {
            string txt = selected!.groups == null || selected.groups.Count == 0
                ? "MST.Empty".Translate().RawText
                : string.Join(", ", selected.groups.Select(g => g.LabelCap.Resolve()));
            if (Widgets.ButtonText(fieldRect, txt))
            {
                var node = selected;
                var groups = node.groups ?? new List<BodyPartGroupDef>();
                OpenChildWindow(new DefListEditorWindow(GetBaseTitle(), groups, typeof(BodyPartGroupDef),
                    list => node.groups = list?.Cast<BodyPartGroupDef>().ToList() ?? new List<BodyPartGroupDef>()));
            }
        });

        y += FieldSpacing;
        DrawStructureButtons(rect, ref y);

        y += FieldSpacing;
        Rect hintRect = new(rect.x, y, rect.width, Mathf.Max(rect.yMax - y, 1f));
        GameFont prevFont = Text.Font;
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(hintRect, "MST.BodyPartTreeHint".Translate().RawText);
        Text.Font = prevFont;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    /// <summary>
    /// 结构操作按钮，与树节点右键菜单的可用项一一对应。
    /// “更换部位定义”“重命名”已分别由 def 行按钮和 customLabel 输入框承担，不再重复放置。
    /// </summary>
    private void DrawStructureButtons(Rect rect, ref float y)
    {
        bool isRoot = selected == root;
        var siblings = isRoot ? null : FindParent(root!, selected!)?.children;
        int index = siblings?.IndexOf(selected!) ?? -1;

        float gap = Spacing;
        float btnW = (rect.width - gap) / 2f;

        for (int row = 0; row < 3; row++)
        {
            Rect left = new(rect.x, y, btnW, FieldHeight);
            Rect right = new(rect.x + btnW + gap, y, rect.width - btnW - gap, FieldHeight);

            switch (row)
            {
                case 0:
                    if (DrawPanelButton(left, "MST.AddChildPart", true)) AddChildTo(selected!);
                    if (DrawPanelButton(right, "MST.AddSiblingPart", !isRoot)) AddSiblingOf(selected!);
                    break;
                case 1:
                    if (DrawPanelButton(left, "MST.DuplicatePart", true)) DuplicateNode(selected!);
                    if (DrawPanelButton(right, "MST.DeletePart", !isRoot)) DeleteNode(selected!);
                    break;
                default:
                    if (DrawPanelButton(left, "MST.MovePartUp", !isRoot && index > 0)) MoveNode(selected!, -1);
                    if (DrawPanelButton(right, "MST.MovePartDown", !isRoot && siblings != null && index < siblings.Count - 1)) MoveNode(selected!, 1);
                    break;
            }

            y += FieldHeight + Spacing;
        }
    }

    private static bool DrawPanelButton(Rect rect, string labelKey, bool enabled)
    {
        bool prevEnabled = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = Widgets.ButtonText(rect, labelKey.Translate().RawText);
        GUI.enabled = prevEnabled;
        return clicked;
    }

    private void DrawDefRow(Rect rect, ref float y)
    {
        Rect labelRect = new(rect.x, y, LabelWidth, FieldHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, "MST.def".Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        if ("MST.defComment".TryTranslate(out var defComment))
            TooltipHandler.TipRegion(labelRect, defComment);

        float fieldX = rect.x + LabelWidth;
        float fieldWidth = rect.width - LabelWidth;
        const float LinkWidth = 30f;

        bool hasDef = selected!.def != null;
        float btnWidth = hasDef ? fieldWidth - LinkWidth - Spacing : fieldWidth;

        Rect btnRect = new(fieldX, y, btnWidth, FieldHeight);
        string defName = !hasDef
            ? "MST.SelectDef".Translate().RawText
            : $"{selected.def!.LabelCap.Resolve()}({selected.def.defName})";
        if (Widgets.ButtonText(btnRect, defName))
        {
            var node = selected;
            OpenDefSelector(node.def, def => node.def = def);
        }

        if (hasDef)
        {
            Rect linkRect = new(fieldX + btnWidth + Spacing, y, LinkWidth, FieldHeight);
            if (Widgets.ButtonText(linkRect, "->"))
            {
                // 跳转到该 BodyPartDef 对应的 BodyPartData。
                // 必须关闭本编辑窗口，否则它会盖在主窗口之上，看不到跳转结果。
                var target = TweakData.GetData(new TweakData.TweakID(selected.def!.defName, SettingType.BodyPart));
                if (target != null)
                {
                    TweakUtility.JumpToData(target);
                    Close();
                }
            }
            TooltipHandler.TipRegion(linkRect, "MST.JumpToDesc".Translate().RawText);
        }

        y += FieldHeight + FieldSpacing;
    }

    private void DrawLabeledField(Rect rect, ref float y, string labelKey, Action<Rect> drawField)
    {
        Rect labelRect = new(rect.x, y, LabelWidth, FieldHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;

        if ((labelKey + "Comment").TryTranslate(out var comment))
            TooltipHandler.TipRegion(labelRect, comment);

        Rect fieldRect = new(rect.x + LabelWidth, y, rect.width - LabelWidth, FieldHeight);
        drawField(fieldRect);

        y += FieldHeight + FieldSpacing;
    }

    private static void OpenEnumMenu(string[] labels, Action<int> onSelected)
    {
        var options = new List<FloatMenuOption>();
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            options.Add(new FloatMenuOption(labels[i].Translate().RawText, () => onSelected(index)));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void OpenDefSelector(BodyPartDef? current, Action<BodyPartDef> onSelected)
    {
        var allDefs = GenDefDatabase.GetAllDefsInDatabaseForDef(typeof(BodyPartDef)).ToList();
        OpenChildWindow(new DefSelectionWindow(GetBaseTitle(), allDefs, current, d =>
        {
            if (d is BodyPartDef bpd) onSelected(bpd);
        }));
    }

    #endregion

    protected override void OnSave()
    {
        CommitRename();
        var encoded = BodyPartTreeCodec.Encode(root);
        if (encoded != null) config!.ApplyValue(data!, encoded);
        Close();
    }

    protected override void OnCancel() => Close();
}
