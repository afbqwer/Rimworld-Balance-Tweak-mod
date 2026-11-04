using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Atmosphere Overhaul（<c>acutus.AtmosphereOverhaul.release</c>）的反射入口。
///
/// <para>
/// AO 是**可选 Mod**：这里一律按全名反射访问其类型与成员，绝不静态引用
/// （包括 <c>typeof</c> / 泛型参数 / 把该类型的变量捕获进闭包 —— 见 <see cref="DataUtility"/> 顶部注释）。
/// 闭包里只允许出现 <c>object</c> / <c>MemberInfo</c> / BCL 值类型。
/// </para>
///
/// <para>
/// 三个自有 Def 类型（气体 / 全球气体 / 爆炸剖面）不在原版里，只能靠
/// <c>[TweakFor(TypeName = ...)]</c> + 本类的全名字面量挂上去；字段读写全部走
/// <see cref="G"/> / <see cref="ApplyF{TV}"/>。
/// </para>
/// </summary>
internal static class AoHelper
{
    internal const string Mod = TweakDatabase.AO;

    // ── 自有 Def 类型（新页签的载体）──
    internal const string GasDef = "AtmosphereOverhaul.Gases.AtmosphereGasDef";
    internal const string GlobalGas = "AtmosphereOverhaul.Simulation.Global.GlobalGasDef";
    internal const string Profile = "AtmosphereOverhaul.Simulation.AtmosphereExplosionProfileDef";

    // ── 挂在原版 Def 上的扩展（现有页签扩列）──
    internal const string TerrainExt = "AtmosphereOverhaul.Simulation.AtmosphereTerrainExtension";
    internal const string BlastExt = "AtmosphereOverhaul.Pawns.BlastOverpressureExtension";
    internal const string ApparelExt = "AtmosphereOverhaul.Pawns.AtmosphereApparelExtension";
    internal const string HearingExt = "AtmosphereOverhaul.Pawns.HearingAftermathExtension";
    internal const string PawnExt = "AtmosphereOverhaul.Pawns.AtmospherePawnExtension";
    internal const string BuildingExt = "AtmosphereOverhaul.Simulation.BuildingAtmosphereExtension";
    internal const string ExplosionExt = "AtmosphereOverhaul.Simulation.ExplosionAtmosphereExtension";

    /// <summary>跨 Mod 枚举：地形的地面温度模式（<c>Normal / Fixed / Capped</c>）。</summary>
    internal const string GroundMode = "AtmosphereOverhaul.Simulation.AtmosphereGroundMode";

    /// <summary>跨 Mod 枚举：风扇的供能方式（<c>Electricity / StoredEnergy / Unlimited</c>）。</summary>
    internal const string FanPowerSource = "AtmosphereOverhaul.Buildings.FanPowerSource";

    // ── 一次性静态缓存的宿主 ──
    private const string BreathingGearType = "AtmosphereOverhaul.Pawns.BreathingGear";
    private const string GroundTableType = "AtmosphereOverhaul.Simulation.GroundMaterialTable";

    #region 类型解析

    private static readonly Dictionary<string, Type?> typeCache = new();

    /// <summary>
    /// 全名 → <see cref="Type"/>，结果（含失败）缓存。
    /// 走 <see cref="DataUtility.OptionalType"/>：Mod 没装时静默返回 null，装了却没这个类型才登记失配。
    /// </summary>
    internal static Type? T(string typeName, [CallerMemberName] string context = "")
    {
        if (typeCache.TryGetValue(typeName, out var cached)) return cached;
        var type = DataUtility.OptionalType(typeName, Mod, context);
        typeCache[typeName] = type;
        return type;
    }

    #endregion

    #region 读写

    // 转发 CallerMemberName：否则失配日志只会写「来自 G」，定位不到是哪个调用点。
    internal static object? G(object? target, string name, [CallerMemberName] string context = "")
        => DataUtility.GetByName(target, name, context);

    internal static void S(object? target, string name, object? value, [CallerMemberName] string context = "")
        => DataUtility.SetByName(target, name, value, context);

    internal static float? F(object? target, string name, [CallerMemberName] string context = "")
        => DataUtility.AsFloat(DataUtility.GetByName(target, name, context));

    internal static int? I(object? target, string name, [CallerMemberName] string context = "")
        => DataUtility.AsInt(DataUtility.GetByName(target, name, context));

    internal static bool B(object? target, string name, [CallerMemberName] string context = "")
        => DataUtility.AsBool(DataUtility.GetByName(target, name, context));

    /// <summary>
    /// 走 <c>ApplyV</c> 三态门控 + 反射写入。<paramref name="target"/> 为 null 直接返回：
    /// 子对象（<c>renderSetting</c> / <c>AO_transferSetting</c>）在 Def 构造时已 new 好，正常不会 null，
    /// 但对方改结构时必须静默跳过而不是在这里 NRE（失配由 <see cref="DataUtility.FindMember"/> 登记）。
    /// </summary>
    internal static void ApplyF<TV>(TweakData data, object? target, string member, string field)
        where TV : struct
    {
        if (target == null) return;
        var mi = DataUtility.FindMember(target.GetType(), member);
        if (mi == null) return;
        data.ApplyRef<TV>(field, v => DataUtility.SetMember(target, mi, v));
    }

