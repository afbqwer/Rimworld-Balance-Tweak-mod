using System;
using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// ④ 第二步（17 个类型从手写 <c>split('|')</c> 迁到 <see cref="PocoCodec"/> 字段表）的验证。
///
/// <para>
/// <b>能在游戏外测什么</b>：字段声明是否解析得到、编码出的文本形状（字段名 / 顺序 / 默认值省略）、
/// <b>非 Def 字段的完整往返</b>、业务校验（<c>count &gt;= 0</c>）、以及列表包装的失败语义。
/// </para>
/// <para>
/// <b>测不了什么</b>：<c>Def</c> 字段的解码（要 <c>DefDatabase</c> 把 defName 查回实例）。
/// 那部分由游戏内覆盖 —— 见 <c>TweakDataSerializationTests</c> 的序列化往返测试。
/// </para>
/// </summary>
public class SerializationCodecsTests
{
    /// <summary>迁移到字段表的 17 个类型。</summary>
    private static readonly Type[] MigratedTypes =
    {
        typeof(StatModifier), typeof(PawnCapacityModifier), typeof(DamageFactor), typeof(SkillGain),
        typeof(Aptitude), typeof(GeneticTraitData), typeof(ThingDefCountClass),
        typeof(SkillRequirement), typeof(TraitRequirement),
        typeof(BiomePlantRecord), typeof(BiomeAnimalRecord), typeof(FishChance),
        typeof(BiomeDiseaseRecord), typeof(WeatherCommonalityRecord), typeof(TerrainThreshold),
        typeof(ProcessIngredientItem), typeof(ProcessResultItem),
    };

    /// <summary>
    /// 确保字段表已初始化。字段声明与登记项都在 <c>TweakData.FieldCodecs.Build()</c>，
    /// 由那条链上的静态构造统一完成 —— 这里借一次 <see cref="SerializationHelper"/> 调用把它拉起来
    /// （GUI 调 <c>SerializeXxxList</c> 走的也是这条路）。
    /// </summary>
    private static void EnsureDeclared() => _ = SerializationHelper.SerializeStatModifierList(null);

    /// <summary>逐条编码 —— 与登记表里 <c>Expose</c> 调的是同一个方法。</summary>
    private static string Item<T>(T value) where T : class => PocoCodec.Encode(value);

    /// <summary>逐条解码 —— 与登记表里 <c>Resolve</c> 调的是同一个方法。失败返回 <c>null</c>。</summary>
    private static T? Parse<T>(string text) where T : class
        => PocoCodec.TryDecode(text, typeof(T), out var value) && value is T item ? item : null;

    [Fact]
    public void 迁移类型都已声明()
    {
        EnsureDeclared();
        foreach (var type in MigratedTypes)
            Assert.True(PocoCodec.IsDeclared(type), $"{type.Name} 没有在字段表里声明");
    }

    /// <summary>声明的字段名都解析得到（拼写正确、原版没改名）。这一条同时兜住 StockGenerator 与这里两处声明。</summary>
    [Fact]
    public void 声明字段全部可解析()
    {
        EnsureDeclared();
        Assert.Empty(PocoCodec.UnresolvedFields);
    }

