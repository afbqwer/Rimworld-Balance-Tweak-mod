using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak;

/// <summary>
/// 独立的 Def 编辑窗口。
///
/// 编辑界面以前只能从「选项 → Mod 设置」进入，而且被塞在 <c>Dialog_ModSettings</c> 的内容区里：
/// 尺寸要靠在 <c>DoSettingsWindowContents</c> 里逐帧改 <c>windowRect</c> 硬掰，开合时机也不归自己管。
/// 现在表格搬进这个窗口：游戏内底部主按钮随时可以打开；从「选项 → Mod 设置」进来时，
/// 设置页会立刻把本窗口顶上去，并且两边同进同出（关掉本窗口，设置页跟着关）。
///
/// 窗口属性刻意与 <c>Dialog_ModSettings</c> 对齐 —— 壳子换了，交互习惯不该跟着变：
/// - <c>closeOnClickedOutside</c> + <c>absorbInputAroundWindow</c>：点窗外（包括底部主按钮）会关窗
///   并吞掉这一次点击。这既是「不会误点地图下指令」的保证，也避免「关窗」与「主按钮再次开窗」
///   在同一帧里互相打架（主按钮的激活发生在 MouseUp，而关窗在 MouseDown 就把事件吃掉了）。
/// - <c>forcePause</c>：编辑期间暂停，和原来在 Mod 设置里编辑的体验一致。
/// </summary>
public class DefEditorWindow : Window
{
    /// <summary>尺寸下限。「总体设置」里的宽高滑条最小也是 800×480，两边保持一致。</summary>
    private const int MinWidth = 800;
    private const int MinHeight = 480;

    public DefEditorWindow()
    {
        doCloseX = true;
        doCloseButton = false;              // 表格要占满下沿，不给底部关闭按钮留 55px
        // 不用 Window 自带的全窗拖动：它在 InnerWindowOnGUI 末尾对整窗调 GUI.DragWindow，
        // 而表格里的左键手势（Shift 涂抹）在按下时刻意不消费事件 —— 结果一按就被判成拖窗口，涂抹起不来。
        // 拖动改由标题行左侧那块区域自己做（见下面的 UpdateTitleDrag）。
        draggable = false;
        resizeable = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        // 不设 optionalTitle：它跟 UI 全局字号（Small）走，在这么大一扇窗口顶端小得像个注脚。
        // 标题由表格自己画在首行（BalanceTweakSettings.DrawTitleBar），右上角还要摆按钮。
    }

    public override Vector2 InitialSize
        => new(Mathf.Max(MinWidth, MainWindowWidth), Mathf.Max(MinHeight, MainWindowHeight));

    // ── 标题行拖动（自己实现，直接改 windowRect）────────────────────────────
    // 为什么不用 Window.draggable / GUI.DragWindow：
    //  · 前者按「整窗矩形」判定按下点，会把表格里「按下但不消费事件」的 Shift 涂抹手势判成拖窗口；
    //  · 后者在这个 IMGUI 后端里靠 mousePosition + windowRect.xy 每帧累积位移，
    //    内容 group 带来的坐标偏移会被反复计入，拖动会越拖越快 / 位置发飘。
    // 自己算「按下点 → 当前点」的屏幕像素差，没有累积、也不依赖 group 坐标。
    private bool draggingTitle;
    private Vector2 titleDragStartMouse;
    private Vector2 titleDragStartWindowPos;
    /// <summary>标题行里可拖动的区域（内容坐标）。按钮宽度随语言变化，所以由 DrawTitleBar 每帧写进来。</summary>
    private Rect titleDragArea;

    /// <summary>DrawTitleBar 每帧把「标题行左侧到按钮之前」这段告诉窗口。</summary>
    public static void NotifyTitleDragArea(Rect areaInContentSpace)
    {
        if (Find.WindowStack.WindowOfType<DefEditorWindow>() is { } win)
            win.titleDragArea = areaInContentSpace;
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        UpdateTitleDrag();
    }

