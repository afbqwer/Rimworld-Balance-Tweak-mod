using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 库存生成器字段的"表单"渲染：按字段表声明的方式（<see cref="PocoCodec.Field.Kind"/>）画控件。
///
/// <para>
/// 标量走内联控件（文本框 / 复选框 / 下拉菜单）；Def 引用、Def 列表、字符串列表、小对象列表复用现有
/// 编辑窗口（<see cref="DefSelectionWindow"/>、<see cref="DefListEditorWindow"/>、<see cref="StringListEditorWindow"/>、
/// <see cref="SGComplexListEditorWindow"/>）—— 这些窗口只认"对象 + 字段表"，不依赖 <c>TweakData</c>，
/// 因此可以脱离主表格直接复用。
/// </para>
/// <para>
/// 读写统一走 <see cref="StockGeneratorCodec.ReadField"/> / <see cref="StockGeneratorCodec.WriteField"/>；
/// 哪些字段可编辑由 <see cref="StockGeneratorCodec.FieldsFor"/> 决定（非原版子类返回空集 —— 本工具对它们
/// 只做只读展示，列表窗口里连编辑窗口都不给开）。
/// </para>
/// </summary>
internal static class StockGeneratorFieldUi
{
    /// <summary>表单里一行的标准高度。</summary>
    public const float RowHeight = 26f;

    /// <summary>
    /// 字段名 → 本地化 key（键名沿用字段名，如 <c>countRange</c> → <c>MST.countRange</c>，
    /// 与 <c>[TweakField]</c> 的列标题键约定一致，因此同名键可以共用一份翻译）。
    /// <para>
    /// 特意写成显式字面量而不是拼 <c>"MST." + 字段名</c>：CheckLoc 的缺失 / 多余键扫描是静态正则，
    /// 拼接出来的键它看不见，会把 Trader.xml 里这些键全判成"多余"。表里没有的字段（例如其它 Mod
    /// 新增的生成器字段）直接显示 C# 字段名，不翻译。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> FieldLabelKeys = new(StringComparer.Ordinal)
    {
        { "countRange", "MST.countRange" },
        { "customCountRanges", "MST.customCountRanges" },
        { "totalPriceRange", "MST.totalPriceRange" },
        { "maxTechLevelGenerate", "MST.maxTechLevelGenerate" },
        { "maxTechLevelBuy", "MST.maxTechLevelBuy" },
        { "price", "MST.price" },
        { "minValuePerUnit", "MST.minValuePerUnit" },
        { "thingDef", "MST.thingDef" },
        { "thingDefs", "MST.thingDefs" },
        { "thingDefCountRange", "MST.thingDefCountRange" },
        { "tag", "MST.tag" },
        { "tradeTag", "MST.tradeTag" },
        { "apparelTag", "MST.apparelTag" },
        { "weaponTag", "MST.weaponTag" },
        { "tradeTagsSell", "MST.tradeTagsSell" },
        { "tradeTagsBuy", "MST.tradeTagsBuy" },
        { "createMatingPair", "MST.createMatingPair" },
        { "kindCountRange", "MST.kindCountRange" },
        { "minWildness", "MST.minWildness" },
        { "maxWildness", "MST.maxWildness" },
        { "checkTemperature", "MST.checkTemperature" },
        { "categoryDef", "MST.categoryDef" },
        { "excludedCategories", "MST.excludedCategories" },
        { "excludedThingDefs", "MST.excludedThingDefs" },
        { "respectPopulationIntent", "MST.respectPopulationIntent" },
        { "slaveKindDef", "MST.slaveKindDef" },
        { "countChances", "MST.countChances" },
        { "count", "MST.count" },
        { "chance", "MST.chance" },
    };

    /// <summary>已本地化的字段名（缺翻译时退化为 C# 字段名）。</summary>
    public static string LabelOf(PocoCodec.Field field)
        => FieldLabelKeys.TryGetValue(field.Name, out var key) && key.TryTranslate(out var text) ? text : field.Name;

