using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified（<c>AOBA.Framework</c>）重型装备系统的反射入口。
///
/// <para>
/// <b>为什么全是字符串</b>：Fortified 是可选 Mod，项目没有、也不允许给它加 csproj 引用（见 Readme 硬约束）。
/// 所有类型与成员都按全名反射，字段值统一装进 <see cref="object"/>，绝不让可选 Mod 的类型出现在
/// 字段 / 局部变量 / 闭包里 —— 否则未装 Fortified 的玩家会在启动期 <c>TypeLoadException</c>。
/// </para>
///
/// <para>
/// <b>档位列表挂在 <see cref="WeaponData.HeavyTierOptions"/></b>：<c>OptionsList</c> 是
/// <c>StatFieldMeta</c> 在 <b>Data 类型自己</b>身上按字段名反射取的，不能写在别的类里。
/// 这里只负责在全部 Def 加载后（<c>[StaticConstructorOnStartup]</c>）把它填满。
/// </para>
///
/// <para>
/// <b>不做缓存失效</b>：装备判定（<c>HeavyEquippableExtension.CanEquippedBy</c> /
/// <c>CheckUtility.IsMechUseable</c>）每次现读扩展与档位 Def，改完立即对下一次装备判定生效；
/// 已经拿在手上的武器不会重新校验，这一点和原版改数值一致。
/// </para>
/// </summary>
[StaticConstructorOnStartup]
internal static class FortifiedHeavy
{
    internal const string TierDefType = "Fortified.HeavyEquippableDef";
    internal const string ExtensionType = "Fortified.HeavyEquippableExtension";
    private const string TierField = "EquippableDef";

    /// <summary>全部档位的 defName（供武器页下拉）。静态构造里填充一次。</summary>
    internal static readonly List<string> TierDefNames = new();

    static FortifiedHeavy()
    {
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return;
        var type = DataUtility.OptionalType(TierDefType, TweakDatabase.FFF, nameof(FortifiedHeavy));
        if (type == null) return;

        foreach (var def in GenDefDatabase.GetAllDefsInDatabaseForDef(type))
            TierDefNames.Add(def.defName);
        TierDefNames.Sort(StringComparer.OrdinalIgnoreCase);

        // clear + add 而不是换引用：WeaponData.HeavyTierOptions 是 static readonly。
        WeaponData.HeavyTierOptions.Clear();
        WeaponData.HeavyTierOptions.AddRange(TierDefNames);
    }

    /// <summary>找武器 / 装备上的重型扩展；没有返回 null。</summary>
    internal static object? FindExtension(Def? def)
        => DataUtility.FindModExtension(def, ExtensionType);

    /// <summary>扩展上记录的档位 defName；扩展缺失或没配返回 null。</summary>
    internal static string? TierNameOf(object? extension)
        => (DataUtility.GetByName(extension, TierField) as Def)?.defName;

    /// <summary>把档位写回扩展。调用方保证 <paramref name="tier"/> 非 null。</summary>
    internal static void SetTier(object extension, Def tier)
        => DataUtility.SetByName(extension, TierField, tier);

    /// <summary>按 defName 取档位 Def；Fortified 缺席 / 档位已删 / 名字拼错都返回 null。</summary>
    internal static Def? FindTier(string? defName)
    {
        if (defName.NullOrEmpty()) return null;
        if (!DataUtility.MayRequire(TweakDatabase.FFF)) return null;
        var type = DataUtility.OptionalType(TierDefType, TweakDatabase.FFF, nameof(FindTier));
        return type == null ? null : GenDefDatabase.GetDefSilentFail(type, defName!);
    }

    /// <summary>
    /// 反查：哪些武器 / 装备正在使用这个档位。
    ///
    /// <para>
    /// 与 <see cref="ProjectileData.FindWeaponLinks"/> / <see cref="BodyData.FindRaceLinks"/> 同一个模式：
    /// 字段只是触发按钮，窗口打开时扫一遍全库，绘制阶段不查询、也不缓存（数据可能刚被改过）。
    /// </para>
    /// <para>
    /// 只列真的有数据行的对象（<see cref="TweakDatabase.GetOriginal"/> 非空），否则点了跳不过去。
    /// 目标页签由 <see cref="TweakData.FindRuleFor"/> 现算 —— 重型扩展理论上可以挂在武器以外的物品上，
    /// 写死 <c>SettingType.Weapon</c> 会让那些行变成死链接。
    /// </para>
    /// </summary>
    internal static List<TweakData.TweakID> FindUsages(Def? tier)
    {
        var result = new List<TweakData.TweakID>();
        if (tier == null) return result;

        foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            var extension = FindExtension(thing);
            if (extension == null) continue;
            if (!ReferenceEquals(DataUtility.GetByName(extension, TierField), tier)) continue;

            var rule = TweakData.FindRuleFor(thing, includeFallback: true);
            if (rule == null) continue;
            var id = new TweakData.TweakID(thing.defName, rule.Tab);
            if (TweakDatabase.GetOriginal(id) == null) continue;
            result.Add(id);
        }

        result.Sort((a, b) => string.Compare(LabelOf(a), LabelOf(b), StringComparison.CurrentCulture));
        return result;
    }

    private static string LabelOf(TweakData.TweakID id)
        => TweakDatabase.GetOriginal(id)?.label ?? id.defName;
}
