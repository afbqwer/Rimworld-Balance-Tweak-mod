using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <see cref="FactionDef.pawnGroupMakers"/>（<c>List&lt;PawnGroupMaker&gt;</c>）的克隆、Def 合并与行摘要。
///
/// <para>
/// <b>字段表与文本编解码不在这里</b>：<see cref="PawnGroupMaker"/> / <see cref="PawnGenOption"/> 都是普通
/// POCO（无多态、无私有字段、无 GetBaseCount 语义），字段声明写在 <c>TweakData.FieldCodecs.Build()</c>
/// 的 <c>PocoList</c> 那一行里，落盘 / 读档 / 剪贴板全部由 <see cref="PocoCodec"/> 的通用实现承担。
/// 本类只保留"字段表表达不了"的三件事：<b>与 Def 活对象的隔离</b>、<b>非原版子类的透传</b>、<b>行摘要</b>。
/// </para>
///
/// <para>
/// <b>为什么要与 Def 隔离</b>：<c>PawnGroupMaker.options</c> 是 <c>List&lt;PawnGenOption&gt;</c>，
/// <c>PawnGenOption.kind</c> 又是 <c>PawnKindDef</c>。若字段直接持有 Def 里的实例，用户在编辑窗口里改的
/// 每一个数字都会立刻落到 Def 上 —— "取消"就变成了假的，而且读档失败时还会留下半截修改。
/// 所以 <c>SetDef</c> 克隆进字段、<c>Apply</c> 再克隆一份交给 Def（见 <see cref="Clone"/> /
/// <see cref="MergeWithDef"/>），两次都不共享实例。
/// </para>
///
/// <para>
/// <b>非原版子类</b>（其它 Mod 给 <see cref="PawnGroupMaker"/> 写的子类）：字段表里没有它的额外字段，
/// 克隆会把那些字段抹成默认值 ⇒ 数据丢失。因此一律只读：不读不写它的字段，存档只留类型名占位，
/// 保存时按"类型相同 + 出现次序相同"从 Def 里把原实例原样放回（<see cref="MergeWithDef"/>）。
/// 这套口径与 <see cref="StockGeneratorCodec"/> 完全一致。
/// </para>
/// </summary>
internal static class PawnGroupMakerCodec
{
    // ── 转发到 PocoCodec（字段表的唯一实现在那里） ──

    /// <summary>是否本工具认识的类型。判据是"恰好是 <see cref="PawnGroupMaker"/>"：子类的字段表不完整，只能只读透传。</summary>
    public static bool IsVanilla(Type type) => type == typeof(PawnGroupMaker);

    /// <summary>类型的显示名 = 原版 XML 里 <c>Class</c> 的写法（如 <c>PawnGroupMaker</c>），也是存档里的类型标记。</summary>
    public static string TypeLabel(Type type) => GenTypes.GetTypeNameWithoutIgnoredNamespaces(type);

    /// <summary>类型可读写的字段（表外类型返回空 —— 本工具对它们不读不写）。</summary>
    public static IReadOnlyList<PocoCodec.Field> FieldsFor(Type type) => PocoCodec.FieldsFor(type);

    public static object? ReadField(PocoCodec.Field field, object target) => PocoCodec.ReadField(field, target);

    public static bool WriteField(PocoCodec.Field field, object target, object? value) => PocoCodec.WriteField(field, target, value);

    // ── 深克隆 ──

    /// <summary>
    /// 克隆一个兵种池：按字段表逐字段复制，<b>列表全部换成新实例</b>（<c>options</c> 等四个子列表连元素一起
    /// 深克隆），因此字段里的编辑永远不会落到 Def 的活对象上。
    /// <para>
    /// 表外（非原版子类）<b>不克隆</b>：沿用原实例不会带来"编辑落到 Def 上"的问题（本工具对它只读），
    /// 而克隆只会把它的额外字段抹掉。
    /// </para>
    /// </summary>
    public static PawnGroupMaker? Clone(PawnGroupMaker? maker)
    {
        if (maker == null) return null;
        var type = maker.GetType();
        if (!IsVanilla(type)) return maker;

        if (PocoCodec.CloneElement(maker, type) is PawnGroupMaker copy) return copy;

        Log.Error($"[BalanceTweak] pawnGroupMakers：克隆 {TypeLabel(type)} 失败，已沿用原实例（该条目的字段会就地改在 Def 上）。");
        return maker;
    }

