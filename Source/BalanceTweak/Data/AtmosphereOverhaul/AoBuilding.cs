using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Atmosphere Overhaul 的**建筑侧**入口：9 个 <c>CompProperties_*</c> + 1 个
/// <c>BuildingAtmosphereExtension</c>。与 <see cref="AoHelper"/> 同一套约定（全名反射、绝不静态引用），
/// 单独成类是因为这里的定位方式是"在 <c>def.comps</c> 里按类型名找" —— 与扩展的
/// <c>modExtensions</c> 查找不是一回事。
///
/// <para>
/// 这些 Comp 都是**纯 POCO**：字段读写即生效（三个"生成时抄进实例"的例外见
/// <c>Docs/AtmosphereOverhaul_Editable_Plan.md</c> §2），没有静态缓存要清 —— 与地形扩展不同。
/// </para>
///
/// <para>
/// <b>不做 Comp 增删</b>：AO 的 Comp 之间有隐含依赖（<c>CompProperties_Mainspring.ConfigErrors</c>
/// 要求同一个 Def 上有 <c>SingleFieldFan</c> 且 <c>powerSource == StoredEnergy</c>），
/// 随手塞一个进去等于制造一个每次都报 <c>ConfigErrors</c> 的 Def。
/// </para>
/// </summary>
internal static class AoBuilding
{
    internal const string FanProps = "AtmosphereOverhaul.Buildings.CompProperties_SingleFieldFan";
    internal const string JetProps = "AtmosphereOverhaul.Buildings.CompProperties_PairedJet";
    internal const string CompressorProps = "AtmosphereOverhaul.Buildings.CompProperties_AirCompressor";
    internal const string GasTankProps = "AtmosphereOverhaul.Buildings.CompProperties_GasTank";
    internal const string HumidifierProps = "AtmosphereOverhaul.Buildings.CompProperties_Humidifier";
    internal const string DehumidifierProps = "AtmosphereOverhaul.Buildings.CompProperties_Dehumidifier";
    internal const string MainspringProps = "AtmosphereOverhaul.Buildings.CompProperties_Mainspring";
    internal const string RainCollectorProps = "AtmosphereOverhaul.Buildings.CompProperties_RainCollector";
    internal const string IrrigatorProps = "AtmosphereOverhaul.Plants.CompProperties_Irrigator";

    /// <summary>
    /// 在 <c>ThingDef.comps</c> 里按类型全名找 CompProperties，返回基类（字段继续反射）。
    /// 用 <c>IsTypeOrSubclassOfName</c> 而不是全名相等：AO 若给某个 Comp 派生子类，全名相等会静默失配。
    /// </summary>
    internal static object? FindComp(Def? def, string compTypeName)
    {
        if (def is not ThingDef td || td.comps.NullOrEmpty()) return null;
        foreach (var props in td.comps)
        {
            if (props != null && DataUtility.IsTypeOrSubclassOfName(props.GetType(), compTypeName))
                return props;
        }
        return null;
    }

    // ── 窗口分节 / 触发列留白的判据 ──
    // 一律写成 bool (TweakData)：可以直接当 Func<TweakData, bool> 方法组传给 ConfigSection，
    // 也满足 TweakField.Hidden 的签名（两者都是 bool (TweakData)）。

    internal static bool HasFan(TweakData d) => FindComp(d.def, FanProps) != null;
    internal static bool HasJet(TweakData d) => FindComp(d.def, JetProps) != null;
    internal static bool HasCompressor(TweakData d) => FindComp(d.def, CompressorProps) != null;
    internal static bool HasGasTank(TweakData d) => FindComp(d.def, GasTankProps) != null;
    internal static bool HasHumidifier(TweakData d) => FindComp(d.def, HumidifierProps) != null;
    internal static bool HasDehumidifier(TweakData d) => FindComp(d.def, DehumidifierProps) != null;
    internal static bool HasMainspring(TweakData d) => FindComp(d.def, MainspringProps) != null;
    internal static bool HasRainCollector(TweakData d) => FindComp(d.def, RainCollectorProps) != null;
    internal static bool HasIrrigator(TweakData d) => FindComp(d.def, IrrigatorProps) != null;

    /// <summary>建筑大气扩展：原版建筑里只有 Cooler / Vent / GeothermalGenerator / GravshipHull / VacBarrier 有。</summary>
    internal static bool HasAtmosphereExtension(TweakData d) => AoHelper.HasExt(d.def, AoHelper.BuildingExt);

    /// <summary>
    /// 这个 Def 有没有任何 AO 建筑数据（9 个 Comp 之一或建筑大气扩展）。
    /// 触发列的 <c>Hidden</c> 用它：没有的建筑整格留白，而不是给上万行建筑挂一个点开空窗口的按钮。
    ///
    /// <para>
    /// 这里是**每帧每行**求值的（<c>Hidden</c> 在绘制时逐格调），所以不能用
    /// <see cref="FindComp"/> 跑九遍 —— 那等于每格九次类型层级比较。改成一次遍历 + 全名哈希：
    /// AO 的九个 Comp 全是 <c>sealed</c>，不存在子类，精确匹配与 <c>IsTypeOrSubclassOfName</c> 等价。
    /// </para>
    /// </summary>
    internal static bool HasAny(Def? def)
    {
        if (AoHelper.HasExt(def, AoHelper.BuildingExt)) return true;
        if (def is not ThingDef td || td.comps.NullOrEmpty()) return false;
        foreach (var props in td.comps)
        {
            if (props != null && AoCompNames.Contains(props.GetType().FullName ?? "")) return true;
        }
        return false;
    }

    private static readonly HashSet<string> AoCompNames = new()
    {
        FanProps, JetProps, CompressorProps, GasTankProps, HumidifierProps,
        DehumidifierProps, MainspringProps, RainCollectorProps, IrrigatorProps,
    };
}
