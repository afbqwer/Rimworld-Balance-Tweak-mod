using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

public class StatColumnConfig
{
    public const int LabelTruncateLength = 5;

    // 配置属性
    public readonly string label;
    public readonly string id;
    public readonly string? comment;
    public readonly string columnDataTypeLabel;
    public readonly string columnTypeLabel;
    public readonly ColumnDataType columnDataType;
    public readonly ColumnStyle columnType;
    // 委托
    public delegate bool IsAvailable(TweakData data);
    public delegate float GetAbstract(TweakData data);
    public readonly IsAvailable? isAvailableDelegate;
    public readonly GetAbstract? getAbstract;

    // 数据定义
    public readonly FieldInfo field;
    public readonly string fieldName; // 缓存 field?.Name
    public readonly StatDef? stat;

    // 缓存
    public readonly Dictionary<TweakID, float> abstractValues = new();

    public readonly bool isSPStat = false;
    public readonly bool isListStyle; // 缓存 IsListStyle() 结果
    private FieldType cachedFieldType;
    public FieldType SortFieldType => cachedFieldType;
    public readonly Type? EnumType;
    public readonly FieldInfo? OptionsListField;
    public readonly Type? DefSelectorType;

    private static readonly Dictionary<FieldType, ColumnStyle[]> ValidStyleMap = new()
    {
        { FieldType.Float, new[] { ColumnStyle.Float, ColumnStyle.Prec, ColumnStyle.Int} },
        { FieldType.Int, new[] { ColumnStyle.Int } },
        { FieldType.Bool, new[] { ColumnStyle.Bool } },
        { FieldType.String, new[] { ColumnStyle.String } },
        { FieldType.FloatRange, new[] { ColumnStyle.Range } },
        { FieldType.IntRange, new[] { ColumnStyle.Range } },
        { FieldType.Enum, new[] { ColumnStyle.Enum, ColumnStyle.Flags } },
        { FieldType.SimpleCurve, new[] { ColumnStyle.Curve } },
        { FieldType.List, new[] { ColumnStyle.StringList, ColumnStyle.IntList, ColumnStyle.StatModList, ColumnStyle.CapModList, ColumnStyle.DamageModList, ColumnStyle.DefList, ColumnStyle.HediffGiverList, ColumnStyle.MentalStateGiverList, ColumnStyle.SkillGainList, ColumnStyle.AptitudeList, ColumnStyle.GeneticTraitList, ColumnStyle.ThingDefCountList, ColumnStyle.IngredientList, ColumnStyle.SkillReqList, ColumnStyle.TraitReqList, ColumnStyle.ProcessIngredientList, ColumnStyle.ProcessResultList, ColumnStyle.BodyPartTree, ColumnStyle.BiomePlantList, ColumnStyle.BiomeAnimalList, ColumnStyle.FishChanceList, ColumnStyle.DiseaseList, ColumnStyle.WeatherCommonalityList, ColumnStyle.TerrainThresholdList, ColumnStyle.StockGenList } },
        { FieldType.TweakID, new[] { ColumnStyle.Link, ColumnStyle.RaceLinks, ColumnStyle.BodyLinks } },
    };

    private Func<TweakData, object?>? _getter;
    private Action<TweakData, object?>? _setter;

    public StatColumnConfig(FieldInfo field,
        ColumnStyle style,
        ColumnDataType type,
        StatDef? stat,
        IsAvailable? isAvailable,
        GetAbstract? getAbstract,
        bool isSPStat,
        Type? enumType,
        FieldInfo? optionsListField
        )
    {
        this.field = field;
        fieldName = field.Name;
        columnDataType = type;
        columnType = style;
        this.isSPStat = isSPStat;
        isListStyle = IsListStyle(style);
        this.isAvailableDelegate = isAvailable;
        this.getAbstract = getAbstract;
        this.EnumType = enumType;
        this.OptionsListField = optionsListField;
        label = ("MST." + field.Name).Translate().RawText;
        columnDataTypeLabel = ("MST." + columnDataType.ToString()).Translate().RawText;
        columnTypeLabel = ("MST." + columnType.ToString()).Translate().RawText;
        id = $"{columnDataType}_{field.Name}";
        string typeName = GetFieldTypeName(field.FieldType);
        if (stat != null)
        {
            this.stat = stat;
            comment = $"{field.Name} ({typeName})\n{stat.LabelForFullStatList}\n{stat.description}";
        }
        else
        {
            if ($"MST.{field.Name}Comment".TryTranslate(out var c))
            {
                comment = $"{field.Name} ({typeName})\n{c}";
            }
            else
            {
                comment = $"{field.Name} ({typeName})";
            }
        }

        CacheFieldType();
        ValidateFieldTypeStyle();
        CompileAccessors();

        if (columnType == ColumnStyle.DefSelector)
        {
            Type fieldType = field.FieldType;
            Type underlying = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
            if (underlying.IsSubclassOf(typeof(Def)))
            {
                DefSelectorType = underlying;
            }
        }
    }