    /// <summary>
    /// 逐个钉住 17 个类型的字段声明。
    ///
    /// <para>
    /// 这既是"迁移前后字段覆盖一致"的核对记录（旧手写格式的段数与这些名字一一对应），
    /// 也防止后来者改动声明而无人察觉 —— 多写一个字段会让存档文本变长、少写会**静默丢字段**、
    /// 顺序错了则老存档错位。
    /// </para>
    /// <para>
    /// 这里**刻意不含**原版有、但历来没被本工具读写的字段（<c>ThingDefCountClass.color/chance/quality</c>、
    /// <c>BiomeDiseaseRecord.biome</c>、<c>PawnCapacityModifier.setMaxCurveOverride</c>）——
    /// 本次是机制迁移，不是补字段覆盖。
    /// </para>
    /// </summary>
    [Fact]
    public void 字段声明逐个钉住()
    {
        EnsureDeclared();

        var expected = new (Type Type, string[] Fields)[]
        {
            (typeof(StatModifier), new[] { "stat", "value" }),
            (typeof(PawnCapacityModifier),
                new[] { "capacity", "offset", "setMax", "postFactor", "statFactorMod", "setMaxCurveEvaluateStat" }),
            (typeof(DamageFactor), new[] { "damageDef", "factor" }),
            (typeof(SkillGain), new[] { "skill", "amount" }),
            (typeof(Aptitude), new[] { "skill", "level" }),
            (typeof(GeneticTraitData), new[] { "def", "degree" }),
            (typeof(ThingDefCountClass), new[] { "thingDef", "stuff", "count" }),
            (typeof(SkillRequirement), new[] { "skill", "minLevel" }),
            (typeof(TraitRequirement), new[] { "def", "degree" }),
            (typeof(BiomePlantRecord), new[] { "plant", "commonality" }),
            (typeof(BiomeAnimalRecord), new[] { "animal", "commonality" }),
            (typeof(FishChance), new[] { "fishDef", "chance" }),
            (typeof(BiomeDiseaseRecord), new[] { "diseaseInc", "commonality" }),
            (typeof(WeatherCommonalityRecord), new[] { "weather", "commonality" }),
            (typeof(TerrainThreshold), new[] { "terrain", "min", "max" }),
            (typeof(ProcessIngredientItem), new[] { "thing", "thingCategory", "disallowedThingDefs", "countNeeded" }),
            (typeof(ProcessResultItem), new[] { "thing", "count" }),
        };

        Assert.Equal(MigratedTypes.Length, expected.Length);
        foreach (var (type, fields) in expected)
            Assert.Equal(fields, PocoCodec.FieldsFor(type).Select(f => f.Name).ToArray());
    }

    // ═══════════════════════ 登记覆盖与包装一致 ═══════════════════════

    /// <summary>
    /// 登记表把 17 个类型都登记成了 <c>List&lt;T&gt;</c> 的收发对，且**列表包装与逐条编码一致**。
    ///
    /// <para>
    /// 前半条守"漏登记"（该字段会静默不落盘）；后半条守"包装层与编解码层分叉"——
    /// 游戏内的往返测试用 <c>PocoCodec.Encode</c> 模拟存档内容，若 <c>SerializeXxxList</c>
    /// 的包装产出与之不同，那些测试就是在编一份存档里根本不会出现的文本。
    /// </para>
    ///
    /// <para>
    /// 注：逐条编解码现在由登记项直接调 <c>PocoCodec</c>（<c>PocoList&lt;T&gt;</c> 一行登记），
    /// 所以"逐条路径一致"由类型系统保证，不需要再断言；这里只测**包装层**。
    /// </para>
    /// </summary>
    [Fact]
    public void 登记表覆盖且列表包装一致()
    {
        EnsureDeclared();

        foreach (var type in MigratedTypes)
        {
            var codec = TweakData.FieldCodecs.Find(typeof(List<>).MakeGenericType(type));
            Assert.True(codec != null, $"List<{type.Name}> 没有登记（该字段会静默不落盘）");
            Assert.True(codec!.Resolve != null, $"List<{type.Name}> 登记了却没有解析分支（读档后不会还原）");
        }

        var list = new List<StatModifier> { new() { value = 1f } };
        Assert.Equal(string.Join("\n", list.Select(PocoCodec.Encode)),
            SerializationHelper.SerializeStatModifierList(list));
    }

    /// <summary>合成 Def 只用来验证编码方向（解码要 DefDatabase 查回实例，测试进程做不到）。</summary>
    private static T Def<T>(string defName) where T : Def, new() => new() { defName = defName };

    // ═══════════════════════ 编码文本 ═══════════════════════

