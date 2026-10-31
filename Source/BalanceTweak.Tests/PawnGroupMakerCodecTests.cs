using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="PawnGroupMakerCodec"/> 与它挂在字段表上的两条声明（<see cref="PawnGroupMaker"/> /
/// <see cref="PawnGenOption"/>）的机制测试。
///
/// <para>
/// <b>在游戏外跑</b>：不启动游戏、不查 <c>DefDatabase</c>。因此凡是涉及 Def 引用的断言
/// （<c>kindDef</c> / <c>disallowedStrategies</c> / <c>PawnGenOption.kind</c>）一律跳过 ——
/// 测试进程里 <c>GenDefDatabase</c> 返回 null，这类断言会静默假绿。
/// </para>
///
/// <para>
/// 这里守的是三件容易在重构中悄悄丢掉的语义：字段表<b>覆盖原版全部字段</b>（原版加了字段会先炸）、
/// 克隆<b>不与 Def 共享实例</b>（否则"取消"是假的）、非原版子类<b>原样透传</b>（否则等于替别的 Mod 删数据）。
/// </para>
/// </summary>
public class PawnGroupMakerCodecTests
{
    /// <summary>
    /// 字段声明写在 <c>TweakData.FieldCodecs</c> 的静态构造里，而 <see cref="PocoCodec.IsDeclared"/> /
    /// <see cref="PocoCodec.FieldsFor"/> 都<b>不会</b>去拉它（反向依赖会构成静态构造环）。
    /// 少了这一下，本文件的断言会随"别的测试是否先跑过"而时红时绿。
    /// </summary>
    static PawnGroupMakerCodecTests() => TweakData.FieldCodecs.EnsureBuilt();

    // ═══════════════════════ 字段表 ═══════════════════════

    /// <summary>
    /// 表里声明的字段都解析得到（原版没改名 / 没移除）。
    /// 必须先跑一遍 <c>FieldsFor</c>：<c>UnresolvedFields</c> 是在解析字段时才填的。
    /// </summary>
    [Fact]
    public void 字段表无未解析项()
    {
        PawnGroupMakerCodec.FieldsFor(typeof(PawnGroupMaker));
        PawnGroupMakerCodec.FieldsFor(typeof(PawnGenOption));
        Assert.Empty(PocoCodec.UnresolvedFields);
    }

    /// <summary>
    /// 原版这两个类型的**每个实例字段**都在表里，且字段类型都被支持。
    /// 原版一旦加字段（或把 <c>options</c> 换成别的容器），这条会先炸 ——
    /// 而不是等用户存档时静默丢字段。
    /// </summary>
    [Fact]
    public void 字段表覆盖原版全部字段()
    {
        var problems = new List<string>();

        foreach (var type in new[] { typeof(PawnGroupMaker), typeof(PawnGenOption) })
        {
            // 判据用 IsDeclared 而不是 IsVanilla：后者只回答"是不是本工具认识的**兵种池**类型"
            // （PawnGenOption 是子列表元素，永远走字段表，不参与那一层判定）。
            if (!PocoCodec.IsDeclared(type))
            {
                problems.Add($"{type.Name} 不在字段表里");
                continue;
            }

            var declared = PawnGroupMakerCodec.FieldsFor(type).Select(f => f.Name).ToHashSet();
            foreach (var field in XmlInstanceFields(type))
                if (!declared.Contains(field.Name))
                    problems.Add($"{type.Name}.{field.Name}（{field.FieldType.Name}）没写进字段表");

            foreach (var field in PawnGroupMakerCodec.FieldsFor(type))
                if (field.Kind == PocoCodec.Kind.Unsupported)
                    problems.Add($"{type.Name}.{field.Name} 的字段类型不受支持");
        }

        Assert.True(problems.Count == 0,
            $"{problems.Count} 处字段表与原版对不上：\n{string.Join("\n", problems)}");
    }

    /// <summary>
    /// 四个 <c>List&lt;PawnGenOption&gt;</c> 必须是 <c>ComplexList</c>：只有元素类型先进表，
    /// <see cref="PocoCodec"/> 才认得它们；退化成 <c>Unsupported</c> 时字段会静默不读不写。
    /// </summary>
    [Fact]
    public void 子列表被识别为小对象列表()
    {
        var byName = PawnGroupMakerCodec.FieldsFor(typeof(PawnGroupMaker)).ToDictionary(f => f.Name);
        foreach (var name in new[] { "options", "traders", "carriers", "guards" })
        {
            Assert.True(byName.TryGetValue(name, out var field), $"{name} 不在字段表里");
            Assert.Equal(PocoCodec.Kind.ComplexList, field!.Kind);
            Assert.Equal(typeof(PawnGenOption), field.ElementType);
        }
    }

