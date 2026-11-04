using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 页签顺序的唯一来源是 <c>SettingType.cs</c> 的 <see cref="BalanceTweakSettings.TabOrder"/>，
/// 它和枚举声明顺序是两回事 —— 于是"新加了枚举成员却忘了写进顺序列表"不会报错，
/// 只会让那个页签在下拉里凭空消失。这组测试就是那一处的报警器。
/// </summary>
public class SettingTypeOrderTests
{
    /// <summary>除 <c>None</c> 外的每个枚举成员都必须恰好出现一次。</summary>
    [Fact]
    public void 顺序列表覆盖全部枚举成员()
    {
        var members = typeof(BalanceTweakSettings.SettingType)
            .GetEnumValues()
            .Cast<BalanceTweakSettings.SettingType>()
            .Where(t => t != BalanceTweakSettings.SettingType.None)
            .ToList();

        var missing = members.Except(BalanceTweakSettings.TabOrder).ToList();

        Assert.True(
            missing.Count == 0,
            $"SettingType.cs 的 TabOrder 缺少这些成员，对应页签不会出现在下拉里：{string.Join(", ", missing)}");
    }

    /// <summary>重复项会让下拉里出现两张同名页签，且后者永远选不中。</summary>
    [Fact]
    public void 顺序列表没有重复项()
    {
        var duplicated = BalanceTweakSettings.TabOrder
            .GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            duplicated.Count == 0,
            $"SettingType.cs 的 TabOrder 有重复项：{string.Join(", ", duplicated)}");
    }

    /// <summary>
    /// <c>None</c> 可有可无（它不指向真实数据，只是历史遗留的下拉占位），
    /// 但一旦写进来就不许重复 —— 顺带把那句"删掉这一行也无妨"的注释钉在测试里。
    /// </summary>
    [Fact]
    public void None可以缺席但不许重复()
    {
        var noneCount = BalanceTweakSettings.TabOrder.Count(t => t == BalanceTweakSettings.SettingType.None);

        Assert.InRange(noneCount, 0, 1);
    }

    /// <summary>
    /// 顺序列表里不该出现枚举里没有的值（用强制转换硬塞进来的），
    /// 否则下拉会拿到一个没有标题、也没有数据的页签。
    /// </summary>
    [Fact]
    public void 顺序列表不含未定义枚举值()
    {
        var defined = new HashSet<BalanceTweakSettings.SettingType>(
            typeof(BalanceTweakSettings.SettingType).GetEnumValues().Cast<BalanceTweakSettings.SettingType>());

        var undefined = BalanceTweakSettings.TabOrder.Where(t => !defined.Contains(t)).ToList();

        Assert.True(
            undefined.Count == 0,
            $"TabOrder 含未定义的枚举值：{string.Join(", ", undefined)}");
    }
}
