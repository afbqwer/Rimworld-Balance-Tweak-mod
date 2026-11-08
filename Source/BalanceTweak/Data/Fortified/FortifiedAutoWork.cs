using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 自动工作台（<c>ModExtension_AutoWorkTable</c>）的反射入口。
///
/// <para>
/// <b>为什么全是字符串</b>：Fortified 是可选 Mod，项目没有、也不允许给它加 csproj 引用。
/// 扩展本身按全名查找，字段读写统一走 <see cref="DataUtility"/> 的反射成员表；
/// 闭包里只捕获 <c>object</c>，绝不让 <c>Fortified.*</c> 的类型出现在字段 / 局部变量 / 委托里。
/// </para>
///
/// <para>
/// DMS 的自动工作台（反应室 / 烧结炉 / 机械打印机 / 自动磨床 / CE 弹药打印机）都只挂这个扩展，
/// 所以入口按扩展判定，不写死任何 DMS 的 defName —— 别的 Mod 用同一套框架也能直接编辑。
/// </para>
///
/// <para>
/// <b>不需要缓存刷新</b>：<c>Building_WorkTableAutonomous</c> 每次现读扩展的
/// <c>workTime</c> / <c>workAmountPerStage</c> / <c>pullCheckIntervalTicks</c> 等字段，
/// 已生成的建筑与新建建筑看到的是同一个扩展对象。
/// </para>
/// </summary>
internal static class FortifiedAutoWork
{
    internal const string ExtensionType = "Fortified.ModExtension_AutoWorkTable";

    internal static object? FindExtension(Def? def)
        => DataUtility.FindModExtension(def, ExtensionType);

    internal static bool Has(Def? def) => FindExtension(def) != null;

    internal static int? WorkTime(object? extension)
        => DataUtility.AsInt(DataUtility.GetByName(extension, "workTime"));

    internal static int? AmountPerStage(object? extension)
        => DataUtility.AsInt(DataUtility.GetByName(extension, "workAmountPerStage"));

    internal static bool? PullFromLinkedStorage(object? extension)
        => DataUtility.GetByName(extension, "pullFromLinkedStorage") as bool?;

    internal static int? PullCheckIntervalTicks(object? extension)
        => DataUtility.AsInt(DataUtility.GetByName(extension, "pullCheckIntervalTicks"));

    internal static bool? AutoEjectProducts(object? extension)
        => DataUtility.GetByName(extension, "autoEjectProducts") as bool?;

    internal static ThingPlaceMode? EjectPlaceMode(object? extension)
        => DataUtility.GetByName(extension, "ejectPlaceMode") is ThingPlaceMode mode ? mode : null;

    /// <summary>
    /// 扩展上的 <c>Dictionary&lt;SkillDef, int&gt;</c> → 可落盘的 <see cref="SkillGain"/> 列表。
    /// 字段表里已经登记过 <c>SkillGain</c>，所以 UI 可以直接复用技能增益编辑器。
    /// </summary>
    internal static List<SkillGain> Skills(object? extension)
    {
        var result = new List<SkillGain>();
        if (DataUtility.GetByName(extension, "skills") is not Dictionary<SkillDef, int> map) return result;

        foreach (var pair in map)
            if (pair.Key != null)
                result.Add(new SkillGain { skill = pair.Key, amount = pair.Value });

        result.Sort((a, b) => string.Compare(a.skill?.defName, b.skill?.defName, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>把编辑窗口里的技能列表还原成扩展需要的字典；空列表 = 清空。</summary>
    internal static Dictionary<SkillDef, int> ToSkillMap(List<SkillGain>? list)
    {
        var map = new Dictionary<SkillDef, int>();
        if (list == null) return map;
        foreach (var gain in list)
        {
            if (gain?.skill == null) continue;
            map[gain.skill] = gain.amount;
        }
        return map;
    }

    internal static void Set(object extension, string field, object? value)
        => DataUtility.SetByName(extension, field, value);
}