    /// <summary>
    /// 鼠标在 UI 坐标系里的位置（左上原点，与 <see cref="Window.windowRect"/> 同一单位）。
    /// <c>Input.mousePosition</c> 是像素、且原点在左下，而窗口坐标是 <c>Screen.height / Prefs.UIScale</c> 那一套，
    /// 两者中间差着 UI 缩放 —— 不换算的话缩放非 1 时拖动会跑偏。
    /// </summary>
    private static Vector2 MousePosUi()
        => new(Input.mousePosition.x / Prefs.UIScale, UI.screenHeight - Input.mousePosition.y / Prefs.UIScale);

    private void UpdateTitleDrag()
    {
        Vector2 mouseUi = MousePosUi();

        if (draggingTitle)
        {
            if (!Input.GetMouseButton(0))
            {
                draggingTitle = false;
                return;
            }
            // 位移按「按下点 → 当前点」直接算，不累积，所以不会越拖越快
            windowRect.x = titleDragStartWindowPos.x + (mouseUi.x - titleDragStartMouse.x);
            windowRect.y = titleDragStartWindowPos.y + (mouseUi.y - titleDragStartMouse.y);
            return;
        }

        // 只在「按下」那一帧起手，且必须这扇窗真的能接输入（上面压着子窗口时不动）
        if (!Input.GetMouseButtonDown(0) || !Find.WindowStack.GetsInput(this)) return;

        // 可拖区：从**窗口左上角**算起，向右到按钮群之前、向下到标题行底部。
        // 内容区左上角本身内缩了 Margin，若只认内容区，边框那 18px 是死区 —— 用户没法判断
        // 「到底哪一像素才开始能拖」。窗口的缩放只认右下角 24×24（WindowResizer），左上角是空的，并进来没有冲突。
        Rect dragScreen = new(
            windowRect.x,
            windowRect.y,
            Margin + titleDragArea.xMax,
            Margin + titleDragArea.yMax);
        if (!dragScreen.Contains(mouseUi)) return;

        draggingTitle = true;
        titleDragStartMouse = mouseUi;
        titleDragStartWindowPos = new Vector2(windowRect.x, windowRect.y);
    }

    public override void DoWindowContents(Rect inRect)
    {
        // 窗口壳只提供边框和关闭 X；标题行 + 表格全在 DoEditorContents 里（标题行要放按钮，得能访问表格的私有状态）
        BalanceTweakMod.settings?.DoEditorContents(inRect);
    }

    /// <summary>
    /// 用上次关闭时的位置打开；没记录过（首次）就居中。
    /// 位置会被夹到「标题至少露出 160px」——换分辨率或把窗口拖出屏幕后，它还抓得回来。
    /// </summary>
    protected override void SetInitialSizeAndPosition()
    {
        if (MainWindowX == int.MinValue || MainWindowY == int.MinValue)
        {
            base.SetInitialSizeAndPosition();
            return;
        }
        Vector2 size = InitialSize;
        float x = Mathf.Clamp(MainWindowX, 0f, Mathf.Max(0f, UI.screenWidth - 160f));
        float y = Mathf.Clamp(MainWindowY, 0f, Mathf.Max(0f, UI.screenHeight - 40f));
        windowRect = new Rect(x, y, size.x, size.y).Rounded();
    }

