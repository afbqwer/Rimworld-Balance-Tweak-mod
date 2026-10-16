using System;
using System.Collections.Generic;
using BalanceTweak;
using RimWorld;
using Verse;

namespace BalanceTweak.Tests;

/// <summary>
/// 登记表（<c>TweakData.FieldCodecs</c>）的查表正确性。
///
/// <para>
/// 这是 ① 的**独立验证**：不启动游戏、不碰 <c>DefDatabase</c>，直接断言"哪种字段类型被分派给谁"。
/// 改造前这件事埋在两条 <c>else if</c> 链里，只能靠读代码确认；现在可以断言。
/// </para>
/// <para>
/// 尤其覆盖三类通配（`Def` 子类 / 可空枚举 / `List&lt;T&gt; where T : Def`）——
/// 它们是原 if 链里唯一有"顺序陷阱"风险的部分，查表实现靠"精确字典优先、通配兜底"避开，
/// 这里把该性质钉住。
/// </para>
/// </summary>
public class FieldCodecsTests
{
    // ── 精确类型 ──

    [Theory]
    [InlineData(typeof(float?))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(FloatRange?))]
    [InlineData(typeof(IntRange?))]
    [InlineData(typeof(string))]
    [InlineData(typeof(SimpleCurve))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(ThingFilter))]
    [InlineData(typeof(List<StatModifier>))]
    [InlineData(typeof(List<PawnCapacityModifier>))]
    [InlineData(typeof(List<DamageFactor>))]
    [InlineData(typeof(List<SkillGain>))]
    [InlineData(typeof(List<Aptitude>))]
    [InlineData(typeof(List<GeneticTraitData>))]
    [InlineData(typeof(List<ThingDefCountClass>))]
    [InlineData(typeof(List<IngredientCount>))]
    [InlineData(typeof(List<SkillRequirement>))]
    [InlineData(typeof(List<TraitRequirement>))]
    [InlineData(typeof(List<ProcessIngredientItem>))]
    [InlineData(typeof(List<ProcessResultItem>))]
    [InlineData(typeof(List<BiomePlantRecord>))]
    [InlineData(typeof(List<BiomeAnimalRecord>))]
    [InlineData(typeof(List<FishChance>))]
    [InlineData(typeof(List<BiomeDiseaseRecord>))]
    [InlineData(typeof(List<WeatherCommonalityRecord>))]
    [InlineData(typeof(List<TerrainThreshold>))]
    [InlineData(typeof(List<StockGenerator>))]
    public void 精确类型都能查到(Type fieldType)
        => Assert.NotNull(TweakData.FieldCodecs.Find(fieldType));

    // ── 通配类型 ──

    [Theory]
    [InlineData(typeof(ThingDef))]
    [InlineData(typeof(PawnKindDef))]
    [InlineData(typeof(SkillDef))]
    [InlineData(typeof(HediffDef))]
    public void Def子类走通配(Type defSubclass)
    {
        var codec = TweakData.FieldCodecs.Find(defSubclass);
        Assert.NotNull(codec);
        Assert.Contains("Def", codec!.Name);
    }

    [Theory]
    [InlineData(typeof(List<ThingDef>))]
    [InlineData(typeof(List<SkillDef>))]
    [InlineData(typeof(List<GeneDef>))]
    public void Def列表走通配(Type defList)
    {
        var codec = TweakData.FieldCodecs.Find(defList);
        Assert.NotNull(codec);
        Assert.Contains("List<T>", codec!.Name);
    }

    [Fact]
    public void 可空枚举走通配()
    {
        var codec = TweakData.FieldCodecs.Find(typeof(TechLevel?));
        Assert.NotNull(codec);
        Assert.Contains("枚举", codec!.Name);
    }

    /// <summary>
    /// 通配不该吃掉精确项：<c>List&lt;string&gt;</c> 的元素不是 Def，
    /// 所以它必须命中精确项、而不是落到 <c>List&lt;T&gt; where T : Def</c>。
    /// </summary>
    [Fact]
    public void 通配不吃掉精确项()
    {
        Assert.Contains("List<string>", TweakData.FieldCodecs.Find(typeof(List<string>))!.Name);
        Assert.Contains("List<int>", TweakData.FieldCodecs.Find(typeof(List<int>))!.Name);
    }

    [Fact]
    public void 确实未登记的类型返回null()
        => Assert.Null(TweakData.FieldCodecs.Find(typeof(Guid)));

    // ── Resolve 的三态语义（§7.6 那条坑） ──

    [Theory]
    [InlineData(typeof(float?))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(bool?))]
    [InlineData(typeof(FloatRange?))]
    [InlineData(typeof(IntRange?))]
    [InlineData(typeof(string))]
    [InlineData(typeof(SimpleCurve))]
    public void 标量已登记但不参与解析(Type scalar)
    {
        var codec = TweakData.FieldCodecs.Find(scalar);
        Assert.NotNull(codec);        // ① 已登记 —— 与"漏登记"不是一回事
        Assert.Null(codec!.Resolve);  // ② 但不参与 resolve（Scribe 已直接落值）
    }

    [Fact]
    public void 可空枚举同样不参与解析()
        => Assert.Null(TweakData.FieldCodecs.Find(typeof(TechLevel?))!.Resolve);

    [Theory]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(List<StatModifier>))]
    [InlineData(typeof(ThingFilter))]
    [InlineData(typeof(List<StockGenerator>))]
    public void 结构化类型参与解析(Type structured)
        => Assert.NotNull(TweakData.FieldCodecs.Find(structured)!.Resolve);

    [Fact]
    public void Def子类参与解析()
        => Assert.NotNull(TweakData.FieldCodecs.Find(typeof(ThingDef))!.Resolve);

    // ── 登记表自洽 ──

    [Fact]
    public void 登记表已构建且字段完整()
    {
        var all = TweakData.FieldCodecs.All;
        Assert.True(all.Count >= 32, $"登记表条目过少：{all.Count}");
        Assert.All(all, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(all, c => Assert.NotNull(c.Expose));
        Assert.All(all, c => Assert.NotNull(c.Matches));
    }

    [Theory]
    [InlineData(typeof(List<HediffGiver>))]
    [InlineData(typeof(List<MentalStateGiver>))]
    public void 由子类自理的字段已登记且标明来源(Type t)
    {
        var codec = TweakData.FieldCodecs.Find(t);
        Assert.NotNull(codec);
        Assert.Contains("自理", codec!.Name);
        Assert.Null(codec.Resolve); // 基类不参与解析（子类自己的 ResolveDefs 负责）
    }

    /// <summary>每个登记项的 <c>Matches</c> 对自己声明的类型必须成立 —— 防止谓词写反。</summary>
    [Fact]
    public void 每个登记项的谓词自洽()
    {
        var probes = new[]
        {
            typeof(float?), typeof(string), typeof(ThingDef), typeof(TechLevel?),
            typeof(List<ThingDef>), typeof(List<string>), typeof(ThingFilter),
        };
        foreach (var codec in TweakData.FieldCodecs.All)
        {
            foreach (var probe in probes)
            {
                bool matched = codec.Matches(probe);
                bool found = ReferenceEquals(TweakData.FieldCodecs.Find(probe), codec);
                if (found) Assert.True(matched, $"{codec.Name} 被 Find 命中但谓词不认 {probe.Name}");
            }
        }
    }
}
