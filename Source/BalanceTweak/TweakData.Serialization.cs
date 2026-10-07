using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public abstract partial class TweakData<T> : TweakData where T : TweakData<T>
{
    public override void ExposeData()
    {
        base.ExposeData();

        // Backward compatibility: if _modifiedFieldNames is empty and _backCompatResolved
        // is false, it might be an old save from before the field tracking was introduced.
        // Load all fields and record which ones are non-null.
        if (!_backCompatResolved && _modifiedFieldNames.Count == 0 &&
            Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.PostLoadInit)
        {
            LoadAllFieldsForBackCompat();
            return;
        }

        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            if (!_modifiedFieldNames.Contains(meta.Field.Name)) continue;

            if (Scribe.mode == LoadSaveMode.Saving &&
                _corruptedFieldNames.Contains(meta.Field.Name) &&
                _savedLoadData != null &&
                _savedLoadData.TryGetValue(meta.Field.Name, out var rawValue))
            {
                // 损坏字段：从 _savedLoadData 保存原始字符串数据
                if (rawValue is string str)
                    Scribe_Values.Look(ref str!, meta.Field.Name);
                else if (rawValue is List<string> list)
                    Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
                else
                    Log.Error($"[BalanceTweak] 损坏字段 {meta.Field.Name}（{id}）的 _savedLoadData 包含不支持的类型: {rawValue?.GetType()}");
            }
            else
            {
                ExposeField(meta); // 正常保存/加载
            }
        }
    }

    private void LoadAllFieldsForBackCompat()
    {
        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            ExposeField(meta);
        }

        // Only modify _modifiedFieldNames in PostLoadInit to avoid state mutation during LoadingVars,
        // which could cause issues if ExposeData is called again.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            foreach (var meta in Fields)
            {
                if (meta.Attr.DataType != ColumnDataType.Field) continue;
                var value = meta.Field.GetValue(this);
                // value != null catches simple types (float?, int?, bool?, etc.) loaded via ExposeSimple.
                // _savedLoadData.ContainsKey catches complex types (Def, List<>, ThingFilter, etc.)
                // whose data was stored in _savedLoadData by ExposeDef/ExposeList/etc. during LoadingVars
                // but whose field value hasn't been SetValue'd yet (will be done by ResolveDefs later).
                if (value != null || _savedLoadData?.ContainsKey(meta.Field.Name) == true)
                    _modifiedFieldNames.Add(meta.Field.Name);
            }
            _backCompatResolved = true;
        }
    }

    private void ExposeField(StatFieldMeta meta)
    {
        var fieldType = meta.Field.FieldType;

        if (fieldType == typeof(float?)) ExposeSimple<float>(meta);
        else if (fieldType == typeof(int?)) ExposeSimple<int>(meta);
        else if (fieldType == typeof(bool?)) ExposeSimple<bool>(meta);
        else if (fieldType == typeof(FloatRange?)) ExposeSimple<FloatRange>(meta);
        else if (fieldType == typeof(IntRange?)) ExposeSimple<IntRange>(meta);
        else if (fieldType == typeof(string)) ExposeString(meta);
        else if (fieldType == typeof(SimpleCurve)) ExposeCurve(meta);
        else if (Nullable.GetUnderlyingType(fieldType)?.IsEnum == true) ExposeEnum(meta);
        else if (fieldType.IsSubclassOf(typeof(Def))) ExposeDef(meta);
        else if (fieldType == typeof(List<StatModifier>)) ExposeStatModifierList(meta);
        else if (fieldType == typeof(List<PawnCapacityModifier>)) ExposePawnCapacityList(meta);
        else if (fieldType == typeof(List<DamageFactor>)) ExposeDamageFactorList(meta);
        else if (fieldType == typeof(List<SkillGain>)) ExposeSkillGainList(meta);
        else if (fieldType == typeof(List<Aptitude>)) ExposeAptitudeList(meta);
        else if (fieldType == typeof(List<GeneticTraitData>)) ExposeGeneticTraitDataList(meta);
        else if (fieldType == typeof(List<ThingDefCountClass>)) ExposeThingDefCountList(meta);
        else if (fieldType == typeof(List<IngredientCount>)) ExposeIngredientList(meta);
        else if (fieldType == typeof(List<SkillRequirement>)) ExposeSkillReqList(meta);
        else if (fieldType == typeof(List<TraitRequirement>)) ExposeTraitReqList(meta);
        else if (fieldType == typeof(List<ProcessIngredientItem>)) ExposeProcessIngredientList(meta);
        else if (fieldType == typeof(List<ProcessResultItem>)) ExposeProcessResultList(meta);
        else if (fieldType == typeof(ThingFilter)) ExposeIngredientFilter(meta);
        else if (fieldType == typeof(List<string>)) ExposeStringList(meta);
        else if (fieldType == typeof(List<int>)) ExposeIntList(meta);
        else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = fieldType.GetGenericArguments()[0];
            if (elementType.IsSubclassOf(typeof(Def))) ExposeDefList(meta);
        }
    }

    private void ExposeSimple<TU>(StatFieldMeta meta) where TU : struct
    {
        var value = (TU?)meta.Field.GetValue(this);
        Scribe_Values.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(this, value);
    }

    private void ExposeString(StatFieldMeta meta)
    {
        var value = (string)meta.Field.GetValue(this);
        Scribe_Values.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(this, value);
    }

    private void ExposeCurve(StatFieldMeta meta)
    {
        var value = (SimpleCurve)meta.Field.GetValue(this);
        Scribe_Deep.Look(ref value, meta.Field.Name);
        meta.Field.SetValue(this, value);
    }

    private void ExposeEnum(StatFieldMeta meta)
    {
        var fieldType = meta.Field.FieldType;
        var enumType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        var underlyingType = Enum.GetUnderlyingType(enumType);
        long? value = meta.Field.GetValue(this) != null ? Convert.ToInt64(meta.Field.GetValue(this)) : null;
        Scribe_Values.Look(ref value, meta.Field.Name);
        if (value.HasValue)
        {
            var underlyingValue = Convert.ChangeType(value.Value, underlyingType);
            meta.Field.SetValue(this, Enum.ToObject(enumType, underlyingValue));
        }
        else
        {
            meta.Field.SetValue(this, null);
        }
    }

    private void ExposeDef(StatFieldMeta meta)
    {
        var defVal = meta.Field.GetValue(this) as Def;
        string? defName = defVal?.defName;
        Scribe_Values.Look(ref defName, meta.Field.Name);
        if (Scribe.mode == LoadSaveMode.LoadingVars && !defName.NullOrEmpty())
        {
            (_savedLoadData ??= new())[meta.Field.Name] = defName;
        }
    }

    private void ExposeList(StatFieldMeta meta, Func<object?, List<string>?> serialize)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            var strList = serialize(meta.Field.GetValue(this));
            if (strList == null || strList.Count == 0) return;
            Scribe_Collections.Look(ref strList, meta.Field.Name, LookMode.Value);
        }
        else
        {
            List<string>? strList = null;
            Scribe_Collections.Look(ref strList, meta.Field.Name, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                (_savedLoadData ??= new())[meta.Field.Name] = strList;
        }
    }

    private void ExposeStatModifierList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<StatModifier>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.StatModifierToItemString).ToList();
    });

    private void ExposePawnCapacityList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<PawnCapacityModifier>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.PawnCapacityModifierToItemString).ToList();
    });

    private void ExposeDamageFactorList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<DamageFactor>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.DamageFactorToItemString).ToList();
    });

    private void ExposeSkillGainList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<SkillGain>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.SkillGainToItemString).ToList();
    });

    private void ExposeAptitudeList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<Aptitude>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.AptitudeToItemString).ToList();
    });

    private void ExposeGeneticTraitDataList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<GeneticTraitData>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.GeneticTraitDataToItemString).ToList();
    });

    private void ExposeThingDefCountList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (List<ThingDefCountClass>?)v;
        if (list == null || list.Count == 0) return null;
        return list.Select(SerializationHelper.ThingDefCountClassToItemString).ToList();
    });

    private void ExposeDefList(StatFieldMeta meta) => ExposeList(meta, v =>
    {
        var list = (System.Collections.IList?)v;
        if (list == null || list.Count == 0) return null;
        var result = new List<string>();
        foreach (var item in list)
            if (item is Def def) result.Add(def.defName);
        return result;
    });

    private void ExposeStringList(StatFieldMeta meta)
    {
        var list = (List<string>?)meta.Field.GetValue(this);
        Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            (_savedLoadData ??= new())[meta.Field.Name] = list;
    }

    private void ExposeIntList(StatFieldMeta meta)
    {
        var list = (List<int>?)meta.Field.GetValue(this);
        Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            (_savedLoadData ??= new())[meta.Field.Name] = list;
    }

    private void ExposeIngredientList(StatFieldMeta meta)
    {
        ExposeList(meta, v =>
        {
            var list = (List<IngredientCount>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(ic =>
            {
                var filterStr = SerializationHelper.IngredientFilterToString(ic.filter);
                if (filterStr.Length == 0) return null;
                return $"{filterStr}|{ic.GetBaseCount()}";
            }).Where(s => s != null).ToList()!;
        });
    }

    private void ExposeSkillReqList(StatFieldMeta meta)
    {
        ExposeList(meta, v =>
        {
            var list = (List<SkillRequirement>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.SkillRequirementToItemString).ToList();
        });
    }

    private void ExposeTraitReqList(StatFieldMeta meta)
    {
        ExposeList(meta, v =>
        {
            var list = (List<TraitRequirement>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.TraitRequirementToItemString).ToList();
        });
    }

    private void ExposeIngredientFilter(StatFieldMeta meta)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            var filter = (ThingFilter?)meta.Field.GetValue(this);
            var str = SerializationHelper.IngredientFilterToString(filter);
            Scribe_Values.Look(ref str, meta.Field.Name);
        }
        else
        {
            string? str = null;
            Scribe_Values.Look(ref str, meta.Field.Name);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                (_savedLoadData ??= new())[meta.Field.Name] = str;
        }
    }

    #region ProcessDef Serialization

    private void ExposeProcessIngredientList(StatFieldMeta meta)
    {
        ExposeList(meta, v =>
        {
            var list = (List<ProcessIngredientItem>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.ProcessIngredientItemToItemString).ToList();
        });
    }

    private void ExposeProcessResultList(StatFieldMeta meta)
    {
        ExposeList(meta, v =>
        {
            var list = (List<ProcessResultItem>?)v;
            if (list == null || list.Count == 0) return null;
            return list.Select(SerializationHelper.ProcessResultItemToItemString).ToList();
        });
    }

    #endregion
}