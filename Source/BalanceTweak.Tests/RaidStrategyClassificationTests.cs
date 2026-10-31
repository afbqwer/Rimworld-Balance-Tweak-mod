using System;
using BalanceTweak;
using RimWorld;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="RaidStrategyData"/> 的分组判定。
///
/// <para>
/// <b>被测的不变量</b>：页签内的四个分组只由 <c>workerClass</c> 决定，且"哪个 worker 归哪组"是硬编码的
/// 全名比较 —— 原版的继承关系有两处反直觉（<c>ImmediateAttackSmart</c> 不继承 <c>ImmediateAttack</c>；
/// <c>Sappers</c> / <c>PsychicRitualSiege</c> 直接继承 <c>WithRequiredPawnKinds</c>），
/// 少列一个就会让该策略掉进"其他"。这条测试把这层对应关系钉住。
/// </para>
/// </summary>
public class RaidStrategyClassificationTests
{
    private const int Immediate = 0;
    private const int Breaching = 1;
    private const int Siege = 2;
    private const int Other = 3;

    private static int PropTypeOf(Type? worker)
    {
        var data = new RaidStrategyData { def = new RaidStrategyDef { workerClass = worker } };
        return data.GetPropType();
    }

    [Theory]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttack), Immediate)]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttackSmart), Immediate)]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttackFriendly), Immediate)]
    [InlineData(typeof(RaidStrategyWorker_ShamblerAssault), Immediate)]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttackBreaching), Breaching)]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttackBreachingSmart), Breaching)]
    [InlineData(typeof(RaidStrategyWorker_ImmediateAttackSappers), Breaching)]
    [InlineData(typeof(RaidStrategyWorker_Siege), Siege)]
    [InlineData(typeof(RaidStrategyWorker_SiegeMechanoid), Siege)]
    [InlineData(typeof(RaidStrategyWorker_PsychicRitualSiege), Siege)]
    [InlineData(typeof(RaidStrategyWorker_StageThenAttack), Other)]
    public void 按workerClass归类(Type worker, int expected)
    {
        Assert.Equal(expected, PropTypeOf(worker));
    }

    [Fact]
    public void 缺workerClass时归入其他而不是抛异常()
    {
        Assert.Equal(Other, PropTypeOf(null));
    }

    [Fact]
    public void 非RaidStrategyDef时归入其他()
    {
        var data = new RaidStrategyData { def = new IncidentDef() };
        Assert.Equal(Other, data.GetPropType());
    }
}
