using BalanceTweak;
using RimWorld;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="QuestData"/> 的分组判定。
///
/// <para>
/// <b>被测的不变量</b>：分组照原版算 <c>IsRootRandomSelected</c> / <c>IsRootDecree</c> / <c>isRootSpecial</c>
/// 的口径，且优先级是 随机 → 敕令 → 特殊 → 其它。两处容易写错的地方各钉了一条：
/// 一是分组只看 <c>rootSelectionWeight</c>、不看 <c>randomlySelectable</c>
/// （否则用户把"随机抽取"关掉后，这一行会从「随机」组掉进「其它」，在筛选里凭空消失）；
/// 二是权重为 0 的敕令任务不能因为 <c>rootSelectionWeight == 0</c> 就先被判成「随机」。
/// </para>
/// </summary>
public class QuestClassificationTests
{
    private const int Random = 0;
    private const int Decree = 1;
    private const int Special = 2;
    private const int Other = 3;

    private static int PropTypeOf(QuestScriptDef def) => new QuestData { def = def }.GetPropType();

    [Fact]
    public void 权重非零归入随机池()
    {
        Assert.Equal(Random, PropTypeOf(new QuestScriptDef { rootSelectionWeight = 1f }));
    }

    [Fact]
    public void 只有敕令权重时归入敕令()
    {
        Assert.Equal(Decree, PropTypeOf(new QuestScriptDef { decreeSelectionWeight = 1f }));
    }

    [Fact]
    public void 特殊根任务归入特殊()
    {
        Assert.Equal(Special, PropTypeOf(new QuestScriptDef { isRootSpecial = true }));
    }

    [Fact]
    public void 三者皆无归入其它()
    {
        Assert.Equal(Other, PropTypeOf(new QuestScriptDef()));
    }

    /// <summary>同一条任务既是随机池又是敕令（原版确实存在）时按「随机」归类。</summary>
    [Fact]
    public void 随机优先于敕令()
    {
        var def = new QuestScriptDef { rootSelectionWeight = 1f, decreeSelectionWeight = 1f };
        Assert.Equal(Random, PropTypeOf(def));
    }

    /// <summary>"随机抽取"开关只管能不能被抽到，不该让这一行换组（换组 = 用户再也找不到它）。</summary>
    [Fact]
    public void 关掉随机抽取后仍在随机组()
    {
        var def = new QuestScriptDef { rootSelectionWeight = 1f, randomlySelectable = false };
        Assert.Equal(Random, PropTypeOf(def));
    }

    [Fact]
    public void 非QuestScriptDef时归入其它而不是抛异常()
    {
        var data = new QuestData { def = new IncidentDef() };
        Assert.Equal(Other, data.GetPropType());
    }
}
