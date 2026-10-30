using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(ThingDef), SettingType.Stuff, Priority = 300, MatchMethod = nameof(DataUtility.MatchStuff))]
class StuffData : TweakData<StuffData>
{
    // 字段定义

    /// <summary>
    /// 该材料在随机生成里的份额（<c>StuffProperties.commonality</c>）。
    /// <para>
    /// 消费方：<c>ThingStuffPair.Commonality</c>（武器/服饰/战利品按它 × 价格加权）、
    /// <c>GenStuff.TryRandomStuffByCommonalityFor</c>、<c>GenStuff.RandomStuffInexpensiveFor</c>（BaseGen 建筑选材）、
    /// <c>ThingSetMakerUtility.TryGetRandomThingWhichCanWeighNoMoreThan</c>。
    /// </para>
    /// <para>
    /// 与 <see cref="deepCommonality"/> 无任何关系：那个是深钻井/地质扫描的储量权重，
    /// 这个是"随机生成时被抽中的概率"。两者互不影响，也可以一个为 0 另一个不为 0。
    /// </para>
    /// </summary>
    [TweakField(Style = ColumnStyle.Float)]
    public float? stuffCommonality = null;

    /// <summary>
    /// 是否参与随机生成（<c>StuffProperties.allowedInStuffGeneration</c>）。
    /// 关闭后 <c>GenStuff.AllowedStuffsFor</c> 会把它排除在随机选材之外
    /// （武器、服饰、战利品、BaseGen 建筑选材均受影响）；玩家手动指定材料建造不受限制。
    /// </summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? stuffAllowedInGeneration = null;

    /// <summary>
    /// 材料的科技等级（<c>ThingDef.techLevel</c>）。
    /// <para>
    /// 随机选材时 <c>GenStuff.AllowedStuffsFor(td, maxTechLevel)</c> 会按它过滤：材料等级高于势力
    /// （或物品集）等级就被排除。所以调高它 = 这种材料不再出现在低科技聚落、部落前哨、低阶战利品里；
    /// 调低则相反。配对表不缓存这一项，改完即时生效。
    /// </para>
    /// </summary>
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;

    /// <summary>
    /// 材料是否气密（<c>StuffProperties.isAirtight</c>）。只对带有
    /// <c>BuildingProperties.isStuffableAirtight</c> 的建筑生效（判定见 <c>Verse.Building.Stuff</c> 一侧），
    /// 影响那类建筑的室内外温度/真空隔离。
    /// </summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? stuffAirtight = null;

    [TweakField(StatDef = "MarketValue")]
    public float? marketValue = null;

