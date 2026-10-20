using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// Prec（百分比）列与 Float（浮点）列的编辑链路对照测试。
///
/// <para>
/// <b>被测的不变量</b>：<c>GetString</c>（显示 = 值 × 100）与 <c>CheckAndApply</c>（写入 = 输入 ÷ 100）
/// 必须互为逆运算，且「输入的值等于当前显示值」不得产生任何字段状态变化。
/// 这两条一旦漂移，表现就是"百分比列改不动 / 改完又跳回去"，而浮点列看不出问题。
/// </para>
/// </summary>
public class PrecStyleProbeTests
{
    private class StyleProbeData : TweakData<StyleProbeData>
    {
        [TweakField(Style = ColumnStyle.Prec)]
        public float? pct = null;

        [TweakField(Style = ColumnStyle.Float)]
        public float? flt = null;

        public override void Apply() { }
        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    private static (StyleProbeData Data, StatColumnConfig Prec, StatColumnConfig Float) Arrange()
    {
        var data = new StyleProbeData
        {
            id = new TweakData.TweakID("__BT_PrecProbe__", SettingType.Gene),
        };
        var cols = data.AllColumn;
        return (data, cols.Single(c => c.fieldName == nameof(StyleProbeData.pct)),
                      cols.Single(c => c.fieldName == nameof(StyleProbeData.flt)));
    }

    private static void Cleanup(StyleProbeData data) => tweakDatas.Remove(data.id);

    [Fact]
    public void 显示为百分比_再写回同一数值_不应算作修改()
    {
        var (data, prec, _) = Arrange();
        try
        {
            data.pct = 0.8f;
            Assert.Equal("80.00", prec.GetString(data));

            prec.CheckAndApply(data, "80.00");
            Assert.Equal(0.8f, data.pct);
            Assert.False(data.IsFieldModified(nameof(StyleProbeData.pct)),
                "输入文本与当前显示值相同 ⇒ 不该标记为已修改");
        }
        finally { Cleanup(data); }
    }

    [Fact]
    public void 输入85_应写入0点85()
    {
        var (data, prec, _) = Arrange();
        try
        {
            data.pct = 0.8f;
            prec.CheckAndApply(data, "85");
            Assert.Equal(0.85f, data.pct!.Value, 5);
            Assert.Equal("85.00", prec.GetString(data));
        }
        finally { Cleanup(data); }
    }

    [Fact]
    public void 浮点列对照_输入0点85_应写入0点85()
    {
        var (data, _, flt) = Arrange();
        try
        {
            data.flt = 0.8f;
            Assert.Equal("0.80", flt.GetString(data));
            flt.CheckAndApply(data, "0.85");
            Assert.Equal(0.85f, data.flt!.Value, 5);
        }
        finally { Cleanup(data); }
    }

    [Fact]
    public void 空值字段_百分比输入应能建值()
    {
        var (data, prec, _) = Arrange();
        try
        {
            Assert.Null(data.pct);
            Assert.Equal("Null", prec.GetString(data));
            prec.CheckAndApply(data, "50");
            Assert.Equal(0.5f, data.pct!.Value, 5);
        }
        finally { Cleanup(data); }
    }

    [Fact]
    public void 输入0_应写入0并标记为已修改()
    {
        var (data, prec, _) = Arrange();
        try
        {
            data.pct = 0.8f;
            prec.CheckAndApply(data, "0");
            Assert.Equal(0f, data.pct!.Value, 5);
            Assert.True(data.IsFieldModified(nameof(StyleProbeData.pct)), "0 是合法值，不能被当成空值");
        }
        finally { Cleanup(data); }
    }
}
