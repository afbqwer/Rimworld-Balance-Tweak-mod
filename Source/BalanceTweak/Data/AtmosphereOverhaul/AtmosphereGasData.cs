using System.Collections.Generic;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 地图内气体（<c>AtmosphereGasDef</c>）：气体 4 种 + 装 CE 时的黑烟，共 5 行。
///
/// <para>
/// <b>为什么整页都是反射</b>：<c>AtmosphereGasDef</c> 来自 AO 的程序集，静态引用会在未装该 Mod 的
/// 玩家启动期炸 <c>TypeLoadException</c>。<c>def</c> 只能当 <c>object</c> 用，绝不强转。
/// </para>
///
/// <para>
/// <b>不做的字段</b>：<c>storageMode</c> / <c>backingVanillaGasType</c>（运行期切换等于把已存在的气体
/// 丢掉或重复计数）、<c>workerClasses</c> / <c>workers</c>（map init 时构造成实例，之后不再读 Def）、
/// <c>requiresBiotech</c> / <c>requiresAnomaly</c>（只决定该 Def 在 DLC 配置下可不可用）。
/// </para>
/// </summary>
[TweakFor(typeof(Def), SettingType.AtmosphereGas, TypeName = AoHelper.GasDef, MayRequire = TweakDatabase.AO)]
class AtmosphereGasData : TweakData<AtmosphereGasData>
{
    // ── 顶层 ──
    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    /// <summary>"多浓算满"的参照。网格里存的是**绝对密度**，改它不动已有气体的绝对量，
    /// 但会改渲染饱和度与熄灭阈值（<c>extinctionDensityFraction × maxDensity</c>）。</summary>
    [TweakField]
    public float? maxDensity = null;

    [TweakField]
    public float? activeThreshold = null;

    /// <summary>对射击命中率的遮蔽系数（0 = 不遮蔽）。</summary>
    [TweakField(Style = ColumnStyle.Prec)]
    public float? shootObscuranceFactor = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? blocksVision = null;

    /// <summary>超过这个浓度就伤人。⚠ 0 → 正数需要清 <c>BreathingGear.harmfulGases</c> 静态缓存，
    /// 见 <see cref="Apply"/> 末尾。</summary>
    [TweakField]
    public float? harmfulDensity = null;

    /// <summary>效果随密度的对比度（1 = 半密度时效果减半）。</summary>
    [TweakField]
    public float? densityContrast = null;

    /// <summary>抗性开关，不是倍率：<c>stat == null || GetStatValue(pawn, stat) &gt; 0</c> 才受伤。</summary>
    [TweakField(Style = ColumnStyle.DefSelector)]
    public StatDef? exposureStat = null;

    // ── renderSetting（纯视觉）──
    [TweakField(Style = ColumnStyle.String)]
    public string? renderTexturePath = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? baseQuadCount = null;
    [TweakField]
    public float? quadJitterScale = null;
    [TweakField]
    public float? maxRenderDensity = null;
    [TweakField]
    public float? drawSize = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? opacityFloor = null;

    // ── AO_transferSetting（真正的平衡项）──
    /// <summary>平流（随风漂移）速率。</summary>
    [TweakField]
    public float? advectionRate = null;
    [TweakField]
    public float? diffusionRate = null;
    /// <summary>扩散迭代次数。性能敏感：调高会成倍增加每 tick 的传质开销。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? diffusionIterations = null;
    [TweakField]
    public float? naturalVanishRatePerTick = null;
    [TweakField]
    public float? roofedVanishFactor = null;
    [TweakField]
    public float? unroofedRelativeVanishPerTick = null;
    /// <summary>熄灭阈值（× maxDensity）：低于它整格清零。</summary>
    [TweakField]
    public float? extinctionDensityFraction = null;
    [TweakField]
    public float? vacuumVanishMultiplier = null;
    /// <summary>是否走后台调度批次（性能开关，不是平衡）。</summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? scheduled = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        object o = def!;
        maxDensity ??= AoHelper.F(o, "maxDensity");
        activeThreshold ??= AoHelper.F(o, "activeThreshold");
        shootObscuranceFactor ??= AoHelper.F(o, "shootObscuranceFactor");
        blocksVision ??= AoHelper.B(o, "blocksVision");
        harmfulDensity ??= AoHelper.F(o, "harmfulDensity");
        densityContrast ??= AoHelper.F(o, "densityContrast");
        exposureStat ??= AoHelper.G(o, "exposureStat") as StatDef;

