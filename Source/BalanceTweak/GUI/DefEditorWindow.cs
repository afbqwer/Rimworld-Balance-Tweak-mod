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
        draggable = true;
        resizeable = true;
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        // 不设 optionalTitle：它跟 UI 全局字号（Small）走，在这么大一扇窗口顶端小得像个注脚。
        // 标题由表格自己画在首行（BalanceTweakSettings.DrawTitleBar），右上角还要摆按钮。
    }

    public override Vector2 InitialSize
        => new(Mathf.Max(MinWidth, MainWindowWidth), Mathf.Max(MinHeight, MainWindowHeight));

    public override void DoWindowContents(Rect inRect)
    {
        // 窗口壳只提供边框和关闭 X；标题行 + 表格全在 DoEditorContents 里（标题行要放按钮，得能访问表格的私有状态）
        BalanceTweakMod.settings?.DoEditorContents(inRect);
    }

    public override void PreClose()
    {
        base.PreClose();
        // 用拖拽缩放定下的尺寸回写设置：下次打开还是这个大小，
        // 并且与「总体设置」里的宽高滑条共用同一份数据，两边不会各说各话。
        // 必须先写尺寸再关 Mod 设置页 —— 那一页关掉时会落盘，顺序反了就会把旧尺寸写进去
        MainWindowWidth = Mathf.Max(MinWidth, Mathf.RoundToInt(windowRect.width));
        MainWindowHeight = Mathf.Max(MinHeight, Mathf.RoundToInt(windowRect.height));
        DragPaint.Reset();   // 关窗时抹掉可能残留的涂抹手势

        // 「选项 → Mod 设置」和本窗口同进同出：那一页的 PreClose 会顺带落盘；
        // 若是从底部主按钮进来的（没经过 Mod 设置），就得自己写 ——
        // ModSettings 默认只在关闭设置界面时才落盘，不补这一次改动会丢
        if (!BalanceTweakMod.TryCloseModSettings())
            BalanceTweakMod.settings?.Write();
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