    [Fact]
    public void 编码文本_字段名与顺序()
    {
        EnsureDeclared();

        var sm = new StatModifier { stat = Def<StatDef>("Foo"), value = 1.5f };
        Assert.Equal("stat=Foo|value=1.5", Item(sm));

        // 默认值省略：value 为 0（= 新实例初值）时不写
        Assert.Equal("stat=Foo", Item(new StatModifier { stat = Def<StatDef>("Foo") }));

        var df = new DamageFactor { damageDef = Def<DamageDef>("Blunt"), factor = 0.25f };
        Assert.Equal("damageDef=Blunt|factor=0.25", Item(df));

        var sg = new SkillGain { skill = Def<SkillDef>("Shooting"), amount = 3 };
        Assert.Equal("skill=Shooting|amount=3", Item(sg));

        var tt = new TerrainThreshold { terrain = Def<TerrainDef>("Soil"), min = 1.5f, max = 9f };
        Assert.Equal("terrain=Soil|min=1.5|max=9", Item(tt));
    }

    /// <summary>旧格式是 <c>a|b|c</c>（位置敏感），新格式自带字段名 —— 顺带钉住"值带转义"这一点。</summary>
    [Fact]
    public void 编码文本_自描述且带转义()
    {
        EnsureDeclared();

        // 字段顺序来自声明，不依赖反射顺序。
        // 数值用 G9 保精度（与 FloatRange / SimpleCurve 同一口径）—— 所以这里挑没有二进制精度噪音的值。
        var pcm = new PawnCapacityModifier
        {
            capacity = Def<PawnCapacityDef>("Moving"),
            offset = 0.25f,
            setMax = 0.75f,
            postFactor = 1.5f,
            statFactorMod = Def<StatDef>("SomeStat"),
        };
        var line = Item(pcm);
        Assert.StartsWith("capacity=Moving|offset=0.25|setMax=0.75|postFactor=1.5", line);
        Assert.Contains("statFactorMod=SomeStat", line);
        Assert.DoesNotContain("setMaxCurveEvaluateStat", line); // 默认 null，省略
    }

    // ═══════════════════════ 非 Def 字段往返 ═══════════════════════

    /// <summary>
    /// <b>Def 字段留空</b>（= 没有引用，编码时被省略、解码时保持 null），其余字段完整往返。
    /// 这一组能在游戏外跑通，是因为它绕开了 <c>DefDatabase</c>。
    /// </summary>
    [Fact]
    public void 非Def字段往返()
    {
        EnsureDeclared();

        var sm = RoundTrip(new StatModifier { value = 1.5f }, SerializationHelper.SerializeStatModifierList,
            SerializationHelper.DeserializeStatModifierList);
        Assert.Equal(1.5f, sm!.value);

        var sg = RoundTrip(new SkillGain { amount = 7 }, SerializationHelper.SerializeSkillGainList,
            SerializationHelper.DeserializeSkillGainList);
        Assert.Equal(7, sg!.amount);

        var ap = RoundTrip(new Aptitude { level = 4 }, SerializationHelper.SerializeAptitudeList,
            SerializationHelper.DeserializeAptitudeList);
        Assert.Equal(4, ap!.level);

        // int? 字段（TraitRequirement.degree）
        var tr = RoundTrip(new TraitRequirement { degree = 2 }, SerializationHelper.SerializeTraitRequirementList,
            SerializationHelper.DeserializeTraitRequirementList);
        Assert.Equal(2, tr!.degree);

        var gtd = RoundTrip(new GeneticTraitData { degree = 1 },
            SerializationHelper.SerializeGeneticTraitDataList, SerializationHelper.DeserializeGeneticTraitDataList);
        Assert.Equal(1, gtd!.degree);

        var plant = RoundTrip(new BiomePlantRecord { commonality = 0.75f },
            SerializationHelper.SerializeBiomePlantList, SerializationHelper.DeserializeBiomePlantList);
        Assert.Equal(0.75f, plant!.commonality);

        var thr = RoundTrip(new TerrainThreshold { min = 0.1f, max = 0.9f },
            SerializationHelper.SerializeTerrainThresholdList, SerializationHelper.DeserializeTerrainThresholdList);
        Assert.Equal(0.1f, thr!.min);
        Assert.Equal(0.9f, thr.max);

        var pr = RoundTrip(new ProcessResultItem { count = 5 },
            SerializationHelper.SerializeProcessResultItemList, SerializationHelper.DeserializeProcessResultItemList);
        Assert.Equal(5, pr!.count);
    }