    /// <summary>画出 <paramref name="field"/> 在 <paramref name="target"/> 上的编辑控件（直接写回 target）。</summary>
    public static void DrawField(Rect rect, PocoCodec.Field field, object target, EditorWindowBase owner)
    {
        var current = StockGeneratorCodec.ReadField(field, target);

        Text.Anchor = TextAnchor.MiddleLeft;
        switch (field.Kind)
        {
            case PocoCodec.Kind.Float:
            case PocoCodec.Kind.Int:
                DrawNumeric(rect, field, target, current);
                break;
            case PocoCodec.Kind.Bool:
                DrawBool(rect, field, target, current);
                break;
            case PocoCodec.Kind.Text:
                DrawText(rect, field, target, current);
                break;
            case PocoCodec.Kind.IntRange:
                DrawRange(rect, field, target, current, isIntRange: true);
                break;
            case PocoCodec.Kind.FloatRange:
                DrawRange(rect, field, target, current, isIntRange: false);
                break;
            case PocoCodec.Kind.Enum:
                DrawEnum(rect, field, target, current, owner);
                break;
            case PocoCodec.Kind.Def:
                DrawDefRef(rect, field, target, current, owner);
                break;
            case PocoCodec.Kind.DefList:
                DrawDefList(rect, field, target, current, owner);
                break;
            case PocoCodec.Kind.StringList:
                DrawStringList(rect, field, target, current, owner);
                break;
            case PocoCodec.Kind.ComplexList:
                DrawComplexList(rect, field, target, current, owner);
                break;
            default:
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                Widgets.Label(rect, "MST.StockGenUnsupported".Translate().RawText);
                Text.Font = prevFont;
                break;
        }
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void DrawNumeric(Rect rect, PocoCodec.Field field, object target, object? current)
    {
        string text = current == null ? "" : Convert.ToString(current, CultureInfo.InvariantCulture) ?? "";
        string next = Widgets.TextField(rect, text);
        if (next == text) return;

        if (field.Kind == PocoCodec.Kind.Int)
        {
            if (int.TryParse(next, out int intValue)) StockGeneratorCodec.WriteField(field, target, intValue);
        }
        else if (float.TryParse(next, out float floatValue))
        {
            StockGeneratorCodec.WriteField(field, target, floatValue);
        }
    }

    private static void DrawBool(Rect rect, PocoCodec.Field field, object target, object? current)
    {
        bool value = current is bool b && b;
        bool next = value;
        float size = Mathf.Min(rect.height - 4f, 24f);
        Widgets.Checkbox(rect.x, rect.y + (rect.height - size) / 2f, ref next, size);
        if (next != value) StockGeneratorCodec.WriteField(field, target, next);
    }

    private static void DrawText(Rect rect, PocoCodec.Field field, object target, object? current)
    {
        string text = current as string ?? "";
        string next = Widgets.TextField(rect, text);
        if (next != text) StockGeneratorCodec.WriteField(field, target, next.Length > 0 ? next : null);
    }

    private static void DrawRange(Rect rect, PocoCodec.Field field, object target, object? current, bool isIntRange)
    {
        string text = current?.ToString() ?? "";
        string next = Widgets.TextField(rect, text);
        if (next == text) return;

        // 合法性由 codec 判（与格式定义同处）：输入中途（如 "5~"）一律不写回，等用户敲完整。
        // 这里不能改用 IntRange.FromString 的"抛异常即非法"—— IntRange.FromString("5~") 是合法的
        // （缺省上界 = int.MaxValue），异常拦不住它；详见 TryParseRangeText 的注释。
        if (StockGeneratorCodec.TryParseRangeText(isIntRange, next, out var value))
            StockGeneratorCodec.WriteField(field, target, value);
    }

    private static void DrawEnum(Rect rect, PocoCodec.Field field, object target, object? current, EditorWindowBase owner)
    {
        var enumType = field.EnumType!;
        if (!Widgets.ButtonText(rect, EnumValueLabel(enumType, current))) return;

        var options = new List<FloatMenuOption>();
        foreach (var value in Enum.GetValues(enumType))
        {
            var captured = value;
            options.Add(new FloatMenuOption(EnumValueLabel(enumType, captured), () => StockGeneratorCodec.WriteField(field, target, captured)));
        }
        // 与 MainWindow 的枚举下拉不同，本窗口重写了 Notify_ClickOutsideWindow（点击窗外即关闭）。
        // 裸 WindowStack.Add 的 FloatMenu 不算子窗口 ⇒ 点选项的那一帧窗口就被关掉。
        // 必须登记成子窗口，见 EditorWindowBase.OpenChildWindow。
        owner.OpenChildWindow(new FloatMenu(options));
    }

    private static void DrawDefRef(Rect rect, PocoCodec.Field field, object target, object? current, EditorWindowBase owner)
    {
        var def = current as Def;
        string label = def == null
            ? "MST.SelectDef".Translate().RawText
            : $"{def.LabelCap.Resolve()}({def.defName})";
        if (def != null && !def.description.NullOrEmpty())
            TooltipHandler.TipRegion(rect, def.description);

        if (!Widgets.ButtonText(rect, label)) return;

        var candidates = GenDefDatabase.GetAllDefsInDatabaseForDef(field.ElementType!).Cast<Def>().ToList();
        owner.OpenChildWindow(new DefSelectionWindow(LabelOf(field), candidates, def,
            d => StockGeneratorCodec.WriteField(field, target, d)));
    }

    private static void DrawDefList(Rect rect, PocoCodec.Field field, object target, object? current, EditorWindowBase owner)
    {
        var list = current as IList;
        string label = list != null && list.Count > 0 ? $"[{list.Count}]" : "MST.Empty".Translate().RawText;
        if (!Widgets.ButtonText(rect, label)) return;

        // DefListEditorWindow 的克隆逻辑要求初始列表非 null（内部会按元素类型重建 List<T>）。
        owner.OpenChildWindow(new DefListEditorWindow(
            LabelOf(field),
            list ?? new List<Def>(),
            field.ElementType!,
            result => StockGeneratorCodec.WriteField(field, target, result)));
    }

    private static void DrawStringList(Rect rect, PocoCodec.Field field, object target, object? current, EditorWindowBase owner)
    {
        var list = current as IList;
        string label = list != null && list.Count > 0 ? $"[{list.Count}]" : "MST.Empty".Translate().RawText;
        if (!Widgets.ButtonText(rect, label)) return;

        var initial = list?.Cast<string>().ToList() ?? new List<string>();
        owner.OpenChildWindow(new StringListEditorWindow(LabelOf(field), initial,
            result => StockGeneratorCodec.WriteField(field, target, result)));
    }

    private static void DrawComplexList(Rect rect, PocoCodec.Field field, object target, object? current, EditorWindowBase owner)
    {
        var list = current as IList;
        string label = list != null && list.Count > 0 ? $"[{list.Count}]" : "MST.Empty".Translate().RawText;
        if (!Widgets.ButtonText(rect, label)) return;

        owner.OpenChildWindow(new SGComplexListEditorWindow(
            LabelOf(field),
            list,
            field.ElementType!,
            result => StockGeneratorCodec.WriteField(field, target, result)));
    }

    /// <summary>
    /// 枚举值的显示文本：优先原版 Keyed 键（<c>TechLevel_Animal</c> / <c>PriceTypeNormal</c>，
    /// 见 Core 的 <c>Keyed/Enums.xml</c>），没有翻译时退回枚举名。
    /// </summary>
    public static string EnumValueLabel(Type enumType, object? value)
    {
        if (value == null) return "MST.None".Translate().RawText;
        string name = value.ToString()!;

        string prefix = enumType == typeof(TechLevel) ? "TechLevel_"
            : enumType == typeof(PriceType) ? "PriceType"
            : "";
        if (prefix.Length > 0 && (prefix + name).TryTranslate(out var text)) return text;
        return name;
    }
}
