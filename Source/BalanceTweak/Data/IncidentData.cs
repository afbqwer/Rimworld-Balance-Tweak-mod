using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(IncidentDef), SettingType.Incident)]
class IncidentData : TweakData<IncidentData>
{
    //// 字段定义
    [TweakField()]
    public float? baseChance = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Prec)]
    public float? actualChance = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String)]
    public string? Category = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? earliestDay = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? minPopulation = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? minGreatestPopulation = null;
    [TweakField(Style = ColumnStyle.Int, MayRequire = "ludeon.rimworld.anomaly")]
    public int? minAnomalyThreatLevel = null;
    [TweakField()]
    public float? minThreatPoints = null;
    [TweakField()]
    public float? maxThreatPoints = null;
    [TweakField(MayRequire = "ludeon.rimworld.royalty")]
    public float? baseChanceWithRoyalty = null;

    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfDisease))]
    public FloatRange? diseaseVictimFractionRange = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // ── 实际概率缓存（category 内 baseChance 之和） ──
    private static Dictionary<int, float> _categorySums = new();

    /// <summary>计算 category 内 baseChance 之和，并更新所有实例的 actualChance。
    /// 优先使用缓存数据（含用户修改），其次回退原始数据，参考 TweakUtility.InitAndUpdateData 的缓存策略。</summary>
    public static void RecalculateFromOriginals()
    {
        _categorySums.Clear();

        // 单次遍历 AllOriginals，预收集所有 IncidentData 实例
        // 优先使用 tweakDatas（用户修改），否则回退到原始数据
        // 避免对无关类型（RaceData、WeaponData 等）的重复遍历
        var incidentDatas = new List<IncidentData>();
        foreach (var (key, value) in TweakDatabase.AllOriginals)
        {
            if (value is not IncidentData original) continue;

            IncidentData id = tweakDatas.TryGetValue(key, out var tweaked) && tweaked is IncidentData tweakedId
                ? tweakedId
                : original;

            incidentDatas.Add(id);
        }

        // 第一遍：计算 category 内 baseChance 之和
        foreach (var id in incidentDatas)
        {
            float baseVal = id.baseChance ?? 0f;
            if (baseVal > 0f)
            {
                _categorySums.TryGetValue(id.propType, out float sum);
                _categorySums[id.propType] = sum + baseVal;
            }
        }

        // 第二遍：更新所有实例的 actualChance
        foreach (var id in incidentDatas)
        {
            UpdateActualChance(id);
        }

        // 同步到 cachedData 中的克隆（UI 使用的实例），确保修改 baseChance 后其它项的 actualChance 刷新
        if (cachedData.TryGetValue(SettingType.Incident, out var cache))
        {
            foreach (var id in incidentDatas)
            {
                if (cache.TryGetValue(id.id, out var cached) && cached is IncidentData cd && cd != id)
                {
                    cd.actualChance = id.actualChance;
                }
            }
        }
    }

    /// <summary>更新单个实例的 actualChance（使用缓存的总和）</summary>
    private static void UpdateActualChance(IncidentData id)
    {
        float baseVal = id.baseChance ?? 0f;
        if (baseVal > 0f && _categorySums.TryGetValue(id.propType, out float sum) && sum > 0f)
        {
            id.actualChance = baseVal / sum;
        }
        else
        {
            id.actualChance = null;
        }
    }

    //public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is IncidentDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.letterText;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            baseChance ??= d.baseChance;
            if(d.category != null){
                Category ??= d.category.label.NullOrEmpty() ? d.category.defName : d.category.label;
            }
            earliestDay ??= d.earliestDay;
            minPopulation ??= d.minPopulation;
            minGreatestPopulation ??= d.minGreatestPopulation;
            minAnomalyThreatLevel ??= d.minAnomalyThreatLevel;
            minThreatPoints ??= d.minThreatPoints;
            maxThreatPoints ??= d.maxThreatPoints;
            baseChanceWithRoyalty ??= d.baseChanceWithRoyalty;
            diseaseVictimFractionRange ??= d.diseaseVictimFractionRange;
        }
    }

    public override void Apply()
    {
        if (this.def is not IncidentDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (baseChance.HasValue) { def.baseChance = baseChance.Value; }
        if (earliestDay.HasValue) { def.earliestDay = earliestDay.Value; }
        if (minPopulation.HasValue) { def.minPopulation = minPopulation.Value; }
        if (minGreatestPopulation.HasValue) { def.minGreatestPopulation = minGreatestPopulation.Value; }
        if (minAnomalyThreatLevel.HasValue) { def.minAnomalyThreatLevel = minAnomalyThreatLevel.Value; }
        if (minThreatPoints.HasValue) { def.minThreatPoints = minThreatPoints.Value; }
        if (maxThreatPoints.HasValue) { def.maxThreatPoints = maxThreatPoints.Value; }
        if (baseChanceWithRoyalty.HasValue) { def.baseChanceWithRoyalty = baseChanceWithRoyalty.Value; }
        if (diseaseVictimFractionRange.HasValue) { def.diseaseVictimFractionRange = diseaseVictimFractionRange.Value; }
        RecalculateFromOriginals();
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(IncidentType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum IncidentType
    {
        ThreatSmall,
        ThreatBig,
        Misc,
        Special,
        Disease,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is IncidentDef def)
        {
            if (def.diseaseIncident != null)
            {
                return (int)IncidentType.Disease;
            }
            if (def.category == IncidentCategoryDefOf.Special)
            {
                return (int)IncidentType.Special;
            }
            if (def.category == IncidentCategoryDefOf.Misc)
            {
                return (int)IncidentType.Misc;
            }
            if (def.category == IncidentCategoryDefOf.ThreatBig)
            {
                return (int)IncidentType.ThreatBig;
            }
            if (def.category == IncidentCategoryDefOf.ThreatSmall)
            {
                return (int)IncidentType.ThreatSmall;
            }
        }
        return (int)IncidentType.Other;
    }

    public static bool AvailableIfDisease(TweakData data) => data.propType == (int)IncidentType.Disease;
}


