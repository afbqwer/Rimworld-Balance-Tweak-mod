using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 「拖拽涂抹」状态机（**赋值式**）。
///
/// 语义：在某个可涂抹单元格上按下左键并拖动，把**起始格当时的值**复制到鼠标扫过的同列单元格。
/// 单击行为完全不变；只有位移超过约 4.5 像素才判定为涂抹手势。
///
/// 为什么不用本体自带的 paintable 机制：
/// - <c>Widgets.ToggleInvisibleDraggable(paintable:true)</c> 是「切换式」（每格各自翻转一次），
///   而本表要的是「赋值式」（整片刷成同一个值），并且要跨 8 种列样式保持同一套语义；
/// - 本体那套是全局静态 + controlID，而本表同时有表头拖拽排序、中键滚动、右键菜单，
///   自持 <see cref="Input"/> 状态不跟它们抢 controlID（见 <see cref="Cell"/> 注释）。
///
/// 状态推进完全由「单元格在绘制时调用 <see cref="Cell"/>」驱动；鼠标一松开
/// （<see cref="BeginFrame"/> 里检测）就整体复位，不依赖任何窗口生命周期回调 —— 本表
/// 挂在 <c>Dialog_ModSettings</c> 上，没有可用的 PostClose 收尾点。
/// </summary>
public static class DragPaint
{
    // 与 Verse.Widgets.DragStartDistanceSquared 同值，避免「复选框拖得动、这里拖不动」的手感分裂
    private const float DragThresholdSquared = 20f;

    /// <summary>本格在本次涂抹中的角色。</summary>
    public enum PaintResult
    {
        /// <summary>不参与。</summary>
        None,
        /// <summary>拖拽起始格：由调用方在此刻定下「要涂的值」。</summary>
        Anchor,
        /// <summary>被扫过、需要涂成起始格的值的格子。</summary>
        Swept,
    }

    /// <summary>是否正处于一次涂抹拖拽中（用于光标图标与点击抑制）。</summary>
    public static bool Active { get; private set; }

    /// <summary>发起本次涂抹的列身份（这里放 <c>StatColumnConfig</c> 引用）。用于把涂抹限制在同一列内。</summary>
    public static object? Tag { get; private set; }

    /// <summary>要涂的值（起始格的 <c>GetCopyData</c> 文本）；由调用方在 <see cref="PaintResult.Anchor"/> 帧写入。</summary>
    public static string? Value { get; set; }

    // 本轮已写入的行（用缓存过的 IdTag 作键，避免每格每帧走 struct 的反射式相等比较）
    private static readonly HashSet<string> paintedIds = new();

    private static bool pressed;
    private static bool dragging;
    private static bool started;
    private static Vector3 pressPos;
    private static int suppressClickUntilFrame = -1;

    /// <summary>左键按着 + 修饰键按着：可能是一次涂抹手势。供文本输入框「事前让位」判断。</summary>
    public static bool HasPaintIntent => Input.GetMouseButton(0) && ModifierHeld;

    /// <summary>当前这轮拖拽是否由指定列发起。</summary>
    public static bool ClaimsTag(object tag) => Active && ReferenceEquals(Tag, tag);

    /// <summary>
    /// 本帧是否应吞掉单元格自身的点击（拖拽结束帧）。
    /// 表格里大量控件是「松手即触发」（编辑器按钮、名称列跳转、表头选列），
    /// 拖完松手停在哪一格就会误触发那一格 —— 必须由这里统一压掉。
    /// </summary>
    public static bool SuppressClick => Time.frameCount <= suppressClickUntilFrame;

    /// <summary>
    /// 修饰键两处取或：<c>Event.current.shift</c> 是本体的口径，但它不一定每个事件都带值
    /// （Layout / Repaint 上可能读成 false），<c>Input.GetKey</c> 则任何一帧都准。
    /// **每帧实时读**，不在按下那一帧缓存 —— 否则「先按鼠标、再按 Shift」和
    /// 「读到修饰键的那一帧正好不带修饰键信息」两个场景都会失效。
    /// </summary>
    public static bool ModifierHeld
        => (Event.current != null && Event.current.shift)
        || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    /// <summary>每帧最先调用一次：鼠标一旦没按着就整体复位，并在拖拽真正发生过的那一帧短期抑制点击。</summary>
    public static void BeginFrame()
    {
        if (Input.GetMouseButton(0)) return;
        // 只有「确实起拖过」才需要压制点击；单纯单击不该影响这一帧的正常点击处理
        if (Active) suppressClickUntilFrame = Time.frameCount + 1;
        // 无条件复位，而不是只在 Active 时复位：一次快速单击（MouseDown 与 MouseUp 落在相邻帧）
        // 会把 pressed 置位但永远走不到 Active，残留的 pressPos 会让之后「在滚动条上按下并拖动」
        // 直接越过阈值而误涂一整列。
        Reset();
    }

