using System.Collections.Generic;

namespace BalanceTweak;

/// <summary>
/// 子项宿主：父 Data 自己声明如何产出子项（武器招式、想法阶段、健康阶段、特性程度……）。
///
/// 取代 TweakDatabase.Init() 里原先 5 处硬编码的子项创建块。
/// 实现方负责：设置子项的 <c>index</c>、调用 <c>SetParentTweak(parent, type, tweaked)</c>，
/// 并跳过数据里为空的条目。
/// </summary>
public interface ISubItemHost
{
    /// <summary>
    /// 产出挂在 <paramref name="parent"/> 下的子项。
    /// <paramref name="parent"/>.<c>def</c> 此时已被赋值。
    /// </summary>
    IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked);
}
