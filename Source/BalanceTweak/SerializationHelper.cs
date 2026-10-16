using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 各类"小对象"与其列表的文本编解码。
///
/// <para>
/// <b>④ 第二步（2026-10-16）</b>：17 个纯数据类型的编解码已从"手写 <c>split('|')</c>"迁到
/// <see cref="PocoCodec"/> 的字段表驱动 —— 见下方静态构造里的声明。文本格式随之从
/// <c>值|值|值</c>（位置敏感）变成 <c>字段名=值|…</c>（自描述、值带转义）。
/// </para>
///
/// <para>
/// <b>仍然保留 bespoke（不走字段表）的</b>：
/// </para>
/// <list type="bullet">
/// <item><b><see cref="ThingFilter"/> / <see cref="IngredientCount"/></b> —— 前者是一整套机制
/// （反射读写私有字段 + <c>ResolveReferences</c> + <c>extraAllowed/DisallowedDefs</c> 差分补全），
/// 后者挂在 <c>GetBaseCount/SetBaseCount</c> 上而非普通字段；</item>
/// <item><b><see cref="SimpleCurve"/></b> —— 数据在 <c>Points</c>（<c>List&lt;CurvePoint&gt;</c>），
/// 而 <c>CurvePoint.loc</c> 是 <c>private Vector2</c>，字段表读写不了；</item>
/// <item><b><c>BodyPartNode</c> 树</b> —— 树形 + 行末 <c>customLabel</c> 需吸收其中可能出现的 <c>|</c>，
/// 是位置敏感的变长行，与字段表模型不同。</item>
/// </list>
/// </summary>
public static class SerializationHelper
{
    // ============================================================
    //  字段表（④ 第二步：手写编解码 → PocoCodec 字段表驱动）
    // ============================================================

    /// <summary>
    /// 声明各类型参与序列化的字段（<b>顺序即文本里的字段顺序</b>）。
    ///
    /// <para>
    /// <b>只声明"迁移前就参与序列化"的字段</b>，而不是原版实际拥有的全部字段 —— 这是刻意的：
    /// 本次是<b>机制迁移</b>，不是"顺带补字段覆盖"。原版有、但历来没被本工具读写的字段
    /// （<c>ThingDefCountClass.color/chance/quality</c>、<c>BiomeDiseaseRecord.biome</c>、
    /// <c>PawnCapacityModifier.setMaxCurveOverride</c>）继续保持不读写，要改另议。
    /// </para>
    ///
    /// <para>
    /// <b>文本格式变了</b>：从 <c>值|值|值</c>（位置敏感）变成 <c>字段名=值|字段名=值</c>（自描述），
    /// 且值里的分隔符会被转义。好处是原版若加字段或调整字段顺序，旧格式会<b>静默错位</b>，新格式不会。
    /// 代价是旧存档里这部分数据不再可读（本项目不考虑迁移）。
    /// </para>
    /// </summary>
    static SerializationHelper()
    {
        PocoCodec.Declare(typeof(StatModifier), "stat", "value");
        PocoCodec.Declare(typeof(PawnCapacityModifier),
            "capacity", "offset", "setMax", "postFactor", "statFactorMod", "setMaxCurveEvaluateStat");
        PocoCodec.Declare(typeof(DamageFactor), "damageDef", "factor");
        PocoCodec.Declare(typeof(SkillGain), "skill", "amount");
        PocoCodec.Declare(typeof(Aptitude), "skill", "level");
        PocoCodec.Declare(typeof(GeneticTraitData), "def", "degree");
        PocoCodec.Declare(typeof(ThingDefCountClass), "thingDef", "stuff", "count");
        PocoCodec.Declare(typeof(SkillRequirement), "skill", "minLevel");
        PocoCodec.Declare(typeof(TraitRequirement), "def", "degree");
        PocoCodec.Declare(typeof(BiomePlantRecord), "plant", "commonality");
        PocoCodec.Declare(typeof(BiomeAnimalRecord), "animal", "commonality");
        PocoCodec.Declare(typeof(FishChance), "fishDef", "chance");
        PocoCodec.Declare(typeof(BiomeDiseaseRecord), "diseaseInc", "commonality");
        PocoCodec.Declare(typeof(WeatherCommonalityRecord), "weather", "commonality");
        PocoCodec.Declare(typeof(TerrainThreshold), "terrain", "min", "max");
        PocoCodec.Declare(typeof(ProcessIngredientItem), "thing", "thingCategory", "disallowedThingDefs", "countNeeded");
        PocoCodec.Declare(typeof(ProcessResultItem), "thing", "count");

        // 字段表表达不了的**取值范围**约束（原来手写在各个 Parse 里）。
        // 没有这个钩子就只能二选一：放弃校验（开始接受以前拒绝的值），或把校验留在编解码里。
        PocoCodec.SetValidator(typeof(ThingDefCountClass), o => ((ThingDefCountClass)o).count >= 0);
        PocoCodec.SetValidator(typeof(ProcessResultItem), o => ((ProcessResultItem)o).count >= 0);
    }

