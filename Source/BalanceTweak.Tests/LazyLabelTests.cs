using System.Collections.Generic;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <c>label</c> / <c>searchString</c> 从"SetDef 里算好"改成"首次读取时才算"（TweakData.label）。
///
/// <para>
/// <b>被测的不变量</b>：① 绑定 def 之后读 label 得到与原来一致的文本；② **def 还没绑定时读到空串，
/// 但不能把空串缓存下来** —— 缓存了就永久空标题，这正是懒加载最容易踩的坑；
/// ③ 换 def（<c>SetDef</c> 复用同一实例）要丢掉上一个 def 算出来的标题。
/// </para>
/// </summary>
public class LazyLabelTests
{
    [Fact]
    public void 绑定def后按需算出标题()
    {
        var data = new WeatherData { def = new WeatherDef { defName = "TestWeather" } };
        Assert.Equal("TestWeather", data.label);
    }

    [Fact]
    public void 搜索串默认等于标题加defName()
    {
        var data = new WeatherData { def = new WeatherDef { defName = "TestWeather" } };
        Assert.Equal(data.label + "TestWeather", data.searchString);
    }

    /// <summary>def 未绑定 ⇒ 读到空串，且**不缓存**；补上 def 之后必须能算出真标题。</summary>
    [Fact]
    public void def未绑定时不缓存空标题()
    {
        var data = new WeatherData();
        Assert.Equal(string.Empty, data.label);

        data.def = new WeatherDef { defName = "LateBound" };
        Assert.Equal("LateBound", data.label);
    }

    /// <summary>同一实例换 def（整表重建时会走到）必须重新算，不能挂着上一个 def 的标题。</summary>
    [Fact]
    public void 换def后标题重新计算()
    {
        var data = new WeatherData();
        data.SetDef(new WeatherDef { defName = "FirstDef" }, BalanceTweakSettings.SettingType.Weather);
        Assert.Equal("FirstDef", data.label);

        data.SetDef(new WeatherDef { defName = "SecondDef" }, BalanceTweakSettings.SettingType.Weather);
        Assert.Equal("SecondDef", data.label);
        Assert.Contains("SecondDef", data.searchString);
    }

    /// <summary>显式赋值仍然有效（编辑器 / 剪贴板路径靠 setter）。</summary>
    [Fact]
    public void 显式赋值覆盖派生值()
    {
        var data = new WeatherData { def = new WeatherDef { defName = "TestWeather" } };
        data.label = "自定义";
        Assert.Equal("自定义", data.label);
    }

    /// <summary>
    /// 标题一改，搜索串必须跟着重算（TweakData.label 的 setter 丢 searchString 缓存）——
    /// 否则用户改完名字，搜索框还按旧名字匹配。
    /// </summary>
    [Fact]
    public void 改标题后搜索串重新派生()
    {
        var data = new WeatherData { def = new WeatherDef { defName = "TestWeather" } };
        _ = data.searchString; // 先读一次，让 searchString 按旧标题缓存下来

        data.label = "改名之后";
        Assert.Equal("改名之后TestWeather", data.searchString);
    }

    /// <summary>
    /// Clone 不能把"已解析"标记抄过来：克隆体之后再改 def.label，克隆行要按新文本重新算，
    /// 不能挂着克隆那一刻的旧标题（TweakData.Clone 统一清缓存标记）。
    /// </summary>
    [Fact]
    public void 克隆后标题按当前def重新解析()
    {
        var def = new WeatherDef { defName = "TestWeather", label = "旧名" };
        var data = new WeatherData();
        data.SetDef(def, BalanceTweakSettings.SettingType.Weather);
        _ = data.label; // 源行先解析并缓存"旧名"

        var clone = data.Clone();
        def.label = "新名";
        def.ClearCachedData(); // 与 ApplyDefLabel 的写回路径一致：改 label 必清 Def 侧 LabelCap 缓存

        Assert.Equal("新名", clone.label);
    }

    /// <summary>
    /// ApplyDefLabel 写回 def.label 后，本行的懒加载缓存必须置脏 —— 否则玩家应用改名后，
    /// 列表里这一行仍显示旧标题，看起来像改动没生效（TweakData{T}.ApplyDefLabel）。
    /// </summary>
    [Fact]
    public void 应用defLabel后行标题立即刷新()
    {
        var def = new WeatherDef { defName = "TestWeather", label = "旧名" };
        var data = new WeatherData();
        data.SetDef(def, BalanceTweakSettings.SettingType.Weather);
        Assert.Equal("旧名", data.label); // 先读一次，缓存旧标题

        data.defLabel = "新名";
        data.MarkFieldModified(nameof(WeatherData.defLabel));
        data.Apply();

        Assert.Equal("新名", def.label);
        Assert.Equal("新名", data.label);
    }
}

/// <summary>
/// <see cref="QuestNameResolver"/> 的保守展开规则：只替换拿得到选项的 token，
/// 任何一处拿不到就整条放弃（返回 null），由上层回落 Def.label。
/// </summary>
public class QuestNameResolverTests
{
    private static Dictionary<string, List<string>> Keywords() => new()
    {
        ["camp"] = new List<string> { "Camp", "Outpost" },
        ["bandit"] = new List<string> { "Bandit" },
        ["loopA"] = new List<string> { "[loopB]" },
        ["loopB"] = new List<string> { "[loopA]" },
        ["nested"] = new List<string> { "[bandit] [camp]" },
    };

    [Fact]
    public void 全部token可解析时替换成第一条选项()
    {
        Assert.Equal("The Bandit Camp", QuestNameResolver.Expand("The [bandit] [camp]", Keywords()));
    }

    [Fact]
    public void 嵌套token递归展开()
    {
        Assert.Equal("Bandit Camp", QuestNameResolver.Expand("[nested]", Keywords()));
    }

    /// <summary>nameDef / slate 变量这类 token 没有静态选项 —— 必须整条放弃，而不是留下 "<c>[x]</c>" 或半截文本。</summary>
    [Fact]
    public void 拿不到选项时整条放弃()
    {
        Assert.Null(QuestNameResolver.Expand("[asker_nameDef] and the [camp]", Keywords()));
        Assert.Null(QuestNameResolver.Expand("Saving [runnerIndef]", Keywords()));
    }

    /// <summary>规则互相引用（a→[b], b→[a]）不会死循环，靠深度上限收敛成"放弃"。</summary>
    [Fact]
    public void 循环引用不会无限递归()
    {
        Assert.Null(QuestNameResolver.Expand("[loopA]", Keywords()));
    }

    [Fact]
    public void 空白折叠()
    {
        Assert.Equal("The Camp", QuestNameResolver.Normalize("  The\n  Camp "));
    }

    /// <summary>任务脚本没有可用的名字规则时，标题回落 Def.label（= defName），不会抛异常。</summary>
    [Fact]
    public void 没有名字规则时回落defName()
    {
        var data = new QuestData { def = new QuestScriptDef { defName = "Test_Quest" } };
        Assert.Equal("Test_Quest", data.label);
    }

    /// <summary>作者显式写了 label 就尊重作者（原版没有这种 def，但 Mod 会有）。</summary>
    [Fact]
    public void 显式label优先于规则解析()
    {
        var data = new QuestData { def = new QuestScriptDef { defName = "Test_Quest", label = "作者起的名字" } };
        Assert.Equal("作者起的名字", data.label);
    }
}
