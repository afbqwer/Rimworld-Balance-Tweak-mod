using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="StockGeneratorCodec"/> / <see cref="PocoCodec"/> 的<b>机制</b>测试。
///
/// <para>
/// <b>在游戏外跑</b>：不启动游戏、不查 <c>DefDatabase</c>。测试进程里 <c>DefDatabase</c> 是空的、
/// <c>ThingDefOf.Steel</c> 是 <c>null</c> —— 所以这一组只覆盖**与 Def 无关**的机制：
/// 转义、文本格式、克隆、区间文本校验、字段表、非原版透传、空值约定。
/// </para>
///
/// <para>
/// <b>Def 相关的那几条留在游戏内</b>（<c>TweakDataSerializationTests</c>：真实商人整表往返、
/// Def 引用的字段级往返）—— 它们的价值恰恰在于"真数据"，用合成 Def 替代就变成自说自话。
/// </para>
///
/// <para>
/// <b>搬测试时的坑</b>：<c>DefDatabase</c> 在测试进程里不抛异常、只返回空。
/// 所以任何依赖 Def 的断言在这里都会<b>静默假绿</b>（<c>x == ThingDefOf.Steel</c> 退化成
/// <c>null == null</c>）。本文件里凡是涉及 Def 的地方都**显式跳过**，不指望它报错。
/// </para>
/// </summary>
public class StockGeneratorCodecTests
{
    // ═══════════════════════ 字段表 ═══════════════════════

    /// <summary>
    /// 表里声明的字段都解析得到（原版没改名 / 没移除）。
    /// <para>
    /// <b>必须先跑一遍 <c>FieldsFor</c></b>：<c>UnresolvedFields</c> 是在解析字段时才填的，
    /// 不触发解析的话它恒为空，这条断言就成了恒真。（2026-10-16 审查发现的假绿。）
    /// </para>
    /// </summary>
    [Fact]
    public void 字段表无未解析项()
    {
        foreach (var type in StockGeneratorCodec.VanillaGeneratorTypes())
            StockGeneratorCodec.FieldsFor(type);
        Assert.Empty(StockGeneratorCodec.UnresolvedFields);
    }

    /// <summary>
    /// 原版每个生成器类型与每个 XML 字段都在表里，且字段类型都被支持。
    /// 原版一旦加了新类型 / 新字段 / 新字段类型，这条会先炸 —— 而不是等用户存档时静默丢字段。
    /// </summary>
    [Fact]
    public void 字段表覆盖原版全部类型与字段()
    {
        var problems = new List<string>();
        foreach (var type in VanillaGeneratorSubclasses())
        {
            if (!StockGeneratorCodec.IsVanilla(type))
            {
                problems.Add($"{type.Name} 不在字段表里（原版加了新生成器类型？）");
                continue;
            }

            var declared = StockGeneratorCodec.FieldsFor(type).Select(f => f.Name).ToHashSet();
            foreach (var field in XmlInstanceFields(type))
                if (!declared.Contains(field.Name))
                    problems.Add($"{type.Name}.{field.Name}（{field.FieldType.Name}）没写进字段表");

            foreach (var field in StockGeneratorCodec.FieldsFor(type))
                if (field.Kind == PocoCodec.Kind.Unsupported)
                    problems.Add($"{type.Name}.{field.Name} 的字段类型不受支持");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} 处字段表与原版对不上（列出前 8 条）：\n{string.Join("\n", problems.Take(8))}");
    }

    /// <summary>原版类型菜单：只含表里的具体类型，不含抽象类与基类。</summary>
    [Fact]
    public void 原版类型菜单()
    {
        var vanilla = StockGeneratorCodec.VanillaGeneratorTypes();
        Assert.True(vanilla.Count > 1, "原版类型列表不应为空");
        Assert.All(vanilla, t => Assert.True(StockGeneratorCodec.IsVanilla(t), $"{t.Name} 应是原版类型"));
        Assert.All(vanilla, t => Assert.True(!t.IsAbstract && t != typeof(StockGenerator),
            $"{t.Name} 不该出现在类型菜单里（抽象类或基类）"));
        Assert.Contains(typeof(StockGenerator_Tag), vanilla);
        Assert.Contains(typeof(StockGenerator_MarketValue), vanilla);
        Assert.DoesNotContain(typeof(StockGenerator_MiscItems), vanilla);
    }

    // ═══════════════════════ 往返 ═══════════════════════