    // ============================================================
    //  字段表驱动的编解码（上述 17 个类型共用同一形状）
    // ============================================================

    /// <summary>元素 → 一行文本（字段名=值 …）。</summary>
    private static string ToItem<T>(T value) where T : class => PocoCodec.Encode(value);

    /// <summary>一行文本 → 元素。任一步失败返回 <c>null</c>（调用方据此丢弃整条）。</summary>
    private static T? FromItem<T>(string str) where T : class
        => PocoCodec.TryDecode(str, typeof(T), out var value) && value is T item ? item : null;

    /// <summary>列表 → 多行文本；空列表返回 <c>null</c>。</summary>
    private static string? SerializeItems<T>(List<T>? list) where T : class => PocoCodec.EncodeList(list);

    /// <summary>多行文本 → 列表；任一行失败 ⇒ 整块 <c>null</c>（绝不写入半截数据）。</summary>
    private static List<T>? DeserializeItems<T>(string? data) where T : class => PocoCodec.DecodeList<T>(data);

    // ============================================================
    //  Generic list serialization helpers
    // ============================================================

    private static string? SerializeList<T>(List<T>? list, Func<T, string> toString)
    {
        if (list == null || list.Count == 0) return null;
        return string.Join("\n", list.Select(toString));
    }

    private static List<T>? DeserializeList<T>(string? data, Func<string, T?> parse, string errorLabel)
    {
        if (data.NullOrEmpty()) return null;
        try
        {
            var result = new List<T>();
            foreach (var line in data!.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.NullOrEmpty()) continue;
                var item = parse(trimmed);
                if (item == null) return null;
                result.Add(item);
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] {errorLabel} 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 记录“语法”解析失败（字段数量不符、数字无法解析等，通常意味着存档损坏或被手工改坏）。
    /// 注意：Def 引用解析失败属于预期情况（例如相关模组被移除），走 <see cref="LogMissingDef"/> 的 info 级日志。
    /// </summary>
    private static void WarnSyntaxError(string parseTarget, string? raw, string expected)
        => Log.Warning($"[BalanceTweak] {parseTarget} 语法解析失败：\"{raw}\"（期望格式：{expected}）");

    /// <summary>
    /// 记录 Def 引用解析失败（预期情况，例如相关模组被移除）：仅 info 级日志，不视为错误。
    /// </summary>
    internal static void LogMissingDef(string parseTarget, string? defName)
        => Log.Message($"[BalanceTweak] {parseTarget} 引用的 Def 缺失，已跳过：'{defName}'");

    // ============================================================
    //  以下 17 组：字段表驱动（④ 第二步迁移）
    // ============================================================

    #region StatModifier

    public static string StatModifierToItemString(StatModifier sm) => ToItem(sm);

    public static StatModifier? ParseStatModifier(string str) => FromItem<StatModifier>(str);

    public static string? SerializeStatModifierList(List<StatModifier>? list) => SerializeItems(list);

    public static List<StatModifier>? DeserializeStatModifierList(string? data) => DeserializeItems<StatModifier>(data);

    #endregion

    #region PawnCapacityModifier

    public static string PawnCapacityModifierToItemString(PawnCapacityModifier pcm) => ToItem(pcm);

    public static PawnCapacityModifier? ParsePawnCapacityModifier(string str) => FromItem<PawnCapacityModifier>(str);

    public static string? SerializePawnCapacityModifierList(List<PawnCapacityModifier>? list) => SerializeItems(list);

    public static List<PawnCapacityModifier>? DeserializePawnCapacityModifierList(string? data)
        => DeserializeItems<PawnCapacityModifier>(data);

    #endregion

    #region DamageFactor

    public static string DamageFactorToItemString(DamageFactor df) => ToItem(df);

    public static DamageFactor? ParseDamageFactor(string str) => FromItem<DamageFactor>(str);

    public static string? SerializeDamageFactorList(List<DamageFactor>? list) => SerializeItems(list);

    public static List<DamageFactor>? DeserializeDamageFactorList(string? data) => DeserializeItems<DamageFactor>(data);

    #endregion

    #region SkillGain

    public static string SkillGainToItemString(SkillGain sg) => ToItem(sg);

    public static SkillGain? ParseSkillGain(string str) => FromItem<SkillGain>(str);

    public static string? SerializeSkillGainList(List<SkillGain>? list) => SerializeItems(list);

    public static List<SkillGain>? DeserializeSkillGainList(string? data) => DeserializeItems<SkillGain>(data);

    #endregion

    #region Aptitude

    public static string AptitudeToItemString(Aptitude a) => ToItem(a);

    public static Aptitude? ParseAptitude(string str) => FromItem<Aptitude>(str);

    public static string? SerializeAptitudeList(List<Aptitude>? list) => SerializeItems(list);

    public static List<Aptitude>? DeserializeAptitudeList(string? data) => DeserializeItems<Aptitude>(data);

    #endregion

    #region GeneticTraitData

    public static string GeneticTraitDataToItemString(GeneticTraitData gtd) => ToItem(gtd);

    public static GeneticTraitData? ParseGeneticTraitData(string str) => FromItem<GeneticTraitData>(str);

    public static string? SerializeGeneticTraitDataList(List<GeneticTraitData>? list) => SerializeItems(list);

    public static List<GeneticTraitData>? DeserializeGeneticTraitDataList(string? data)
        => DeserializeItems<GeneticTraitData>(data);

    #endregion

    #region ThingDefCountClass

    public static string ThingDefCountClassToItemString(ThingDefCountClass tdc) => ToItem(tdc);

    public static ThingDefCountClass? ParseThingDefCountClass(string str) => FromItem<ThingDefCountClass>(str);

    public static string? SerializeThingDefCountClassList(List<ThingDefCountClass>? list) => SerializeItems(list);

    public static List<ThingDefCountClass>? DeserializeThingDefCountClassList(string? data)
        => DeserializeItems<ThingDefCountClass>(data);

    #endregion

    #region IngredientCount（bespoke：挂在 ThingFilter + GetBaseCount/SetBaseCount 上）

    public static string? SerializeIngredientCountList(List<IngredientCount>? list)
    {
        if (list == null || list.Count == 0) return null;
        var lines = new List<string>();
        foreach (var ic in list)
        {
            var filterStr = IngredientFilterToString(ic.filter);
            if (filterStr.NullOrEmpty()) continue;
            lines.Add($"{filterStr}|{ic.GetBaseCount()}");
        }
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    public static List<IngredientCount>? DeserializeIngredientCountList(string? data)
    {
        if (data.NullOrEmpty()) return null;
        try
        {
            var result = new List<IngredientCount>();
            foreach (var line in data!.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.NullOrEmpty()) continue;
                var pipeIdx = trimmed.LastIndexOf('|');
                if (pipeIdx < 0)
                {
                    WarnSyntaxError("IngredientCount", trimmed, "filter|count");
                    return null;
                }
                var filterStr = trimmed[..pipeIdx];
                var countStr = trimmed[(pipeIdx + 1)..];
                if (!float.TryParse(countStr, out var count) || count <= 0)
                {
                    WarnSyntaxError("IngredientCount", trimmed, "filter|count（count 应为正浮点数）");
                    return null;
                }
                var ic = new IngredientCount();
                ic.SetBaseCount(count);
                var filter = ParseIngredientFilter(filterStr);
                if (filter == null) return null;
                ic.filter.CopyAllowancesFrom(filter);
                if (ic.filter.AllowedDefCount == 0) return null; // 过滤器解析后无任何允许项：跳过（缺失的 Def 已在 ParseIngredientFilter 中记录）
                result.Add(ic);
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] DeserializeIngredientCountList 失败: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region SkillRequirement

    public static string SkillRequirementToItemString(SkillRequirement sr) => ToItem(sr);

    public static SkillRequirement? ParseSkillRequirement(string str) => FromItem<SkillRequirement>(str);

    public static string? SerializeSkillRequirementList(List<SkillRequirement>? list) => SerializeItems(list);

    public static List<SkillRequirement>? DeserializeSkillRequirementList(string? data)
        => DeserializeItems<SkillRequirement>(data);

    #endregion

    #region TraitRequirement

    public static string TraitRequirementToItemString(TraitRequirement tr) => ToItem(tr);

    public static TraitRequirement? ParseTraitRequirement(string str) => FromItem<TraitRequirement>(str);

    public static string? SerializeTraitRequirementList(List<TraitRequirement>? list) => SerializeItems(list);

    public static List<TraitRequirement>? DeserializeTraitRequirementList(string? data)
        => DeserializeItems<TraitRequirement>(data);

    #endregion

    #region BiomePlantRecord

    public static string BiomePlantRecordToItemString(BiomePlantRecord r) => ToItem(r);

    public static BiomePlantRecord? ParseBiomePlantRecord(string str) => FromItem<BiomePlantRecord>(str);

    public static string? SerializeBiomePlantList(List<BiomePlantRecord>? list) => SerializeItems(list);

    public static List<BiomePlantRecord>? DeserializeBiomePlantList(string? data)
        => DeserializeItems<BiomePlantRecord>(data);

    #endregion

    #region BiomeAnimalRecord

    public static string BiomeAnimalRecordToItemString(BiomeAnimalRecord r) => ToItem(r);

    public static BiomeAnimalRecord? ParseBiomeAnimalRecord(string str) => FromItem<BiomeAnimalRecord>(str);

    public static string? SerializeBiomeAnimalList(List<BiomeAnimalRecord>? list) => SerializeItems(list);

    public static List<BiomeAnimalRecord>? DeserializeBiomeAnimalList(string? data)
        => DeserializeItems<BiomeAnimalRecord>(data);

    #endregion

    #region FishChance

    public static string FishChanceToItemString(FishChance f) => ToItem(f);

    public static FishChance? ParseFishChance(string str) => FromItem<FishChance>(str);

    public static string? SerializeFishChanceList(List<FishChance>? list) => SerializeItems(list);

    public static List<FishChance>? DeserializeFishChanceList(string? data) => DeserializeItems<FishChance>(data);

    #endregion

    #region BiomeDiseaseRecord

    public static string BiomeDiseaseRecordToItemString(BiomeDiseaseRecord r) => ToItem(r);

    public static BiomeDiseaseRecord? ParseBiomeDiseaseRecord(string str) => FromItem<BiomeDiseaseRecord>(str);

    public static string? SerializeDiseaseList(List<BiomeDiseaseRecord>? list) => SerializeItems(list);

    public static List<BiomeDiseaseRecord>? DeserializeDiseaseList(string? data)
        => DeserializeItems<BiomeDiseaseRecord>(data);

    #endregion

    #region WeatherCommonalityRecord

    public static string WeatherCommonalityRecordToItemString(WeatherCommonalityRecord r) => ToItem(r);

    public static WeatherCommonalityRecord? ParseWeatherCommonalityRecord(string str)
        => FromItem<WeatherCommonalityRecord>(str);

    public static string? SerializeWeatherCommonalityList(List<WeatherCommonalityRecord>? list) => SerializeItems(list);

    public static List<WeatherCommonalityRecord>? DeserializeWeatherCommonalityList(string? data)
        => DeserializeItems<WeatherCommonalityRecord>(data);

    #endregion

    #region TerrainThreshold

    public static string TerrainThresholdToItemString(TerrainThreshold t) => ToItem(t);

    public static TerrainThreshold? ParseTerrainThreshold(string str) => FromItem<TerrainThreshold>(str);

    public static string? SerializeTerrainThresholdList(List<TerrainThreshold>? list) => SerializeItems(list);

    public static List<TerrainThreshold>? DeserializeTerrainThresholdList(string? data)
        => DeserializeItems<TerrainThreshold>(data);

    #endregion

    #region ProcessIngredientItem

    public static string ProcessIngredientItemToItemString(ProcessIngredientItem i) => ToItem(i);

    public static ProcessIngredientItem? ParseProcessIngredientItem(string str) => FromItem<ProcessIngredientItem>(str);

    public static string? SerializeProcessIngredientItemList(List<ProcessIngredientItem>? list) => SerializeItems(list);

    public static List<ProcessIngredientItem>? DeserializeProcessIngredientItemList(string? data)
        => DeserializeItems<ProcessIngredientItem>(data);

    #endregion

    #region ProcessResultItem

    public static string ProcessResultItemToItemString(ProcessResultItem r) => ToItem(r);

    public static ProcessResultItem? ParseProcessResultItem(string str) => FromItem<ProcessResultItem>(str);

    public static string? SerializeProcessResultItemList(List<ProcessResultItem>? list) => SerializeItems(list);

    public static List<ProcessResultItem>? DeserializeProcessResultItemList(string? data)
        => DeserializeItems<ProcessResultItem>(data);

    #endregion

    #region String list

    public static string? SerializeStringList(List<string>? list)
        => SerializeList(list, s => s);

    public static List<string>? DeserializeStringList(string? data)
        => DeserializeList(data, s => s, "DeserializeStringList");

    #endregion

    #region Int list

    public static string? SerializeIntList(List<int>? list)
        => SerializeList(list, i => i.ToString());

    public static List<int>? DeserializeIntList(string? data)
    {
        if (data.NullOrEmpty()) return null;
        try
        {
            var result = new List<int>();
            foreach (var line in data!.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.NullOrEmpty()) continue;
                if (!int.TryParse(trimmed, out var val))
                {
                    WarnSyntaxError("IntList", trimmed, "每行一个整数");
                    return null;
                }
                result.Add(val);
            }
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] DeserializeIntList 失败: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Def list

    public static string? SerializeDefList<T>(List<T>? list) where T : Def
        => SerializeList(list, d => d.defName);

    public static List<T>? DeserializeDefList<T>(string? data) where T : Def
        => DeserializeList(data, s => DefDatabase<T>.GetNamedSilentFail(s), "DeserializeDefList");

    #endregion

    // ============================================================
    //  Simple value serialization helpers
    // ============================================================

    #region SimpleCurve（bespoke：数据在 Points，而 CurvePoint.loc 是 private Vector2）

    public static string? SerializeSimpleCurve(SimpleCurve? curve)
    {
        if (curve == null || curve.PointsCount == 0) return null;
        return string.Join("\n", curve.Points.Select(p => $"{p.x}|{p.y}"));
    }

    public static SimpleCurve? DeserializeSimpleCurve(string? data)
    {
        if (data.NullOrEmpty()) return null;
        try
        {
            var curve = new SimpleCurve();
            foreach (var line in data!.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.NullOrEmpty()) continue;
                var parts = trimmed.Split('|');
                if (parts.Length != 2)
                {
                    WarnSyntaxError("SimpleCurve", trimmed, "x|y");
                    return null;
                }
                if (!float.TryParse(parts[0], out var x) || !float.TryParse(parts[1], out var y))
                {
                    WarnSyntaxError("SimpleCurve", trimmed, "x|y（x、y 应为浮点数）");
                    return null;
                }
                curve.Add(x, y);
            }
            return curve.PointsCount > 0 ? curve : null;
        }
        catch (Exception ex)
        {
            Log.Error($"[BalanceTweak] DeserializeSimpleCurve 失败: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region ThingFilter（bespoke：反射读写私有字段 + ResolveReferences + 差分补全）

    public static string? SerializeThingFilter(ThingFilter? filter)
    {
        return IngredientFilterToString(filter);
    }

    public static ThingFilter? DeserializeThingFilter(string? data)
    {
        return ParseIngredientFilter(data);
    }

    public static readonly FieldInfo TfCategoriesField = Reflect.Field(typeof(ThingFilter), "categories");
    public static readonly FieldInfo TfDisallowedCategoriesField = Reflect.Field(typeof(ThingFilter), "disallowedCategories");
    public static readonly FieldInfo TfThingDefsField = Reflect.Field(typeof(ThingFilter), "thingDefs");
    public static readonly FieldInfo TfSpecialFiltersToAllowField = Reflect.Field(typeof(ThingFilter), "specialFiltersToAllow");
    public static readonly FieldInfo TfSpecialFiltersToDisallowField = Reflect.Field(typeof(ThingFilter), "specialFiltersToDisallow");
    public static readonly FieldInfo TfStuffCategoriesToAllowField = Reflect.Field(typeof(ThingFilter), "stuffCategoriesToAllow");

    public static readonly FieldInfo TfTradeTagsToAllowField = Reflect.Field(typeof(ThingFilter), "tradeTagsToAllow");
    public static readonly FieldInfo TfTradeTagsToDisallowField = Reflect.Field(typeof(ThingFilter), "tradeTagsToDisallow");
    public static readonly FieldInfo TfThingSetMakerTagsToAllowField = Reflect.Field(typeof(ThingFilter), "thingSetMakerTagsToAllow");
    public static readonly FieldInfo TfThingSetMakerTagsToDisallowField = Reflect.Field(typeof(ThingFilter), "thingSetMakerTagsToDisallow");
    public static readonly FieldInfo TfAllowAllWhoCanMakeField = Reflect.Field(typeof(ThingFilter), "allowAllWhoCanMake");
    public static readonly FieldInfo TfDisallowedThingDefsField = Reflect.Field(typeof(ThingFilter), "disallowedThingDefs");

    public static readonly FieldInfo TfAllowedDefsField = Reflect.Field(typeof(ThingFilter), "allowedDefs");

    public static void CopyFieldsTo(ThingFilter source, ThingFilter target)
    {
        TfCategoriesField?.SetValue(target, TfCategoriesField.GetValue(source));
        TfDisallowedCategoriesField?.SetValue(target, TfDisallowedCategoriesField.GetValue(source));
        TfThingDefsField?.SetValue(target, TfThingDefsField.GetValue(source));
        TfSpecialFiltersToAllowField?.SetValue(target, TfSpecialFiltersToAllowField.GetValue(source));
        TfSpecialFiltersToDisallowField?.SetValue(target, TfSpecialFiltersToDisallowField.GetValue(source));
        TfStuffCategoriesToAllowField?.SetValue(target, TfStuffCategoriesToAllowField.GetValue(source));
        TfTradeTagsToAllowField?.SetValue(target, TfTradeTagsToAllowField.GetValue(source));
        TfTradeTagsToDisallowField?.SetValue(target, TfTradeTagsToDisallowField.GetValue(source));
        TfThingSetMakerTagsToAllowField?.SetValue(target, TfThingSetMakerTagsToAllowField.GetValue(source));
        TfThingSetMakerTagsToDisallowField?.SetValue(target, TfThingSetMakerTagsToDisallowField.GetValue(source));
        TfAllowAllWhoCanMakeField?.SetValue(target, TfAllowAllWhoCanMakeField.GetValue(source));
        TfDisallowedThingDefsField?.SetValue(target, TfDisallowedThingDefsField.GetValue(source));
    }

    public static string IngredientFilterToString(ThingFilter? filter)
    {
        if (filter == null) return "";
        var parts = new List<string>();

        var categories = TfCategoriesField?.GetValue(filter) as List<string>;
        if (categories?.Count > 0)
            parts.Add("categories=" + string.Join(",", categories));

        var thingDefs = TfThingDefsField?.GetValue(filter) as List<ThingDef>;
        if (thingDefs?.Count > 0)
            parts.Add("thingDefs=" + string.Join(",", thingDefs.Select(d => d.defName)));

        var disallowedCategories = TfDisallowedCategoriesField?.GetValue(filter) as List<string>;
        if (disallowedCategories?.Count > 0)
            parts.Add("disallowedCategories=" + string.Join(",", disallowedCategories));

        var specialFiltersToAllow = TfSpecialFiltersToAllowField?.GetValue(filter) as List<string>;
        if (specialFiltersToAllow?.Count > 0)
            parts.Add("specialFiltersToAllow=" + string.Join(",", specialFiltersToAllow));

        var specialFiltersToDisallow = TfSpecialFiltersToDisallowField?.GetValue(filter) as List<string>;
        if (specialFiltersToDisallow?.Count > 0)
            parts.Add("specialFiltersToDisallow=" + string.Join(",", specialFiltersToDisallow));

        var stuffCategoriesToAllow = TfStuffCategoriesToAllowField?.GetValue(filter) as List<StuffCategoryDef>;
        if (stuffCategoriesToAllow?.Count > 0)
            parts.Add("stuffCategoriesToAllow=" + string.Join(",", stuffCategoriesToAllow.Select(d => d.defName)));

        var tradeTagsToAllow = TfTradeTagsToAllowField?.GetValue(filter) as List<string>;
        if (tradeTagsToAllow?.Count > 0)
            parts.Add("tradeTagsToAllow=" + string.Join(",", tradeTagsToAllow));

        var tradeTagsToDisallow = TfTradeTagsToDisallowField?.GetValue(filter) as List<string>;
        if (tradeTagsToDisallow?.Count > 0)
            parts.Add("tradeTagsToDisallow=" + string.Join(",", tradeTagsToDisallow));

        var thingSetMakerTagsToAllow = TfThingSetMakerTagsToAllowField?.GetValue(filter) as List<string>;
        if (thingSetMakerTagsToAllow?.Count > 0)
            parts.Add("thingSetMakerTagsToAllow=" + string.Join(",", thingSetMakerTagsToAllow));

        var thingSetMakerTagsToDisallow = TfThingSetMakerTagsToDisallowField?.GetValue(filter) as List<string>;
        if (thingSetMakerTagsToDisallow?.Count > 0)
            parts.Add("thingSetMakerTagsToDisallow=" + string.Join(",", thingSetMakerTagsToDisallow));

        var allowAllWhoCanMake = TfAllowAllWhoCanMakeField?.GetValue(filter) as List<ThingDef>;
        if (allowAllWhoCanMake?.Count > 0)
            parts.Add("allowAllWhoCanMake=" + string.Join(",", allowAllWhoCanMake.Select(d => d.defName)));

        var disallowedThingDefs = TfDisallowedThingDefsField?.GetValue(filter) as List<ThingDef>;
        if (disallowedThingDefs?.Count > 0)
            parts.Add("disallowedThingDefs=" + string.Join(",", disallowedThingDefs.Select(d => d.defName)));

        // Compute and serialize extra allowed/disallowed defs from allowedDefs HashSet
        // that cannot be represented by the field lists alone.
        var fieldOnly = new ThingFilter();
        CopyFieldsTo(filter, fieldOnly);
        fieldOnly.ResolveReferences();

        var extraAllowed = new List<string>();
        var extraDisallowed = new List<string>();
        var filterAllowed = TfAllowedDefsField?.GetValue(filter) as HashSet<ThingDef>;
        var fieldOnlyAllowed = TfAllowedDefsField?.GetValue(fieldOnly) as HashSet<ThingDef>;
        var checkedDefs = new HashSet<ThingDef>();
        if (filterAllowed != null) checkedDefs.UnionWith(filterAllowed);
        if (fieldOnlyAllowed != null) checkedDefs.UnionWith(fieldOnlyAllowed);
        foreach (var def in checkedDefs)
        {
            bool fieldAllows = fieldOnly.Allows(def);
            bool filterAllows = filter.Allows(def);
            if (filterAllows && !fieldAllows)
                extraAllowed.Add(def.defName);
            else if (fieldAllows && !filterAllows)
                extraDisallowed.Add(def.defName);
        }
        if (extraAllowed.Count > 0)
            parts.Add("extraAllowedDefs=" + string.Join(",", extraAllowed));
        if (extraDisallowed.Count > 0)
            parts.Add("extraDisallowedDefs=" + string.Join(",", extraDisallowed));

        return parts.Count > 0 ? string.Join("; ", parts) : "";
    }

    public static ThingFilter? ParseIngredientFilter(string? text)
    {
        if (text == null) return null;
        var filter = new ThingFilter();
        if (text.NullOrEmpty()) return filter;
        var segments = text.Split(';').Select(s => s.Trim()).Where(s => !s.NullOrEmpty());

        var extraAllowed = new List<string>();
        var extraDisallowed = new List<string>();

        foreach (var segment in segments)
        {
            var colonIdx = segment.IndexOf('=');
            if (colonIdx < 0)
            {
                WarnSyntaxError("ThingFilter", segment, "key=value");
                continue;
            }
            var key = segment[..colonIdx].Trim();
            var value = segment[(colonIdx + 1)..].Trim();
            var items = value.Split(',').Select(s => s.Trim()).Where(s => !s.NullOrEmpty()).ToList();

            switch (key.ToLowerInvariant())
            {
                case "categories":
                    TfCategoriesField?.SetValue(filter, items);
                    break;
                case "thingdefs":
                    var thingDefs = new List<ThingDef>();
                    foreach (var defName in items)
                    {
                        var td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (td != null) thingDefs.Add(td);
                        else LogMissingDef("ThingFilter.thingDefs", defName);
                    }
                    if (thingDefs.Count > 0)
                        TfThingDefsField?.SetValue(filter, thingDefs);
                    break;
                case "disallowedcategories":
                    TfDisallowedCategoriesField?.SetValue(filter, items);
                    break;
                case "specialfilterstoallow":
                    TfSpecialFiltersToAllowField?.SetValue(filter, items);
                    break;
                case "specialfilterstodisallow":
                    TfSpecialFiltersToDisallowField?.SetValue(filter, items);
                    break;
                case "stuffcategoriestoallow":
                    var stuffCats = new List<StuffCategoryDef>();
                    foreach (var defName in items)
                    {
                        var scd = DefDatabase<StuffCategoryDef>.GetNamedSilentFail(defName);
                        if (scd != null) stuffCats.Add(scd);
                        else LogMissingDef("ThingFilter.stuffCategoriesToAllow", defName);
                    }
                    if (stuffCats.Count > 0)
                        TfStuffCategoriesToAllowField?.SetValue(filter, stuffCats);
                    break;
                case "tradetagstoallow":
                    TfTradeTagsToAllowField?.SetValue(filter, items);
                    break;
                case "tradetagstodisallow":
                    TfTradeTagsToDisallowField?.SetValue(filter, items);
                    break;
                case "thingsetmakertagstoallow":
                    TfThingSetMakerTagsToAllowField?.SetValue(filter, items);
                    break;
                case "thingsetmakertagstodisallow":
                    TfThingSetMakerTagsToDisallowField?.SetValue(filter, items);
                    break;
                case "allowallwhocanmake":
                    var allowList = new List<ThingDef>();
                    foreach (var defName in items)
                    {
                        var td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (td != null) allowList.Add(td);
                        else LogMissingDef("ThingFilter.allowAllWhoCanMake", defName);
                    }
                    if (allowList.Count > 0)
                        TfAllowAllWhoCanMakeField?.SetValue(filter, allowList);
                    break;
                case "disallowedthingdefs":
                    var disallowList = new List<ThingDef>();
                    foreach (var defName in items)
                    {
                        var td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (td != null) disallowList.Add(td);
                        else LogMissingDef("ThingFilter.disallowedThingDefs", defName);
                    }
                    if (disallowList.Count > 0)
                        TfDisallowedThingDefsField?.SetValue(filter, disallowList);
                    break;
                case "extraalloweddefs":
                    extraAllowed.AddRange(items);
                    break;
                case "extradisalloweddefs":
                    extraDisallowed.AddRange(items);
                    break;
            }
        }

        // Resolve all field lists into allowedDefs, then apply extras
        filter.ResolveReferences();
        foreach (var defName in extraAllowed)
        {
            var td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td != null) filter.SetAllow(td, true);
            else LogMissingDef("ThingFilter.extraAllowedDefs", defName);
        }
        foreach (var defName in extraDisallowed)
        {
            var td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (td != null) filter.SetAllow(td, false);
            else LogMissingDef("ThingFilter.extraDisallowedDefs", defName);
        }

        return filter;
    }

    public static List<string>? GetThingFilterCategories(ThingFilter filter)
    {
        return TfCategoriesField?.GetValue(filter) as List<string>;
    }

    public static List<StuffCategoryDef>? GetThingFilterStuffCategories(ThingFilter filter)
    {
        return TfStuffCategoriesToAllowField?.GetValue(filter) as List<StuffCategoryDef>;
    }

    public static List<ThingDef>? GetThingFilterThingDefs(ThingFilter filter)
    {
        return TfThingDefsField?.GetValue(filter) as List<ThingDef>;
    }

    #endregion

    // ============================================================
    //  BodyPart tree (BodyDef.corePart)
    // ============================================================

    #region BodyPart tree（bespoke：树形 + 行末 customLabel 吸收 '|'）

    /// <summary>
    /// 把 <see cref="BodyPartNode"/> 编码为一行字符串。字段以 <c>'|'</c> 分隔：
    /// <c>depth | defName | (int)height | (int)depth | coverage | groups(逗号连接的 defName) | customLabel</c>。
    ///
    /// 分隔符必须是 **XML 合法字符**：存档经 ScribeSaver（<c>XmlWriter</c>，CheckCharacters=true）写入，
    /// 用 <c>\u001F</c>/<c>\u001E</c> 等控制字符会在保存时抛 <c>ArgumentException</c> 并损坏存档。
    /// <c>customLabel</c> 是唯一可能含任意字符的字段，故放在**行末**，由
    /// <see cref="TryParseBodyPartNodeLine"/> 的 <c>Split(..., 7)</c> 吸收其中可能出现的 <c>'|'</c>。
    /// </summary>
    public static string BodyPartNodeToItemString(BodyPartNode node, int depth)
    {
        var groups = node.groups == null
            ? ""
            : string.Join(",", node.groups.Where(g => g != null).Select(g => g.defName));
        return string.Join("|",
            depth.ToString(CultureInfo.InvariantCulture),
            node.def?.defName ?? "",
            ((int)node.height).ToString(CultureInfo.InvariantCulture),
            ((int)node.depth).ToString(CultureInfo.InvariantCulture),
            node.coverage.ToString(CultureInfo.InvariantCulture),
            groups,
            node.customLabel ?? "");
    }

    /// <summary>解析 <see cref="BodyPartNodeToItemString"/> 产出的行。失败返回 false。</summary>
    public static bool TryParseBodyPartNodeLine(string line, out int depth, out BodyPartNode? node)
    {
        depth = 0;
        node = null;
        if (line.NullOrEmpty()) return false;

        var parts = line.Split(new[] { '|' }, 7);
        if (parts.Length < 7)
        {
            WarnSyntaxError("BodyPartNode", line, "depth|defName|height|depth|coverage|groups|customLabel");
            return false;
        }
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out depth))
        {
            WarnSyntaxError("BodyPartNode", line, "depth 应为整数");
            return false;
        }

        // Def 引用解析失败：记录 info 级日志后跳过（例如相关模组被移除）
        var def = parts[1].NullOrEmpty() ? null : DefDatabase<BodyPartDef>.GetNamedSilentFail(parts[1]);
        if (def == null)
        {
            if (!parts[1].NullOrEmpty()) LogMissingDef("BodyPartNode.def", parts[1]);
            return false;
        }

        if (!Enum.TryParse(parts[2], out BodyPartHeight h))
        {
            WarnSyntaxError("BodyPartNode", line, "height 枚举值无效");
            h = BodyPartHeight.Undefined;
        }
        if (!Enum.TryParse(parts[3], out BodyPartDepth d))
        {
            WarnSyntaxError("BodyPartNode", line, "depth 枚举值无效");
            d = BodyPartDepth.Undefined;
        }
        if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float cov))
        {
            WarnSyntaxError("BodyPartNode", line, "coverage 应为浮点数");
            cov = 1f;
        }

        node = new BodyPartNode
        {
            def = def,
            height = h,
            depth = d,
            coverage = cov,
            groups = parts[5].Length == 0
                ? new List<BodyPartGroupDef>()
                : parts[5].Split(',')
                    .Select(n =>
                    {
                        var g = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(n);
                        if (g == null) LogMissingDef("BodyPartNode.groups", n);
                        return g;
                    })
                    .Where(g => g != null)
                    .Cast<BodyPartGroupDef>()
                    .ToList(),
            customLabel = parts[6].Length == 0 ? null : parts[6],
        };
        return true;
    }