    /// <summary>
    /// 把字段的声明类型格式化成简洁的可读名称，供列描述使用：
    /// 省去命名空间前缀、泛型展开为 List&lt;T&gt;、可空类型追加 ?，基本类型用 C# 关键字。
    /// 例：float? / int? / List&lt;ResearchProjectDef&gt; / TechLevel? / SimpleCurve。
    /// </summary>
    private static string GetFieldTypeName(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return GetFieldTypeName(underlying) + "?";
        if (type.IsArray) return GetFieldTypeName(type.GetElementType()!) + "[]";
        if (type.IsGenericType)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            string args = string.Join(", ", type.GetGenericArguments().Select(GetFieldTypeName));
            return $"{name}<{args}>";
        }
        return type switch
        {
            _ when type == typeof(float) => "float",
            _ when type == typeof(int) => "int",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(string) => "string",
            _ => type.Name,
        };
    }

    public enum ColumnDataType { Field, Display }
    public enum ColumnStyle { Float, Prec, Int, Bool, Range, String, Link, Enum, Curve, Flags, StatModList, DefSelector, CapModList, DamageModList, StringList, IntList, DefList, HediffGiverList, MentalStateGiverList, SkillGainList, AptitudeList, GeneticTraitList, ThingDefCountList, IngredientList, IngredientFilter, SkillReqList, TraitReqList, ProcessIngredientList, ProcessResultList, BodyPartTree, RaceLinks, BodyLinks, BiomePlantList, BiomeAnimalList, FishChanceList, DiseaseList, WeatherCommonalityList, TerrainThresholdList, StockGenList }
    public enum FieldType { Float, Int, Bool, FloatRange, IntRange, String, TweakID, Enum, SimpleCurve, List, Def, ThingFilter, Other }

    public static bool IsListStyle(ColumnStyle style)
    {
        return style is ColumnStyle.StatModList or ColumnStyle.CapModList or ColumnStyle.DamageModList
            or ColumnStyle.DefList or ColumnStyle.HediffGiverList or ColumnStyle.MentalStateGiverList
            or ColumnStyle.StringList or ColumnStyle.IntList or ColumnStyle.SkillGainList or ColumnStyle.AptitudeList or ColumnStyle.GeneticTraitList
            or ColumnStyle.ThingDefCountList or ColumnStyle.IngredientList or ColumnStyle.SkillReqList or ColumnStyle.TraitReqList
            or ColumnStyle.ProcessIngredientList or ColumnStyle.ProcessResultList
            or ColumnStyle.BodyPartTree
            or ColumnStyle.BiomePlantList or ColumnStyle.BiomeAnimalList
            or ColumnStyle.FishChanceList or ColumnStyle.DiseaseList or ColumnStyle.WeatherCommonalityList
            or ColumnStyle.TerrainThresholdList
            or ColumnStyle.StockGenList;
    }

    private void ValidateFieldTypeStyle()
    {
        if (field == null) return;
        if (!ValidStyleMap.TryGetValue(cachedFieldType, out var validStyles)) return;
        if (!validStyles.Contains(columnType))
        {
            Log.Error($"[BalanceTweak] Field '{field.Name}' has FieldType.{cachedFieldType} but ColumnStyle.{columnType}, expected one of: {string.Join(", ", validStyles.Select(s => $"ColumnStyle.{s}"))}");
        }
    }

    #region 动态编译

    private void CacheFieldType()
    {
        if (field == null) return;
        Type type = field.FieldType;
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(float)) cachedFieldType = FieldType.Float;
        else if (underlying == typeof(int)) cachedFieldType = FieldType.Int;
        else if (underlying == typeof(bool)) cachedFieldType = FieldType.Bool;
        else if (underlying == typeof(string)) cachedFieldType = FieldType.String;
        else if (underlying == typeof(FloatRange)) cachedFieldType = FieldType.FloatRange;
        else if (underlying == typeof(IntRange)) cachedFieldType = FieldType.IntRange;
        else if (underlying.IsEnum) cachedFieldType = FieldType.Enum;
        else if (underlying == typeof(SimpleCurve)) cachedFieldType = FieldType.SimpleCurve;
        else if (underlying == typeof(TweakID)) cachedFieldType = FieldType.TweakID;
        else if (underlying == typeof(ThingFilter)) cachedFieldType = FieldType.ThingFilter;
        else if (typeof(IList).IsAssignableFrom(underlying)) cachedFieldType = FieldType.List;
        else if (typeof(Def).IsAssignableFrom(underlying)) cachedFieldType = FieldType.Def;
        else cachedFieldType = FieldType.Other;
    }

    private void CompileAccessors()
    {
        if (field == null) return;

        // 参数：(TweakData data)
        var paramData = Expression.Parameter(typeof(TweakData), "data");

        Expression instanceExpr = paramData;
        if (field.DeclaringType != typeof(TweakData))
        {
            // 生成代码：((TweakData)data) 或 ((TweakData<Def>)data)
            instanceExpr = Expression.Convert(paramData, field.DeclaringType!);
        }

        // 生成代码：return (object)((DeclaringType)data).Field;
        var fieldAccess = Expression.Field(instanceExpr, field);
        var convertToObject = Expression.Convert(fieldAccess, typeof(object));
        _getter = Expression.Lambda<Func<TweakData, object?>>(convertToObject, paramData).Compile();

        // 生成代码：((DeclaringType)data).Field = (FieldType)value;
        var paramValue = Expression.Parameter(typeof(object), "value");

        // 处理 Nullable 类型转换：如果字段是 float?，传入的 object 需要拆箱为 float 再隐式转换
        Type targetType = field.FieldType;
        Expression valueCast = Expression.Convert(paramValue, targetType);

        var assign = Expression.Assign(fieldAccess, valueCast);
        _setter = Expression.Lambda<Action<TweakData, object?>>(assign, paramData, paramValue).Compile();
    }

    #endregion

    #region 数据读写

    public object? GetRawValue(TweakData data)
    {
        if (_getter != null)
        {
            try { return _getter(data); } catch { return null; }
        }
        return null;
    }

    public float? GetNumericValue(TweakData data)
    {
        if (field == null) return null;

        object? rawValue = GetRawValue(data);
        float? result;

        switch (cachedFieldType)
        {
            case FieldType.Float: result = (float?)rawValue; break;
            case FieldType.Int: result = (int?)rawValue; break;
            case FieldType.Bool: result = (bool?)rawValue == true ? 1f : 0f; break;
            case FieldType.Enum: result = rawValue != null ? Convert.ToInt32(rawValue) : null; break;
            default: return null;
        }

        if (result == -1 && showAbstract && selectedCol == this)
        {
            if (abstractValues.TryGetValue(data.id, out float cachedVal)) return cachedVal;
            if (getAbstract != null)
            {
                float abstractVal = getAbstract(data);
                abstractValues.SetOrAdd(data.id, abstractVal);
                return abstractVal;
            }

        }
        if (result == null && showAbstract && selectedCol == this)
        {
            if (abstractValues.TryGetValue(data.id, out float cachedVal)) return cachedVal;
            if (data.def is BuildableDef def && stat != null)
            {
                float abstractVal = def.GetStatValueAbstract(stat);
                abstractValues.SetOrAdd(data.id, abstractVal);
                return abstractVal;
            }
        }
        return result;
    }

    private void SetRawValue(TweakData data, object? value, bool isModified = true)
    {
        if (_setter == null || field == null) return;

        object? finalValue = value;

        if (value != null)
        {
            Type valType = value.GetType();
            Type targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

            if (valType != targetType)
            {
                if (targetType == typeof(float) && value is int i) finalValue = (float)i;
                else if (targetType == typeof(float) && value is bool b) finalValue = b ? 1f : 0f;
                else if (targetType == typeof(bool) && value is float f) finalValue = f > 0.5f;
                // 数值 -> 枚举：批量应用（ApplyBatchOperation）对枚举列会算出 float 值并走到这里，
                // 缺此分支会让表达式编译的 Convert(object -> Enum) 拆箱 float 抛 InvalidCastException。
                else if (targetType.IsEnum && value is float ef) finalValue = Enum.ToObject(targetType, (int)ef);
                else if (targetType.IsEnum && value is int ei) finalValue = Enum.ToObject(targetType, ei);
            }
        }

        _setter(data, finalValue);
        tweakDatas.SetOrAdd(data.id, data);
        if (isModified)
        {
            data.tweaked = true;
            data.MarkFieldModified(field.Name);
        }
        else
        {
            data.RemoveFieldModified(field.Name);
            if (data.CountFieldModified() == 0)
            {
                data.tweaked = false;
            }
        }
    }

    public void CheckAndApply(TweakData data, string newText)
    {
        if (field == null) return;

        // 严格判据：文本与当前显示值逐字相同 ⇒ 用户没有编辑 ⇒ 不产生任何状态变化。
        // 编辑缓冲以显示文本为种子，因此「聚焦后原样失焦」必然命中这里。
        // 这一步专治：原本就是 Null/空的字段，只因点了一下输入框就被记为已修改/已清空。
        // 注意此时不能依赖下面各分支的 changed：它们的 old==null 判据会把「无 → 无」误判为变化。
        if (string.Equals(newText, GetString(data), StringComparison.Ordinal)) return;

        object? newValue = null;
        bool changed = false;
        // 非 String 列：输入 null 关键字 = 显式清空（唯一入口，不再靠"已改且空"反推）。
        // String 列不参与清空 —— 对字符串而言 "null" 就是普通文本，走下方常规解析，按字面量 "null" 赋值。
        if (newText.IndexOf("null", StringComparison.OrdinalIgnoreCase) > -1
            && columnType != ColumnStyle.String && cachedFieldType != FieldType.String)
        {
            if (data.IsFieldCleared(field.Name)) return; // 幂等
            if (GetRawValue(data) == null) return; // 本就没有值，无物可清 ⇒ 不该留下任何状态
            SetRawValue(data, null, isModified: false);
            data.MarkFieldCleared(field.Name);
            data.Apply();
            return;
        }
        if (showChanged && selectedCol == this)
        {
            return;
        }
        // 解析逻辑
        switch (columnType)
        {
            case ColumnStyle.Float:
                if (float.TryParse(newText, out float f))
                {
                    newValue = f;
                    float? oldVal = GetNumericValue(data);
                    changed = oldVal == null || FloatChanged(oldVal.Value, f, 0.01f);
                }
                break;
            case ColumnStyle.Prec:
                if (float.TryParse(newText, out float p))
                {
                    float v = p / 100f;
                    newValue = v;
                    float? oldVal = GetNumericValue(data);
                    changed = oldVal == null || FloatChanged(oldVal.Value, v, 0.0001f);
                }
                break;
            case ColumnStyle.Int:
                if (int.TryParse(newText, out int i)) { newValue = i; changed = !(((int?)GetNumericValue(data)).Equals(i)); }
                break;
            case ColumnStyle.String or ColumnStyle.Range:
                if (cachedFieldType == FieldType.FloatRange)
                {
                    try
                    {
                        FloatRange newRange = FloatRange.FromString(newText);
                        newValue = newRange;
                        changed = !Equals(GetRawValue(data), newRange);
                    }
                    catch
                    {
                    }
                }
                else if (cachedFieldType == FieldType.IntRange)
                {
                    IntRange newRange = IntRange.FromString(newText);
                    newValue = newRange;
                    changed = !Equals(GetRawValue(data), newRange);
                }
                else
                {
                    newValue = newText;
                    changed = !Equals(GetRawValue(data)?.ToString(), newText);
                }

                break;
            case ColumnStyle.Bool:
                if (bool.TryParse(newText, out bool b))
                {
                    bool target = b;
                    newValue = target;
                    changed = (bool?)GetRawValue(data) != target;
                }
                break;
            case ColumnStyle.Enum:
                if (OptionsListField != null)
                {
                    var options = GetOptions();
                    if (options != null && options.Contains(newText))
                    {
                        newValue = newText;
                        changed = !Equals(GetRawValue(data), newValue);
                    }
                }
                else if (EnumType != null && Enum.IsDefined(EnumType, newText))
                {
                    newValue = Enum.Parse(EnumType, newText);
                    changed = !Equals(GetRawValue(data), newValue);
                }
                break;
            case ColumnStyle.Flags:
                if (EnumType != null)
                {
                    var names = newText.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s));
                    long result = 0;
                    foreach (var name in names)
                    {
                        if (Enum.IsDefined(EnumType, name))
                        {
                            result |= Convert.ToInt64(Enum.Parse(EnumType, name));
                        }
                    }
                    newValue = Enum.ToObject(EnumType, result);
                    changed = !Equals(GetRawValue(data), newValue);
                }
                break;
        }

        if (changed && newValue != null)
        {
            //Log.Message($"[BalanceTweak] {data.label}({data.id}) - {label}: {GetRawValue(data)?.ToString() ?? "null"} -> {newValue}");
            ApplyValue(data, newValue);
        }
    }

    public void ApplyValue(TweakData data, object? newValue, bool isModified = true)
    {
        SetRawValue(data, newValue, isModified);
        data.Apply();
    }

    public List<Def> GetAllDefsForSelector()
    {
        if (DefSelectorType == null) return new List<Def>();
        return GenDefDatabase.GetAllDefsInDatabaseForDef(DefSelectorType).ToList();
    }
    #endregion

    public List<string>? GetOptions()
    {
        if (OptionsListField != null)
        {
            var options = OptionsListField.GetValue(null);
            if (options is IEnumerable<Def> defList)
            {
                return defList.Select(d => d.defName).ToList();
            }
            if (options is IEnumerable<string> stringList)
            {
                return stringList.ToList();
            }
        }
        return null;
    }

    public override string ToString() => id;

    /// <summary>
    /// 检查浮点数值是否有意义地发生了变化。
    /// <para><paramref name="absTol"/> = 「一个显示单位」：Float 列为 0.01（F2 显示），Prec 列为 0.0001（百分比小数）。</para>
    /// <para>阈值取 <c>max(半个显示单位, float 表示误差兜底)</c>：</para>
    /// <list type="bullet">
    /// <item>半个显示单位：float 无法精确表示 0.01（<c>1.01f - 1f = 0.00999999</c>），若用「一个显示单位」当阈值，
    /// 恰好一个单位的编辑会因 <c>diff &lt;= absTol</c> 被误判为无变化；取半个单位才能稳定地「改一个显示单位即算变化」。</item>
    /// <item>相对兜底只吸收 float 舍入噪声，系数取 ~1e-7（机器精度量级）。<b>绝不能</b>用 absTol 放大 —— 否则
    /// 值越大容差越大（值 100 时达 1.0），会把 <c>100.00 -&gt; 100.01</c> 这类合法编辑一并吞掉。</item>
    /// </list>
    /// </summary>
    public static bool FloatChanged(float oldVal, float newVal, float absTol)
    {
        if (oldVal == newVal) return false;
        float magnitude = Math.Max(Math.Abs(oldVal), Math.Abs(newVal));
        float threshold = Math.Max(absTol * 0.5f, magnitude * 1e-7f);
        return Math.Abs(oldVal - newVal) > threshold;
    }

    /// <summary>
    /// 该列的列表元素类型是否已进 <see cref="PocoCodec"/> 的字段表 —— 是则剪贴板可走通用编解码。
    ///
    /// <para>
    /// 判据完全由<b>字段类型</b>推出（<c>List&lt;T&gt;</c> 的 <c>T</c>），**不需要 <c>ColumnStyle</c> 参与**。
    /// 这也自动把 <c>IngredientCount</c> / <c>int</c> 排除在外，无需维护白名单。
    /// </para>
    /// </summary>
    private bool TryGetDeclaredListElementType(out Type elementType)
    {
        // 字段声明由 TweakData.FieldCodecs 的静态构造写入 PocoCodec，而 PocoCodec.IsDeclared 不会
        // 触发那条链 —— 这里显式碰一下，免得"GUI 早于任何一次 ExposeData 被打开"时全部判 false
        // ⇒ 剪贴板/拖拽涂抹静默失效。
        TweakData.FieldCodecs.EnsureBuilt();

        elementType = typeof(object);
        var fieldType = field?.FieldType;
        if (fieldType == null || !fieldType.IsGenericType) return false;
        if (fieldType.GetGenericTypeDefinition() != typeof(List<>)) return false;
        elementType = fieldType.GetGenericArguments()[0];
        return PocoCodec.IsDeclared(elementType);
    }

    public string? GetCopyData(TweakData data)
    {
        // 简单类型 (TextField 可编辑)
        if (columnType is ColumnStyle.Float or ColumnStyle.Int or ColumnStyle.Prec or ColumnStyle.Bool or ColumnStyle.Range or ColumnStyle.String or ColumnStyle.Enum)
        {
            return GetString(data);
        }

        // ── 列表：元素类型已进字段表 ⇒ 走字段表通用编解码 ──
        //
        // 判据由字段自身推出，**不认 ColumnStyle** —— 这一点是刻意的：
        // 原先这里是 18 项手写样式 switch、`TryPasteData` 里另有一份 27 项手写清单，两份漂移过一次，
        // 把 `FishChanceList` / `DiseaseList` / `WeatherCommonalityList` 漏在守卫之外 ⇒ 本方法恒返回 null
        // ⇒ 那几列的 Shift+拖拽涂抹**静默失效**（无日志无提示，见 Docs §3.9.1）。
        // 改判据之后，增删列表列都不必碰这里，"两份清单漂移"从根上没了。
        if (TryGetDeclaredListElementType(out _))
        {
            var list = (IList?)GetRawValue(data) ?? Array.Empty<object>();
            var listText = PocoCodec.EncodeList(list);
            if (listText != null) return listText;
        }

        // `IngredientCount` 未进字段表（它带 GetBaseCount 语义、文本格式也自成一格），单列一支。
        if (columnType == ColumnStyle.IngredientList)
        {
            var list = (IList?)GetRawValue(data) ?? Array.Empty<object>();
            return SerializationHelper.SerializeIngredientCountList(list.Cast<IngredientCount>().ToList());
        }

        if (columnType == ColumnStyle.Curve)
        {
            var rawValue = GetRawValue(data);
            return SerializationHelper.SerializeSimpleCurve(rawValue as SimpleCurve);
        }

        if (columnType == ColumnStyle.IngredientFilter)
        {
            var rawValue = GetRawValue(data);
            return SerializationHelper.SerializeThingFilter(rawValue as ThingFilter);
        }

        if (columnType == ColumnStyle.Flags)
        {
            var rawValue = GetRawValue(data);
            if (rawValue == null) return "0";
            return Convert.ToInt64(rawValue).ToString();
        }

        if (columnType == ColumnStyle.DefSelector)
        {
            var rawValue = GetRawValue(data);
            return (rawValue as Def)?.defName;
        }

        if (columnType == ColumnStyle.Link)
        {
            return GetString(data);
        }

        // StringList：每个元素占一行。
        if (columnType == ColumnStyle.StringList)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return null;
            return string.Join("\n", list.Cast<string>());
        }

        // StockGenList：字段值就是生成器对象，出去时按"一行一条生成器文本"编码（见 StockGeneratorCodec）。
        // 优先用编辑器里的结构化窗口改；复制/粘贴只是把一整套库存搬到另一行时的快捷方式。
        if (columnType == ColumnStyle.StockGenList)
        {
            var list = (List<StockGenerator>?)GetRawValue(data);
            var lines = StockGeneratorCodec.Serialize(list);
            return lines != null ? string.Join("\n", lines) : null;
        }

        if (columnType == ColumnStyle.IntList)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return null;
            return string.Join(",", list.Cast<int>());
        }

        if (columnType == ColumnStyle.DefList)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return null;
            var elementType = field.FieldType.GetGenericArguments()[0];
            var serializeMethod = typeof(SerializationHelper).GetMethod(nameof(SerializationHelper.SerializeDefList), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(elementType);
            return (string?)serializeMethod.Invoke(null, [list]);
        }

        // 部位树：整棵树按行编码（每行一个节点，前缀序），与存档格式一致
        if (columnType == ColumnStyle.BodyPartTree)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null || list.Count == 0) return null;
            return string.Join("\n", list.Cast<string>());
        }

        return null;
    }
    // TryPasteData用于数据粘贴的解析
    public bool TryPasteData(TweakData data, string clipboardData)
    {
        try
        {
            // 简单类型 (TextField 可编辑)
            if (columnType is ColumnStyle.Float or ColumnStyle.Int or ColumnStyle.Prec or ColumnStyle.Bool or ColumnStyle.Range or ColumnStyle.String or ColumnStyle.Enum)
            {
                CheckAndApply(data, clipboardData);
                return true;
            }

            // ── 列表：元素类型已进字段表 ⇒ 走字段表通用解码（判据同 GetCopyData，同样不认 ColumnStyle） ──
            if (TryGetDeclaredListElementType(out var listElementType))
            {
                var list = PocoCodec.DecodeList(listElementType, clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }

            // `IngredientCount` 未进字段表，单列一支（见 GetCopyData 的同类分支）。
            if (columnType == ColumnStyle.IngredientList)
            {
                var list = SerializationHelper.DeserializeIngredientCountList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }

            if (columnType == ColumnStyle.Curve)
            {
                var curve = SerializationHelper.DeserializeSimpleCurve(clipboardData);
                if (curve != null) { ApplyValue(data, curve); return true; }
                return false;
            }

            if (columnType == ColumnStyle.IngredientFilter)
            {
                var filter = SerializationHelper.DeserializeThingFilter(clipboardData);
                if (filter != null) { ApplyValue(data, filter); return true; }
                return false;
            }

            if (columnType == ColumnStyle.Flags)
            {
                if (long.TryParse(clipboardData, out var val) && EnumType != null)
                {
                    ApplyValue(data, Enum.ToObject(EnumType, val));
                    return true;
                }
                return false;
            }

            if (columnType == ColumnStyle.DefSelector)
            {
                var def = GetAllDefsForSelector().FirstOrDefault(d => d.defName == clipboardData);
                if (def != null) { ApplyValue(data, def); return true; }
                return false;
            }

            // StringList：每个元素占一行。
            if (columnType == ColumnStyle.StringList)
            {
                var list = clipboardData.Split('\n').ToList();
                ApplyValue(data, list);
                return true;
            }

            // StockGenList：整体校验（任一条解析不了就整块拒绝），免得把半截数据塞进字段。
            if (columnType == ColumnStyle.StockGenList)
            {
                var lines = clipboardData.Split('\n').Where(l => !l.NullOrEmpty()).ToList();
                if (lines.Count == 0) return false;
                var list = StockGeneratorCodec.Deserialize(lines);
                if (list == null) return false;
                ApplyValue(data, list);
                return true;
            }

            if (columnType == ColumnStyle.IntList)
            {
                var parts = clipboardData.Split(',');
                var list = new List<int>();
                foreach (var part in parts)
                {
                    if (int.TryParse(part.Trim(), out var val))
                        list.Add(val);
                    else
                        return false;
                }
                ApplyValue(data, list);
                return true;
            }

            if (columnType == ColumnStyle.DefList)
            {
                var elementType = field.FieldType.GetGenericArguments()[0];
                var deserializeMethod = typeof(SerializationHelper).GetMethod(nameof(SerializationHelper.DeserializeDefList), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(elementType);
                var list = deserializeMethod.Invoke(null, [clipboardData]);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }

            // 部位树：把剪贴板文本按行还原成整棵树，再重新编码写回
            if (columnType == ColumnStyle.BodyPartTree)
            {
                var lines = clipboardData.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                var treeRoot = SerializationHelper.DeserializeBodyPartTree(lines);
                if (treeRoot == null) return false;
                var encoded = SerializationHelper.SerializeBodyPartTree(treeRoot);
                if (encoded == null || encoded.Count == 0) return false;
                ApplyValue(data, encoded);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
    // 用于数据的显示
    public string GetString(TweakData data)
    {
        if (columnType == ColumnStyle.Link)
        {
            var d = GetData((TweakID?)GetRawValue(data));
            if (d is TweakData td) return td.label ?? "";
        }
        if (columnType == ColumnStyle.RaceLinks)
        {
            // 静态按钮文字，避免每帧扫描所有种族；真正的反向查询在窗口打开时进行。
            return "MST.raceLinks".Translate().RawText;
        }
        if (columnType == ColumnStyle.BodyLinks)
        {
            // 同上：静态按钮文字，反向查询在窗口打开时进行。
            return "MST.bodyLinks".Translate().RawText;
        }
        if (cachedFieldType == FieldType.FloatRange)
        {
            var fr = (FloatRange?)GetRawValue(data);
            if (showChanged && selectedCol == this && fr.HasValue)
            {
                return new FloatRange((fr.Value.min + batchFlat) * batchPrec, (fr.Value.max + batchFlat) * batchPrec).ToString();
            }
            return fr.HasValue ? fr.Value.ToString() : "Null";
        }
        if (cachedFieldType == FieldType.IntRange)
        {
            var ir = (IntRange?)GetRawValue(data);
            if (showChanged && selectedCol == this && ir.HasValue)
            {
                return new IntRange((int)((ir.Value.min + batchFlat) * batchPrec), (int)((ir.Value.max + batchFlat) * batchPrec)).ToString();
            }
            return ir.HasValue ? ir.Value.ToString() : "";
        }
        if (columnType == ColumnStyle.String ||
            columnType == ColumnStyle.Link ||
            cachedFieldType == FieldType.String
            )
        {
            var rawValue = GetRawValue(data);
            return rawValue?.ToString() ?? "";
        }

        if (columnType == ColumnStyle.Enum && cachedFieldType == FieldType.Enum)
        {
            var enumValue = GetRawValue(data);
            return enumValue?.ToString() ?? "Null";
        }
        if (columnType == ColumnStyle.Flags && cachedFieldType == FieldType.Enum)
        {
            var rawValue = GetRawValue(data);
            if (rawValue == null) return "MST.Empty".Translate().RawText;
            var enumVal = (Enum)rawValue;
            long intVal = Convert.ToInt64(enumVal);
            if (intVal == 0) return "None";
            var names = enumVal.ToString().Split(',').Select(s => s.Trim());
            return string.Join(", ", names);
        }
        if (columnType == ColumnStyle.Enum && OptionsListField != null)
        {
            var rawValue = GetRawValue(data);
            return rawValue?.ToString() ?? "Null";
        }

        if (cachedFieldType == FieldType.SimpleCurve)
        {
            var curve = (SimpleCurve?)GetRawValue(data);
            if (curve == null) return "Null";
            return $"[{curve.PointsCount}]";
        }

        if (isListStyle)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return "MST.Empty".Translate().RawText;
            if (list.Count == 1)
            {
                var element = list[0];
                switch (columnType)
                {
                    case ColumnStyle.StatModList:
                        if (element is StatModifier sm)
                        {
                            var label = sm.stat?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.CapModList:
                        if (element is PawnCapacityModifier pcm)
                        {
                            var label = pcm.capacity?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.DamageModList:
                        if (element is DamageFactor dmf)
                        {
                            var label = dmf.damageDef?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.DefList:
                        if (element is Def de)
                        {
                            var label = de.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.HediffGiverList:
                        if (element is HediffGiver hg)
                        {
                            var label = hg.hediff?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.MentalStateGiverList:
                        if (element is MentalStateGiver msg)
                        {
                            var label = msg.mentalState?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.SkillGainList:
                        if (element is SkillGain sg)
                        {
                            var label = sg.skill?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.AptitudeList:
                        if (element is Aptitude ap)
                        {
                            var label = ap.skill?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.GeneticTraitList:
                        if (element is GeneticTraitData gtd)
                        {
                            var label = gtd.def?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.ThingDefCountList:
                        if (element is ThingDefCountClass tdc)
                        {
                            var label = tdc.thingDef?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > 5
                                    ? $"[{label[..LabelTruncateLength]}..*{tdc.count}]"
                                    : $"[{label}*{tdc.count}]";
                        }
                        break;
                    case ColumnStyle.IngredientList:
                        if (element is IngredientCount ic)
                        {
                            if (ic.IsFixedIngredient)
                            {
                                var label = ic.FixedIngredient?.label;
                                if (!label.NullOrEmpty())
                                    return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                            }
                            return $"[{ic.filter.AllowedDefCount} types]";
                        }
                        break;
                    case ColumnStyle.SkillReqList:
                        if (element is SkillRequirement sr)
                        {
                            var label = sr.skill?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.TraitReqList:
                        if (element is TraitRequirement tr)
                        {
                            var label = tr.def?.label;
                            if (!label.NullOrEmpty())
                            {
                                var degStr = tr.degree.HasValue ? $"*{tr.degree.Value}" : "";
                                return label!.Length > LabelTruncateLength
                                    ? $"[{label[..LabelTruncateLength]}..{degStr}]"
                                    : $"[{label}{degStr}]";
                            }
                        }
                        break;
                    case ColumnStyle.ProcessIngredientList:
                        if (element is ProcessIngredientItem pii)
                        {
                            var label = pii.thing?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.ProcessResultList:
                        if (element is ProcessResultItem pri)
                        {
                            var label = pri.thing?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > 5
                                    ? $"[{label[..LabelTruncateLength]}..*{pri.count}]"
                                    : $"[{label}*{pri.count}]";
                        }
                        break;
                    case ColumnStyle.BiomePlantList:
                        if (element is BiomePlantRecord bpr)
                        {
                            var label = bpr.plant?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.BiomeAnimalList:
                        if (element is BiomeAnimalRecord bar)
                        {
                            var label = bar.animal?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.FishChanceList:
                        if (element is FishChance fc)
                        {
                            var label = fc.fishDef?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.DiseaseList:
                        if (element is BiomeDiseaseRecord bdr)
                        {
                            var label = bdr.diseaseInc?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.WeatherCommonalityList:
                        if (element is WeatherCommonalityRecord wcr)
                        {
                            var label = wcr.weather?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                    case ColumnStyle.TerrainThresholdList:
                        if (element is TerrainThreshold tt)
                        {
                            var label = tt.terrain?.label;
                            if (!label.NullOrEmpty())
                                return label!.Length > LabelTruncateLength ? $"[{label[..LabelTruncateLength]}..]" : $"[{label}]";
                        }
                        break;
                }
            }
            return $"[{list.Count}]";
        }

        if (columnType == ColumnStyle.IngredientFilter)
        {
            var filter = (ThingFilter?)GetRawValue(data);
            if (filter == null) return "MST.Empty".Translate().RawText;
            return $"({filter.AllowedDefCount})";
        }

        if (columnType == ColumnStyle.DefSelector)
        {
            var def = (Def?)GetRawValue(data);
            if (def == null) return "MST.Empty".Translate().RawText;
            return def.label.NullOrEmpty() ? def.defName : def.LabelCap;
        }

        float? val = GetNumericValue(data);
        if (showChanged && selectedCol == this && val != null && !(getAbstract != null && val == -1f))
        {
            val = (val + batchFlat) * batchPrec;
        }
        switch (columnType)
        {
            case ColumnStyle.Float: return val?.ToString("F2") ?? "Null";
            case ColumnStyle.Prec: return val.HasValue ? (val.Value * 100f).ToString("F2") : "Null";
            case ColumnStyle.Int: return val?.ToString("F0") ?? "Null";
            case ColumnStyle.Bool:
                if (cachedFieldType == FieldType.Bool) return (bool?)GetRawValue(data) == true ? "True" : "False";
                return val > 0.5f ? "True" : "False";
            default: return val?.ToString() ?? "Null";
        }
    }
}
