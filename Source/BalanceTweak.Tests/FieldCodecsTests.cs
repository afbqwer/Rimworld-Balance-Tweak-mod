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
        Assert.NotEmpty(all);
        Assert.All(all, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(all, c => Assert.NotNull(c.Expose));
        Assert.All(all, c => Assert.NotNull(c.Matches));
    }

    /// <summary>
    /// 同一类型不得被登记两次 —— <c>Exact</c> 是 <c>dict[type] = …</c>，后者会<b>静默覆盖</b>前者，
    /// 于是前一条登记白写（该字段既不落盘也不还原），且运行期毫无信号。
    ///
    /// <para>
    /// 这里原先是一条 <c>all.Count &gt;= 32</c> 的弱下界（登记表实际 34 条，掉 1~2 条抓不到）。
    /// 真正的"漏登记"已由 <c>字段表覆盖原版全部类型与字段</c>（遍历真实程序集）守着，所以把这条换成
    /// <b>重复登记</b>守卫更有价值 —— 它补的正是那次登记表审查指出的防御缺口。
    /// </para>
    /// </summary>
    [Fact]
    public void 登记表无重复条目()
        => Assert.Empty(TweakData.FieldCodecs.DuplicateRegistrations);

    /// <summary>
    /// 登记的枚举字段必须<b>不是</b> <c>[Flags]</c>。
    ///
    /// <para>
    /// <see cref="PocoCodec"/> 解 <c>Kind.Enum</c> 时用 <c>Enum.IsDefined</c> 拦未定义值，而 <c>[Flags]</c> 的
    /// <b>多标志组合值不在定义集合里</b> ⇒ 编码端 <c>ToString()</c> 照写 <c>"A, B"</c>、解码端必拒 ⇒ "能写、不能读"。
    /// 今天登记的枚举（<c>TechLevel</c> / <c>PriceType</c> 等）都不是 <c>[Flags]</c>，所以这条不会红；
    /// 一旦有人加了这类字段，它会立刻红并提示先改 <see cref="PocoCodec"/> 的 <c>Kind.Enum</c> 判据 ——
    /// 而不是等到玩家读档时才发现字段被拒。
    /// </para>
    /// </summary>
    [Fact]
    public void 枚举字段不得是Flags()
    {
        // 必须先拉起两条声明链，否则 DeclaredTypes 为空 ⇒ 扫描不到任何字段 ⇒ 断言恒真绿。
        TweakData.FieldCodecs.EnsureBuilt();
        Assert.NotEmpty(PocoCodec.DeclaredTypes);

        var offenders = new List<string>();
        var scanned = 0;
        foreach (var type in PocoCodec.DeclaredTypes)
        {
            foreach (var f in PocoCodec.FieldsFor(type))
            {
                if (f.Kind != PocoCodec.Kind.Enum || f.EnumType == null) continue;
                scanned++;
                if (f.EnumType.IsDefined(typeof(FlagsAttribute), inherit: false))
                    offenders.Add($"{type.Name}.{f.Name}（{f.EnumType.Name}）");
            }
        }

        // 扫到 0 条枚举字段 = 扫描面失真（声明链没拉起 / 字段都改了）—— 那样本测试会静默恒真绿。
        Assert.True(scanned > 0, "一条枚举字段都没扫到 —— 扫描面失真，这条守卫会静默恒真");
        Assert.True(offenders.Count == 0,
            "以下枚举字段带 [Flags]，PocoCodec 的 Kind.Enum 解码会拒收多标志组合值（能写不能读）："
            + "要么别用 [Flags]，要么先改 PocoCodec 的 Kind.Enum 判据：\n" + string.Join("\n", offenders));
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
