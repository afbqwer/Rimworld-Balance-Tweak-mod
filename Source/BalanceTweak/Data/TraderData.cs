using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


/// <summary>
/// 商队/访客/轨道商人的类型（<see cref="TraderKindDef"/>）。
/// <para>
/// <c>stockGenerators</c>（库存生成器）是多态列表（原版 15 个子类、字段各不相同），字段直接持有
/// <see cref="StockGenerator"/> 对象，用 <see cref="ColumnStyle.StockGenList"/> 的结构化窗口增删改
/// （见 <c>StockGeneratorListEditorWindow</c>）：每行一个生成器，编辑时直接读写该对象的字段，
/// Apply 时把同一批对象交给 Def —— 中间<b>不</b>经过任何文本格式。
/// </para>
/// <para>
/// 文本只在两处出现，且都是 <see cref="StockGeneratorCodec"/> 的紧凑写法（非 XML、非反射遍历）：
/// 存档读写与跨行复制粘贴。
/// </para>
/// </summary>
[TweakFor(typeof(TraderKindDef), SettingType.Trader)]
class TraderData : TweakData<TraderData>
{
    [TweakField()]
    public float? commonality = null;

    [TweakField(Style = ColumnStyle.Bool, DataType = ColumnDataType.Display)]
    public bool? orbital = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? requestable = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? hideThingsNotWillingToTrade = null;

    [TweakField(Style = ColumnStyle.String, DataType = ColumnDataType.Display)]
    public string? category = null;

    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? commonalityMultFromPopulationIntent = null;

    [TweakField(Style = ColumnStyle.DefSelector, DataType = ColumnDataType.Display)]
    public FactionDef? faction = null;

    /// <summary>交易货币（银 / 荣誉点）。只有 Royalty 用得上，未装时不显示该列。</summary>
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TradeCurrency), MayRequire = "ludeon.rimworld.royalty", DataType = ColumnDataType.Display)]
    public TradeCurrency? tradeCurrency = null;

    [TweakField(Style = ColumnStyle.DefSelector, MayRequire = "ludeon.rimworld.royalty")]
    public RoyalTitlePermitDef? permitRequiredForTrading = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    /// <summary>库存生成器列表：直接持有生成器对象，窗口按字段编辑，Apply 原样写回 Def。</summary>
    [TweakField(Style = ColumnStyle.StockGenList)]
    public List<StockGenerator>? stockGenerators = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is TraderKindDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= d.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            commonality ??= d.commonality;
            orbital ??= d.orbital;
            requestable ??= d.requestable;
            hideThingsNotWillingToTrade ??= d.hideThingsNotWillingToTrade;
            category ??= d.category;
            commonalityMultFromPopulationIntent ??= d.commonalityMultFromPopulationIntent;
            faction ??= d.faction;
            tradeCurrency ??= d.tradeCurrency;
            permitRequiredForTrading ??= d.permitRequiredForTrading;
            // 已清空的字段不得被 Def 回填复活（L0 Cleared 语义，SC2 自检会报"复活"）。
            // 深克隆：字段里的对象归本实例所有，之后无论怎么改都不会碰到 Def 的活对象。
            if (!IsFieldModified(nameof(stockGenerators)))
                stockGenerators ??= StockGeneratorCodec.CloneAll(d.stockGenerators);
        }
    }

    public override void Apply()
    {
        if (this.def is not TraderKindDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<float>(nameof(commonality), v => def.commonality = v);
        ApplyV<bool>(nameof(orbital), v => def.orbital = v);
        ApplyV<bool>(nameof(requestable), v => def.requestable = v);
        ApplyV<bool>(nameof(hideThingsNotWillingToTrade), v => def.hideThingsNotWillingToTrade = v);
        ApplyS(nameof(category), v => def.category = v);
        ApplyV<SimpleCurve>(nameof(commonalityMultFromPopulationIntent), v => def.commonalityMultFromPopulationIntent = v);
        ApplyD<FactionDef>(nameof(faction), v => def.faction = v);
        ApplyV<TradeCurrency>(nameof(tradeCurrency), v => def.tradeCurrency = v);
        ApplyD<RoyalTitlePermitDef>(nameof(permitRequiredForTrading), v => def.permitRequiredForTrading = v);
        ApplyL<StockGenerator>(nameof(stockGenerators), ApplyStockGenerators);
    }

    /// <summary>
    /// 写入库存生成器。<paramref name="gens"/> 为 null（用户清空该字段）⇒ 清空库存列表；
    /// 否则原版生成器按字段表克隆后交给 Def，非原版（其它 Mod 新增）条目则按类型名从 Def 现有的
    /// 生成器里找回原实例原样放回（见 <see cref="StockGeneratorCodec.MergeWithDef"/>）—— 本工具从不读也不写
    /// 非原版生成器的字段，所以"改过这个商人"绝不会弄坏它们（Def 里找不到原实例的条目会被丢弃并告警，
    /// 不会写入默认值实例）。最后对每个生成器调一次 <see cref="StockGenerator.ResolveReferences"/>
    /// （<c>trader</c> 是 <c>[Unsaved]</c>，必须靠它写回），逐条隔离异常、失败只告警不中断。
    /// </summary>
    private void ApplyStockGenerators(List<StockGenerator>? gens)
    {
        if (this.def is not TraderKindDef def) return;

        if (gens.NullOrEmpty())
        {
            def.stockGenerators = new List<StockGenerator>();
            return;
        }

        var list = StockGeneratorCodec.MergeWithDef(gens, def.stockGenerators);
        if (list == null)
        {
            // gens 非空 ⇒ MergeWithDef 返回 null 只可能是"条目全被丢弃"（旧版这里不可达：那时会把空壳塞进 result）。
            Log.Warning($"[BalanceTweak] {id} 的库存生成器无可写回条目（条目都被丢弃，见上面的告警），已保持原有库存不变。");
            return;
        }
        def.stockGenerators = list;
        foreach (var gen in list)
        {
            // ResolveReferences 负责把 trader 写回（trader 是 [Unsaved]，只能靠它）。原版实现只有这一句赋值、
            // 全库没有任何子类覆写它，但 list 里可能有别的 Mod 的生成器（MergeWithDef 放回的原实例）——
            // 它们的覆写一旦抛异常，不能让整条链断掉：后面那条 StockGenerator_Slaves 会因此拿不到 trader
            // （全库唯一读点），运行时生成库存就 NRE。失败只告警，该条目保持 trader 为空。
            try { gen.ResolveReferences(def); }
            catch (Exception e)
            {
                Log.Error($"[BalanceTweak] {id} 的库存生成器 {StockGeneratorCodec.TypeLabel(gen.GetType())}"
                    + $" 解析引用失败（trader 引用会保持为空）：{e}");
            }
        }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(TraderType).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (this.def is TraderKindDef def)
        {
            if (def.orbital) return (int)TraderType.Orbital;
            if (!def.category.NullOrEmpty()) return (int)TraderType.Specialist;
        }
        return (int)TraderType.Standard;
    }

    private enum TraderType
    {
        Orbital,
        Specialist,
        Standard,
    }
}
