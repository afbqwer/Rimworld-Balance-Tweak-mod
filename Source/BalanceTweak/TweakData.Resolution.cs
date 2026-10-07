using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public abstract partial class TweakData<T> : TweakData where T : TweakData<T>
{
    public override bool ResolveDefs()
    {
        if (_savedLoadData == null) return true;

        // Backup current field values so we can restore on partial failure,
        // preventing inconsistent state where some fields are resolved and others aren't.
        var backup = new Dictionary<string, object?>();
        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            if (_savedLoadData.ContainsKey(meta.Field.Name))
                backup[meta.Field.Name] = meta.Field.GetValue(this);
        }

        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;

            if (!TryResolveField(meta))
            {
                _corruptedFieldNames.Add(meta.Field.Name);
                continue;
            }
            _corruptedFieldNames.Remove(meta.Field.Name);
        }

        // 有损坏字段时：仅回滚损坏字段，保留成功解析的字段
        // 同时保留 _savedLoadData 供保存损坏字段的原始字符串
        if (_corruptedFieldNames.Count > 0)
        {
            foreach (var meta in Fields)
            {
                if (_corruptedFieldNames.Contains(meta.Field.Name) && backup.TryGetValue(meta.Field.Name, out var val))
                    meta.Field.SetValue(this, val);
            }
        }
        else
        {
            _savedLoadData = null;
        }
        return _corruptedFieldNames.Count == 0;
    }

    private bool TryResolveField(StatFieldMeta meta)
    {
        var fieldType = meta.Field.FieldType;

        if (fieldType.IsSubclassOf(typeof(Def))) return ResolveDef(meta);
        if (fieldType == typeof(List<StatModifier>)) return ResolveStatModifierList(meta);
        if (fieldType == typeof(List<PawnCapacityModifier>)) return ResolvePawnCapacityList(meta);
        if (fieldType == typeof(List<DamageFactor>)) return ResolveDamageFactorList(meta);
        if (fieldType == typeof(List<SkillGain>)) return ResolveSkillGainList(meta);
        if (fieldType == typeof(List<Aptitude>)) return ResolveAptitudeList(meta);
        if (fieldType == typeof(List<GeneticTraitData>)) return ResolveGeneticTraitDataList(meta);
        if (fieldType == typeof(List<ThingDefCountClass>)) return ResolveThingDefCountList(meta);
        if (fieldType == typeof(List<IngredientCount>)) return ResolveIngredientList(meta);
        if (fieldType == typeof(List<SkillRequirement>)) return ResolveSkillReqList(meta);
        if (fieldType == typeof(List<TraitRequirement>)) return ResolveTraitReqList(meta);
        if (fieldType == typeof(List<ProcessIngredientItem>)) return ResolveProcessIngredientList(meta);
        if (fieldType == typeof(List<ProcessResultItem>)) return ResolveProcessResultList(meta);
        if (fieldType == typeof(ThingFilter)) return ResolveIngredientFilter(meta);
        if (fieldType == typeof(List<string>)) return ResolveStringList(meta);
        if (fieldType == typeof(List<int>)) return ResolveIntList(meta);
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)
            && fieldType.GetGenericArguments()[0].IsSubclassOf(typeof(Def)))
            return ResolveDefList(meta);
        return true;
    }

    private bool ResolveList(StatFieldMeta meta, Func<List<string>, (object? result, bool success)> parse)
    {
        if (_savedLoadData == null || !_savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not List<string> strList || strList.Count == 0)
            return true;
        var (result, success) = parse(strList);
        if (!success) return false;
        if (result != null)
            meta.Field.SetValue(this, result);
        return true;
    }

    private bool ResolveDef(StatFieldMeta meta)
    {
        if (_savedLoadData == null || !_savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not string defName || defName.NullOrEmpty())
            return true;
        var def = GenDefDatabase.GetDefSilentFail(meta.Field.FieldType, defName);
        if (def != null)
        {
            meta.Field.SetValue(this, def);
            return true;
        }
        // Def 引用解析失败（例如相关模组被移除）：记录 info 级日志后跳过。
        // 调用方会将该字段标记为损坏并回滚，原始 defName 仍会被保留以便日后重新解析。
        SerializationHelper.LogMissingDef($"字段 {meta.Field.Name}（{meta.Field.FieldType.Name}）", defName);
        return false;
    }

    private bool ResolveStatModifierList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<StatModifier>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseStatModifier(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolvePawnCapacityList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<PawnCapacityModifier>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParsePawnCapacityModifier(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveDamageFactorList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<DamageFactor>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseDamageFactor(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveSkillGainList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<SkillGain>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseSkillGain(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveAptitudeList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<Aptitude>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseAptitude(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveGeneticTraitDataList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<GeneticTraitData>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseGeneticTraitData(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveThingDefCountList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<ThingDefCountClass>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseThingDefCountClass(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveDefList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var defType = meta.Field.FieldType.GetGenericArguments()[0];
        var listType = typeof(List<>).MakeGenericType(defType);
        var result = (System.Collections.IList)Activator.CreateInstance(listType);
        foreach (var str in strList)
        {
            var def = GenDefDatabase.GetDefSilentFail(defType, str);
            if (def != null) result.Add(def);
            else
            {
                SerializationHelper.LogMissingDef($"字段 {meta.Field.Name}（List<{defType.Name}>）", str);
                return (null, false);
            }
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveIngredientList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<IngredientCount>(strList.Count);
        foreach (var str in strList)
        {
            var pipeIdx = str.LastIndexOf('|');
            if (pipeIdx < 0)
            {
                Log.Warning($"[BalanceTweak] IngredientCount 语法解析失败：\"{str}\"（期望 filter|count，字段: {meta.Field.Name}），数据: {id}");
                return (null, false);
            }
            var filterStr = str[..pipeIdx];
            var countStr = str[(pipeIdx + 1)..];
            if (!float.TryParse(countStr, out float count) || count <= 0)
            {
                Log.Warning($"[BalanceTweak] IngredientCount 语法解析失败：\"{str}\"（count 应为正浮点数，字段: {meta.Field.Name}），数据: {id}");
                return (null, false);
            }
            var ic = new IngredientCount();
            ic.SetBaseCount(count);
            var filter = SerializationHelper.ParseIngredientFilter(filterStr);
            if (filter == null) return (null, false);
            ic.filter.CopyAllowancesFrom(filter);
            if (ic.filter.AllowedDefCount == 0) return (null, false); // 过滤器解析后无任何允许项：跳过（缺失的 Def 已在 ParseIngredientFilter 中记录）
            result.Add(ic);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveSkillReqList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<SkillRequirement>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseSkillRequirement(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveTraitReqList(StatFieldMeta meta) => ResolveList(meta, strList =>
    {
        var result = new List<TraitRequirement>(strList.Count);
        foreach (var str in strList)
        {
            var item = SerializationHelper.ParseTraitRequirement(str);
            if (item == null) return (null, false);
            result.Add(item);
        }
        return (result.Count > 0 ? result : null, true);
    });

    private bool ResolveIngredientFilter(StatFieldMeta meta)
    {
        if (_savedLoadData == null || !_savedLoadData.TryGetValue(meta.Field.Name, out var saved) || saved is not string str || str.NullOrEmpty())
            return true;
        var filter = SerializationHelper.ParseIngredientFilter(str);
        if (filter != null)
        {
            meta.Field.SetValue(this, filter);
            return true;
        }
        return false;
    }

    #region ProcessDef Resolution

    private bool ResolveProcessIngredientList(StatFieldMeta meta)
    {
        return ResolveList(meta, strList =>
        {
            var result = new List<ProcessIngredientItem>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseProcessIngredientItem(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    private bool ResolveProcessResultList(StatFieldMeta meta)
    {
        return ResolveList(meta, strList =>
        {
            var result = new List<ProcessResultItem>(strList.Count);
            foreach (var str in strList)
            {
                var item = SerializationHelper.ParseProcessResultItem(str);
                if (item == null) return (null, false);
                result.Add(item);
            }
            return (result.Count > 0 ? result : null, true);
        });
    }

    #endregion

    private bool ResolveStringList(StatFieldMeta meta)
    {
        if (_savedLoadData == null || !_savedLoadData.TryGetValue(meta.Field.Name, out var saved))
            return true;
        var list = saved as List<string>;
        if (list is { Count: > 0 })
            meta.Field.SetValue(this, list);
        return true;
    }

    private bool ResolveIntList(StatFieldMeta meta)
    {
        if (_savedLoadData == null || !_savedLoadData.TryGetValue(meta.Field.Name, out var saved))
            return true;
        var list = saved as List<int>;
        if (list is { Count: > 0 })
            meta.Field.SetValue(this, list);
        return true;
    }
}