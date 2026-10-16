using System.Collections.Generic;
using BalanceTweak;

namespace BalanceTweak.Tests;

/// <summary>
/// 纯逻辑断言 —— 不启动游戏、不碰 <c>DefDatabase</c>、不依赖 <c>Scribe</c>，
/// 直接在普通 .NET 进程里跑（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3.4）。
/// 这一组就是 ③ 想要的那个"秒级入口"。
/// </summary>
public class PureLogicTests
{
    [Fact]
    public void IsEmptyValue_判据()
    {
        Assert.True(TweakData.IsEmptyValue(null));
        Assert.True(TweakData.IsEmptyValue(""));
        Assert.False(TweakData.IsEmptyValue("x"));
        Assert.True(TweakData.IsEmptyValue(new List<int>()));
        Assert.False(TweakData.IsEmptyValue(new List<int> { 1 }));
        Assert.False(TweakData.IsEmptyValue(0));   // 数值 0 不算空
        Assert.False(TweakData.IsEmptyValue(false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a|b=c;[d]\\e;f~g,h=i")]
    [InlineData("\\")]
    [InlineData("[[;;]]")]
    [InlineData("==")]
    public void StockGen转义_互逆(string raw)
        => Assert.Equal(raw, StockGeneratorCodec.Unescape(StockGeneratorCodec.Escape(raw)));
}