    /// <summary>整棵树（前缀序遍历）编码为 <c>List&lt;string&gt;</c>。<paramref name="root"/> 为 null 时返回 null。</summary>
    public static List<string>? SerializeBodyPartTree(BodyPartNode? root)
    {
        if (root == null) return null;
        var lines = new List<string>();
        SerializeBodyPartTreeRecursive(root, 0, lines);
        return lines;
    }

    private static void SerializeBodyPartTreeRecursive(BodyPartNode node, int depth, List<string> lines)
    {
        lines.Add(BodyPartNodeToItemString(node, depth));
        if (node.children == null) return;
        foreach (var child in node.children)
        {
            if (child == null) continue;
            SerializeBodyPartTreeRecursive(child, depth + 1, lines);
        }
    }

    /// <summary><c>List&lt;string&gt;</c>（前缀序）还原为整棵树；按 depth 用栈重建父子关系。</summary>
    public static BodyPartNode? DeserializeBodyPartTree(List<string>? lines)
    {
        if (lines == null || lines.Count == 0) return null;

        BodyPartNode? root = null;
        var stack = new List<BodyPartNode>();
        foreach (var line in lines)
        {
            if (!TryParseBodyPartNodeLine(line, out int depth, out var node) || node == null) continue;

            // 根节点：仅接受第一个 depth<=0 的节点
            if (depth <= 0 || stack.Count == 0)
            {
                if (root == null)
                {
                    root = node;
                    stack.Clear();
                    stack.Add(node);
                }
                continue;
            }

            if (depth > stack.Count) depth = stack.Count; // 容错：跳过中间层级
            stack[depth - 1].children.Add(node);
            if (stack.Count > depth) stack.RemoveRange(depth, stack.Count - depth);
            stack.Add(node);
        }
        return root;
    }

    #endregion
}