    // ═══════════════════════ 克隆 ═══════════════════════

    /// <summary>
    /// 克隆必须换掉**所有**可变引用：兵种池本身、四个子列表、子列表里的每个 <see cref="PawnGenOption"/>。
    /// 少换一层，"编辑副本"就会经由共享的列表写回 Def —— 取消按钮随即变成假的。
    /// </summary>
    [Fact]
    public void 克隆不与原对象共享任何列表()
    {
        var maker = new PawnGroupMaker { commonality = 30f };
        maker.options.Add(new PawnGenOption { selectionWeight = 1f });

        var copy = PawnGroupMakerCodec.Clone(maker)!;

        Assert.NotSame(maker, copy);
        Assert.Equal(30f, copy.commonality);
        Assert.NotSame(maker.options, copy.options);
        Assert.NotSame(maker.options[0], copy.options[0]);
        Assert.Equal(1f, copy.options[0].selectionWeight);

        copy.options.Add(new PawnGenOption());
        Assert.Single(maker.options);
    }

    /// <summary>空列表克隆成空列表（不是 null）—— 原版对这两者不等价：裸 <c>foreach</c> 会 NRE。</summary>
    [Fact]
    public void 克隆保留空列表而非null()
    {
        var copy = PawnGroupMakerCodec.Clone(new PawnGroupMaker())!;
        Assert.NotNull(copy.options);
        Assert.Empty(copy.options);
    }

    [Fact]
    public void 克隆全部_空输入返回null()
        => Assert.Null(PawnGroupMakerCodec.CloneAll(null));

    // ═══════════════════════ 文本往返（不含 Def） ═══════════════════════

    /// <summary>
    /// 字段表驱动的文本格式往返。Def 字段（<c>kindDef</c> / <c>kind</c> / <c>disallowedStrategies</c>）
    /// 在测试进程里解析不到，这里留空 —— 空 Def 引用本来也是合法状态（编码端直接省略）。
    /// </summary>
    [Fact]
    public void 兵种池文本往返()
    {
        var maker = new PawnGroupMaker { commonality = 42.5f, maxTotalPoints = 800f };
        maker.options.Add(new PawnGenOption { selectionWeight = 3f });

        var text = PocoCodec.Encode(maker);
        Assert.False(string.IsNullOrEmpty(text), "非默认值的兵种池不该编码成空串");

        Assert.True(PocoCodec.TryDecode(text, typeof(PawnGroupMaker), out var decoded));
        var copy = Assert.IsType<PawnGroupMaker>(decoded);

        Assert.Equal(42.5f, copy.commonality);
        Assert.Equal(800f, copy.maxTotalPoints);
        Assert.Single(copy.options);
        Assert.Equal(3f, copy.options[0].selectionWeight);
        Assert.Null(copy.options[0].kind);
    }

    /// <summary>等于新实例初值的字段不写进文本（<c>commonality</c> 默认 100、<c>maxTotalPoints</c> 默认 9999999）。</summary>
    [Fact]
    public void 默认值不写进文本()
    {
        var text = PocoCodec.Encode(new PawnGroupMaker());
        Assert.DoesNotContain("commonality=", text);
        Assert.DoesNotContain("maxTotalPoints=", text);
    }

    // ═══════════════════════ 与 Def 合并 ═══════════════════════

    /// <summary>
    /// 清空（字段为空列表 / null）时交给 Def 的必须是**空列表**而不是 null ——
    /// 硬约束：集合的终态是空集合，<c>def.pawnGroupMakers = null</c> 会让袭击生成期 NRE。
    /// </summary>
    [Fact]
    public void 合并_空字段得到空列表而非null()
    {
        Assert.NotNull(PawnGroupMakerCodec.MergeWithDef(null, new List<PawnGroupMaker>()));
        Assert.Empty(PawnGroupMakerCodec.MergeWithDef(null, null));
        Assert.Empty(PawnGroupMakerCodec.MergeWithDef(new List<PawnGroupMaker>(), null));
    }

    /// <summary>原版条目写回 Def 时必须是新实例 —— 字段与 Def 永不共享对象。</summary>
    [Fact]
    public void 合并_原版条目写回时再克隆一次()
    {
        var def = new List<PawnGroupMaker> { new PawnGroupMaker { kindDef = TestKind() } };
        var field = PawnGroupMakerCodec.CloneAll(def)!;

        var merged = PawnGroupMakerCodec.MergeWithDef(field, def);

        Assert.Single(merged);
        Assert.NotSame(field[0], merged[0]);
        Assert.NotSame(def[0], merged[0]);
    }

