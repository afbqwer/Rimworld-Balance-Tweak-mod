using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak.Tests;

/// <summary>
/// 行清单的枚举侧（<see cref="ISubItemHost.SubItemRefs"/>）—— 不碰 <c>DefDatabase</c>，手搓 Def 即可。
///
/// <para>
/// 这里钉的是两件最容易悄悄出错的事：<b>id 的后缀规则</b>（阶段 / 招式用序号，特性程度用 degree），
/// 以及<b>清单与 <c>SetParentTweak</c> 算出的 id 必须一致</b>。后者一旦跑偏，那一行永远显示不出来，
/// 而且不会抛异常、不会有日志 —— 只能靠断言。
/// </para>
/// </summary>
public class SubItemRefsTests
{
    [Fact]
    public void 武器招式按序号产行()
    {
        var def = new ThingDef
        {
            defName = "BT_TestGun",
            tools = new List<Tool> { new(), new() },
        };

        var refs = new WeaponData().SubItemRefs(def).ToList();

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Equal(SettingType.MeleeTool, r.Type));
        Assert.Equal(new[] { "0", "1" }, refs.Select(r => r.IdSuffix));
        // Create 只造实例、设好 index，不挂父项（挂父项要父实例，清单阶段还没有）
        Assert.Equal(1, ((ToolData)refs[1].Create()).index);
    }

    [Fact]
    public void 没有招式就不产行()
    {
        var def = new ThingDef { defName = "BT_TestEmpty" };
        Assert.Empty(new WeaponData().SubItemRefs(def));
        // 类型不符时也必须安静返回空，不能抛
        Assert.Empty(new WeaponData().SubItemRefs(new TraitDef { defName = "BT_NotAWeapon" }));
    }

    [Fact]
    public void 特性程度用degree而不是序号()
    {
        var def = new TraitDef
        {
            defName = "BT_TestTrait",
            degreeDatas = new List<TraitDegreeData>
            {
                new() { degree = -1 },
                new() { degree = 0 },
                new() { degree = 2 },
            },
        };

        var refs = new TraitData().SubItemRefs(def).ToList();

        Assert.Equal(3, refs.Count);
        Assert.All(refs, r => Assert.Equal(SettingType.TraitDegree, r.Type));
        // 序号是 0/1/2，后缀必须是 -1/0/2：旧档按 degree 存，重排 degreeDatas 不该让修改失配
        Assert.Equal(new[] { "-1", "0", "2" }, refs.Select(r => r.IdSuffix));
    }

    [Fact]
    public void 空阶段不产行()
    {
        var def = new ThoughtDef
        {
            defName = "BT_TestThought",
            stages = new List<ThoughtStage> { new(), null!, new() },
        };

        var refs = new ThoughtData().SubItemRefs(def).ToList();

        Assert.Equal(2, refs.Count);
        Assert.Equal(new[] { "0", "2" }, refs.Select(r => r.IdSuffix));
    }

    /// <summary>
    /// 清单里的后缀必须与各类自己的 id 规则同源。
    /// <para>
    /// 完整链路（清单 id == <c>SetParentTweak</c> 实算的 id）没法在单测里跑：那要 <c>SetDef</c>，
    /// 而它会碰 <c>ModLister</c> / 静态字段元数据，需要真实游戏环境。所以这里钉住"两边都取自
    /// <c>IdSuffix</c>"这个前提，剩下的由代码结构保证（<c>SetParentTweak</c> 里只调 <c>IdSuffix</c>）。
    /// </para>
    /// </summary>
    [Fact]
    public void 清单后缀与各自id规则同源()
    {
        var gun = new ThingDef { defName = "BT_TestSync", tools = new List<Tool> { new(), new() } };
        var toolRefs = new WeaponData().SubItemRefs(gun).ToList();
        for (int i = 0; i < toolRefs.Count; i++)
            Assert.Equal(ToolData.IdSuffix(gun, i), toolRefs[i].IdSuffix);

        var trait = new TraitDef
        {
            defName = "BT_TestSyncTrait",
            degreeDatas = new List<TraitDegreeData> { new() { degree = -1 }, new() { degree = 3 } },
        };
        var degreeRefs = new TraitData().SubItemRefs(trait).ToList();
        for (int i = 0; i < degreeRefs.Count; i++)
            Assert.Equal(TraitDegreeNodeData.IdSuffix(trait, i), degreeRefs[i].IdSuffix);
    }
}
