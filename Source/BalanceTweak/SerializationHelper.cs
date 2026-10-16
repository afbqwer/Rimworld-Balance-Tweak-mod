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
/// <see cref="PocoCodec"/> 的字段表驱动 —— 字段声明与登记项都在
/// <c>TweakData.FieldCodecs.Build()</c> 的 <c>PocoList&lt;T&gt;(...)</c> 一行里。
/// 文本格式随之从 <c>值|值|值</c>（位置敏感）变成 <c>字段名=值|…</c>（自描述、值带转义）。
/// 本文件只保留这些类型的<b>列表级</b>入口（GUI 剪贴板要用）。
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
    /// 确保字段表已初始化。
    ///
    /// <para>
    /// <b>字段声明与登记项现在都在 <c>TweakData.FieldCodecs.Build()</c> 一处</b>
    /// （见那里的 <c>PocoList&lt;T&gt;(...)</c>）—— 加一个 POCO 列表类型只改那一行。
    /// 本静态构造只做一次触发，保证任何 <c>SerializationHelper.*</c> 调用
    /// （例如 GUI 的 <c>SerializeXxxList</c>）拿到的都是已声明的字段表。
    /// </para>
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
    static SerializationHelper() => _ = TweakData.FieldCodecs.All;

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


    #region IngredientCount（bespoke：挂在 ThingFilter + GetBaseCount/SetBaseCount 上）

    public static string? SerializeIngredientCountList(List<IngredientCount>? list)
    {
        if (list == null || list.Count == 0) return null;
        var lines = new List<string>();
        foreach (var ic in list)
        {
            var filterStr = IngredientFilterToString(ic.filter);
            if (filterStr.NullOrEmpty()) continue;
            // 数值一律 InvariantCulture + G9：与 PocoCodec 同一口径。
            // 用当前区域性写会让逗号小数点地区的 "1,5" 在换 locale 后读成 15 或读不出（前者静默错值）。
            lines.Add($"{filterStr}|{ic.GetBaseCount().ToString("G9", CultureInfo.InvariantCulture)}");
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
                if (!float.TryParse(countStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var count) || count <= 0)
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

    #region Int list

    public static string? SerializeIntList(List<int>? list)
        => SerializeList(list, i => i.ToString(CultureInfo.InvariantCulture));

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
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
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
        // x / y 走 InvariantCulture + G9，与 PocoCodec 的 FloatRange 同口径（原先用默认 ToString，口径其实不一致）。
        return string.Join("\n", curve.Points.Select(p =>
            $"{p.x.ToString("G9", CultureInfo.InvariantCulture)}|{p.y.ToString("G9", CultureInfo.InvariantCulture)}"));
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
                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
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
