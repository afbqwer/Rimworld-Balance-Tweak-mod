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
        { FieldType.List, new[] { ColumnStyle.StringList, ColumnStyle.IntList, ColumnStyle.StatModList, ColumnStyle.CapModList, ColumnStyle.DamageModList, ColumnStyle.DefList, ColumnStyle.HediffGiverList, ColumnStyle.MentalStateGiverList, ColumnStyle.SkillGainList, ColumnStyle.AptitudeList, ColumnStyle.GeneticTraitList, ColumnStyle.ThingDefCountList, ColumnStyle.IngredientList, ColumnStyle.SkillReqList, ColumnStyle.ProcessIngredientList, ColumnStyle.ProcessResultList, ColumnStyle.BodyPartTree } },
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
        if (stat != null)
        {
            this.stat = stat;
            comment = $"{field.Name}\n{stat.LabelForFullStatList}\n{stat.description}";
        }
        else
        {
            if ($"MST.{field.Name}Comment".TryTranslate(out var c))
            {
                comment = $"{field.Name}\n{c}";
            }
            else
            {
                comment = field.Name;
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

    public enum ColumnDataType { Field, Display }
    public enum ColumnStyle { Float, Prec, Int, Bool, Range, String, Link, Enum, Curve, Flags, StatModList, DefSelector, CapModList, DamageModList, StringList, IntList, DefList, HediffGiverList, MentalStateGiverList, SkillGainList, AptitudeList, GeneticTraitList, ThingDefCountList, IngredientList, IngredientFilter, SkillReqList, ProcessIngredientList, ProcessResultList, BodyPartTree, RaceLinks, BodyLinks }
    public enum FieldType { Float, Int, Bool, FloatRange, IntRange, String, TweakID, Enum, SimpleCurve, List, Def, ThingFilter, Other }

    public static bool IsListStyle(ColumnStyle style)
    {
        return style is ColumnStyle.StatModList or ColumnStyle.CapModList or ColumnStyle.DamageModList
            or ColumnStyle.DefList or ColumnStyle.HediffGiverList or ColumnStyle.MentalStateGiverList
            or ColumnStyle.StringList or ColumnStyle.IntList or ColumnStyle.SkillGainList or ColumnStyle.AptitudeList or ColumnStyle.GeneticTraitList
            or ColumnStyle.ThingDefCountList or ColumnStyle.IngredientList or ColumnStyle.SkillReqList
            or ColumnStyle.ProcessIngredientList or ColumnStyle.ProcessResultList
            or ColumnStyle.BodyPartTree;
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
            instanceExpr = Expression.Convert(paramData, field.DeclaringType);
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

        object? newValue = null;
        bool changed = false;
        if (newText.IndexOf("null", StringComparison.OrdinalIgnoreCase) > -1)
        {
            if (GetRawValue(data) == null)
            {
                return;
            }
            ApplyValue(data, null);
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
    /// 使用组合容差：小数值用绝对容差，大数值用相对容差，避免 float 精度在大数值时失效。
    /// </summary>
    public static bool FloatChanged(float oldVal, float newVal, float absTol)
    {
        float diff = Math.Abs(oldVal - newVal);
        if (diff <= absTol) return false;
        float magnitude = Math.Max(Math.Abs(oldVal), Math.Abs(newVal));
        float threshold = Math.Max(absTol, absTol * magnitude);
        return diff > threshold;
    }

    public string? GetCopyData(TweakData data)
    {
        // 简单类型 (TextField 可编辑)
        if (columnType is ColumnStyle.Float or ColumnStyle.Int or ColumnStyle.Prec or ColumnStyle.Bool or ColumnStyle.Range or ColumnStyle.String or ColumnStyle.Enum)
        {
            return GetString(data);
        }

        // 列表类型 (通过 SerializationHelper 序列化)
        if (columnType is ColumnStyle.StatModList or ColumnStyle.CapModList or ColumnStyle.DamageModList or ColumnStyle.SkillGainList or ColumnStyle.SkillReqList or ColumnStyle.AptitudeList or ColumnStyle.GeneticTraitList or ColumnStyle.ThingDefCountList or ColumnStyle.IngredientList or ColumnStyle.ProcessIngredientList or ColumnStyle.ProcessResultList)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return null;
            return columnType switch
            {
                ColumnStyle.StatModList => SerializationHelper.SerializeStatModifierList(list.Cast<StatModifier>().ToList()),
                ColumnStyle.CapModList => SerializationHelper.SerializePawnCapacityModifierList(list.Cast<PawnCapacityModifier>().ToList()),
                ColumnStyle.DamageModList => SerializationHelper.SerializeDamageFactorList(list.Cast<DamageFactor>().ToList()),
                ColumnStyle.SkillGainList => SerializationHelper.SerializeSkillGainList(list.Cast<SkillGain>().ToList()),
                ColumnStyle.SkillReqList => SerializationHelper.SerializeSkillRequirementList(list.Cast<SkillRequirement>().ToList()),
                ColumnStyle.AptitudeList => SerializationHelper.SerializeAptitudeList(list.Cast<Aptitude>().ToList()),
                ColumnStyle.GeneticTraitList => SerializationHelper.SerializeGeneticTraitDataList(list.Cast<GeneticTraitData>().ToList()),
                ColumnStyle.ThingDefCountList => SerializationHelper.SerializeThingDefCountClassList(list.Cast<ThingDefCountClass>().ToList()),
                ColumnStyle.IngredientList => SerializationHelper.SerializeIngredientCountList(list.Cast<IngredientCount>().ToList()),
                ColumnStyle.ProcessIngredientList => SerializationHelper.SerializeProcessIngredientItemList(list.Cast<ProcessIngredientItem>().ToList()),
                ColumnStyle.ProcessResultList => SerializationHelper.SerializeProcessResultItemList(list.Cast<ProcessResultItem>().ToList()),
                _ => null
            };
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

        if (columnType == ColumnStyle.StringList)
        {
            var list = (IList?)GetRawValue(data);
            if (list == null) return null;
            return string.Join("\n", list.Cast<string>());
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
            var serializeMethod = typeof(SerializationHelper).GetMethod("SerializeDefList", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(elementType);
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

            if (columnType == ColumnStyle.StatModList)
            {
                var list = SerializationHelper.DeserializeStatModifierList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.CapModList)
            {
                var list = SerializationHelper.DeserializePawnCapacityModifierList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.DamageModList)
            {
                var list = SerializationHelper.DeserializeDamageFactorList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.SkillGainList)
            {
                var list = SerializationHelper.DeserializeSkillGainList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.SkillReqList)
            {
                var list = SerializationHelper.DeserializeSkillRequirementList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.AptitudeList)
            {
                var list = SerializationHelper.DeserializeAptitudeList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.GeneticTraitList)
            {
                var list = SerializationHelper.DeserializeGeneticTraitDataList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.ThingDefCountList)
            {
                var list = SerializationHelper.DeserializeThingDefCountClassList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.IngredientList)
            {
                var list = SerializationHelper.DeserializeIngredientCountList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.ProcessIngredientList)
            {
                var list = SerializationHelper.DeserializeProcessIngredientItemList(clipboardData);
                if (list != null) { ApplyValue(data, list); return true; }
                return false;
            }
            if (columnType == ColumnStyle.ProcessResultList)
            {
                var list = SerializationHelper.DeserializeProcessResultItemList(clipboardData);
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

            if (columnType == ColumnStyle.StringList)
            {
                var list = clipboardData.Split('\n').ToList();
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
                var deserializeMethod = typeof(SerializationHelper).GetMethod("DeserializeDefList", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(elementType);
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
            return !fr.HasValue ? "Null" : fr.ToString();
        }
        if (cachedFieldType == FieldType.IntRange)
        {
            var ir = (IntRange?)GetRawValue(data);
            if (showChanged && selectedCol == this && ir.HasValue)
            {
                return new IntRange((int)((ir.Value.min + batchFlat) * batchPrec), (int)((ir.Value.max + batchFlat) * batchPrec)).ToString();
            }
            return ir.ToString();
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
