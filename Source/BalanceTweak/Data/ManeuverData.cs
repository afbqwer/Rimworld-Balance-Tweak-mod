using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 招式类型（<see cref="ManeuverDef"/>）自己的一行。
/// <para>
/// 存在的理由：<see cref="ManeuverDef"/> 与 <see cref="ToolCapacityDef"/> 是 1:1 的，
/// 而 <c>verb.meleeDamageDef</c> 是<b>全表共享</b>的同一个对象 —— 招式行（<see cref="ToolData"/>）
/// 过去直接写它，改一把 Cut 武器的伤害类型等于改掉所有 Cut 武器。共享对象没有自己的数据行，
/// 就没有"只改它"这件事可言，所以先把行建出来（先例：<see cref="ProjectileData"/>）。
/// </para>
/// <para>
/// 招式行上的 <c>damageType</c> 随后改成这一行的<b>镜像</b>（<c>IsMirror = true</c>），
/// 编辑它 = 编辑这里，见 <see cref="ToolData"/>。
/// </para>
/// </summary>
[TweakFor(typeof(ManeuverDef), SettingType.Maneuver)]
class ManeuverData : TweakData<ManeuverData>
{
    /// <summary>该招式打出的伤害类型。写进 <c>ManeuverDef.verb.meleeDamageDef</c>。</summary>
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? meleeDamageDef = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    public string? requiredCapacity = null;

    /// <summary>
    /// 必须早于 <see cref="ToolData"/> 的 400：招式行的 <c>damageType</c> 是它的镜像，
    /// 招式行在建快照 / 解析时直接读这一行。
    /// <para>
    /// 这个先后关系<b>只写在这里，没有任何机制拦</b>。踩过一次同款坑：弹丸（100）晚于武器时，
    /// 武器的镜像按"没有弹丸"算出来并被固化（见 <see cref="WeaponData.ResolveProjectile"/> 的注释）。
    /// </para>
    /// </summary>
    public override int LoadingOrd => 100;

    /// <summary>
    /// 引用本招式的招式行（运行期索引，不序列化）。
    /// <para>
    /// 与 <see cref="ProjectileData"/> 的 sharedWeapons 同一个理由：共享对象一改，
    /// 所有引用它的行都得把镜像与派生值刷掉，只刷自己那一行等于放任其余行显示与实际脱节。
    /// </para>
    /// <para>按 ManeuverDef 的 defName 归组，克隆 / 原始 / 已存三层实例天然共享同一份登记。</para>
    /// </summary>
    private static readonly Dictionary<string, List<TweakID>> sharedTools = new();

    /// <summary>登记一个引用本招式的招式行（幂等）。由 <see cref="ToolData"/> 解析出招式时调用。</summary>
    public void RegisterSharedTool(TweakID toolId)
    {
        if (string.IsNullOrEmpty(id.defName)) return;
        if (!sharedTools.TryGetValue(id.defName, out var list))
        {
            list = new List<TweakID>();
            sharedTools.Add(id.defName, list);
        }
        if (!list.Contains(toolId)) list.Add(toolId);
    }

