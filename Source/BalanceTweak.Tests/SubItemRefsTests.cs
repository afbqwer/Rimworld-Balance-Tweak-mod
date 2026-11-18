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
    /// 清单后缀 → 实建 id 的完整链路：<c>Create()</c> + <c>SetParentTweak</c> 之后，
    /// 实例的 <c>id</c> 必须逐字等于「父 defName_清单后缀」。
    ///
    /// <para>
    /// 这是本次重构唯一无法靠 review 保证的一环：两边各算一次 id，跑偏时不会有异常，
    /// 只会"某些子行永远显示不出来"。四类都测（招式 / 想法阶段 / 健康阶段 / 特性程度），
    /// 因为它们的后缀规则不同 —— 前三个是序号，特性程度是 degree。
    /// </para>
    /// <para>
    /// 父项用手搓 Def + 对象初始化器，不走 <c>SetDef</c>：那条路会初始化静态字段元数据
    /// （`MayRequire` → `ModLister`），需要真实游戏环境。同 <c>MirrorColumnTests</c> 的做法。
    /// </para>
    /// </summary>
    [Fact]
    public void 实建id与清单后缀逐字一致()
    {
        // ① 招式
        var gun = new ThingDef { defName = "BT_TestToolId", tools = new List<Tool> { new(), new() } };
        var weapon = new WeaponData { def = gun, id = new TweakID(gun.defName, SettingType.Weapon) };
        AssertAllIdsMatch(weapon);

        // ② 想法阶段：中间夹 null，序号不连续时最容易和清单错开
        var thought = new ThoughtDef
        {
            defName = "BT_TestThoughtId",
            stages = new List<ThoughtStage> { new(), null!, new() },
        };
        AssertAllIdsMatch(new ThoughtData { def = thought, id = new TweakID(thought.defName, SettingType.Thought) });

        // ③ 特性程度：后缀必须是 degree
        var trait = new TraitDef
        {
            defName = "BT_TestTraitId",
            degreeDatas = new List<TraitDegreeData> { new() { degree = 5 }, new() { degree = 9 } },
        };
        AssertAllIdsMatch(new TraitData { def = trait, id = new TweakID(trait.defName, SettingType.Trait) });
    }

    /// <summary>④ 健康阶段（单独一条，因为 HediffData 同时产阶段和 VerbGiver 招式两段）。</summary>
    [Fact]
    public void 健康阶段实建id与清单后缀一致()
    {
        var hediff = new HediffDef { defName = "BT_TestHediffId", stages = new List<HediffStage> { new() } };
        AssertAllIdsMatch(new HediffData { def = hediff, id = new TweakID(hediff.defName, SettingType.Hediff) });
    }

    private static void AssertAllIdsMatch(TweakData parent)
    {
        var host = (ISubItemHost)parent;
        var refs = host.SubItemRefs(parent.def!).ToList();
        Assert.NotEmpty(refs);
        foreach (var r in refs)
        {
            var sub = r.Create();
            Assert.True(sub.SetParentTweak(parent, r.Type), $"{r.Type} 应按清单后缀 {r.IdSuffix} 挂上父项");
            Assert.Equal(new TweakID(parent.def!.defName + "_" + r.IdSuffix, r.Type), sub.id);
        }
    }
}