        // 子对象只在这里取一次引用做回填，不存进字段（克隆 / 重置后会指向别处）。
        var render = AoHelper.G(o, "renderSetting");
        renderTexturePath ??= AoHelper.G(render, "renderTexturePath") as string;
        baseQuadCount ??= AoHelper.I(render, "baseQuadCount");
        quadJitterScale ??= AoHelper.F(render, "quadJitterScale");
        maxRenderDensity ??= AoHelper.F(render, "maxRenderDensity");
        drawSize ??= AoHelper.F(render, "drawSize");
        opacityFloor ??= AoHelper.F(render, "opacityFloor");

        var transfer = AoHelper.G(o, "AO_transferSetting");
        advectionRate ??= AoHelper.F(transfer, "advectionRate");
        diffusionRate ??= AoHelper.F(transfer, "diffusionRate");
        diffusionIterations ??= AoHelper.I(transfer, "diffusionIterations");
        naturalVanishRatePerTick ??= AoHelper.F(transfer, "naturalVanishRatePerTick");
        roofedVanishFactor ??= AoHelper.F(transfer, "roofedVanishFactor");
        unroofedRelativeVanishPerTick ??= AoHelper.F(transfer, "unroofedRelativeVanishPerTick");
        extinctionDensityFraction ??= AoHelper.F(transfer, "extinctionDensityFraction");
        vacuumVanishMultiplier ??= AoHelper.F(transfer, "vacuumVanishMultiplier");
        scheduled ??= AoHelper.B(transfer, "scheduled");
    }

    public override void Apply()
    {
        if (this.def == null)
        {
            Log.Error($"[BalanceTweak]{this}的def为null!");
            return;
        }
        object o = this.def!;

        AoHelper.ApplyF<float>(this, o, "maxDensity", nameof(maxDensity));
        AoHelper.ApplyF<float>(this, o, "activeThreshold", nameof(activeThreshold));
        AoHelper.ApplyF<float>(this, o, "shootObscuranceFactor", nameof(shootObscuranceFactor));
        AoHelper.ApplyF<bool>(this, o, "blocksVision", nameof(blocksVision));
        AoHelper.ApplyF<float>(this, o, "harmfulDensity", nameof(harmfulDensity));
        AoHelper.ApplyF<float>(this, o, "densityContrast", nameof(densityContrast));
        ApplyD<StatDef>(nameof(exposureStat), v => AoHelper.S(o, "exposureStat", v));

        // 子对象每次 Apply 现取（Def 构造时 new 好，正常不会 null；ApplyF 自己兜 null）
        var render = AoHelper.G(o, "renderSetting");
        AoHelper.ApplyString(this, render, "renderTexturePath", nameof(renderTexturePath));
        AoHelper.ApplyF<int>(this, render, "baseQuadCount", nameof(baseQuadCount));
        AoHelper.ApplyF<float>(this, render, "quadJitterScale", nameof(quadJitterScale));
        AoHelper.ApplyF<float>(this, render, "maxRenderDensity", nameof(maxRenderDensity));
        AoHelper.ApplyF<float>(this, render, "drawSize", nameof(drawSize));
        AoHelper.ApplyF<float>(this, render, "opacityFloor", nameof(opacityFloor));

        var transfer = AoHelper.G(o, "AO_transferSetting");
        AoHelper.ApplyF<float>(this, transfer, "advectionRate", nameof(advectionRate));
        AoHelper.ApplyF<float>(this, transfer, "diffusionRate", nameof(diffusionRate));
        AoHelper.ApplyF<int>(this, transfer, "diffusionIterations", nameof(diffusionIterations));
        AoHelper.ApplyF<float>(this, transfer, "naturalVanishRatePerTick", nameof(naturalVanishRatePerTick));
        AoHelper.ApplyF<float>(this, transfer, "roofedVanishFactor", nameof(roofedVanishFactor));
        AoHelper.ApplyF<float>(this, transfer, "unroofedRelativeVanishPerTick", nameof(unroofedRelativeVanishPerTick));
        AoHelper.ApplyF<float>(this, transfer, "extinctionDensityFraction", nameof(extinctionDensityFraction));
        AoHelper.ApplyF<float>(this, transfer, "vacuumVanishMultiplier", nameof(vacuumVanishMultiplier));
        AoHelper.ApplyF<bool>(this, transfer, "scheduled", nameof(scheduled));

        // `harmfulGases` 是"哪些气体算有害"的一次性静态列表，只在 == null 时重建。
        // 不清的话，把 harmfulDensity 从 0 改成正数这种最自然的操作**永远不生效**（只能重启游戏）。
        // 只在真正写过这一格时清：清缓存会让下一次伤害结算重新扫一遍全部气体。
        if (IsForceWriteField(nameof(harmfulDensity)) || StateOf(nameof(harmfulDensity)) != FieldState.Unset)
            AoHelper.ClearHarmfulGases();
    }

    // 只有一类：页签本身就是分类，复用页签标题那条 key，不新增重复定义。
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = ["MST.AtmosphereGas"];

    public override int GetPropType() => 0;
}