    /// <summary>
    /// 刷新全部引用本招式的招式行镜像。
    /// <para>
    /// <b>原始快照一律跳过</b>：它是 <c>Revert</c>（还原原值）与重置的唯一"原值"来源，
    /// 派生同步写进快照等于把"原值"改成"现值"，之后重置会还原成改后的值。
    /// 快照的镜像停在建快照那一刻即可；UI 打开时 <see cref="ToolData.EnsureDisplayValues"/>
    /// 会给从快照克隆出来的行补一次同步。
    /// </para>
    /// </summary>
    public void RefreshSharedTools()
    {
        if (string.IsNullOrEmpty(id.defName)) return;
        if (!sharedTools.TryGetValue(id.defName, out var list)) return;
        // 先取长度：刷新途中可能解析并登记新的共享者，边遍历边扩容不安全。
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            // 跳过尚未绑定 def 的行：招式行在解析出招式之前 def 是 null，此时刷镜像只会拿到半成品。
            if (GetLiveData(list[i]) is not ToolData td || td.def == null) continue;
            if (ReferenceEquals(TweakDatabase.GetOriginal(td.id), td)) continue;
            td.RefreshManeuverMirror();
        }
    }

    /// <summary>招式能力 → 招式（verb）的只读查找表，给 DPS 计算取"伤害的护甲类别"用（见 <see cref="DataUtility"/>）。</summary>
    private static Dictionary<ToolCapacityDef, VerbProperties>? verbByCapacity;

    /// <summary>
    /// 按能力取共享的 <see cref="VerbProperties"/>。
    /// <para>
    /// 原先这份索引是 <c>TweakDatabase.allMeleeVerb</c>，由 Init 一次填好；它与"招式行"无关，
    /// 只是原版 Def 数据的一个视图，所以随 <see cref="ManeuverDef"/> 的消费方一起挪到这里。
    /// 懒建一次即可：Init 在 Def 全部加载之后才跑，之后也不会再冒出新的 ManeuverDef。
    /// </para>
    /// </summary>
    internal static VerbProperties? VerbOf(ToolCapacityDef? capacity)
    {
        if (capacity == null) return null;
        if (verbByCapacity == null)
        {
            verbByCapacity = new Dictionary<ToolCapacityDef, VerbProperties>();
            foreach (var m in DefDatabase<ManeuverDef>.AllDefsListForReading)
            {
                if (m.requiredCapacity != null && m.verb != null)
                    verbByCapacity.TryAdd(m.requiredCapacity, m.verb);
            }
        }
        return verbByCapacity.TryGetValue(capacity, out var verb) ? verb : null;
    }

    public override void Apply()
    {
        SetManeuverProp(def);
    }

    /// <summary>
    /// 把本行的伤害类型写进共享的 verb，并刷新所有引用它的招式行。
    /// <para>
    /// 招式行上的"改伤害类型"最终落在这里（<see cref="ToolData"/> 的推送路径），
    /// 所以它必须做完整的两件事：写 def、刷镜像 —— 少刷一步，界面上其它 Cut 武器的伤害类型就停在旧值。
    /// </para>
    /// </summary>
    /// <param name="refreshTools">招式行推送时传 true；建快照 / 只读路径不需要。</param>
    public void SetManeuverProp(Def? def, bool refreshTools = true)
    {
        if (def is not ManeuverDef maneuver)
        {
            Log.Error($"[BalanceTweak]{this}的def为{def}!");
            return;
        }
        // 没有 verb 的 ManeuverDef 无从写起（原版数据里恒有，mod 写坏才会缺席）。
        if (maneuver.verb == null)
        {
            Log.Error($"[BalanceTweak]{maneuver.defName} 没有 verb，无法写入伤害类型。");
            return;
        }
        ApplyD<DamageDef>(nameof(meleeDamageDef), v => maneuver.verb.meleeDamageDef = v);
        // Cleared（含重置此格）写的是"还原原值"，落在 def 上；字段里留的还是旧值，
        // 不回读一次的话，招式行的镜像会被刷回改后的值。
        if (StateOf(nameof(meleeDamageDef)) == FieldState.Cleared)
            meleeDamageDef = maneuver.verb.meleeDamageDef;

        if (refreshTools) RefreshSharedTools();
    }

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (def is not ManeuverDef maneuver) return;
        var capacity = maneuver.requiredCapacity;
        requiredCapacity = capacity == null
            ? null
            : capacity.LabelCap.NullOrEmpty() ? capacity.defName : capacity.LabelCap.ToString();
        // 回填守卫：已改 / 已清空的字段不能被 def 覆盖（Readme 硬约束）。
        if (!IsFieldModified(nameof(meleeDamageDef)))
            meleeDamageDef = maneuver.verb?.meleeDamageDef;
    }

    /// <summary>ManeuverDef 大多没写 label（原版全是），标题回落到能力的可读名而不是裸 defName。</summary>
    protected override string ResolveLabel()
    {
        if (def is ManeuverDef m && m.requiredCapacity != null)
        {
            var cap = m.requiredCapacity.LabelCap;
            return cap.NullOrEmpty() ? m.defName : $"{cap}({m.defName})";
        }
        return base.ResolveLabel();
    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ManeuverType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum ManeuverType
    {
        // ManeuverDef 目前只服务近战（requiredCapacity + Verb_MeleeAttackDamage），暂不细分。
        MeleeManeuver,
    }

    public override int GetPropType() => (int)ManeuverType.MeleeManeuver;
}
