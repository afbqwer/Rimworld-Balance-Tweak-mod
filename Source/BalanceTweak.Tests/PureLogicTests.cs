using System.Collections.Generic;
using BalanceTweak;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 纯逻辑断言 —— 不启动游戏、不碰 <c>DefDatabase</c>、不依赖 <c>Scribe</c>，
/// 直接在普通 .NET 进程里跑（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3.4）。
/// 这一组就是 ③ 想要的那个"秒级入口"。
///
/// <para>
/// <b>分工</b>：这里放<b>不碰 Def</b> 的底层断言；<see cref="StockGeneratorCodecTests"/> 放
/// StockGenerator 编解码的机制测试。依赖真 <c>DefDatabase</c> 的往返测试留在游戏内
/// （<c>TweakDataSerializationTests</c>）。
/// </para>
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

    /// <summary>
    /// <c>ThingFilter</c> 的判空分支必须<b>早于</b> <c>ICollection</c> ——
    /// 否则过滤器会被当普通集合误判（"无允许项"的过滤器其实该判空）。
    /// </summary>
    [Fact]
    public void IsEmptyValue_ThingFilter优先于集合()
    {
        Assert.True(TweakData.IsEmptyValue(new ThingFilter()), "无允许项的 ThingFilter 应判空");

        var tf = new ThingFilter();
        tf.SetAllow(new ThingDef { defName = "Steel" }, true);   // 合成 Def：SetAllow 不需要 DefDatabase
        Assert.False(TweakData.IsEmptyValue(tf), "有允许项的 ThingFilter 不判空");
    }

    /// <summary>
    /// 未声明类型（= 其它 Mod 新增的 StockGenerator 子类）必须能按"空字段表"解码成占位实例。
    /// <para>
    /// 这条守着一条真实的回归：<c>TryDecode</c> 曾经要求"类型已声明"，于是非原版条目在
    /// <c>TryParse</c> 里被整条拒绝 —— 代价是"存档里少一条"+ 两个测试变红。
    /// 这里直接盯住 <c>PocoCodec.TryDecode</c> 本身（不走 <c>GenTypes</c> 按名查类型那一步）。
    /// </para>
    /// </summary>
    [Fact]
    public void 未声明类型不被TryDecode拒绝()
    {
        Assert.False(PocoCodec.IsDeclared(typeof(FakeModGenerator)));
        Assert.Empty(PocoCodec.FieldsFor(typeof(FakeModGenerator)));

        Assert.True(PocoCodec.TryDecode("", typeof(FakeModGenerator), out var value));
        Assert.IsType<FakeModGenerator>(value);
    }
}
