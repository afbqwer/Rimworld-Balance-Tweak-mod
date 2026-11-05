using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
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

    /// <summary>
    /// 本 Mod 亲手上过扩展的 Def（按 defName）。只用于判断「摘扩展时能不能动」：
    /// XML 配的、别的 Mod 加的一律不碰。
    /// </summary>
    private static readonly HashSet<string> extAddedByUs = new();

    internal static bool AddedByUs(Def? def) => def != null && extAddedByUs.Contains(def.defName);

    /// <summary>
    /// 保证 <paramref name="def"/> 上有该扩展：已经有就原样返回，没有就 new 一个挂上去。
    /// <para>
    /// 挂完必须清 <c>BuildingAtmosphereExtension.Cache</c> —— 它是
    /// <c>Dictionary&lt;ThingDef, 扩展&gt;</c>，只在缺键时才查 <c>GetModExtension</c>；
    /// 不清的话新扩展永远读不到，<c>For(def)</c> 一直返回那张共享的 <c>Defaults</c>。
    /// </para>
    /// <para>
    /// 新建的实例全是字段默认值（倍率 1、<c>airFillPercent = -1</c>），与「没扩展」语义等价，
    /// 所以这个操作本身不改变游戏行为，只是给了字段一个可写的落点。
    /// </para>
    /// </summary>
    internal static object? EnsureExt(Def? def, string typeName, [CallerMemberName] string context = "")
    {
        if (def == null) return null;
        var existing = Ext(def, typeName);
        if (existing != null) return existing;

        var type = T(typeName, context);
        if (type == null) return null;
        if (Activator.CreateInstance(type) is not DefModExtension instance)
        {
            Diagnostics.MissMember(type, ".ctor", "无参构造", context);
            return null;
        }

        (def.modExtensions ??= new List<DefModExtension>()).Add(instance);
        extAddedByUs.Add(def.defName);
        ClearBuildingExtensionCache(context);
        return instance;
    }

    /// <summary>
    /// 摘掉**本 Mod 加的**扩展。XML 配的、别的 Mod 加的一律不动（返回 false）。
    /// 用于「用户把窗口里那几格全清空了」的情况 —— 此时留着空扩展等于留下一堆恒等默认值的死字段。
    /// </summary>
    internal static bool RemoveExtAddedByUs(Def? def, string typeName, [CallerMemberName] string context = "")
    {
        if (def?.modExtensions == null) return false;
        if (!extAddedByUs.Remove(def.defName)) return false;

        int removed = def.modExtensions.RemoveAll(e => e != null && e.GetType().FullName == typeName);
        if (removed > 0) ClearBuildingExtensionCache(context);
        return removed > 0;
    }

    #endregion

    #region 墙压估算

    /// <summary>
    /// AO 的风压/风载常数。全部取自 <c>LocalAtmosphereSettings.Default</c>（反射读，失败用源码里的初始值兜底），
    /// 这样 AO 改默认值时窗口里显示的耐压值跟着变，而不是写死一套过期数字。
    /// </summary>
    private const string SettingsType = "AtmosphereOverhaul.Simulation.LocalAtmosphereSettings";

    private static bool settingsRead;
    private static float baseSpeed = 2.24f;
    private static float speedPerHp = 0.056f;
    private static float wallPressureScale = 0.0125f;
    private static float shareBeta = 0.8f;
    private static float capacityExponent = 1.7f;
    private static float minCapacity = 0.25f;
    private static float gustLoad = 2.5f;

    private static void ReadSettings()
    {
        if (settingsRead) return;
        settingsRead = true;

        var t = T(SettingsType);
        var defaults = t?.GetField("Default", StaticAny)?.GetValue(null);
        if (defaults == null) return;         // 没装 AO / 改了结构：保持常量兜底

        baseSpeed = F(defaults, "WindDamageBaseSpeed") ?? baseSpeed;
        speedPerHp = F(defaults, "WindDamageSpeedPerHitPoint") ?? speedPerHp;
        wallPressureScale = F(defaults, "WindDamageWallPressureScale") ?? wallPressureScale;
        shareBeta = F(defaults, "WindDamageShareBeta") ?? shareBeta;
        capacityExponent = F(defaults, "WindDamageCapacityExponent") ?? capacityExponent;
        minCapacity = F(defaults, "WindDamageMinCapacity") ?? minCapacity;
        gustLoad = F(defaults, "WindDamageGustLoad") ?? gustLoad;
    }

    /// <summary>一格墙的耐压估算结果（单位：Pa / 无量纲）。</summary>
    internal readonly struct WallPressure
    {
        internal readonly float hp;
        internal readonly float rating;
        internal readonly float hold;
        /// <summary>单格满血墙链开始掉血的两侧压差（kPa）。</summary>
        internal readonly float yieldKPa;
        /// <summary>单格满血墙链被一击摧毁的压差（kPa）。</summary>
        internal readonly float breakKPa;

        internal WallPressure(float hp, float rating, float hold, float yieldKPa, float breakKPa)
        {
            this.hp = hp;
            this.rating = rating;
            this.hold = hold;
            this.yieldKPa = yieldKPa;
            this.breakKPa = breakKPa;
        }
    }

    /// <summary>
    /// 复刻 AO 的 <c>WindLoadField</c> 公式算一格墙的耐压，供窗口显示。
    /// <para>
    /// 载荷比 = <c>chainQ / (capacityCoef × Σhold)</c>，其中
    /// <c>chainQ = 2 × scale × ΔP / 1.225</c>（ΔP 单位 Pa）；
    /// 单格满血链的 <c>capacityCoef = (1 − 0.5·β)^exponent</c>（β=0.8、exp=1.7 时约 0.42）。
    /// 反解出载荷比 = 1（开始掉血）与 = <c>WindDamageGustLoad</c>（一击摧毁）对应的 ΔP。
    /// </para>
    /// <para>
    /// 这里按**单格墙链**算：多格厚链的 Σhold 变大、但 capacityCoef 同时掉到 0.25 附近，
    /// 净收益只有两成左右，所以单格值是玩家实际会遇到的最常见情形。
    /// </para>
    /// </summary>
    internal static WallPressure WallPressureOf(ThingDef? def, float pressureFactor)
    {
        ReadSettings();

        float hp = Math.Max(1f, def?.GetStatValueAbstract(StatDefOf.MaxHitPoints) ?? 1f);
        float speed = Math.Max(0.1f, baseSpeed + speedPerHp * hp);
        float rating = speed * speed;
        float hold = rating * Math.Max(0.01f, pressureFactor * 1.2f);

        // capacityCoef 下限是 minCapacity（默认 0.25），单格链按公式算正好落在 0.42 附近。
        float coef = Math.Max(minCapacity, (float)Math.Pow(Math.Max(0.01f, 1f - 0.5f * shareBeta), capacityExponent));
        float paPerLoad = coef * hold * 1.225f / Math.Max(0.0001f, 2f * wallPressureScale);

        return new WallPressure(hp, rating, hold, paPerLoad / 1000f, paPerLoad * gustLoad / 1000f);
    }

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
