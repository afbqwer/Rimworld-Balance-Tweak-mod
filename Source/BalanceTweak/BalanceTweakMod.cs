using RimWorld;
using UnityEngine;
using Verse;
namespace BalanceTweak;

public class BalanceTweakMod : Mod
{
    public static BalanceTweakSettings? settings;
    const int openFlameCount = 3;
    private static int openFlame = openFlameCount;

    public BalanceTweakMod(ModContentPack pack) : base(pack)
    {
        settings = GetSettings<BalanceTweakSettings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        if(openFlame > 0){
            if (Find.WindowStack.TryGetWindow<Dialog_ModSettings>(out var win))
            {
                Vector2 size = new(BalanceTweakSettings.MainWindowWidth, BalanceTweakSettings.MainWindowHeight);
                win.windowRect = new Rect(
                    (UI.screenWidth - size.x) / 2f,
                    (UI.screenHeight - size.y) / 2f,
                    size.x,
                    size.y);
                win.windowRect = win.windowRect.Rounded();
                openFlame -= 1;
            }
        }
        settings!.DoSettingsWindowContents(inRect);
    }

    /// <summary>重新居中主窗口（用于调整大小后即时刷新位置）</summary>
    public static void RecenterWindow()
    {
        if (Find.WindowStack.TryGetWindow<Dialog_ModSettings>(out var win))
        {
            Vector2 size = new(BalanceTweakSettings.MainWindowWidth, BalanceTweakSettings.MainWindowHeight);
            win.windowRect = new Rect(
                (UI.screenWidth - size.x) / 2f,
                (UI.screenHeight - size.y) / 2f,
                size.x,
                size.y);
            win.windowRect = win.windowRect.Rounded();
        }
    }

    public override void WriteSettings()
    {
        settings!.Write();
        openFlame = openFlameCount;
    }

    public override string SettingsCategory()
    {
        return "MST.SimpleDefEditer".Translate().RawText;
    }
}

