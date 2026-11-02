using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 的机械族武器过滤（<c>Fortified.MechWeaponExtension</c>）的反射入口。
///
/// <para>
/// 这个扩展挂在<b>种族 ThingDef</b> 上，是"这台机兵能用哪些武器"的主开关，与武器侧的
/// <c>HeavyEquippableExtension</c> 构成两道门（判定见 <c>CheckUtility.IsMechUseable</c>）。
/// 字段多且互相配合，表格里逐列展示没有意义，所以在 <c>MechWeaponFilterWindow</c> 里统一编辑。
/// </para>
///
/// <para>和 <see cref="FortifiedHeavy"/> 一样：可选 Mod 只按全名反射，绝不静态引用其类型。</para>
/// </summary>
internal static class FortifiedMech
{
    internal const string ExtensionType = "Fortified.MechWeaponExtension";

    internal static object? FindExtension(Def? def)
        => DataUtility.FindModExtension(def, ExtensionType);
}
