using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 防御类 Comp 的全名反射入口（目前只有主动防御系统 APS）。
///
/// <para>
/// APS 的 <c>CompProperties_ActiveProtectionSystem</c> 挂在 <see cref="AbilityDef.comps"/> 上，
/// 不是 ThingDef。所有访问都只按字符串全名判定；字段读写交给 <see cref="DataUtility"/>，
/// 闭包里只捕获 <c>object</c>，不产生对 Fortified.dll 的静态引用。
/// </para>
/// </summary>
internal static class FortifiedProtection
{
    internal const string ActiveProtectionPropsType = "Fortified.CompProperties_ActiveProtectionSystem";

    /// <summary>在 <c>AbilityDef.comps</c> 里按类型全名找 APS 的 CompProperties；找不到返回 null。</summary>
    internal static object? FindActiveProtection(Def? def)
    {
        if (def is not AbilityDef ability || ability.comps.NullOrEmpty()) return null;
        foreach (var props in ability.comps)
        {
            if (props != null && DataUtility.IsTypeOrSubclassOfName(props.GetType(), ActiveProtectionPropsType))
                return props;
        }
        return null;
    }

    internal static bool HasActiveProtection(Def? def) => FindActiveProtection(def) != null;
}
