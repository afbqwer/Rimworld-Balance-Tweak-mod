using System;
using BalanceTweak;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 兼容性诊断的机制断言：失配必须被登记、同一处只登记一次、没有目标时不许登记。
///
/// <para>
/// 断言用 <see cref="Diagnostics.Hits"/> / <see cref="Diagnostics.ContainsType"/> 这类按 key 查询的接口，
/// 而不是全局计数 —— <c>Diagnostics</c> 是静态状态，xunit 会让别的测试类并行跑，
/// 计数类断言会被无关的登记搅乱（表现是偶发失败、单跑全绿）。
/// </para>
/// </summary>
public class DiagnosticsTests
{
    private sealed class Target
    {
        public int field = 0;
        public int Property { get; set; }
    }

    [Fact]
    public void FindMember_命中不登记()
    {
        Diagnostics.Reset();
        Assert.NotNull(DataUtility.FindMember(typeof(Target), "field"));
        Assert.NotNull(DataUtility.FindMember(typeof(Target), "Property"));
        Assert.Equal(0, Diagnostics.Hits(typeof(Target), "field"));
        Assert.Equal(0, Diagnostics.Hits(typeof(Target), "Property"));
    }

    [Fact]
    public void FindMember_缺失登记且按类型去重()
    {
        Diagnostics.Reset();
        Assert.Null(DataUtility.FindMember(typeof(Target), "noSuchMember"));
        Assert.Null(DataUtility.FindMember(typeof(Target), "noSuchMember"));
        Assert.Null(DataUtility.FindMember(typeof(Target), "noSuchMember"));
        // 一条记录，命中三次 —— 日志里不会因为重复访问刷三遍。
        Assert.Equal(3, Diagnostics.Hits(typeof(Target), "noSuchMember"));
    }

    [Fact]
    public void 目标为null时按成员缺失处理是错的_不登记()
    {
        Diagnostics.Reset();
        // 上游没拿到对象（该 Def 没有这个扩展 / 可选 Mod 未装），不是成员缺失。
        Assert.Null(DataUtility.GetByName(null, "__no_target__"));
        Assert.False(Diagnostics.ContainsMember("__no_target__"));
    }

    [Fact]
    public void 可选Mod未安装时静默返回()
    {
        Diagnostics.Reset();
        Assert.Null(DataUtility.OptionalType("@NoSuchMod.NeverExists@", null));
        Assert.False(Diagnostics.ContainsType("@NoSuchMod.NeverExists@"));
    }

    [Fact]
    public void 归属程序集在但类型不在时登记失配()
    {
        Diagnostics.Reset();
        // 类型名首段 = 已加载程序集短名，说明归属方在，类型却没了 —— 这正是"兼容失效"。
        Assert.Null(DataUtility.OptionalType("BalanceTweak.__NoSuchType__", null));
        Assert.True(Diagnostics.ContainsType("BalanceTweak.__NoSuchType__"));
    }

    [Fact]
    public void Report_无失配与重复调用都安全()
    {
        Diagnostics.Reset();
        Diagnostics.Report();   // 空表：不打日志
        Diagnostics.Report();   // 幂等

        DataUtility.FindMember(typeof(Target), "__report_probe__");
        Diagnostics.Report();
        Diagnostics.Report();
        Diagnostics.Reset();
    }
}