    private static T? RoundTrip<T>(
        T item,
        Func<List<T>?, string?> serialize,
        Func<string?, List<T>?> deserialize) where T : class
    {
        var text = serialize(new List<T> { item });
        Assert.False(text.NullOrEmpty(), $"{typeof(T).Name} 应该编码出非空文本");
        var back = deserialize(text);
        Assert.True(back is { Count: 1 }, $"{typeof(T).Name} 应还原成 1 个元素，实际 {back?.Count}");
        return back![0];
    }

    // ═══════════════════════ 业务校验（Validate 钩子）═══════════════════════

    /// <summary>
    /// 字段表只能表达"这个值合不合法"，表达不了 <c>count &gt;= 0</c> 这类<b>取值范围</b>约束。
    /// 没有 <c>Validator</c> 钩子就会开始接受以前拒绝的值。
    /// </summary>
    [Fact]
    public void 业务校验_负数count被拒()
    {
        EnsureDeclared();

        Assert.Null(Parse<ThingDefCountClass>("count=-1"));
        Assert.Null(Parse<ThingDefCountClass>("count=-1|thingDef="));
        Assert.NotNull(Parse<ThingDefCountClass>("count=0"));

        Assert.Null(Parse<ProcessResultItem>("count=-3"));
        Assert.NotNull(Parse<ProcessResultItem>("count=0"));
    }

    /// <summary>未知字段名 / 值类型不对 ⇒ 整条丢弃（旧手写格式同样如此，且更严格）。</summary>
    [Fact]
    public void 坏输入被拒()
    {
        EnsureDeclared();

        Assert.Null(Parse<StatModifier>("noSuchField=1"));
        Assert.Null(Parse<StatModifier>("value=abc"));
        Assert.Null(Parse<SkillGain>("amount=1.5"));  // amount 是 int
        Assert.Null(SerializationHelper.DeserializeStatModifierList("value=abc\nvalue=1"));
    }

    // ═══════════════════════ 列表包装的失败语义 ═══════════════════════

    [Fact]
    public void 列表包装_空与null()
    {
        EnsureDeclared();

        Assert.Null(SerializationHelper.SerializeStatModifierList(null));
        Assert.Null(SerializationHelper.SerializeStatModifierList(new List<StatModifier>()));
        Assert.Null(SerializationHelper.DeserializeStatModifierList(null));
        Assert.Null(SerializationHelper.DeserializeStatModifierList(""));
        Assert.Null(SerializationHelper.DeserializeStatModifierList("   \n  "));
    }

    /// <summary>
    /// 元素"字段全默认"时 <c>Encode</c> 给出空串 —— 写进文本就成了空行、解码会被跳过，等于<b>静默少一条</b>。
    /// 所以整块返回 <c>null</c>（与旧格式"该元素解析不出 ⇒ 整块拒绝"一致）。
    /// </summary>
    [Fact]
    public void 列表包装_全默认元素整块失败()
    {
        EnsureDeclared();

        Assert.Null(SerializationHelper.SerializeStatModifierList(new List<StatModifier> { new() }));
    }

    /// <summary>多元素：一行一个，顺序保持。</summary>
    [Fact]
    public void 列表包装_多元素顺序保持()
    {
        EnsureDeclared();

        var list = new List<StatModifier>
        {
            new() { value = 1f },
            new() { value = 2f },
            new() { value = 3f },
        };
        var text = SerializationHelper.SerializeStatModifierList(list);
        Assert.Equal("value=1\nvalue=2\nvalue=3", text);
        var back = SerializationHelper.DeserializeStatModifierList(text);
        Assert.Equal(new[] { 1f, 2f, 3f }, back!.Select(x => x.value));
    }
}
