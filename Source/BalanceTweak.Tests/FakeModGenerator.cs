using RimWorld;

namespace BalanceTweak.Tests;

/// <summary>
/// 模拟"其它 Mod 新增的 <see cref="StockGenerator"/> 子类"：派生自原版类型（免去实现抽象成员），
/// 另外带一个自有字段 <c>extraModField</c>。
///
/// <para>
/// 它不在字段表里，所以 <c>StockGeneratorCodec</c> 对它只做只读展示：编码只写类型名，
/// 回填时按类型名把 Def 里那个原实例原样放回 —— 它自己的字段一个都不丢。
/// </para>
/// <para>
/// <b>必须是顶层类型</b>：<c>GenTypes.GetTypeInAnyAssembly</c> 按名字查类型时找不到嵌套类型
/// （游戏内测试曾因此踩过坑）。
/// </para>
/// </summary>
public class FakeModGenerator : StockGenerator_SingleDef
{
    public string extraModField = "mod";
}
