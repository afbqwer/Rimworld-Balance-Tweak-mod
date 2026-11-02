using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 中继指挥系统的全名反射入口（<c>CompProperties_CommandRelay</c> /
/// <c>CompProperties_SubRelay</c> / <c>CompProperties_Overseer</c> /
/// <c>CompProperties_OverseerFacility</c>）。
///
/// <para>
/// 四个 Comp 全部只按字符串全名访问：本工程不引用 Fortified.dll，闭包里也绝不出现可选 Mod 类型
/// （硬约束见 Readme「可选 Mod 的反射系列」）。字段读写一律交给 <see cref="DataUtility"/>。
/// </para>
///
/// <para>
/// <b>刷新：</b>Comp 持有的是 Def 上同一份 <c>CompProperties</c> 实例，标量改完即被下一次读取看到；
/// 需要手动刷新的只有缓存 —— <c>CompOverseer</c> 的带宽 / 指挥范围、<c>CompCommandRelay</c> 与
/// <c>CompSubRelay</c> 的平方距离。刷新按 Def 排队，由 <see cref="Flush"/> 在编辑结束后扫一遍地图。
/// </para>
/// </summary>
internal static class FortifiedRelay
{
    internal const string CommandRelayPropsType = "Fortified.CompProperties_CommandRelay";
    internal const string SubRelayPropsType = "Fortified.CompProperties_SubRelay";
    internal const string OverseerPropsType = "Fortified.CompProperties_Overseer";
    internal const string OverseerFacilityPropsType = "Fortified.CompProperties_OverseerFacility";

    private const string CommandRelayCompType = "Fortified.CompCommandRelay";
    private const string SubRelayCompType = "Fortified.CompSubRelay";
    private const string OverseerCompType = "Fortified.CompOverseer";

    /// <summary>在 <c>ThingDef.comps</c> 里按类型全名找 CompProperties；找不到返回 null。</summary>
    internal static object? FindComp(Def? def, string fullTypeName)
    {
        if (def is not ThingDef td || td.comps.NullOrEmpty()) return null;
        foreach (var props in td.comps)
        {
            if (props != null && DataUtility.IsTypeOrSubclassOfName(props.GetType(), fullTypeName))
                return props;
        }
        return null;
    }

    internal static object? FindCommandRelay(Def? def) => FindComp(def, CommandRelayPropsType);
    internal static object? FindSubRelay(Def? def) => FindComp(def, SubRelayPropsType);
    internal static object? FindOverseer(Def? def) => FindComp(def, OverseerPropsType);
    internal static object? FindOverseerFacility(Def? def) => FindComp(def, OverseerFacilityPropsType);

    /// <summary>该 Def 是否挂了任意一种中继 Comp。每帧每格求值，只扫自己的 comps 一遍（通常 &lt; 20 个）。</summary>
    internal static bool HasAny(Def? def)
    {
        if (def is not ThingDef td || td.comps.NullOrEmpty()) return false;
        foreach (var props in td.comps)
        {
            if (props == null) continue;
            var type = props.GetType();
            if (DataUtility.IsTypeOrSubclassOfName(type, CommandRelayPropsType)
                || DataUtility.IsTypeOrSubclassOfName(type, SubRelayPropsType)
                || DataUtility.IsTypeOrSubclassOfName(type, OverseerPropsType)
                || DataUtility.IsTypeOrSubclassOfName(type, OverseerFacilityPropsType))
                return true;
        }
        return false;
    }

    // ── 运行时缓存刷新 ──

    private static readonly HashSet<ThingDef> dirtyDefs = new();

    /// <summary>登记"这个 Def 的中继缓存需要重算"；没有中继 Comp 的 Def 直接忽略（不会扫地图）。</summary>
    internal static void RequestRefresh(ThingDef? def)
    {
        if (def == null || !HasAny(def)) return;
        dirtyDefs.Add(def);
    }

    /// <summary>
    /// 刷新已登记 Def 的已生成实例。读档 / 未进游戏时直接丢弃：那时新建的 Comp 会按新 Props 初始化，
    /// 不存在"旧缓存"。
    /// </summary>
    internal static void Flush()
    {
        if (dirtyDefs.Count == 0) return;
        if (Current.ProgramState != ProgramState.Playing || Find.Maps == null)
        {
            dirtyDefs.Clear();
            return;
        }

        try
        {
            var seen = new HashSet<ThingWithComps>();
            foreach (var map in Find.Maps)
            {
                foreach (var thing in map.listerThings.AllThings)
                {
                    if (thing is ThingWithComps twc && dirtyDefs.Contains(twc.def) && seen.Add(twc))
                        RefreshThing(twc);
                }
                // 身上的服装不在 listerThings 里（未 Spawn），单独扫一遍。
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (dirtyDefs.Contains(pawn.def) && seen.Add(pawn))
                        RefreshThing(pawn);
                    if (pawn.apparel == null) continue;
                    foreach (var apparel in pawn.apparel.WornApparel)
                    {
                        if (apparel != null && dirtyDefs.Contains(apparel.def) && seen.Add(apparel))
                            RefreshThing(apparel);
                    }
                }
            }
        }
        finally
        {
            dirtyDefs.Clear();
        }
    }

    private static void RefreshThing(ThingWithComps thing)
    {
        foreach (var comp in thing.AllComps)
        {
            if (comp == null) continue;
            try
            {
                var type = comp.GetType();
                if (DataUtility.IsTypeOrSubclassOfName(type, OverseerCompType))
                {
                    // public：清 cachedBandwidth / cachedCommandRange，并重刷 dummy 的 Severity。
                    Invoke(comp, "RecacheValues");
                    Invoke(comp, "Notify_BandwidthChanged");
                }
                else if (DataUtility.IsTypeOrSubclassOfName(type, CommandRelayCompType))
                {
                    // UpdateCurrentRadius 内部没有 null 检查，必须先确认 overseer 存在（与 PostDraw 同一条判据）。
                    if (thing is Pawn pawn && MechanitorUtility.GetOverseer(pawn) != null)
                        Invoke(comp, "UpdateCurrentRadius");
                    DataUtility.SetByName(comp, "cacheDistance", 0f);
                }
                else if (DataUtility.IsTypeOrSubclassOfName(type, SubRelayCompType))
                {
                    // CurrentRadius 直接读 Props.relayRange，清掉平方缓存即可。
                    DataUtility.SetByName(comp, "cacheDistance", 0f);
                }
            }
            catch (Exception e)
            {
                // 某个 Comp 版本字段 / 方法改名时不能让整轮刷新中断；诊断日志受「冗杂日志」开关控制。
                TweakLog.Message($"[BalanceTweak] 刷新中继缓存失败（{thing.def?.defName}/{comp.GetType().Name}）：{e.Message}");
            }
        }
    }

    private static void Invoke(object target, string method)
    {
        var mi = target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        mi?.Invoke(target, null);
    }
}