    public static List<PawnGroupMaker>? CloneAll(List<PawnGroupMaker>? list)
    {
        if (list == null) return null;
        var result = new List<PawnGroupMaker>(list.Count);
        foreach (var maker in list)
        {
            var clone = Clone(maker);
            if (clone != null) result.Add(clone);
        }
        return result;
    }

    // ── 字段 → Def ──

    /// <summary>
    /// 字段 → Def。<b>返回的列表永远不是 <c>null</c></b>（硬约束：集合的终态是空集合）——
    /// 用户把行删光时 Def 上留下的是空列表，"这个派系再也凑不出袭击编成"是他自己选的结果。
    ///
    /// <para>
    /// 本工具认识的条目按字段表克隆成新实例；<b>非原版子类不重建</b>，而是按"类型相同、出现次序相同"从
    /// <paramref name="defCurrent"/> 里找回那个原实例原样放回 —— 本工具不读也不写它的字段，
    /// 所以它不会因为"这个派系被改过 + 存档读档"而丢数据，位置也保持在原处。
    /// </para>
    ///
    /// <para>
    /// <paramref name="defCurrent"/> 里没被字段认领的非原版条目（两次存档之间某个 Mod 给这个派系加了兵种池）
    /// 一律追加在末尾，绝不丢。代价是：<b>清空只清得掉本工具认识的条目</b> —— 非原版条目会留下来，
    /// 因为丢掉它们等于永久删除一份本工具读不到的数据。
    /// </para>
    /// </summary>
    public static List<PawnGroupMaker> MergeWithDef(List<PawnGroupMaker>? picked, List<PawnGroupMaker>? defCurrent)
    {
        var pool = new List<PawnGroupMaker>();
        if (defCurrent != null)
            foreach (var maker in defCurrent)
                if (maker != null && !IsVanilla(maker.GetType())) pool.Add(maker);

        var used = new List<PawnGroupMaker>();
        var result = new List<PawnGroupMaker>((picked?.Count ?? 0) + pool.Count);

        if (picked != null)
        {
            foreach (var entry in picked)
            {
                if (entry == null) continue;
                if (IsVanilla(entry.GetType()))
                {
                    var clone = Clone(entry);
                    if (clone != null && Sanitize(clone)) result.Add(clone);
                    continue;
                }

                // 非原版：找 Def 里同类型的下一个原实例（PawnGroupMaker 不重写 Equals，Contains 即引用比较）
                var match = pool.FirstOrDefault(m => m.GetType() == entry.GetType() && !used.Contains(m));
                if (match != null)
                {
                    used.Add(match);
                    result.Add(match);
                }
                else
                {
                    // 不匹配只可能来自"复制/粘贴到别的派系"：编辑器里改的就是本 Def 自己的实例，必然匹配。
                    // 此时绝不能把 entry 放进 Def —— 它是解析类型名占位标记时 Activator 造出来的、
                    // 字段全默认的实例，写进去就是替别的 Mod 编了一份它自己都没写过的数据。
                    Log.Warning($"[BalanceTweak] pawnGroupMakers：Def 里找不到 {TypeLabel(entry.GetType())} 的原实例"
                        + "（粘贴来的条目，或相关 Mod 变动过？），本工具不读不写它的字段、无法重建，已丢弃这一条。");
                }
            }
        }

        foreach (var leftover in pool)
        {
            if (used.Contains(leftover)) continue;
            Log.Message($"[BalanceTweak] pawnGroupMakers：Def 里的非原版兵种池 {TypeLabel(leftover.GetType())}"
                + " 不在字段里，已原样保留（追加在末尾）。");
            result.Add(leftover);
        }

        return result;
    }