    /// <summary>
    /// 每个原版类型的**非 Def 字段**都填上非默认值，往返后必须逐字段相等。
    /// <para>Def / DefList 字段在这里显式跳过（测试进程没有 <c>DefDatabase</c>）—— 那部分由游戏内的
    /// <c>testStockGen_DefFieldsRoundTrip</c> 覆盖。</para>
    /// </summary>
    [Fact]
    public void 全部原版类型的非Def字段往返()
    {
        var problems = new List<string>();
        int coveredTypes = 0, coveredFields = 0, skippedDefFields = 0;

        foreach (var type in StockGeneratorCodec.VanillaGeneratorTypes())
        {
            var gen = (StockGenerator)Activator.CreateInstance(type)!;
            bool anyField = false;
            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                if (IsDefKind(field.Kind)) { skippedDefFields++; continue; }

                var sample = SampleValueOf(field, StockGeneratorCodec.ReadField(field, gen));
                Assert.True(sample != null, $"{type.Name}.{field.Name}（{field.Kind}）造不出样本值");
                StockGeneratorCodec.WriteField(field, gen, sample);
                anyField = true;
                coveredFields++;
            }
            if (!anyField) continue;
            coveredTypes++;

            string line = StockGeneratorCodec.ToItemString(gen);
            Assert.True(!line.Contains('\n'), $"{type.Name}：一行不得含换行");
            Assert.True(StockGeneratorCodec.TryParse(line, out var back) && back != null,
                $"{type.Name}：解析不了自己编码出来的行：{line}");
            if (back!.GetType() != type) { problems.Add($"{type.Name}: 往返后类型变成 {back.GetType().Name}"); continue; }
            if (StockGeneratorCodec.ToItemString(back) != line)
            {
                problems.Add($"{type.Name}: 再编码不一致\n{line}\n{StockGeneratorCodec.ToItemString(back)}");
                continue;
            }

            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                if (IsDefKind(field.Kind)) continue;
                var expected = StockGeneratorCodec.ReadField(field, gen);
                var actual = StockGeneratorCodec.ReadField(field, back);
                if (!SameValue(expected, actual))
                    problems.Add($"{type.Name}.{field.Name}: {DescribeValueOf(expected)} != {DescribeValueOf(actual)}");
            }
        }

        Assert.True(coveredTypes > 0, "没有覆盖到任何类型");
        Assert.True(problems.Count == 0,
            $"{problems.Count} 处字段没往返一致（列出前 6 条）：\n{string.Join("\n", problems.Take(6))}");
    }

    /// <summary>
    /// 字段值要真的来回一致（不只是"文本一致"）：区间、枚举、私有字段、嵌套小对象列表、字符串列表。
    ///
    /// <para>
    /// <b>这里不涉及 Def</b>。注意一个容易漏的坑：<c>customCountRanges</c> 的元素类型
    /// <c>ThingDefCountRangeClass</c> 自带一个 <c>thingDef</c>（<c>Kind.Def</c>）—— 它是<b>列表元素内部</b>
    /// 的 Def 字段，"跳过顶层 Def 字段"的写法拦不住它，解码时照样要去查 <c>DefDatabase</c>。
    /// 所以这里的元素只填 <c>countRange</c>，<c>thingDef</c> 留空（空 = 没有引用，解码不查库）。
    /// 带真 Def 的那份由游戏内覆盖。
    /// </para>
    /// </summary>
    [Fact]
    public void 标量与嵌套字段往返()
    {
        // StockGenerator_Tag：公开 + 私有字段 + 嵌套小对象列表
        var tag = new StockGenerator_Tag
        {
            tradeTag = "ExoticMisc",
            countRange = new IntRange(1, 4),
            totalPriceRange = new FloatRange(10f, 99.5f),
            maxTechLevelBuy = TechLevel.Medieval,
            price = PriceType.Expensive,
            customCountRanges = new List<ThingDefCountRangeClass>
            {
                RangeOnly(new IntRange(2, 6)),
                RangeOnly(new IntRange(7, 7)),
            },
        };
        SetGenField(tag, "thingDefCountRange", new IntRange(3, 5));

        var back = RoundTrip(tag) as StockGenerator_Tag;
        Assert.True(back != null, "往返后类型应仍是 StockGenerator_Tag");
        Assert.True(back!.tradeTag == "ExoticMisc", $"tradeTag 应保持，实际 {back.tradeTag}");
        Assert.True(back.countRange.Equals(new IntRange(1, 4)), "countRange 应保持");
        Assert.True(back.totalPriceRange.Equals(new FloatRange(10f, 99.5f)), "totalPriceRange 应保持");
        Assert.True(back.maxTechLevelBuy == TechLevel.Medieval, "maxTechLevelBuy（枚举）应保持");
        Assert.True(back.price == PriceType.Expensive, "price（枚举）应保持");
        Assert.True(back.customCountRanges is { Count: 2 }, "customCountRanges 应保持 2 项");
        Assert.True(back.customCountRanges![0].countRange.Equals(new IntRange(2, 6)), "嵌套元素 countRange 应保持");
        Assert.True(back.customCountRanges[1].countRange.Equals(new IntRange(7, 7)), "嵌套元素 [1] 应保持");
        Assert.True(back.customCountRanges[0].thingDef == null, "未引用 Def 的嵌套元素应还原成空引用");
        Assert.True(GetGenField(back, "thingDefCountRange") is IntRange r2 && r2.Equals(new IntRange(3, 5)),
            "私有字段 thingDefCountRange 也应保持");
        Assert.True(back.trader == null, "[Unsaved] 的 trader 不应被编码");

        // 字符串列表（StockGenerator_Animals 的 tradeTagsSell 是 private）
        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "AnimalCommon", "AnimalUncommon" });
        SetGenField(animals, "kindCountRange", new IntRange(1, 3));
        SetGenField(animals, "minWildness", 0.25f);
        SetGenField(animals, "checkTemperature", false);
        var animalsBack = RoundTrip(animals) as StockGenerator_Animals;
        Assert.True(animalsBack != null, "往返后类型应仍是 StockGenerator_Animals");
        var tagsBack = GetGenField(animalsBack!, "tradeTagsSell") as List<string>;
        Assert.True(tagsBack is { Count: 2 } && tagsBack[0] == "AnimalCommon" && tagsBack[1] == "AnimalUncommon",
            "字符串列表 tradeTagsSell 应保持");
        Assert.True(GetGenField(animalsBack!, "kindCountRange") is IntRange kcr && kcr.Equals(new IntRange(1, 3)),
            "私有字段 kindCountRange 应保持");
        Assert.True(GetGenField(animalsBack!, "minWildness") is 0.25f, "私有字段 minWildness 应保持");
        Assert.True(GetGenField(animalsBack!, "checkTemperature") is false, "私有字段 checkTemperature 应保持");

        // 小对象列表（StockGenerator_Techprints 的 countChances 是 private List<CountChance>）
        var techprints = new StockGenerator_Techprints();
        SetGenField(techprints, "countChances", new List<CountChance>
        {
            new() { count = 2, chance = 0.5f },
            new() { count = 3, chance = 1f },
        });
        var techprintsBack = RoundTrip(techprints) as StockGenerator_Techprints;
        var chances = techprintsBack == null ? null : GetGenField(techprintsBack, "countChances") as List<CountChance>;
        Assert.True(chances is { Count: 2 } && chances[0].count == 2 && chances[0].chance == 0.5f && chances[1].count == 3,
            "小对象列表 countChances 应保持");
    }

    /// <summary>字段值里出现分隔符（<c>| = [ ] ; \</c>）时也必须原样往返 —— 转义是这套格式唯一的"复杂"之处。</summary>
    [Fact]
    public void 转义往返()
    {
        string nasty = "a|b=c;[d]\\e;f~g,h=i";
        var tag = new StockGenerator_Tag { tradeTag = nasty };
        var back = RoundTrip(tag) as StockGenerator_Tag;
        Assert.True(back != null && back.tradeTag == nasty,
            $"含分隔符的字符串应原样往返，实际 {(back == null ? "<null>" : back.tradeTag)}");

        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "a|b", "c;d", "[e]", "f\\g", "" });
        var animalsBack = RoundTrip(animals) as StockGenerator_Animals;
        var tags = animalsBack == null ? null : GetGenField(animalsBack, "tradeTagsSell") as List<string>;
        Assert.True(tags is { Count: 4 }, $"空字符串元素跳过（其余 4 个保持），实际 {tags?.Count}");
        Assert.True(tags!.Contains("a|b") && tags.Contains("c;d") && tags.Contains("[e]") && tags.Contains("f\\g"),
            "含分隔符的列表元素应原样往返");
    }

    [Fact]
    public void 转义反转义互逆()
    {
        foreach (var s in new[] { "", "a", "a|b=c;[d]\\e;f~g,h=i", "\\", "[[;;]]", "==" })
            Assert.True(StockGeneratorCodec.Unescape(StockGeneratorCodec.Escape(s)) == s, $"Escape/Unescape 不互逆：\"{s}\"");
    }

    // ═══════════════════════ 文本格式 ═══════════════════════

    /// <summary>
    /// 钉住文本格式：值里的 <c>[ ] ; = | \</c> 都要转义，默认值省略，列表两端必须成对出现。
    /// <para>
    /// （曾经的 bug：<c>new StringBuilder(ListStart)</c> 命中"容量"构造函数，开头的 <c>[</c> 被静默丢掉，
    /// 于是所有带列表字段的生成器都解析不回来 —— 这条用例就是为了让这种回归一眼可查。）
    /// </para>
    /// <para>
    /// Def 列只验证**编码文本**：解码需要 <c>DefDatabase</c> 把 defName 查回 Def 实例，
    /// 测试进程里做不到（那部分归游戏内）。
    /// </para>
    /// </summary>
    [Fact]
    public void 文本格式_转义与省略默认值()
    {
        var tag = new StockGenerator_Tag { tradeTag = "Foo" };
        Assert.True(StockGeneratorCodec.ToItemString(tag) == "StockGenerator_Tag|tradeTag=Foo",
            $"默认值应省略、只留类型与非默认字段，实际：{StockGeneratorCodec.ToItemString(tag)}");

        SetGenField(tag, "excludedThingDefs", new List<ThingDef> { SteelLike("Steel"), SteelLike("Cloth") });
        string line = StockGeneratorCodec.ToItemString(tag);
        Assert.Contains(@"excludedThingDefs=\[Steel\;Cloth\]", line);

        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "first", "second|x" });
        string animalLine = StockGeneratorCodec.ToItemString(animals);
        Assert.Contains(@"tradeTagsSell=\[first\;second\\\|x\]", animalLine);

        // 编码出来的行必须能解析回来（格式自洽）—— 字符串列表这条不碰 DefDatabase。
        Assert.True(StockGeneratorCodec.TryParse(animalLine, out var back) && back != null,
            $"格式自洽性（字符串列表）：{animalLine}");
    }

    // ═══════════════════════ 克隆 ═══════════════════════

    /// <summary>克隆：改副本不得影响原对象；非原版（字段表外）子类直接沿用原实例。</summary>
    [Fact]
    public void 克隆是深拷贝()
    {
        var tag = new StockGenerator_Tag { tradeTag = "Foo" };
        tag.customCountRanges = new List<ThingDefCountRangeClass> { new(SteelLike("Steel"), new IntRange(1, 2)) };

        var clone = StockGeneratorCodec.Clone(tag) as StockGenerator_Tag;
        Assert.True(clone != null && !ReferenceEquals(clone, tag), "克隆应是新实例");
        Assert.True(!ReferenceEquals(clone!.customCountRanges, tag.customCountRanges), "嵌套列表应是新实例");
        Assert.True(clone.customCountRanges![0].thingDef == tag.customCountRanges[0].thingDef, "嵌套元素内容应保持");
        Assert.True(!ReferenceEquals(clone.customCountRanges[0], tag.customCountRanges[0]), "嵌套元素也应是新实例");

        clone.tradeTag = "Changed";
        clone.customCountRanges[0].countRange = new IntRange(9, 9);
        Assert.True(tag.tradeTag == "Foo", "改副本不应影响原对象（标量）");
        Assert.True(tag.customCountRanges[0].countRange.Equals(new IntRange(1, 2)), "改副本不应影响原对象（嵌套元素）");

        // 列表字段（CountChance 是值类型，装箱即复制）也应独立
        var techprints = new StockGenerator_Techprints();
        SetGenField(techprints, "countChances", new List<CountChance> { new() { count = 1, chance = 1f } });
        var techprintsClone = StockGeneratorCodec.Clone(techprints) as StockGenerator_Techprints;
        var cloneChances = techprintsClone == null ? null : GetGenField(techprintsClone, "countChances") as List<CountChance>;
        Assert.True(cloneChances is { Count: 1 } && cloneChances[0].count == 1, "小对象列表应被克隆");
        Assert.True(!ReferenceEquals(cloneChances, GetGenField(techprints, "countChances")), "克隆的列表应是新实例");

        // 字符串列表也必须跟着克隆（少复制一个字段 = 静默丢数据，克隆宁可失败也不能交出半份副本）
        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "AnimalFarm", "AnimalPet" });
        SetGenField(animals, "tradeTagsBuy", new List<string> { "AnimalExotic" });
        SetGenField(animals, "createMatingPair", new List<string> { "AnimalFarm" });
        var animalsClone = StockGeneratorCodec.Clone(animals) as StockGenerator_Animals;
        Assert.True(animalsClone != null && !ReferenceEquals(animalsClone, animals), "有字符串列表的生成器也应克隆成新实例");
        foreach (var name in new[] { "tradeTagsSell", "tradeTagsBuy", "createMatingPair" })
        {
            var from = GetGenField(animals, name) as List<string>;
            var to = GetGenField(animalsClone!, name) as List<string>;
            Assert.True(to != null && !ReferenceEquals(from, to) && to.SequenceEqual(from!),
                $"字符串列表 {name} 应被克隆成内容相同的新实例");
        }
        var cloneTags = GetGenField(animalsClone!, "tradeTagsSell") as List<string>;
        Assert.True(cloneTags is { Count: 2 }, "克隆的 tradeTagsSell 应是 2 项的列表");
        cloneTags![0] = "OnlyInClone";
        Assert.True((GetGenField(animals, "tradeTagsSell") as List<string>)![0] == "AnimalFarm",
            "改克隆的字符串列表不应影响原对象");

        // 字段表外的类型不克隆：它自己的字段复制不了，克隆只会丢数据，所以直接沿用原实例
        var mod = new FakeModGenerator { extraModField = "mod" };
        Assert.True(ReferenceEquals(StockGeneratorCodec.Clone(mod), mod), "非原版子类应直接沿用原实例（不克隆）");
    }

    // ═══════════════════════ 空值 / 坏输入 ═══════════════════════

    [Fact]
    public void 空与null的约定()
    {
        Assert.True(StockGeneratorCodec.Serialize(null) == null, "null 列表应编码为 null");
        Assert.True(StockGeneratorCodec.Serialize(new List<StockGenerator>()) == null, "空列表应编码为 null");
        Assert.True(StockGeneratorCodec.Deserialize(null) == null, "null 输入应还原为 null");
        Assert.True(StockGeneratorCodec.Deserialize(new List<string>()) == null, "空输入应还原为 null");
        Assert.True(StockGeneratorCodec.CloneAll(null) == null, "克隆 null 应得到 null");
        Assert.True(!StockGeneratorCodec.TryParse(null, out _), "null 行应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("", out _), "空行应解析失败");
    }

    /// <summary>
    /// 坏输入一律拒绝。注意"缺 Def"那两条在这里也成立 ——
    /// 测试进程里 <c>DefDatabase</c> 为空，任何 defName 都查不到，正好等于"该 Def 不存在"。
    /// </summary>
    [Fact]
    public void 坏输入一律拒绝()
    {
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_ThisTypeDoesNotExist|a=b", out _), "未知类型应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_Tag|noSuchField=x", out _), "未知字段应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_SingleDef|thingDef=__BT_Not_A_Real_Def__", out _),
            "缺 Def 应解析失败（由调用方保持 Def 现状）");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_Tag|countRange=abc", out _), "区间值非法应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_Tag|tradeTag", out _), "缺 \"=\" 的字段段应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator|tradeTag=x", out _), "抽象基类名应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_MiscItems|countRange=1~2", out _), "抽象类型应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("CountChance|count=1", out _), "字段表里的小对象类型不是生成器，应解析失败");
        Assert.True(!StockGeneratorCodec.TryParse("StockGenerator_Tag|excludedThingDefs=[__BT_Not_A_Real_Def__]", out _),
            "Def 列表里缺 Def 应解析失败");

        var good = StockGeneratorCodec.ToItemString(new StockGenerator_Tag { tradeTag = "Foo" });
        Assert.True(StockGeneratorCodec.Deserialize(new List<string> { good, "StockGenerator_Tag|noSuchField=x" }) == null,
            "整块里有一条坏行 ⇒ 整块拒绝");
    }

    /// <summary>
    /// 表单文本框里的区间文本：<b>"输到一半"必须被拒</b>。
    /// 判据不能靠 <c>Verse.*.FromString</c> 抛不抛异常 —— <c>IntRange.FromString("1~")</c> 完全不抛，
    /// 它把缺省的上界当 <c>int.MaxValue</c>，于是"在 "~" 之后离开输入框"会把 <c>countRange</c> 写成
    /// 1~2147483647，而库存数量直接取自 <c>countRange.RandomInRange</c>（生成时按这个数量造物品）。
    /// </summary>
    [Fact]
    public void 区间文本_半成品必须被拒()
    {
        foreach (var bad in new[] { "1~", "~4", "1~2~3", "abc", "", " ", "1.5", "1~abc" })
            Assert.True(!StockGeneratorCodec.TryParseRangeText(isIntRange: true, bad, out _),
                $"整数区间不该接受 \"{bad}\"");

        // 单值 = 两端同值：IntRange.ToString() 在 min==max 时只显示一个数，必须能编辑回去
        Assert.True(StockGeneratorCodec.TryParseRangeText(true, "5", out var single)
            && single is IntRange s && s.min == 5 && s.max == 5, "整数区间应接受单值（= min~max）");
        Assert.True(StockGeneratorCodec.TryParseRangeText(true, "1~4", out var pair)
            && pair is IntRange p && p.min == 1 && p.max == 4, "整数区间应接受 min~max");

        // 浮点：显示用 G9，可能带指数写法，必须能读回来
        Assert.True(StockGeneratorCodec.TryParseRangeText(false, "1.5~2.5", out var fr)
            && fr is FloatRange f && f.min == 1.5f && f.max == 2.5f, "浮点区间应接受 min~max");
        Assert.True(StockGeneratorCodec.TryParseRangeText(false, "1E-05~2", out var exp)
            && exp is FloatRange e && e.min == 1E-05f, "浮点区间应接受 G9 可能产出的指数写法");
        Assert.True(!StockGeneratorCodec.TryParseRangeText(false, "1~", out _), "浮点区间不该接受 1~");

        // 千位分隔符不算合法数字（NumberStyles.Float 不含 AllowThousands）：
        // 否则逗号小数点环境下 "1.5" 会被裸 float.TryParse 读成 15
        Assert.True(!StockGeneratorCodec.TryParseRangeText(false, "1,5~2", out _), "浮点区间不该接受带千位分隔符的写法");
    }

    // ═══════════════════════ 字段表分类与摘要 ═══════════════════════

    [Fact]
    public void 字段表分类与摘要()
    {
        var fields = StockGeneratorCodec.FieldsFor(typeof(StockGenerator_Tag));
        Assert.True(fields.Any(f => f.Name == "tradeTag" && f.Kind == PocoCodec.Kind.Text), "tradeTag 应是文本字段");
        Assert.True(fields.Any(f => f.Name == "countRange" && f.Kind == PocoCodec.Kind.IntRange), "countRange 应是整数区间字段");
        Assert.True(fields.Any(f => f.Name == "totalPriceRange" && f.Kind == PocoCodec.Kind.FloatRange), "totalPriceRange 应是浮点区间字段");
        Assert.True(fields.Any(f => f.Name == "maxTechLevelBuy" && f.Kind == PocoCodec.Kind.Enum), "maxTechLevelBuy 应是枚举字段");
        Assert.True(fields.Any(f => f.Name == "price" && f.Kind == PocoCodec.Kind.Enum), "price 应是枚举字段");
        Assert.True(fields.Any(f => f.Name == "excludedThingDefs" && f.Kind == PocoCodec.Kind.DefList), "excludedThingDefs 应是 Def 列表字段");
        Assert.True(fields.Any(f => f.Name == "customCountRanges" && f.Kind == PocoCodec.Kind.ComplexList), "customCountRanges 应是小对象列表字段");
        Assert.True(!fields.Any(f => f.Name == "trader"), "字段表不应含 [Unsaved] 的 trader");
        Assert.True(!fields.Any(f => f.Name == "tmpGenerated"), "字段表不应含运行期临时表 tmpGenerated");

        // 小对象列表的元素类型也用同一张字段表（SGComplexListEditorWindow 直接读它）
        Assert.True(StockGeneratorCodec.FieldsFor(typeof(CountChance)).Any(f => f.Name == "chance" && f.Kind == PocoCodec.Kind.Float),
            "CountChance.chance 应是浮点字段");
        Assert.True(StockGeneratorCodec.FieldsFor(typeof(ThingDefCountRangeClass)).Any(f => f.Name == "thingDef" && f.Kind == PocoCodec.Kind.Def),
            "ThingDefCountRangeClass.thingDef 应是 Def 字段");

        string summary = StockGeneratorCodec.Summarize(new StockGenerator_Tag { tradeTag = "Foo" });
        Assert.Contains("tradeTag=Foo", summary);
        Assert.True(StockGeneratorCodec.Summarize(new FakeModGenerator()) == "", "非原版类型不打摘要");
        Assert.True(StockGeneratorCodec.TypeLabel(typeof(StockGenerator_Tag)) == "StockGenerator_Tag",
            "类型显示名应是原版 XML Class 的写法");
    }

    // ═══════════════════════ 非原版 ═══════════════════════

    /// <summary>
    /// 原版 / 非原版的判据就是"在不在字段表里"：类型菜单只列表里的具体类型；表外（其它 Mod 新增）
    /// 的子类字段集为空 —— 本工具对它们只做只读展示，不读取也不修改它们的字段。
    /// </summary>
    [Fact]
    public void 非原版判定与字段表外只读()
    {
        Assert.True(StockGeneratorCodec.IsVanilla(typeof(StockGenerator_Tag)), "原版 StockGenerator_Tag 应判定为原版");
        Assert.False(StockGeneratorCodec.IsVanilla(typeof(FakeModGenerator)), "字段表外的类型应判定为非原版");

        Assert.DoesNotContain(typeof(FakeModGenerator), StockGeneratorCodec.VanillaGeneratorTypes());
        Assert.True(StockGeneratorCodec.FieldsFor(typeof(FakeModGenerator)).Count == 0,
            "非原版子类的字段表应为空（本工具不读不写它的字段）");
        Assert.True(StockGeneratorCodec.Clone(new FakeModGenerator()) is FakeModGenerator, "非原版子类应能原样透传");

        // 继承链上的字段也跟着走（表内类型）
        Assert.True(StockGeneratorCodec.FieldsFor(typeof(StockGenerator_Tomes)).Any(f => f.Name == "thingDef"),
            "StockGenerator_Tomes 应含它从 StockGenerator_SingleDef 继承的 thingDef");
        var marketValue = StockGeneratorCodec.FieldsFor(typeof(StockGenerator_MarketValue)).Select(f => f.Name).ToList();
        Assert.True(marketValue.Contains("countRange") && marketValue.Contains("tradeTag")
            && marketValue.Contains("weaponTag") && marketValue.Contains("apparelTag"),
            "StockGenerator_MarketValue 应含基类字段与自有的三个标签字段");
    }

    /// <summary>
    /// 非原版（其它 Mod 新增）生成器：只读展示 + 占位标记透传。
    /// 编码只写类型名；回填时按类型名把 Def 里那个原实例原样放回原位 —— 它自己的字段一个都不丢。
    /// </summary>
    [Fact]
    public void 非原版占位标记与回填()
    {
        var mod = new FakeModGenerator { extraModField = "mod" };

        // 编码：只留类型名
        string line = StockGeneratorCodec.ToItemString(mod);
        Assert.True(line == StockGeneratorCodec.TypeLabel(typeof(FakeModGenerator)),
            $"非原版条目应只写类型名，实际：{line}");
        Assert.True(!line.Contains('|'), "非原版条目的行里不应有任何字段段");

        // 解码：得到一个同类型的"空壳"。占位实例直接用 new —— 与 TryParse 的产物等价，
        // 但不经过 GenTypes 按名查类型（那一步在测试进程里可用性另说，不是本用例要验证的东西）。
        var shell = new FakeModGenerator();
        Assert.True(StockGeneratorCodec.ToItemString(shell) == line, "占位标记应能稳定地再编码");

        // 回填：非原版条目必须换成 Def 里的原实例（身份与字段都不变），顺序保持在原处
        var defGen1 = new StockGenerator_SingleDef();
        var defMod1 = new FakeModGenerator { extraModField = "keep-1" };
        var defGen2 = new StockGenerator_Tag();
        var defMod2 = new FakeModGenerator { extraModField = "keep-2" };
        var defList = new List<StockGenerator> { defGen1, defMod1, defGen2, defMod2 };

        var merged = StockGeneratorCodec.MergeWithDef(new List<StockGenerator> { shell, defGen2 }, defList);
        Assert.True(merged is { Count: 3 }, $"应得到 1 个原版克隆 + 2 个原实例 共 3 条，实际 {merged?.Count}");
        // 输出顺序 = picked 顺序（= 用户编辑时的列表顺序），逐项替换、位置保持。
        // 本用例 picked = [shell, defGen2] ⇒ merged[0] 来自 shell（非原版 → 换回 Def 原实例），
        //                                  merged[1] 来自 defGen2（原版 → 克隆）。
        Assert.True(merged![0] is FakeModGenerator m1 && m1.extraModField == "keep-1",
            "非原版条目应按类型名换回 Def 里的原实例（字段不丢），且停在 picked 原位");
        Assert.True(!ReferenceEquals(merged[0], shell), "绝不能把空壳塞回 Def");
        Assert.True(merged[1].GetType() == typeof(StockGenerator_Tag) && !ReferenceEquals(merged[1], defGen2),
            "原版条目应是克隆（不与 Def 共享实例）");
        Assert.True(merged[2] is FakeModGenerator m2 && m2.extraModField == "keep-2",
            "Def 里剩下的非原版生成器应原样保留（追加在末尾，绝不丢）");

        // 清空字段（用户显式清掉整个字段）⇒ 不合并，直接清空
        Assert.True(StockGeneratorCodec.MergeWithDef(new List<StockGenerator>(), defList) == null, "空字段应返回 null（清空库存）");
        Assert.True(StockGeneratorCodec.MergeWithDef(null, defList) == null, "null 字段应返回 null");
    }

    // ═══════════════════════ 辅助 ═══════════════════════

    private static bool IsDefKind(PocoCodec.Kind kind)
        => kind == PocoCodec.Kind.Def || kind == PocoCodec.Kind.DefList;

    /// <summary>
    /// 造一个**只有 defName** 的合成 Def。测试进程里没有 <c>DefDatabase</c>，但编码方向只需要
    /// <c>def.defName</c>，所以用它验证"Def 列表怎么写成文本"、以及"克隆是否复制了引用"是可靠的
    /// （编码文本与游戏内逐字一致）。解码方向它做不到 —— 那要 <c>DefDatabase</c> 把 defName 查回实例。
    /// </summary>
    private static ThingDef SteelLike(string defName) => new() { defName = defName };

    /// <summary>
    /// 造一个 <c>ThingDefCountRangeClass</c>，只填 <paramref name="range"/>，<c>thingDef</c> 留空。
    /// <para>元素内部的 <c>thingDef</c> 是 <c>Kind.Def</c>，一旦填上，解码就会去查 <c>DefDatabase</c>
    /// （测试进程里查不到 ⇒ 整条解析失败）。带真 Def 的嵌套元素由游戏内覆盖。</para>
    /// </summary>
    private static ThingDefCountRangeClass RangeOnly(IntRange range)
    {
        var element = (ThingDefCountRangeClass)Activator.CreateInstance(typeof(ThingDefCountRangeClass))!;
        element.countRange = range;
        return element;
    }

    private static StockGenerator RoundTrip(StockGenerator gen)
    {
        string line = StockGeneratorCodec.ToItemString(gen);
        Assert.True(!line.Contains('\n'), "一行必须是一条（不含换行）");
        Assert.True(StockGeneratorCodec.TryParse(line, out var back) && back != null,
            $"解析不了自己编码出来的行：{line}");
        return back!;
    }

    /// <summary>反射设字段（原版不少生成器字段是 private，测试造样本只能这么设）。</summary>
    private static void SetGenField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.True(field != null, $"找不到字段 {target.GetType().Name}.{name}");
        field!.SetValue(target, value);
    }

    /// <summary>反射读字段（同上，配合 <see cref="SetGenField"/> 校验 private 字段的往返）。</summary>
    private static object? GetGenField(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.True(field != null, $"找不到字段 {target.GetType().Name}.{name}");
        return field!.GetValue(target);
    }

    /// <summary>游戏本体程序集里的全部 StockGenerator 子类（含官方 DLC 的类型；原版与 DLC 编在同一个 Assembly-CSharp.dll 里）。</summary>
    private static IEnumerable<Type> VanillaGeneratorSubclasses()
        => typeof(StockGenerator).Assembly.GetTypes()
            .Where(t => t != typeof(StockGenerator) && typeof(StockGenerator).IsAssignableFrom(t) && !t.IsAbstract);

    /// <summary>原版会写进 Def XML 的实例字段：跳过 static / 编译器生成 / <c>[Unsaved]</c> / 运行期临时表。</summary>
    private static IEnumerable<FieldInfo> XmlInstanceFields(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsDefined(typeof(UnsavedAttribute), false)) continue;
                if (f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) continue;
                if (f.Name.IndexOf('<') >= 0) continue;
                if (f.Name == "tmpGenerated") continue; // 生成库存时的临时收集表（生成完就 Clear），故意不入字段表
                yield return f;
            }
        }
    }

    /// <summary>
    /// 按字段表的种类造一个"非默认"的样本值（<paramref name="baselineValue"/> 是新实例上的初始值）。
    /// <c>Kind.Def</c> / <c>Kind.DefList</c> 返回 <c>null</c> —— 测试进程里取不到 Def；
    /// 调用点据此跳过该字段（Def 字段由游戏内那条覆盖）。
    /// </summary>
    private static object? SampleValueOf(PocoCodec.Field field, object? baselineValue)
    {
        switch (field.Kind)
        {
            case PocoCodec.Kind.Float: return 7.25f;
            case PocoCodec.Kind.Int: return 3;
            case PocoCodec.Kind.Bool: return !(baselineValue is bool b && b);
            case PocoCodec.Kind.Text: return "示例|标签=值;[x]\\y";
            case PocoCodec.Kind.IntRange: return new IntRange(2, 5);
            case PocoCodec.Kind.FloatRange: return new FloatRange(1.5f, 9.75f);
            case PocoCodec.Kind.Enum:
            {
                foreach (var value in Enum.GetValues(field.EnumType!))
                    if (!Equals(value, baselineValue)) return value;
                return null;
            }
            case PocoCodec.Kind.StringList:
                return new List<string> { "first", "second|x" };
            case PocoCodec.Kind.ComplexList:
            {
                var elementType = field.ElementType!;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                var element = Activator.CreateInstance(elementType)!;
                foreach (var sub in StockGeneratorCodec.FieldsFor(elementType))
                {
                    var sample = SampleValueOf(sub, StockGeneratorCodec.ReadField(sub, element));
                    if (sample != null) StockGeneratorCodec.WriteField(sub, element, sample);
                }
                list.Add(element);
                return list;
            }
            default: return null;
        }
    }

    private static bool SameValue(object? left, object? right)
    {
        if (left == null || right == null) return left == null && right == null;
        if (left is IList listLeft && right is IList listRight)
        {
            if (listLeft.Count != listRight.Count) return false;
            for (int i = 0; i < listLeft.Count; i++)
                if (!SameValue(listLeft[i], listRight[i])) return false;
            return true;
        }
        // 字段表里也声明了字段的小对象（CountChance / ThingDefCountRangeClass）逐字段比
        if (left is CountChance ca && right is CountChance cb) return ca.count == cb.count && ca.chance == cb.chance;
        if (left is ThingDefCountRangeClass ta && right is ThingDefCountRangeClass tb)
            return ta.thingDef == tb.thingDef && ta.countRange.Equals(tb.countRange);
        return Equals(left, right);
    }

    private static string DescribeValueOf(object? value) => value switch
    {
        null => "<null>",
        Def def => def.defName ?? "<无名 Def>",
        string str => $"\"{str}\"",
        IList list => $"[{list.Count}]",
        _ => value.ToString() ?? "<?>",
    };
}