    public override void PreClose()
    {
        base.PreClose();

        // 1. 先把从属窗口关掉：本窗口一关它们就成了浮在地图上的孤儿，
        //    而且它们之后的编辑不会再有窗口负责落盘（下面这次写盘已经发生过了）
        CloseChildWindows();

        // 2. 尺寸与位置回写：下次打开原样恢复，也与「总体设置」里的宽高滑条共用同一份数据。
        //    必须先写尺寸再关 Mod 设置页 —— 那一页关掉时会落盘，顺序反了就把旧尺寸写进去了
        MainWindowWidth = Mathf.Max(MinWidth, Mathf.RoundToInt(windowRect.width));
        MainWindowHeight = Mathf.Max(MinHeight, Mathf.RoundToInt(windowRect.height));
        MainWindowX = Mathf.RoundToInt(windowRect.x);
        MainWindowY = Mathf.RoundToInt(windowRect.y);
        DragPaint.Reset();   // 关窗时抹掉可能残留的涂抹手势

        // 3. 落盘：「选项 → Mod 设置」和本窗口同进同出，那一页的 PreClose 会顺带写；
        //    从底部主按钮进来的（没经过 Mod 设置）就得自己写 ——
        //    ModSettings 默认只在关闭设置界面时才落盘，不补这一次改动会丢
        if (!BalanceTweakMod.TryCloseModSettings())
            BalanceTweakMod.settings?.Write();
    }

    /// <summary>
    /// 关掉所有从属窗口（列配置 / 总体设置 / 各类列表编辑器…）。
    /// 本 mod 的窗口都在 <c>BalanceTweak</c> 命名空间下，按命名空间认即可，不必逐个列类型。
    /// </summary>
    private static void CloseChildWindows()
    {
        // 先取快照：TryRemove 会改窗口栈
        List<Window> children = Find.WindowStack.Windows
            .Where(w => w is not DefEditorWindow && w.GetType().Namespace == typeof(DefEditorWindow).Namespace)
            .ToList();
        foreach (Window child in children)
            Find.WindowStack.TryRemove(child, doCloseSound: false);
    }

    /// <summary>按设置里的宽高重新居中（窗口没开时不用调）。</summary>
    public void ApplyConfiguredSize()
    {
        Vector2 size = InitialSize;
        windowRect = new Rect(
            (UI.screenWidth - size.x) / 2f,
            (UI.screenHeight - size.y) / 2f,
            size.x,
            size.y).Rounded();
    }

    /// <summary>打开编辑窗口；已经开着就什么也不做（关窗靠 Esc、右上角 X 或点窗外）。</summary>
    public static void Open()
    {
        if (Find.WindowStack.WindowOfType<DefEditorWindow>() != null) return;
        Find.WindowStack.Add(new DefEditorWindow());
    }
}

/// <summary>
/// 底部主按钮的激活逻辑：直接开关独立编辑窗口，不占用 <c>MainTabWindow</c>
/// （那是个整屏窗口，会把表格挤成全屏，还要重做尺寸逻辑）。
/// <para>
/// 类型全名被 <c>1.7/Defs/MainButtonDefs/MainButton_BalanceTweak.xml</c> 的 <c>workerClass</c> 引用，
/// 改名必须同步改 XML，否则按钮加载失败。
/// </para>
/// </summary>
public class MainButtonWorker_OpenEditor : MainButtonWorker
{
    /// <summary>
    /// 可见性直接读「总体设置」里的开关，**不写 <c>def.buttonVisible</c>**。
    /// <para>
    /// 这是原版自己用的层次：<c>MainButtonsRoot.DoButtons</c> 每帧问一次 <c>Worker.Visible</c>
    /// 并按「可见按钮数」重排各自宽度 —— 可见性是**每帧查询**，不是一个要提前写进 Def 的字段。
    /// 原版 Mech 页签就是这么做的（<c>MainButtonWorker_ToggleMechTab.Visible =&gt; !Disabled</c>，
    /// 按当前地图上有没有机械体实时算）。跟着写的好处：
    /// 开关立即生效、不用在启动期摸 Def（省掉一个加载时序问题），也不会把别的 Mod 能看到的 Def 字段改脏。
    /// </para>
    /// </summary>
    public override bool Visible => BalanceTweakSettings.showMainButton;

    public override void Activate() => DefEditorWindow.Open();
}
