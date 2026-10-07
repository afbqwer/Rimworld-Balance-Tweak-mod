using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BalanceTweak;

public static class SerializationHelper
{
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
    //  StatModifier
    // ============================================================

    #region StatModifier

    public static string StatModifierToItemString(StatModifier sm)
        => $"{sm.stat?.defName ?? "NULL"}|{sm.value}";

    public static StatModifier? ParseStatModifier(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("StatModifier", str, "statDefName|value");
            return null;
        }
        var statDef = DefDatabase<StatDef>.GetNamedSilentFail(parts[0]);
        if (statDef == null)
        {
            LogMissingDef("StatModifier.stat", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var val))
        {
            WarnSyntaxError("StatModifier", str, "statDefName|value");
            return null;
        }
        return new StatModifier { stat = statDef, value = val };
    }

    public static string? SerializeStatModifierList(List<StatModifier>? list)
        => SerializeList(list, StatModifierToItemString);

    public static List<StatModifier>? DeserializeStatModifierList(string? data)
        => DeserializeList(data, ParseStatModifier, "DeserializeStatModifierList");

    #endregion

    #region PawnCapacityModifier

    public static string PawnCapacityModifierToItemString(PawnCapacityModifier pcm)
        => $"{pcm.capacity?.defName ?? "NULL"}|{pcm.offset}|{pcm.setMax}|{pcm.postFactor}|{pcm.statFactorMod?.defName ?? "NULL"}|{pcm.setMaxCurveEvaluateStat?.defName ?? "NULL"}";

    public static PawnCapacityModifier? ParsePawnCapacityModifier(string str)
    {
        var parts = str.Split('|');
        if (parts.Length < 4)
        {
            WarnSyntaxError("PawnCapacityModifier", str, "capacityDefName|offset|setMax|postFactor[|statFactorModDefName[|setMaxCurveEvaluateStatDefName]]");
            return null;
        }
        var pcm = new PawnCapacityModifier();
        pcm.capacity = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(parts[0]);
        if (pcm.capacity == null)
        {
            LogMissingDef("PawnCapacityModifier.capacity", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out pcm.offset))
        {
            WarnSyntaxError("PawnCapacityModifier", str, "offset 应为浮点数");
            return null;
        }
        if (!float.TryParse(parts[2], out pcm.setMax))
        {
            WarnSyntaxError("PawnCapacityModifier", str, "setMax 应为浮点数");
            return null;
        }
        if (!float.TryParse(parts[3], out pcm.postFactor))
        {
            WarnSyntaxError("PawnCapacityModifier", str, "postFactor 应为浮点数");
            return null;
        }
        if (parts.Length >= 5 && parts[4] != "NULL" && !parts[4].NullOrEmpty())
        {
            pcm.statFactorMod = DefDatabase<StatDef>.GetNamedSilentFail(parts[4]);
            if (pcm.statFactorMod == null)
            {
                LogMissingDef("PawnCapacityModifier.statFactorMod", parts[4]);
                return null;
            }
        }
        if (parts.Length >= 6 && parts[5] != "NULL" && !parts[5].NullOrEmpty())
        {
            pcm.setMaxCurveEvaluateStat = DefDatabase<StatDef>.GetNamedSilentFail(parts[5]);
            if (pcm.setMaxCurveEvaluateStat == null)
            {
                LogMissingDef("PawnCapacityModifier.setMaxCurveEvaluateStat", parts[5]);
                return null;
            }
        }
        return pcm;
    }

    public static string? SerializePawnCapacityModifierList(List<PawnCapacityModifier>? list)
        => SerializeList(list, PawnCapacityModifierToItemString);

    public static List<PawnCapacityModifier>? DeserializePawnCapacityModifierList(string? data)
        => DeserializeList(data, ParsePawnCapacityModifier, "DeserializePawnCapacityModifierList");

    #endregion

    #region DamageFactor

    public static string DamageFactorToItemString(DamageFactor df)
        => $"{df.damageDef?.defName ?? "NULL"}|{df.factor}";

    public static DamageFactor? ParseDamageFactor(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("DamageFactor", str, "damageDefName|factor");
            return null;
        }
        var damageDef = DefDatabase<DamageDef>.GetNamedSilentFail(parts[0]);
        if (damageDef == null)
        {
            LogMissingDef("DamageFactor.damageDef", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var val))
        {
            WarnSyntaxError("DamageFactor", str, "damageDefName|factor");
            return null;
        }
        return new DamageFactor { damageDef = damageDef, factor = val };
    }

    public static string? SerializeDamageFactorList(List<DamageFactor>? list)
        => SerializeList(list, DamageFactorToItemString);

    public static List<DamageFactor>? DeserializeDamageFactorList(string? data)
        => DeserializeList(data, ParseDamageFactor, "DeserializeDamageFactorList");

    #endregion

    #region SkillGain

    public static string SkillGainToItemString(SkillGain sg)
        => $"{sg.skill?.defName ?? "NULL"}|{sg.amount}";

    public static SkillGain? ParseSkillGain(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("SkillGain", str, "skillDefName|amount");
            return null;
        }
        var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(parts[0]);
        if (skillDef == null)
        {
            LogMissingDef("SkillGain.skill", parts[0]);
            return null;
        }
        if (!int.TryParse(parts[1], out var val))
        {
            WarnSyntaxError("SkillGain", str, "skillDefName|amount（amount 应为整数）");
            return null;
        }
        return new SkillGain { skill = skillDef, amount = val };
    }

    public static string? SerializeSkillGainList(List<SkillGain>? list)
        => SerializeList(list, SkillGainToItemString);

    public static List<SkillGain>? DeserializeSkillGainList(string? data)
        => DeserializeList(data, ParseSkillGain, "DeserializeSkillGainList");

    #endregion

    #region Aptitude

    public static string AptitudeToItemString(Aptitude a)
        => $"{a.skill?.defName ?? "NULL"}|{a.level}";

    public static Aptitude? ParseAptitude(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("Aptitude", str, "skillDefName|level");
            return null;
        }
        var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(parts[0]);
        if (skillDef == null)
        {
            LogMissingDef("Aptitude.skill", parts[0]);
            return null;
        }
        if (!int.TryParse(parts[1], out var val))
        {
            WarnSyntaxError("Aptitude", str, "skillDefName|level（level 应为整数）");
            return null;
        }
        return new Aptitude(skillDef, val);
    }

    public static string? SerializeAptitudeList(List<Aptitude>? list)
        => SerializeList(list, AptitudeToItemString);

    public static List<Aptitude>? DeserializeAptitudeList(string? data)
        => DeserializeList(data, ParseAptitude, "DeserializeAptitudeList");

    #endregion

    #region GeneticTraitData

    public static string GeneticTraitDataToItemString(GeneticTraitData gtd)
        => $"{gtd.def?.defName ?? "NULL"}|{gtd.degree}";

    public static GeneticTraitData? ParseGeneticTraitData(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("GeneticTraitData", str, "traitDefName|degree");
            return null;
        }
        var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(parts[0]);
        if (traitDef == null)
        {
            LogMissingDef("GeneticTraitData.def", parts[0]);
            return null;
        }
        if (!int.TryParse(parts[1], out var degree))
        {
            WarnSyntaxError("GeneticTraitData", str, "traitDefName|degree（degree 应为整数）");
            return null;
        }
        return new GeneticTraitData { def = traitDef, degree = degree };
    }

    public static string? SerializeGeneticTraitDataList(List<GeneticTraitData>? list)
        => SerializeList(list, GeneticTraitDataToItemString);

    public static List<GeneticTraitData>? DeserializeGeneticTraitDataList(string? data)
        => DeserializeList(data, ParseGeneticTraitData, "DeserializeGeneticTraitDataList");

    #endregion

    #region ThingDefCountClass

    public static string ThingDefCountClassToItemString(ThingDefCountClass tdc)
        => $"{tdc.thingDef?.defName ?? "NULL"}|{tdc.stuff?.defName ?? "NULL"}|{tdc.count}";

    public static ThingDefCountClass? ParseThingDefCountClass(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 3)
        {
            WarnSyntaxError("ThingDefCountClass", str, "thingDefName|stuffDefName|count");
            return null;
        }
        var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
        if (thingDef == null)
        {
            LogMissingDef("ThingDefCountClass.thingDef", parts[0]);
            return null;
        }
        ThingDef? stuff = null;
        if (parts[1] != "NULL" && !parts[1].NullOrEmpty())
        {
            stuff = DefDatabase<ThingDef>.GetNamedSilentFail(parts[1]);
            if (stuff == null)
            {
                LogMissingDef("ThingDefCountClass.stuff", parts[1]);
                return null;
            }
        }
        if (!int.TryParse(parts[2], out var count) || count < 0)
        {
            WarnSyntaxError("ThingDefCountClass", str, "count 应为非负整数");
            return null;
        }
        return new ThingDefCountClass
        {
            thingDef = thingDef,
            stuff = stuff,
            count = count
        };
    }

    public static string? SerializeThingDefCountClassList(List<ThingDefCountClass>? list)
        => SerializeList(list, ThingDefCountClassToItemString);

    public static List<ThingDefCountClass>? DeserializeThingDefCountClassList(string? data)
        => DeserializeList(data, ParseThingDefCountClass, "DeserializeThingDefCountClassList");

    #endregion

    #region IngredientCount

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

    public static string SkillRequirementToItemString(SkillRequirement sr)
        => $"{sr.skill?.defName ?? "NULL"}|{sr.minLevel}";

    public static SkillRequirement? ParseSkillRequirement(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("SkillRequirement", str, "skillDefName|minLevel");
            return null;
        }
        var skill = DefDatabase<SkillDef>.GetNamedSilentFail(parts[0]);
        if (skill == null)
        {
            LogMissingDef("SkillRequirement.skill", parts[0]);
            return null;
        }
        if (!int.TryParse(parts[1], out var minLevel))
        {
            WarnSyntaxError("SkillRequirement", str, "skillDefName|minLevel（minLevel 应为整数）");
            return null;
        }
        return new SkillRequirement { skill = skill, minLevel = minLevel };
    }

    public static string? SerializeSkillRequirementList(List<SkillRequirement>? list)
        => SerializeList(list, SkillRequirementToItemString);

    public static List<SkillRequirement>? DeserializeSkillRequirementList(string? data)
        => DeserializeList(data, ParseSkillRequirement, "DeserializeSkillRequirementList");

    #endregion

    #region TraitRequirement

    public static string TraitRequirementToItemString(TraitRequirement tr)
        => $"{tr.def?.defName ?? "NULL"}|{(tr.degree.HasValue ? tr.degree.Value.ToString() : "")}";

    public static TraitRequirement? ParseTraitRequirement(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("TraitRequirement", str, "traitDefName|degree（degree 可为空表示任意强度）");
            return null;
        }
        var trait = DefDatabase<TraitDef>.GetNamedSilentFail(parts[0]);
        if (trait == null)
        {
            LogMissingDef("TraitRequirement.def", parts[0]);
            return null;
        }
        int? degree = null;
        if (!parts[1].NullOrEmpty())
        {
            if (!int.TryParse(parts[1], out var deg))
            {
                WarnSyntaxError("TraitRequirement", str, "traitDefName|degree（degree 应为整数或空）");
                return null;
            }
            degree = deg;
        }
        return new TraitRequirement { def = trait, degree = degree };
    }

    public static string? SerializeTraitRequirementList(List<TraitRequirement>? list)
        => SerializeList(list, TraitRequirementToItemString);

    public static List<TraitRequirement>? DeserializeTraitRequirementList(string? data)
        => DeserializeList(data, ParseTraitRequirement, "DeserializeTraitRequirementList");

    #endregion

    #region BiomePlantRecord

    public static string BiomePlantRecordToItemString(BiomePlantRecord r)
        => $"{r.plant?.defName ?? "NULL"}|{r.commonality}";

    public static BiomePlantRecord? ParseBiomePlantRecord(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("BiomePlantRecord", str, "plantDefName|commonality");
            return null;
        }
        var plant = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
        if (plant == null)
        {
            LogMissingDef("BiomePlantRecord.plant", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var commonality))
        {
            WarnSyntaxError("BiomePlantRecord", str, "plantDefName|commonality（commonality 应为浮点数）");
            return null;
        }
        return new BiomePlantRecord { plant = plant, commonality = commonality };
    }

    public static string? SerializeBiomePlantList(List<BiomePlantRecord>? list)
        => SerializeList(list, BiomePlantRecordToItemString);

    public static List<BiomePlantRecord>? DeserializeBiomePlantList(string? data)
        => DeserializeList(data, ParseBiomePlantRecord, "DeserializeBiomePlantList");

    #endregion

    #region BiomeAnimalRecord

    public static string BiomeAnimalRecordToItemString(BiomeAnimalRecord r)
        => $"{r.animal?.defName ?? "NULL"}|{r.commonality}";

    public static BiomeAnimalRecord? ParseBiomeAnimalRecord(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("BiomeAnimalRecord", str, "animalKindDefName|commonality");
            return null;
        }
        var animal = DefDatabase<PawnKindDef>.GetNamedSilentFail(parts[0]);
        if (animal == null)
        {
            LogMissingDef("BiomeAnimalRecord.animal", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var commonality))
        {
            WarnSyntaxError("BiomeAnimalRecord", str, "animalKindDefName|commonality（commonality 应为浮点数）");
            return null;
        }
        return new BiomeAnimalRecord { animal = animal, commonality = commonality };
    }

    public static string? SerializeBiomeAnimalList(List<BiomeAnimalRecord>? list)
        => SerializeList(list, BiomeAnimalRecordToItemString);

    public static List<BiomeAnimalRecord>? DeserializeBiomeAnimalList(string? data)
        => DeserializeList(data, ParseBiomeAnimalRecord, "DeserializeBiomeAnimalList");

    #endregion

    #region FishChance

    public static string FishChanceToItemString(FishChance f)
        => $"{f.fishDef?.defName ?? "NULL"}|{f.chance}";

    public static FishChance? ParseFishChance(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("FishChance", str, "fishDefName|chance");
            return null;
        }
        var fish = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
        if (fish == null)
        {
            LogMissingDef("FishChance.fishDef", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var chance))
        {
            WarnSyntaxError("FishChance", str, "fishDefName|chance（chance 应为浮点数）");
            return null;
        }
        return new FishChance { fishDef = fish, chance = chance };
    }

    public static string? SerializeFishChanceList(List<FishChance>? list)
        => SerializeList(list, FishChanceToItemString);

    public static List<FishChance>? DeserializeFishChanceList(string? data)
        => DeserializeList(data, ParseFishChance, "DeserializeFishChanceList");

    #endregion

    #region BiomeDiseaseRecord

    public static string BiomeDiseaseRecordToItemString(BiomeDiseaseRecord r)
        => $"{r.diseaseInc?.defName ?? "NULL"}|{r.commonality}";

    public static BiomeDiseaseRecord? ParseBiomeDiseaseRecord(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("BiomeDiseaseRecord", str, "diseaseIncidentDefName|commonality");
            return null;
        }
        var diseaseInc = DefDatabase<IncidentDef>.GetNamedSilentFail(parts[0]);
        if (diseaseInc == null)
        {
            LogMissingDef("BiomeDiseaseRecord.diseaseInc", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var commonality))
        {
            WarnSyntaxError("BiomeDiseaseRecord", str, "diseaseIncidentDefName|commonality（commonality 应为浮点数）");
            return null;
        }
        return new BiomeDiseaseRecord { diseaseInc = diseaseInc, commonality = commonality };
    }

    public static string? SerializeDiseaseList(List<BiomeDiseaseRecord>? list)
        => SerializeList(list, BiomeDiseaseRecordToItemString);

    public static List<BiomeDiseaseRecord>? DeserializeDiseaseList(string? data)
        => DeserializeList(data, ParseBiomeDiseaseRecord, "DeserializeDiseaseList");

    #endregion

    #region WeatherCommonalityRecord

    public static string WeatherCommonalityRecordToItemString(WeatherCommonalityRecord r)
        => $"{r.weather?.defName ?? "NULL"}|{r.commonality}";

    public static WeatherCommonalityRecord? ParseWeatherCommonalityRecord(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 2)
        {
            WarnSyntaxError("WeatherCommonalityRecord", str, "weatherDefName|commonality");
            return null;
        }
        var weather = DefDatabase<WeatherDef>.GetNamedSilentFail(parts[0]);
        if (weather == null)
        {
            LogMissingDef("WeatherCommonalityRecord.weather", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var commonality))
        {
            WarnSyntaxError("WeatherCommonalityRecord", str, "weatherDefName|commonality（commonality 应为浮点数）");
            return null;
        }
        return new WeatherCommonalityRecord { weather = weather, commonality = commonality };
    }

    public static string? SerializeWeatherCommonalityList(List<WeatherCommonalityRecord>? list)
        => SerializeList(list, WeatherCommonalityRecordToItemString);

    public static List<WeatherCommonalityRecord>? DeserializeWeatherCommonalityList(string? data)
        => DeserializeList(data, ParseWeatherCommonalityRecord, "DeserializeWeatherCommonalityList");

    #endregion

    #region TerrainThreshold

    public static string TerrainThresholdToItemString(TerrainThreshold t)
        => $"{t.terrain?.defName ?? "NULL"}|{t.min}|{t.max}";

    public static TerrainThreshold? ParseTerrainThreshold(string str)
    {
        var parts = str.Split('|');
        if (parts.Length != 3)
        {
            WarnSyntaxError("TerrainThreshold", str, "terrainDefName|min|max");
            return null;
        }
        var terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(parts[0]);
        if (terrain == null)
        {
            LogMissingDef("TerrainThreshold.terrain", parts[0]);
            return null;
        }
        if (!float.TryParse(parts[1], out var min) || !float.TryParse(parts[2], out var max))
        {
            WarnSyntaxError("TerrainThreshold", str, "terrainDefName|min|max（min/max 应为浮点数）");
            return null;
        }
        return new TerrainThreshold { terrain = terrain, min = min, max = max };
    }

    public static string? SerializeTerrainThresholdList(List<TerrainThreshold>? list)
        => SerializeList(list, TerrainThresholdToItemString);

    public static List<TerrainThreshold>? DeserializeTerrainThresholdList(string? data)
        => DeserializeList(data, ParseTerrainThreshold, "DeserializeTerrainThresholdList");

    #endregion

    #region ProcessIngredientItem

    public static string ProcessIngredientItemToItemString(ProcessIngredientItem i)
    {
        var thingStr = i.thing?.defName ?? "";
        var catStr = i.thingCategory?.defName ?? "";
        var disallowedStr = i.disallowedThingDefs != null && i.disallowedThingDefs.Count > 0
            ? string.Join(",", i.disallowedThingDefs.Select(d => d.defName))
            : "";
        return $"{thingStr}|{catStr}|{disallowedStr}|{i.countNeeded}";
    }

    public static ProcessIngredientItem? ParseProcessIngredientItem(string str)
    {
        var parts = str.Split('|');
        if (parts.Length < 4)
        {
            WarnSyntaxError("ProcessIngredientItem", str, "thingDefName|thingCategoryDefName|disallowedThingDefs|countNeeded");
            return null;
        }
        var item = new ProcessIngredientItem();
        if (!parts[0].NullOrEmpty())
        {
            item.thing = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
            if (item.thing == null)
            {
                LogMissingDef("ProcessIngredientItem.thing", parts[0]);
                return null;
            }
        }
        if (!parts[1].NullOrEmpty())
        {
            item.thingCategory = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(parts[1]);
            if (item.thingCategory == null)
            {
                LogMissingDef("ProcessIngredientItem.thingCategory", parts[1]);
                return null;
            }
        }
        if (parts.Length >= 3 && !parts[2].NullOrEmpty())
        {
            var disallowedNames = parts[2].Split(',');
            item.disallowedThingDefs = new List<ThingDef>();
            foreach (var name in disallowedNames)
            {
                var td = DefDatabase<ThingDef>.GetNamedSilentFail(name.Trim());
                if (td == null)
                {
                    LogMissingDef("ProcessIngredientItem.disallowedThingDefs", name.Trim());
                    return null;
                }
                item.disallowedThingDefs.Add(td);
            }
        }
        if (parts.Length >= 4)
        {
            if (!float.TryParse(parts[3], out item.countNeeded))
            {
                WarnSyntaxError("ProcessIngredientItem", str, "countNeeded 应为浮点数");
                return null;
            }
        }
        return item;
    }

    public static string? SerializeProcessIngredientItemList(List<ProcessIngredientItem>? list)
        => SerializeList(list, ProcessIngredientItemToItemString);

    public static List<ProcessIngredientItem>? DeserializeProcessIngredientItemList(string? data)
        => DeserializeList(data, ParseProcessIngredientItem, "DeserializeProcessIngredientItemList");

    #endregion

    #region ProcessResultItem

    public static string ProcessResultItemToItemString(ProcessResultItem r)
    {
        var thingStr = r.thing?.defName ?? "";
        return $"{thingStr}|{r.count}";
    }

    public static ProcessResultItem? ParseProcessResultItem(string str)
    {
        var parts = str.Split('|');
        if (parts.Length < 2)
        {
            WarnSyntaxError("ProcessResultItem", str, "thingDefName|count");
            return null;
        }
        var item = new ProcessResultItem();
        if (!parts[0].NullOrEmpty())
        {
            item.thing = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0]);
            if (item.thing == null)
            {
                LogMissingDef("ProcessResultItem.thing", parts[0]);
                return null;
            }
        }
        if (!int.TryParse(parts[1], out item.count) || item.count < 0)
        {
            WarnSyntaxError("ProcessResultItem", str, "count 应为非负整数");
            return null;
        }
        return item;
    }

    public static string? SerializeProcessResultItemList(List<ProcessResultItem>? list)
        => SerializeList(list, ProcessResultItemToItemString);

    public static List<ProcessResultItem>? DeserializeProcessResultItemList(string? data)
        => DeserializeList(data, ParseProcessResultItem, "DeserializeProcessResultItemList");

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

    #region SimpleCurve

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

    #region ThingFilter

    public static string? SerializeThingFilter(ThingFilter? filter)
    {
        return IngredientFilterToString(filter);
    }

    public static ThingFilter? DeserializeThingFilter(string? data)
    {
        return ParseIngredientFilter(data);
    }

    public static readonly FieldInfo TfCategoriesField = AccessTools.Field(typeof(ThingFilter), "categories");
    public static readonly FieldInfo TfDisallowedCategoriesField = AccessTools.Field(typeof(ThingFilter), "disallowedCategories");
    public static readonly FieldInfo TfThingDefsField = AccessTools.Field(typeof(ThingFilter), "thingDefs");
    public static readonly FieldInfo TfSpecialFiltersToAllowField = AccessTools.Field(typeof(ThingFilter), "specialFiltersToAllow");
    public static readonly FieldInfo TfSpecialFiltersToDisallowField = AccessTools.Field(typeof(ThingFilter), "specialFiltersToDisallow");
    public static readonly FieldInfo TfStuffCategoriesToAllowField = AccessTools.Field(typeof(ThingFilter), "stuffCategoriesToAllow");

    public static readonly FieldInfo TfTradeTagsToAllowField = AccessTools.Field(typeof(ThingFilter), "tradeTagsToAllow");
    public static readonly FieldInfo TfTradeTagsToDisallowField = AccessTools.Field(typeof(ThingFilter), "tradeTagsToDisallow");
    public static readonly FieldInfo TfThingSetMakerTagsToAllowField = AccessTools.Field(typeof(ThingFilter), "thingSetMakerTagsToAllow");
    public static readonly FieldInfo TfThingSetMakerTagsToDisallowField = AccessTools.Field(typeof(ThingFilter), "thingSetMakerTagsToDisallow");
    public static readonly FieldInfo TfAllowAllWhoCanMakeField = AccessTools.Field(typeof(ThingFilter), "allowAllWhoCanMake");
    public static readonly FieldInfo TfDisallowedThingDefsField = AccessTools.Field(typeof(ThingFilter), "disallowedThingDefs");

    public static readonly FieldInfo TfAllowedDefsField = AccessTools.Field(typeof(ThingFilter), "allowedDefs");

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

    #region BodyPart tree

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