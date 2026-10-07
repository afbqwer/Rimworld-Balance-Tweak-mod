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
