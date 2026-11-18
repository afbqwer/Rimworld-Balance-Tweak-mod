using System;
using System.Collections.Generic;
using Verse;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak;

/// <summary>
/// 一条子项行的「身份 + 构造方式」。行清单与实例化都只认它 —— 这是本结构存在的全部理由。
/// <para>
/// <see cref="IdSuffix"/> 必须与 <c>SetParentTweak</c> 里算 id 的那套规则一致（同一个静态方法供给两边），
/// 否则清单里的一行和建出来的那一行会对不上；那种错配不会抛异常，只会让行悄悄消失。
/// </para>
/// <para>
/// <see cref="Create"/> 只负责 <c>new</c>（并设好 <c>index</c>），<b>不</b>调用 <c>SetParentTweak</c>：
/// 挂父项要用到父实例，而清单是在谁都没实例化的时候算出来的。
/// </para>
/// </summary>
public readonly struct SubItemRef
{
    /// <summary>子项所属的页签。</summary>
    public readonly SettingType Type;

    /// <summary>id 的后半段（<c>{父 defName}_{IdSuffix}</c>）。招式 / 阶段是序号，特性程度是 degree。</summary>
    public readonly string IdSuffix;

    /// <summary>造一个尚未挂父项的实例。</summary>
    public readonly Func<TweakData> Create;

    public SubItemRef(SettingType type, string idSuffix, Func<TweakData> create)
    {
        Type = type;
        IdSuffix = idSuffix;
        Create = create;
    }
}

/// <summary>
/// 子项宿主：父 Data 自己声明"我下面有哪些子行"（武器招式、想法阶段、健康阶段、特性程度……）。
/// <para>
/// <b>只读 def，不造实例、不挂父项。</b>它必须是纯函数：调用方会拿一个从未 SetDef 过的空实例当探针
/// 反复问（<see cref="TweakDatabase"/> 每个宿主类型只留一个探针），所以实现里不许碰 <c>def</c> /
/// <c>index</c> / <c>parentTweakId</c> 之外的实例状态。这条约束一旦破了，就会退回到"枚举与构造各算一遍"，
/// 那正是行清单要消灭的东西。
/// </para>
/// <para>取代 TweakDatabase.Init() 里原先 5 处硬编码的子项创建块。</para>
/// </summary>
public interface ISubItemHost
{
    /// <summary>
    /// 枚举挂在 <paramref name="parentDef"/> 下的子行。<paramref name="parentDef"/> 与宿主 Def 类型不符时返回空。
    /// </summary>
    IEnumerable<SubItemRef> SubItemRefs(Def parentDef);
}
