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

    /// <summary>自绘标题占的高度：正好是顶部控制栏的一行（与表格行高同一个常量）。</summary>
    private const float TitleHeight = LineHeight;

    /// <summary>标题右侧让给关闭 X 的宽度。X 画在窗口右上角（距右 22px、18px 见方），换算到内容坐标约吃掉 40px。</summary>
    private const float CloseButtonReserve = 44f;

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
        // 标题改为 DoWindowContents 里自绘，占掉顶部一行。
    }

    public override Vector2 InitialSize
        => new(Mathf.Max(MinWidth, MainWindowWidth), Mathf.Max(MinHeight, MainWindowHeight));

    public override void DoWindowContents(Rect inRect)
    {
        DrawTitle(new Rect(0f, 0f, inRect.width, TitleHeight));

        // 表格本体仍在 BalanceTweakSettings（持久化字段与它绑在一起），这里只提供一个窗口壳；
        // 整体下移一行给标题让位，表格内部的布局一行都不用改
        BalanceTweakMod.settings?.DoEditorContents(
            new Rect(0f, TitleHeight, inRect.width, inRect.height - TitleHeight));
    }

    private static void DrawTitle(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = Color.white;
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - CloseButtonReserve, rect.height),
            "MST.SimpleDefEditer".Translate().RawText);

        // 与表格里「控制栏 / 表头」之间那两条分隔线同款，把标题和下面的控件隔开
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        GUI.color = Color.white;

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
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
    public override void Activate() => DefEditorWindow.Open();
}
