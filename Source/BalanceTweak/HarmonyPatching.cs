using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BalanceTweak;

[StaticConstructorOnStartup]
public static class HarmonyPatching
{
    static HarmonyPatching()
    {
        var harmony = new Harmony("assssssqwww.BalanceTweaks");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // 拖拽涂抹的光标图标只是装饰性补丁：目标万一改名，不该让整个 mod 的加载一起失败，
        // 所以用显式 TryPatch 而不是 [HarmonyPatch] 属性。
        try
        {
            var target = AccessTools.Method(typeof(Widgets), nameof(Widgets.WidgetsOnGUI));
            if (target == null)
            {
                Log.Warning("[BalanceTweak] 未找到 Widgets.WidgetsOnGUI，拖拽涂抹的光标提示已禁用");
            }
            else
            {
                harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(DragPaintIconPatch), nameof(DragPaintIconPatch.Postfix))));
            }
        }
        catch (Exception e)
        {
            Log.Warning("[BalanceTweak] 拖拽涂抹光标提示补丁失败：" + e);
        }
    }
}

/// <summary>
/// 在顶层（屏幕坐标系）画跟随光标的涂抹值提示。
/// 必须挂在 <c>Widgets.WidgetsOnGUI</c> 上：窗口内容里的 <c>Event.current.mousePosition</c>
/// 是当前 GUI 组的局部坐标，图标会被 BeginGroup + BeginScrollView 两层偏移带偏，跑到窗口外面去。
/// </summary>
internal static class DragPaintIconPatch
{
    public static void Postfix()
    {
        DragPaint.DrawCursorIcon();
    }
}

[HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn))]
[HarmonyPatch([typeof(Pawn), typeof(SkillDef), typeof(bool)])]
static class QualityUtilityPatch
{
    public static void Prefix(Pawn pawn, ref SkillDef relevantSkill, bool consumeInspiration = true)
    {
        if (relevantSkill == null)
        {
            relevantSkill = SkillDefOf.Crafting;
        }
    }
}