    /// <summary>
    /// 绘制每个可涂抹单元格时调用。返回本格角色：
    /// <see cref="PaintResult.None"/> 表示不参与；<see cref="PaintResult.Anchor"/> 表示起拖这一帧的起始格；
    /// <see cref="PaintResult.Swept"/> 表示被扫过、应当涂成起始格的值。
    ///
    /// <paramref name="requireModifier"/> 默认为 <c>true</c>：本表约定**所有涂抹都必须按住 Shift + 左键**，
    /// 免得普通拖动（选字、拖选、误触）就把一大片数据改掉。所有调用点都沿用默认值。
    ///
    /// 为什么只认 <see cref="Mouse.IsOver"/> 而不记「哪些格子被涂过」：涂抹写入是幂等的，
    /// 重复刷同一格没有副作用。但**只有鼠标底下那一格会推进状态**，其余格一律提前返回。
    /// 这也让「起始格」天然唯一：同一帧里只有一个矩形能通过 <see cref="Mouse.IsOver"/>。
    /// </summary>
    public static PaintResult Cell(Rect rect, object tag, bool requireModifier = true)
    {
        if (!Input.GetMouseButton(0)) return PaintResult.None;   // 没按着就结束本轮
        if (!Mouse.IsOver(rect)) return PaintResult.None;         // 只有鼠标底下那一格推进状态

        // 起拖闸门：用「本帧的 MouseDown 事件」而不是 Input.GetMouseButtonDown ——
        // 后者是帧级的，在 Layout / Repaint 这些 pass 上同样为真，会在鼠标其实压在滚动条上时
        // 提前把 pressed 置位；而 MouseDown 事件若已被别的控件吃掉（Unity 的 GUI.BeginScrollView
        // 在内容之前就处理滚动条并 Use() 掉它），事件类型会变成 Used，这个分支自然不成立。
        // 若不挡：拖滚动条会把滚动条底下那一列整片涂掉（单元格矩形是延伸到滚动条下面的）。
        // 枚举格不受影响 —— 它的 ButtonText 在本格之后才处理这次 MouseDown。
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (GUIUtility.hotControl != 0) return PaintResult.None;
            pressed = true;
            dragging = false;
            started = false;
            pressPos = Input.mousePosition;
            return PaintResult.None;
        }
        if (!pressed) return PaintResult.None;

        // 修饰键每帧实时重读：越过阈值之前（4.5 像素内）本来也谈不上「已经开始的手势」，
        // 所以「拖到一半补按 Shift 也开始涂」是想要的容错，不是 bug。
        bool armed = !requireModifier || ModifierHeld;
        if (!dragging && armed && (pressPos - Input.mousePosition).sqrMagnitude > DragThresholdSquared)
            dragging = true;
        if (!dragging) return PaintResult.None;

        if (!started)
        {
            started = true;
            Active = true;
            Tag = tag;
            Value = null;
            paintedIds.Clear();
            // 起始格自己也算被涂（写入幂等，值不变），好让整片高光连续；值由调用方此刻定下。
            return PaintResult.Anchor;
        }

        // 跨列不涂：相邻列若也用涂抹而值类型不同，靠 tag 隔开，避免把 A 类的值塞进 B 类格子里
        return ClaimsTag(tag) ? PaintResult.Swept : PaintResult.None;
    }

    /// <summary>记录本格已在本轮被写入（首次返回 true）。既用于高光，也用于避免重复 <c>Apply</c>。</summary>
    public static bool MarkPainted(string idTag) => paintedIds.Add(idTag);

    /// <summary>本格是否属于本轮涂抹范围（用于画高光）。</summary>
    public static bool IsPainted(string idTag) => paintedIds.Contains(idTag);

    public static void Reset()
    {
        Active = false;
        Tag = null;
        Value = null;
        pressed = false;
        dragging = false;
        started = false;
        paintedIds.Clear();
    }

    /// <summary>
    /// 在 <c>Widgets.WidgetsOnGUI</c> 的顶层（屏幕坐标系）画跟随光标的值提示。
    /// **不能在窗口内容里画** —— 那里的 <c>Event.current.mousePosition</c> 是「当前 GUI 组」的局部坐标，
    /// 窗口内容外面还套着两层偏移（BeginGroup + BeginScrollView），图标会跑到窗口外面去。
    /// </summary>
    public static void DrawCursorIcon()
    {
        // 本函数跑在窗口绘制之前，读到的是上一帧的 Active；补判一次按键，松开后不再多留一帧
        if (!Active || !Input.GetMouseButton(0)) return;
        if (Value is not string text || text.Length == 0) return;
        text = text.Replace('\n', ' ');
        if (text.Length > 24) text = text.Substring(0, 24) + "…";
        // 显式走 (Texture iconTex, string text, …) 重载；传 null 图标、只显示值文本
        GenUI.DrawMouseAttachment(null!, text);
    }
}
