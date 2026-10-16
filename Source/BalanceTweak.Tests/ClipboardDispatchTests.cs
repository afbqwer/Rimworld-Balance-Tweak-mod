using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// 剪贴板分派（<see cref="StatColumnConfig.GetCopyData"/> / <see cref="StatColumnConfig.TryPasteData"/>）的回归测试。
///
/// <para>
/// <b>为什么非有不可</b>：这两处在这轮去样板里从"两份手写样式清单"改成了"由字段类型推出元素类型、
/// 再问 <c>PocoCodec.IsDeclared</c>"，但当时**一个测试都没有** —— 于是"通用判据截胡专用格式"
/// 这个回归被漏到了手测阶段（见 Docs §3.9.3）。本文件就是为它补的守卫。
/// </para>
///
/// <para>
/// <b>被测的那条不变量</b>：判据「元素类型已声明」**不等于**「该列的文本格式就是通用 POCO 格式」。
/// <c>StockGenList</c> 列的 <c>List&lt;StockGenerator&gt;</c> 元素类型确实已声明（<c>StockGenerator</c>
/// 基类在 <c>StockGeneratorCodec</c> 的静态构造里声明过），但它的文本必须由 <c>StockGeneratorCodec</c>
/// 带**类型标记**写出 —— 否则非原版子类无法透传，而且 <c>StockGenerator</c> 是抽象类、根本解不回来。
/// </para>
/// </summary>
public class ClipboardDispatchTests
{
    /// <summary>只为这两个字段存在的测试数据类（生产里没有这么小的数据类，故自造一个）。</summary>
    private class ClipboardProbeData : TweakData<ClipboardProbeData>
    {
        [TweakField(Style = ColumnStyle.StockGenList)]
        public List<StockGenerator> stockGenerators = new();

        [TweakField(Style = ColumnStyle.StatModList)]
        public List<StatModifier> statOffsets = new();

        // TweakData 的三个抽象成员：本测试不走这些路径，给最小实现即可。
        public override void Apply() { }
        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    /// <summary>
    /// 造一份数据 + 取到目标列的配置。<c>StatColumnConfig</c> 走生产同一条构造路径（<c>AllColumn</c>）。
    /// </summary>
    private static (ClipboardProbeData Data, StatColumnConfig Column) Arrange(string fieldName)
    {
        // 判据依赖字段声明表；PocoCodec 自己不会去拉声明类，必须先显式确保。
        TweakData.FieldCodecs.EnsureBuilt();

        var data = new ClipboardProbeData
        {
            id = new TweakData.TweakID($"__BT_Clip_{fieldName}__", SettingType.Gene),
        };
        return (data, data.AllColumn.Single(c => c.fieldName == fieldName));
    }

    private static void Cleanup(ClipboardProbeData data) => BalanceTweakSettings.tweakDatas.Remove(data.id);

    // ═══════════════════ 专用格式列：StockGenList ═══════════════════

    /// <summary>
    /// <c>StockGenList</c> 列的复制文本必须**带类型标记**（<c>StockGenerator_Tomes|countRange=…</c>），
    /// 而不是通用 POCO 格式（<c>countRange=…</c>）—— 后者会让粘贴端解不出具体类型。
    ///
    /// <para>
    /// 用<b>非默认字段值</b>（<c>countRange = 1~4</c>）是关键：全默认值的生成器走通用路径会得到空串、
    /// 于是回落专用分支而"碰巧正确"，测不出截胡。填一个非默认字段，两条路径才会给出不同结果。
    /// </para>
    /// </summary>
    [Fact]
    public void StockGenList列_复制必须带类型标记()
    {
        var (data, column) = Arrange(nameof(ClipboardProbeData.stockGenerators));
        try
        {
            var gen = new StockGenerator_Tomes { countRange = new IntRange(1, 4) };
            data.stockGenerators = new List<StockGenerator> { gen };

            string copied = column.GetCopyData(data)!;
            Assert.False(copied.NullOrEmpty(), "StockGenList 列应复制出文本");
            Assert.Equal(StockGeneratorCodec.ToItemString(gen), copied);
            Assert.StartsWith(StockGeneratorCodec.TypeLabel(typeof(StockGenerator_Tomes)), copied);
            Assert.Contains(PocoCodec.RecordSep, copied);   // 类型标记分隔符在场
        }
        finally { Cleanup(data); }
    }

    /// <summary>
    /// 复制出来的文本必须能粘回去。这条比"文本对"更接近用户实际动作：复制 → 粘贴 → 字段值不变。
    /// <para>
    /// 截胡时粘贴端会必失败：门把文本交给 <c>DecodeList(typeof(StockGenerator), …)</c>，
    /// 而 <c>StockGenerator</c> 是<b>抽象类</b>，<c>Activator.CreateInstance</c> 造不出实例 ⇒ 整块被拒。
    /// </para>
    /// </summary>
    [Fact]
    public void StockGenList列_复制粘贴应往返()
    {
        var (data, column) = Arrange(nameof(ClipboardProbeData.stockGenerators));
        try
        {
            var gen = new StockGenerator_Tomes { countRange = new IntRange(2, 6) };
            data.stockGenerators = new List<StockGenerator> { gen };

            string copied = column.GetCopyData(data)!;
            Assert.True(column.TryPasteData(data, copied), "粘回自己刚复制出来的文本必须成功");

            var back = Assert.IsType<StockGenerator_Tomes>(data.stockGenerators.Single());
            Assert.Equal(new IntRange(2, 6), back.countRange);
        }
        finally { Cleanup(data); }
    }

    // ═══════════════════ 通用格式列：StatModList ═══════════════════

    /// <summary>
    /// 元素类型已进字段表的普通列表列，走的是字段表通用格式（<c>字段名=值</c>，无类型标记），且能往返。
    /// 与上面两条构成对照：同一个门对这两类列必须给出不同结果。
    /// </summary>
    [Fact]
    public void 通用列表列_走字段表格式且能往返()
    {
        var (data, column) = Arrange(nameof(ClipboardProbeData.statOffsets));
        try
        {
            data.statOffsets = new List<StatModifier> { new() { value = 1.5f } };

            string copied = column.GetCopyData(data)!;
            Assert.Equal("value=1.5", copied);
            Assert.DoesNotContain(PocoCodec.RecordSep, copied);   // 无类型标记

            Assert.True(column.TryPasteData(data, copied));
            Assert.Equal(1.5f, data.statOffsets.Single().value);
        }
        finally { Cleanup(data); }
    }

    /// <summary>
    /// 两条路径的文本**必须不同** —— 若哪天又变成相同，说明专用分支再次被通用判据截胡（或反之）。
    /// 这是对上面三条的"反向"钉死，防止有人把断言改松。
    /// </summary>
    [Fact]
    public void 两类列表列的文本格式必须不同()
    {
        var (traderData, traderColumn) = Arrange(nameof(ClipboardProbeData.stockGenerators));
        var (statData, statColumn) = Arrange(nameof(ClipboardProbeData.statOffsets));
        try
        {
            traderData.stockGenerators = new List<StockGenerator>
            {
                new StockGenerator_Tomes { countRange = new IntRange(1, 4) },
            };
            statData.statOffsets = new List<StatModifier> { new() { value = 1f } };

            string genText = traderColumn.GetCopyData(traderData)!;
            string statText = statColumn.GetCopyData(statData)!;

            Assert.StartsWith(StockGeneratorCodec.TypeLabel(typeof(StockGenerator_Tomes)), genText);
            Assert.StartsWith("value=", statText);
        }
        finally { Cleanup(traderData); Cleanup(statData); }
    }
}