    [TweakField(StatDef = "StuffPower_Armor_Sharp", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorSharp = null;
    [TweakField(StatDef = "StuffPower_Armor_Blunt", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorBlunt = null;
    [TweakField(StatDef = "StuffPower_Armor_Heat", Style = ColumnStyle.Float)]
    public float? stuffPowerArmorHeat = null;
    [TweakField(StatDef = "StuffPower_Insulation_Cold", Style = ColumnStyle.Float)]
    public float? stuffPowerInsulationCold = null;
    [TweakField(StatDef = "StuffPower_Insulation_Heat", Style = ColumnStyle.Float)]
    public float? stuffPowerInsulationHeat = null;
    [TweakField(StatDef = "SharpDamageMultiplier", Style = ColumnStyle.Prec)]
    public float? sharpDamageMultiplier = null;
    [TweakField(StatDef = "BluntDamageMultiplier", Style = ColumnStyle.Prec)]
    public float? bluntDamageMultiplier = null;
    [TweakField(StatDef = "MeleeWeapon_CooldownMultiplier", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffMeleeWeaponCooldownMultiplier = null;
    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffMaxHitPoints = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffFlammability = null;
    [TweakField(StatDef = "Beauty", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffBeauty = null;
    [TweakField(StatDef = "WorkToMake", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffWorkToMake = null;
    [TweakField(StatDef = "WorkToBuild", Style = ColumnStyle.Prec, IsStuffFactorStat = true)]
    public float? stuffWorkToBuild = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? deepCommonality = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? deepCountPerPortion = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? deepCountPerCell = null;
    [TweakField(Style = ColumnStyle.Range)]
    public IntRange? deepLumpSizeRange = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? thingSetMakerTags = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 100;



    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            stuffCommonality ??= d.stuffProps?.commonality;
            stuffAllowedInGeneration ??= d.stuffProps?.allowedInStuffGeneration;
            techLevel ??= d.techLevel;
            stuffAirtight ??= d.stuffProps?.isAirtight;
            deepCommonality ??= d.deepCommonality;
            deepCountPerPortion ??= d.deepCountPerPortion;
            deepCountPerCell ??= d.deepCountPerCell;
            deepLumpSizeRange ??= d.deepLumpSizeRange;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(thingSetMakerTags))) thingSetMakerTags ??= d.thingSetMakerTags;
            if (!IsFieldModified(nameof(statOffsets))) statOffsets ??= d.stuffProps?.statOffsets;
            if (!IsFieldModified(nameof(statFactors))) statFactors ??= d.stuffProps?.statFactors;
        }
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        // ApplyDefStats 末尾会把 forceResetStats 清零，判断"是否要重建随机生成缓存"必须在这之前取值。
        bool forcedReset = forceResetStats;
        ApplyDefLabel(nameof(defLabel), def);
        ApplyDefStats(def);
        ApplyL<string>(nameof(thingSetMakerTags), v => def.thingSetMakerTags = v);
        if (IsFieldModified(nameof(thingSetMakerTags))) DataUtility.RefreshThingSetMakerFilters();
        ApplyV<float>(nameof(deepCommonality), v => def.deepCommonality = v);
        ApplyV<int>(nameof(deepCountPerPortion), v => def.deepCountPerPortion = v);
        ApplyV<int>(nameof(deepCountPerCell), v => def.deepCountPerCell = v);
        ApplyV<IntRange>(nameof(deepLumpSizeRange), v => def.deepLumpSizeRange = v);

        // stuffProps 为 null 的行（MatchStuff 也会收进只有 deepCommonality、没有 stuffProps 的矿脉类 Def）
        // 没有可写目标，静默跳过 —— 与 ApplyDefStats 对 statOffsets / statFactors 的处理保持一致。
        var stuffProps = def.stuffProps;
        if (stuffProps != null)
        {
            ApplyV<float>(nameof(stuffCommonality), v => stuffProps.commonality = v);
            ApplyV<bool>(nameof(stuffAllowedInGeneration), v => stuffProps.allowedInStuffGeneration = v);
            ApplyV<bool>(nameof(stuffAirtight), v => stuffProps.isAirtight = v);
        }
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);

        var aspect = forcedReset ? DataUtility.StuffCacheAspect.All : RequestedCacheAspect();
        DataUtility.RefreshStuffGenerationCaches(def, aspect);
    }

    /// <summary>
    /// 这些字段既不出现在配对条目的缓存值里，也不参与归一化，改它们完全不必碰配对表。
    /// 其余字段（含以后新增的）默认按"要重建条目"处理 —— 默认取安全的一侧，免得加字段时漏改。
    /// </summary>
    private static readonly HashSet<string> CacheNeutralFields = new()
    {
        nameof(defLabel), nameof(thingSetMakerTags),
        nameof(deepCommonality), nameof(deepCountPerPortion),
        nameof(deepCountPerCell), nameof(deepLumpSizeRange),
        // 只影响选材时的过滤条件（AllowedStuffsFor 现算）或建筑判定，不参与配对表构建。
        nameof(techLevel), nameof(stuffAirtight), nameof(stuffAllowedInGeneration),
    };

    /// <summary>
    /// 按字段判定本次改动要碰配对表的哪一部分：
    /// commonality 只需重算归一化倍数，其余数值字段要重建条目（价格/保温等被烘进结构体）。
    /// </summary>
    private DataUtility.StuffCacheAspect RequestedCacheAspect()
    {
        var aspect = DataUtility.StuffCacheAspect.None;
        foreach (var meta in Fields)
        {
            string name = meta.Field.Name;
            if (!IsFieldModified(name)) continue;
            if (name == nameof(stuffCommonality)) aspect |= DataUtility.StuffCacheAspect.Commonality;
            else if (!CacheNeutralFields.Contains(name)) aspect |= DataUtility.StuffCacheAspect.PairStats;
        }
        return aspect;
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(StuffType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum StuffType
    {
        Metallic,
        Woody,
        Stony,
        Fabric,
        Leathery,
        Other
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.stuffProps == null)
            {
                return (int)StuffType.Other;
            }
            if (!def.stuffProps.categories.NullOrEmpty())
            {
                var f = def.stuffProps.categories.FirstOrDefault();
                if (f == StuffCategoryDefOf.Metallic) { return (int)StuffType.Metallic; }
                if (f == StuffCategoryDefOf.Woody) { return (int)StuffType.Woody; }
                if (f == StuffCategoryDefOf.Stony) { return (int)StuffType.Stony; }
                if (f == StuffCategoryDefOf.Fabric) { return (int)StuffType.Fabric; }
                if (f == StuffCategoryDefOf.Leathery) { return (int)StuffType.Leathery; }
            }
        }
        return (int)StuffType.Other;
    }
}