    /// <summary>
    /// 写回 Def 前丢掉"游戏读它必炸"的条目：没有 kindDef 的兵种池（<c>CanGenerateFrom</c> 解引用
    /// <c>kindDef.Worker</c>）与没有 kind 的兵种（<c>GetOptions</c> 拿它查异种人可用性）。
    /// </summary>
    [Fact]
    public void 合并_丢弃没有kindDef的兵种池()
    {
        var merged = PawnGroupMakerCodec.MergeWithDef(new List<PawnGroupMaker> { new PawnGroupMaker() }, null);
        Assert.Empty(merged);
    }

    [Fact]
    public void 合并_丢弃没有兵种的选项()
    {
        var maker = new PawnGroupMaker { kindDef = TestKind() };
        maker.options.Add(new PawnGenOption { kind = null, selectionWeight = 1f });

        var merged = PawnGroupMakerCodec.MergeWithDef(new List<PawnGroupMaker> { maker }, null);

        Assert.Single(merged);
        Assert.Empty(merged[0].options);
    }

    /// <summary>
    /// 非原版子类（其它 Mod 写的）：不克隆、不读字段，按"类型 + 出现次序"从 Def 里找回**原实例**。
    /// 克隆它只会把子类自己的字段抹成默认值。
    /// </summary>
    [Fact]
    public void 合并_非原版子类原样透传()
    {
        var original = new FakeGroupMaker { extra = 7 };
        var def = new List<PawnGroupMaker> { original };

        var field = PawnGroupMakerCodec.CloneAll(def)!;
        Assert.Same(original, field[0]);   // 不克隆

        var merged = PawnGroupMakerCodec.MergeWithDef(field, def);
        Assert.Same(original, merged[0]);  // 原实例归位
        Assert.Equal(7, ((FakeGroupMaker)merged[0]).extra);
    }

    /// <summary>找不到原实例的非原版条目（粘贴到别的派系）直接丢弃，绝不把空壳写进 Def。</summary>
    [Fact]
    public void 合并_找不到原实例的非原版条目被丢弃()
    {
        var merged = PawnGroupMakerCodec.MergeWithDef(
            new List<PawnGroupMaker> { new FakeGroupMaker() }, new List<PawnGroupMaker>());
        Assert.Empty(merged);
    }

    /// <summary>Def 里有、字段里没有的非原版条目追加在末尾，绝不丢（丢掉就是永久删除别人的数据）。</summary>
    [Fact]
    public void 合并_未被认领的非原版条目追加在末尾()
    {
        var stray = new FakeGroupMaker();
        var merged = PawnGroupMakerCodec.MergeWithDef(
            new List<PawnGroupMaker>(), new List<PawnGroupMaker> { stray });
        Assert.Same(stray, merged[0]);
    }

    // ═══════════════════════ 展示 ═══════════════════════

    /// <summary>缺 kindDef 的兵种池没有标签（数据层不做翻译，由 UI 决定怎么提示）。</summary>
    [Fact]
    public void 缺kindDef时没有标签()
        => Assert.Equal("", PawnGroupMakerCodec.KindLabel(new PawnGroupMaker()));

    [Fact]
    public void 摘要包含权重与兵种数()
    {
        var maker = new PawnGroupMaker { commonality = 55f };
        maker.options.Add(new PawnGenOption());

        var summary = PawnGroupMakerCodec.Summarize(maker);

        Assert.Contains("commonality=55", summary);
        Assert.Contains("options=1", summary);
    }

    // ═══════════════════════ 辅助 ═══════════════════════

    /// <summary>
    /// 造一个不入库的 <see cref="PawnGroupKindDef"/>。测试进程里 <c>DefDatabase</c> 是空的，
    /// 但 <see cref="PawnGroupMaker"/> 的校验只要求"非 null"，因此直接 new 一个就够。
    /// </summary>
    private static PawnGroupKindDef TestKind() => new() { defName = "BT_TestKind" };

    /// <summary>原版会写进 Def XML 的实例字段：跳过 static / 编译器生成 / <c>[Unsaved]</c>。</summary>
    private static IEnumerable<FieldInfo> XmlInstanceFields(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsDefined(typeof(UnsavedAttribute), false)) continue;
                if (f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) continue;
                if (f.Name.IndexOf('<') >= 0) continue;
                yield return f;
            }
        }
    }

    /// <summary>
    /// 冒充"其它 Mod 的 PawnGroupMaker 子类"：<see cref="PawnGroupMakerCodec.IsVanilla"/> 按<b>精确类型</b>
    /// 判定，所以它会被当成非原版处理（只读 + 透传）。
    /// </summary>
    private class FakeGroupMaker : PawnGroupMaker
    {
        public int extra;
    }
}
