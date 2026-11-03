using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 兼容性诊断：把「按全名找类型 / 找成员」的失败从**静默**变成**日志**。
///
/// <para>
/// <b>为什么需要它</b>：跨 Mod 兼容一律走字符串反射（理由见 <see cref="DataUtility"/> 顶部注释），
/// 代价是失配不留痕迹 —— <c>GetByName</c> 找不到成员就返回 null，调用点一句 <c>as bool?</c> 之后
/// 还是 null，界面上那一列从此恒为空白。玩家只看到「这个 Mod 不支持」，日志里一个字都没有。
/// </para>
///
/// <para>
/// <b>为什么先登记、末尾汇总</b>：一次 Mod 版本升级往往同时改名三五个成员，逐条 <c>Log.Error</c>
/// 会把日志刷成一片红，反而盖住真正的崩溃。所以启动期只登记，<see cref="Report"/> 在初始化收尾时
/// 输出**一条**带完整清单的 <c>Log.Error</c>；收尾之后才出现的缺失（运行期惰性解析）没有汇总机会，
/// 改为首见即报。
/// </para>
///
/// <para>
/// <b>为什么不报「目标为 null」</b>：<c>GetByName(null, "x")</c> 说明上游根本没拿到对象
/// （该 Def 没有这个扩展 / 可选 Mod 未装），那不是成员缺失；只有「类型在、成员不在」才是失配。
/// </para>
/// </summary>
internal static class Diagnostics
{
    /// <summary>一次失配的登记项。同一 (目标, 成员) 只留一条，只累计次数。</summary>
    private sealed class Miss
    {
        public string Target = "";
        public string? Member;
        public string Kind = "";
        public string Context = "";
        public int Count;
    }

    private const string Tag = "[BalanceTweak]";

    /// <summary>登记顺序即输出顺序（先报顶层类型问题，再报成员问题会更乱，故统一按发现顺序）。</summary>
    private static readonly List<Miss> order = new();

    // 两级字典而不是字符串拼 key：成员名在调用点都是字面量 / nameof，
    // 这样失配路径上不产生额外分配（该路径可能在绘制期被反复走到）。
    private static readonly Dictionary<Type, Dictionary<string, Miss>> memberIndex = new();
    private static readonly Dictionary<string, Miss> typeIndex = new(StringComparer.Ordinal);

    private static bool reported;

    /// <summary>某个成员失配被访问到的次数；没登记过返回 0（测试用）。</summary>
    internal static int Hits(Type type, string member)
        => memberIndex.TryGetValue(type, out var perType) && perType.TryGetValue(member, out var miss)
            ? miss.Count
            : 0;

    /// <summary>是否登记过该成员名的失配（不看类型，测试用）。</summary>
    internal static bool ContainsMember(string member)
        => order.Exists(m => m.Member == member);

    /// <summary>是否登记过该类型名的失配（测试用）。</summary>
    internal static bool ContainsType(string typeName) => typeIndex.ContainsKey(typeName);

    /// <summary>记录一次成员失配（字段 / 属性 / 方法）。<paramref name="type"/> 为 null 时忽略。</summary>
    internal static void MissMember(Type? type, string member, string kind, string? context)
    {
        if (type == null || string.IsNullOrEmpty(member)) return;

        if (!memberIndex.TryGetValue(type, out var perType))
            memberIndex[type] = perType = new Dictionary<string, Miss>(StringComparer.Ordinal);

        if (perType.TryGetValue(member, out var seen))
        {
            seen.Count++;
            return;
        }

        var miss = new Miss
        {
            Target = type.FullName ?? type.Name,
            Member = member,
            Kind = kind,
            Context = context ?? "",
            Count = 1,
        };
        perType[member] = miss;
        Register(miss);
    }

    /// <summary>记录一次类型失配（按全名反射取不到类型）。</summary>
    /// <param name="modId">本该提供该类型的 Mod packageId；为 null 表示按"归属程序集已加载"判定的失配。</param>
    internal static void MissType(string typeName, string context, string? modId)
    {
        if (string.IsNullOrEmpty(typeName)) return;
        if (typeIndex.TryGetValue(typeName, out var seen))
        {
            seen.Count++;
            return;
        }

        var miss = new Miss
        {
            Target = typeName,
            Member = null,
            Kind = "类型",
            Context = modId == null
                ? $"{context}；归属程序集已加载，但该类型不存在"
                : $"{context}；所需的 Mod {modId} 已启用，但该类型不存在",
            Count = 1,
        };
        typeIndex[typeName] = miss;
        Register(miss);
    }

    private static void Register(Miss miss)
    {
        order.Add(miss);
        // 汇总已经输出过（说明在运行期才发现）：没有第二次汇总机会，直接单条报出来。
        if (reported) Log.Error($"{Tag} 反射失配：{Describe(miss).TrimStart(' ', '·')}");
    }

    /// <summary>初始化收尾调用：把启动期登记的全部失配合成一条 <c>Log.Error</c>。无失配时静默。</summary>
    internal static void Report()
    {
        if (reported) return;
        reported = true;
        if (order.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append(Tag).Append(" ===== 兼容性诊断：反射失配 ").Append(order.Count).Append(" 处 =====");
        foreach (var miss in order) sb.Append('\n').Append(Describe(miss));
        sb.Append('\n').Append("  上面这些位置的功能在本次运行中不会生效（对应列 / 按钮保持空白或无效）。");
        sb.Append('\n').Append("  常见原因：可选 Mod 更新后改了成员名或换了基类、游戏版本升级。");
        Log.Error(sb.ToString());
    }

    private static string Describe(Miss miss)
    {
        var sb = new StringBuilder("  · ");
        sb.Append(miss.Kind).Append(' ').Append(miss.Target);
        if (miss.Member != null) sb.Append('.').Append(miss.Member);
        if (miss.Context.Length > 0) sb.Append(" —— 来自 ").Append(miss.Context);
        if (miss.Count > 1) sb.Append("（累计 ").Append(miss.Count).Append(" 次）");
        return sb.ToString();
    }

    /// <summary>仅供测试：清空登记并允许重新汇总。</summary>
    internal static void Reset()
    {
        order.Clear();
        memberIndex.Clear();
        typeIndex.Clear();
        reported = false;
    }
}
