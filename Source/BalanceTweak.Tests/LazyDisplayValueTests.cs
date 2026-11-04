using System.Collections.Generic;
using BalanceTweak;
using RimWorld;
using UnityEngine;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak.Tests;

/// <summary>
/// <c>propType</c> / <c>uiIcon</c> / <c>uiIconColor</c> / <c>desc</c> 从"SetDef 里算好"改成"首次读取时才算"
/// （<c>TweakData.propType</c> 与各 <c>Resolve*</c>）。
///
/// <para>
/// <b>被测的不变量</b>：① <c>SetDef</c> 一个都不算（否则懒加载名存实亡）；
/// ② 同一实例重复读取只算一次（缓存生效）；③ 换 def 后缓存作废、按新 def 重算；
/// ④ def 未绑定时读到兜底值但**不缓存**，补上 def 后必须能算出来。
/// </para>
/// </summary>
public class LazyDisplayValueTests
{
    /// <summary>记账用的假数据：每个 <c>Resolve*</c> 被调用就 +1，用来验证"什么时候算"。</summary>
    private sealed class CountingData : TweakData<CountingData>
    {
        public int PropTypeReads;
        public int DescReads;
        public int IconReads;
        public int ColorReads;

        public override List<StatColumnConfig> AllColumn => new();
        public override List<string> TypeStrings => new();
        public override int GetPropType() { PropTypeReads++; return 7; }
        public override void Apply() { }
        public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false) => true;

        protected override string ResolveDesc() { DescReads++; return "desc"; }
        protected override Texture2D? ResolveUiIcon() { IconReads++; return null; }
        protected override Color ResolveUiIconColor() { ColorReads++; return Color.white; }
    }

    [Fact]
    public void SetDef不解析任何展示属性()
    {
        var data = new CountingData();
        data.SetDef(new WeatherDef { defName = "Lazy" }, BalanceTweakSettings.SettingType.Weather);

        Assert.Equal(0, data.PropTypeReads);
        Assert.Equal(0, data.DescReads);
        Assert.Equal(0, data.IconReads);
        Assert.Equal(0, data.ColorReads);
    }

    [Fact]
    public void 同一实例重复读取只解析一次()
    {
        var data = new CountingData();
        data.SetDef(new WeatherDef { defName = "Lazy" }, BalanceTweakSettings.SettingType.Weather);

        _ = data.propType;
        _ = data.propType;
        _ = data.desc;
        _ = data.desc;
        _ = data.uiIcon;
        _ = data.uiIcon;
        _ = data.uiIconColor;
        _ = data.uiIconColor;

        Assert.Equal(1, data.PropTypeReads);
        Assert.Equal(1, data.DescReads);
        Assert.Equal(1, data.IconReads);
        Assert.Equal(1, data.ColorReads);
    }

    [Fact]
    public void 换def后缓存作废()
    {
        var data = new CountingData();
        data.SetDef(new WeatherDef { defName = "First" }, BalanceTweakSettings.SettingType.Weather);
        _ = data.propType;
        _ = data.desc;

        data.SetDef(new WeatherDef { defName = "Second" }, BalanceTweakSettings.SettingType.Weather);
        _ = data.propType;
        _ = data.desc;

        Assert.Equal(2, data.PropTypeReads);
        Assert.Equal(2, data.DescReads);
    }

    /// <summary>克隆体不能用源行的缓存：源行解析之后 def 可能被改写（ApplyDefLabel / 换绑）。</summary>
    [Fact]
    public void 克隆后展示属性按当前def重新解析()
    {
        var def = new WeatherDef { defName = "Cloned", description = "旧描述" };
        var data = new WeatherData();
        data.SetDef(def, BalanceTweakSettings.SettingType.Weather);
        Assert.Equal("旧描述", data.desc);

        def.description = "新描述";
        var clone = data.Clone();

        Assert.Equal("新描述", clone.desc);
    }

    /// <summary>def 未绑定 ⇒ 读到兜底值，且**不缓存**；补上 def 之后必须能算出真值。</summary>
    [Fact]
    public void def未绑定时不缓存兜底值()
    {
        var data = new WeatherData();
        Assert.Equal(string.Empty, data.desc);
        Assert.Null(data.uiIcon);
        Assert.Equal(Color.white, data.uiIconColor);

        data.def = new WeatherDef { defName = "LateBound", description = "迟到的描述" };
        Assert.Equal("迟到的描述", data.desc);
    }

    /// <summary>propType 的判据来自 def（这里用可遗传性），换 def 必须重新判定。</summary>
    [Fact]
    public void propType换def后重新判定()
    {
        var data = new XenotypeData();
        data.SetDef(new XenotypeDef { defName = "NonInheritable", inheritable = false },
            BalanceTweakSettings.SettingType.Xenotype);
        Assert.Equal(1, data.propType); // XenotypeType.NonInheritable

        data.SetDef(new XenotypeDef { defName = "Inheritable", inheritable = true },
            BalanceTweakSettings.SettingType.Xenotype);
        Assert.Equal(0, data.propType); // XenotypeType.Inheritable
    }
}
