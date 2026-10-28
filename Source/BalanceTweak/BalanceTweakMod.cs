using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public class BalanceTweakMod : Mod
{
    public static BalanceTweakSettings? settings;

    public BalanceTweakMod(ModContentPack pack) : base(pack)
    {
        settings = GetSettings<BalanceTweakSettings>();
    }

    /// <summary>
    /// 「选项 → Mod 设置」里本 mod 的那一页不承载任何内容：一进来就把独立窗口
    /// <see cref="DefEditorWindow"/> 顶上去，用户不必再点一次按钮。
    ///
    /// 两边是绑死的一对：编辑器关闭时会顺手关掉本页（<see cref="DefEditorWindow.PreClose"/>
    /// 里的 <see cref="TryCloseModSettings"/>），反过来本页也关不出来 —— 它永远被编辑器盖住，
    /// 玩家能点到的最上层窗口始终是编辑器。
    /// </summary>
    public override void DoSettingsWindowContents(Rect inRect)
    {
        // 本方法会被 Layout / Repaint 等事件各调一次，重复调用由 Open 内部的去重挡掉
        DefEditorWindow.Open();

        // 正常流程里看不到这行（编辑器已盖在上面）；万一窗口没能打开，至少不留一个空白页。
        // 往下让出一段：Dialog_ModSettings 自己会在内容区顶部用 Medium 画 mod 名，贴着 y=0 会被那行标题压住
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(new Rect(0f, 60f, inRect.width, 60f), "MST.SettingsHint".Translate().RawText);
    }

    /// <summary>
    /// 关掉「选项 → Mod 设置」那一页，返回是否真的关掉了。
    /// 同一时刻只可能开着唯一一页（只能从「选项」里进），所以不必分辨是哪个 mod 的。
    /// 注意这里用 <c>doCloseSound: false</c>：它是被编辑器连坐关掉的，不该有独立的关闭音效。
    /// </summary>
    public static bool TryCloseModSettings()
    {
        if (!Find.WindowStack.TryGetWindow<Dialog_ModSettings>(out var win) || win == null) return false;
        return Find.WindowStack.TryRemove(win, doCloseSound: false);
    }

    /// <summary>宽高改变后让编辑窗口跟着重排（窗口没开就什么也不做）。</summary>
    public static void RecenterWindow()
    {
        Find.WindowStack.WindowOfType<DefEditorWindow>()?.ApplyConfiguredSize();
    }

    public override void WriteSettings()
    {
        settings!.Write();
    }

    public override string SettingsCategory()
    {
        return "MST.SimpleDefEditer".Translate().RawText;
    }
}
