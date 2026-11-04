using System.Collections.Generic;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 行星尺度的全球气体（<c>GlobalGasDef</c>）：沙尘 / 毒烟 / 烟雾 / 火山灰，4 行。
///
/// <para>
/// 和地图内的 <see cref="AtmosphereGasData"/> 是两套东西：这一层按**小时**步进，决定世界地图上的
/// 浓度、随风雨的冲刷与天气切换；地图内的那层按 tick 传质。
/// </para>
///
/// <para>
/// <b>不做的字段</b>：<c>tracerIndex</c>（索引 <c>GasTiles[tracerIndex]</c> 这个定长数组，改了错位）、
/// <c>color</c>（无对应列样式，纯视觉）、<c>sources</c> / <c>mapConditions</c>（元素是 AO 程序集里的
/// 多字段 POCO，镜像类型 + 按位置双向转换的成本远高于收益，见 Docs 计划 §3.2）。
/// </para>
/// </summary>
[TweakFor(typeof(Def), SettingType.GlobalGas, TypeName = AoHelper.GlobalGas, MayRequire = TweakDatabase.AO)]
class GlobalGasData : TweakData<GlobalGasData>
{
    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    /// <summary>世界地图读数上显示的名字（留空则回落 Def 的 label）。</summary>
    [TweakField(Style = ColumnStyle.String)]
    public string? readoutLabel = null;

    /// <summary>示踪剂寿命（小时）。</summary>
    [TweakField]
    public float? lifetimeHours = null;
    /// <summary>每毫米降水冲刷掉的比例。</summary>
    [TweakField]
    public float? washoutPerMm = null;
    /// <summary>起沙风速门槛（m/s）。</summary>
    [TweakField]
    public float? windThresholdMps = null;
    /// <summary>风致排放系数。</summary>
    [TweakField]
    public float? windScale = null;
    /// <summary>需要连续干燥多少小时才起沙。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? dryHours = null;
    /// <summary>污染地每小时排放（只有 Smog 配了非 0）。</summary>
    [TweakField]
    public float? pollutionPerHour = null;

    /// <summary>随机事件的间隔均值（天）。</summary>
    [TweakField]
    public float? episodeMeanDays = null;
    [TweakField]
    public float? episodeMinDays = null;
    [TweakField]
    public float? episodeMaxDays = null;
    /// <summary>随机事件源数量（只有 Toxic / Ash 配了非 0）。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? randomEpisodeSources = null;
    [TweakField]
    public float? randomEpisodePerHour = null;

    /// <summary>向相邻格子泄放的比例。</summary>
    [TweakField(Style = ColumnStyle.Prec)]
    public float? ventFraction = null;
    /// <summary>低于此浓度世界图上不画。</summary>
    [TweakField]
    public float? displayFloor = null;
    /// <summary>世界地图上的绘制半径（纯视觉）。</summary>
    [TweakField]
    public float? renderRadius = null;
    /// <summary>绘制层序（纯视觉）。3536 是默认值，改错会与其它图层穿插。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? renderQueue = null;

    /// <summary>浓度过阈值时把地图天气切成哪个 defName。注意 AO 里它是 <c>string</c> 不是 Def 引用。</summary>
    [TweakField(Style = ColumnStyle.String)]
    public string? mapWeather = null;
    /// <summary>没装 Odyssey 时的天气回落项。</summary>
    [TweakField(Style = ColumnStyle.String)]
    public string? mapWeatherFallback = null;
    [TweakField]
    public float? weatherThreshold = null;
    /// <summary>切换天气后维持多少小时（区间）。</summary>
    [TweakField(Style = ColumnStyle.Range)]
    public FloatRange? weatherEpisodeHours = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        object o = def!;
        readoutLabel ??= AoHelper.G(o, "readoutLabel") as string;
        lifetimeHours ??= AoHelper.F(o, "lifetimeHours");
        washoutPerMm ??= AoHelper.F(o, "washoutPerMm");
        windThresholdMps ??= AoHelper.F(o, "windThresholdMps");
        windScale ??= AoHelper.F(o, "windScale");
        dryHours ??= AoHelper.I(o, "dryHours");
        pollutionPerHour ??= AoHelper.F(o, "pollutionPerHour");
        episodeMeanDays ??= AoHelper.F(o, "episodeMeanDays");
        episodeMinDays ??= AoHelper.F(o, "episodeMinDays");
        episodeMaxDays ??= AoHelper.F(o, "episodeMaxDays");
        randomEpisodeSources ??= AoHelper.I(o, "randomEpisodeSources");
        randomEpisodePerHour ??= AoHelper.F(o, "randomEpisodePerHour");
        ventFraction ??= AoHelper.F(o, "ventFraction");
        displayFloor ??= AoHelper.F(o, "displayFloor");
        renderRadius ??= AoHelper.F(o, "renderRadius");
        renderQueue ??= AoHelper.I(o, "renderQueue");
        mapWeather ??= AoHelper.G(o, "mapWeather") as string;
        mapWeatherFallback ??= AoHelper.G(o, "mapWeatherFallback") as string;
        weatherThreshold ??= AoHelper.F(o, "weatherThreshold");
        weatherEpisodeHours ??= AoHelper.G(o, "weatherEpisodeHours") as FloatRange?;
    }

    public override void Apply()
    {
        if (this.def == null)
        {
            Log.Error($"[BalanceTweak]{this}的def为null!");
            return;
        }
        object o = this.def!;

        AoHelper.ApplyString(this, o, "readoutLabel", nameof(readoutLabel));
        AoHelper.ApplyF<float>(this, o, "lifetimeHours", nameof(lifetimeHours));
        AoHelper.ApplyF<float>(this, o, "washoutPerMm", nameof(washoutPerMm));
        AoHelper.ApplyF<float>(this, o, "windThresholdMps", nameof(windThresholdMps));
        AoHelper.ApplyF<float>(this, o, "windScale", nameof(windScale));
        AoHelper.ApplyF<int>(this, o, "dryHours", nameof(dryHours));
        AoHelper.ApplyF<float>(this, o, "pollutionPerHour", nameof(pollutionPerHour));
        AoHelper.ApplyF<float>(this, o, "episodeMeanDays", nameof(episodeMeanDays));
        AoHelper.ApplyF<float>(this, o, "episodeMinDays", nameof(episodeMinDays));
        AoHelper.ApplyF<float>(this, o, "episodeMaxDays", nameof(episodeMaxDays));
        AoHelper.ApplyF<int>(this, o, "randomEpisodeSources", nameof(randomEpisodeSources));
        AoHelper.ApplyF<float>(this, o, "randomEpisodePerHour", nameof(randomEpisodePerHour));
        AoHelper.ApplyF<float>(this, o, "ventFraction", nameof(ventFraction));
        AoHelper.ApplyF<float>(this, o, "displayFloor", nameof(displayFloor));
        AoHelper.ApplyF<float>(this, o, "renderRadius", nameof(renderRadius));
        AoHelper.ApplyF<int>(this, o, "renderQueue", nameof(renderQueue));
        AoHelper.ApplyString(this, o, "mapWeather", nameof(mapWeather));
        AoHelper.ApplyString(this, o, "mapWeatherFallback", nameof(mapWeatherFallback));
        AoHelper.ApplyF<float>(this, o, "weatherThreshold", nameof(weatherThreshold));
        AoHelper.ApplyF<FloatRange>(this, o, "weatherEpisodeHours", nameof(weatherEpisodeHours));
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = ["MST.GlobalGas"];

    public override int GetPropType() => 0;
}