    /// <summary>
    /// Def 引用版本（AO 建筑的两个「产品 Def」：<c>productDef</c> / <c>oxygenProductDef</c>）。
    /// <c>ApplyF&lt;TV&gt;</c> 要求 <c>TV : struct</c>，<see cref="ThingDef"/> 是类，只能走 <c>ApplyD</c>。
    /// </summary>
    internal static void ApplyDef<TV>(TweakData data, object? target, string member, string field) where TV : Def
    {
        if (target == null) return;
        var mi = DataUtility.FindMember(target.GetType(), member);
        if (mi == null) return;
        data.ApplyDefRef<TV>(field, v => DataUtility.SetMember(target, mi, v));
    }

    /// <summary>字符串版本（<c>readoutLabel</c> / <c>renderTexturePath</c> / <c>mapWeather</c>）。</summary>
    internal static void ApplyString(TweakData data, object? target, string member, string field)
    {
        if (target == null) return;
        var mi = DataUtility.FindMember(target.GetType(), member);
        if (mi == null) return;
        data.ApplyStrRef(field, v => DataUtility.SetMember(target, mi, v));
    }

    #endregion

    #region 原版 Def 上的扩展

    /// <summary>按全名找 <c>DefModExtension</c>（返回基类，字段继续反射）。</summary>
    internal static object? Ext(Def? def, string typeName) => DataUtility.FindModExtension(def, typeName);

    internal static bool HasExt(Def? def, string typeName) => DataUtility.FindModExtension(def, typeName) != null;

    #endregion

    #region 静态缓存刷新

    // AO 有四处 private static 一次性缓存，全程序集没有任何重置点。改完 Def 字段，代码仍读旧值，
    // 日志里一句话都没有 —— 表现就是「功能做完了但不生效」。这里只处理必须清的三处：
    //   · harmfulGases          0 → 正数时列表里根本没有这条气体
    //   · GroundTable.ResolveCache  地形扩展的六个字段全部被固化成结构体
    //   · BuildingAtmosphereExtension.Cache  增删扩展后仍返回旧的 Defaults
    // 第四个 AtmosphereTuningManager.baseSettings 不做，理由见 Docs 计划 §5.1。

    private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// 把静态字段**置 null**（下次读时由 AO 自己重建）。
    /// 只能用在「null 即缓存未建」的字段上：<c>harmfulGases</c> 的读取是
    /// <c>if (harmfulGases == null) 重建</c>，清空集合会让它永远非 null、永远不重建 ——
    /// 那比不刷新更糟（所有气体一起失去毒性）。
    /// </summary>
    internal static void ResetStaticToNull(string typeName, string fieldName, [CallerMemberName] string context = "")
    {
        var type = T(typeName, context);
        if (type == null) return;
        var field = type.GetField(fieldName, StaticAny);
        if (field == null) { Diagnostics.MissMember(type, fieldName, "静态字段", context); return; }
        field.SetValue(null, null);
    }

    /// <summary>
    /// 清空静态集合（字典 / 列表），供只做查表缓存的容器使用。
    /// <paramref name="lockFieldName"/> 不为空时在宿主自己的锁对象上操作：
    /// <c>GroundMaterialTable.ResolveCache</c> 的读写都在 <c>ResolveCacheLock</c> 里，
    /// 不加同一把锁就清，模拟线程可能正在插入。
    /// </summary>
    internal static void ClearStaticCollection(string typeName, string fieldName, string? lockFieldName = null,
        [CallerMemberName] string context = "")
    {
        var type = T(typeName, context);
        if (type == null) return;
        var field = type.GetField(fieldName, StaticAny);
        if (field == null) { Diagnostics.MissMember(type, fieldName, "静态字段", context); return; }
        object? gate = lockFieldName.NullOrEmpty() ? null : type.GetField(lockFieldName!, StaticAny)?.GetValue(null);

        void Clear()
        {
            switch (field.GetValue(null))
            {
                case IDictionary dict: dict.Clear(); break;
                case IList list: list.Clear(); break;
            }
        }

        if (gate != null) { lock (gate) { Clear(); } }
        else Clear();
    }

    /// <summary><c>BreathingGear.harmfulGases</c>：哪些气体算「有害」，只在 == null 时建一次。</summary>
    internal static void ClearHarmfulGases([CallerMemberName] string context = "")
        => ResetStaticToNull(BreathingGearType, "harmfulGases", context);

    /// <summary><c>GroundMaterialTable.ResolveCache</c>：地形扩展六个字段的固化结果。</summary>
    internal static void ClearGroundCache([CallerMemberName] string context = "")
        => ClearStaticCollection(GroundTableType, "ResolveCache", "ResolveCacheLock", context);

    /// <summary><c>BuildingAtmosphereExtension.Cache</c>：只在增删扩展时需要清（改字段值不用）。</summary>
    internal static void ClearBuildingExtensionCache([CallerMemberName] string context = "")
        => ClearStaticCollection(BuildingExt, "Cache", null, context);

    #endregion
}