    /// <summary>
    /// 写回 Def 前的最后一道闸：丢掉"游戏读它必炸"的条目，返回 <c>false</c> 表示整个兵种池不可用。
    ///
    /// <list type="bullet">
    /// <item><b>没有 kindDef 的兵种池</b>：<c>PawnGroupMaker.CanGenerateFrom</c> 直接解引用
    /// <c>kindDef.Worker</c> ⇒ 袭击生成期 NRE。UI 层（<c>IsItemValid</c>）已经挡过一次，
    /// 这里兜住"复制粘贴 / 手工改存档"进来的那种。</item>
    /// <item><b>没有 kind 的 <see cref="PawnGenOption"/></b>：<c>PawnGroupMakerUtility.GetOptions</c> 会拿
    /// <c>option.kind</c> 去查异种人可用性（<c>XenotypesAvailableFor</c>）⇒ 同样 NRE。
    /// 单选一条选择器的"清除"按钮就能造出这种条目，所以必须挡。</item>
    /// </list>
    ///
    /// <para>
    /// 只在<b>写回 Def</b> 这一步清：字段里仍然忠实保留用户填的东西（编辑窗口里那一行还在，
    /// 补上一个兵种就能救回来），不会在用户还没填完时就悄悄删他的行。
    /// </para>
    /// </summary>
    private static bool Sanitize(PawnGroupMaker maker)
    {
        if (maker.kindDef == null)
        {
            Log.Warning($"[BalanceTweak] pawnGroupMakers：{TypeLabel(maker.GetType())} 没有 kindDef，"
                + "原版 CanGenerateFrom 会直接 NRE，已丢弃这一条。");
            return false;
        }

        DropInvalidOptions(maker.options, maker, "options");
        DropInvalidOptions(maker.traders, maker, "traders");
        DropInvalidOptions(maker.carriers, maker, "carriers");
        DropInvalidOptions(maker.guards, maker, "guards");
        return true;
    }

    private static void DropInvalidOptions(List<PawnGenOption>? options, PawnGroupMaker maker, string fieldName)
    {
        if (options == null) return;
        int removed = options.RemoveAll(o => o == null || o.kind == null);
        if (removed > 0)
            Log.Warning($"[BalanceTweak] pawnGroupMakers：{maker.kindDef?.defName} 的 {fieldName} 里有 {removed} 条"
                + "没有兵种（kind 为空），原版取选项时会 NRE，已丢弃。");
    }

    // ── 展示 ──

    /// <summary>
    /// 行首标题：兵种池类型（<see cref="PawnGroupKindDef"/>）的标签；<b>没有 kindDef 时返回空串</b>，
    /// 由调用方决定怎么提示（数据层不做翻译）。这种条目在保存时会被丢弃 —— 原版
    /// <c>PawnGroupMaker.CanGenerateFrom</c> 直接解引用 <c>kindDef.Worker</c>，留着只会让袭击生成期 NRE。
    /// </summary>
    public static string KindLabel(PawnGroupMaker maker)
    {
        var kind = maker.kindDef;
        if (kind == null) return "";
        var label = kind.LabelCap.Resolve();
        return label.NullOrEmpty() ? kind.defName : label;
    }

    /// <summary>
    /// 行摘要：commonality + 各子列表的条目数 + 非常规的 maxTotalPoints。
    /// <para>
    /// 刻意用字段名而不是翻译键 —— 与 <see cref="StockGeneratorCodec.Summarize"/> 同口径：
    /// 这是表格里的技术性附带信息，翻译键会占掉行宽，也让"哪个字段"变得不可对照。
    /// </para>
    /// </summary>
    public static string Summarize(PawnGroupMaker maker)
    {
        var parts = new List<string>(3) { $"commonality={maker.commonality:G4}" };

        int options = maker.options?.Count ?? 0;
        parts.Add($"options={options}");

        int traders = maker.traders?.Count ?? 0;
        int carriers = maker.carriers?.Count ?? 0;
        int guards = maker.guards?.Count ?? 0;
        if (traders + carriers + guards > 0)
            parts.Add($"trader={traders}/{carriers}/{guards}");

        // 原版默认 9999999 = "不设上限"，写出来只会占地方
        if (maker.maxTotalPoints < 9999999f)
            parts.Add($"max={maker.maxTotalPoints:G4}");

        if (!maker.disallowedStrategies.NullOrEmpty())
            parts.Add($"noStrategy={maker.disallowedStrategies.Count}");

        return string.Join("  ", parts);
    }
}
