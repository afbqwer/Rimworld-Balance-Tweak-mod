using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(TerrainDef), SettingType.Terrain)]
class TerrainData : TweakData<TerrainData>
{
    [TweakField(StatDef = "Beauty", Style = ColumnStyle.Int)]
    public float? beauty = null;
    [TweakField(StatDef = "WorkToBuild", Style = ColumnStyle.Int)]
    public float? workToBuild = null;
    [TweakField(StatDef = "CleaningTimeFactor", Style = ColumnStyle.Prec)]
    public float? cleaningTimeFactor = null;
    [TweakField(StatDef = "FilthMultiplier", Style = ColumnStyle.Prec)]
    public float? filthMultiplier = null;

    [TweakField(Style = ColumnStyle.Int)]
    public int? pathCost = null;
    [TweakField]
    public float? fertility = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? extraDeteriorationFactor = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? toxicBuildupFactor = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? changeable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? holdSnowOrSand = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? layerable = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canEverTerraform = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? extinguishesFire = null;
    [TweakField]
    public float? destroyOnBombDamageThreshold = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? tags = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<TerrainAffordanceDef>? affordances = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ResearchProjectDef>? researchPrerequisites = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList)]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<StuffCategoryDef>? stuffCategories = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    // ── Atmosphere Overhaul：地形大气扩展（可选 Mod，见 AtmosphereConfigWindow）──
    // 只有挂了 AtmosphereTerrainExtension 的地形（Space / HotSpring / LavaDeep…）才有这几格。
    // 六格统一由「大气地形」触发列 + 配置窗口编辑，不再逐列展示（列太多，而每行只命中一两格）。

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
        Editor = typeof(AtmosphereConfigWindow), MayRequire = TweakDatabase.AO,
        Hidden = nameof(HiddenIfNoAoExtension))]
    public TweakID? aoTerrainConfig = null;

    /// <summary>地面温度模式。<c>Normal</c> = 由太阳与空气决定；<c>Fixed</c> = 恒温；<c>Capped</c> = 夹在区间内。</summary>
    [TweakField(NotAColumn = true, Style = ColumnStyle.Enum, OptionsList = nameof(AoTerrainModeOptions),
        MayRequire = TweakDatabase.AO)]
    public string? mode = null;

    /// <summary>模式下拉的数据源。存字符串而不是枚举：类型来自 AO 的程序集，字段类型不能用它。</summary>
    public static readonly List<string> AoTerrainModeOptions = ["Normal", "Fixed", "Capped"];

    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? fixedTemperature = null;
    /// <summary>Capped 模式的下限（上下限填反时 AO 会自己交换）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? minTemperature = null;
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? maxTemperature = null;
    /// <summary>反照率。空 = 用 AO 的推导值（内部用 NaN 表示）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? albedo = null;
    /// <summary>热响应速率。空 = 用 AO 的推导值（内部用 NaN 表示）。</summary>
    [TweakField(NotAColumn = true, MayRequire = TweakDatabase.AO)]
    public float? thermalRate = null;

    public static bool HiddenIfNoAoExtension(TweakData data) => !AoHelper.HasExt(data.def, AoHelper.TerrainExt);

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is TerrainDef d)
        {
            pathCost ??= d.pathCost;
            destroyOnBombDamageThreshold ??= d.destroyOnBombDamageThreshold;
            fertility ??= d.fertility;
            extraDeteriorationFactor ??= d.extraDeteriorationFactor;
            toxicBuildupFactor ??= d.toxicBuildupFactor;
            changeable ??= d.changeable;
            holdSnowOrSand ??= d.holdSnowOrSand;
            layerable ??= d.layerable;
            canEverTerraform ??= d.canEverTerraform;
            extinguishesFire ??= d.extinguishesFire;
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
            if (!IsFieldModified(nameof(tags))) tags ??= d.tags;
            if (!IsFieldModified(nameof(affordances))) affordances ??= d.affordances;
            if (!IsFieldModified(nameof(researchPrerequisites))) researchPrerequisites ??= d.researchPrerequisites;
            if (!IsFieldModified(nameof(costList))) costList ??= d.costList;
            if (!IsFieldModified(nameof(stuffCategories))) stuffCategories ??= d.stuffCategories;
            LoadAoExtension(d);
        }
    }

    /// <summary>回填 AO 的地形扩展字段；该地形没有扩展时整块跳过。</summary>
    private void LoadAoExtension(TerrainDef d)
    {
        var ext = AoHelper.Ext(d, AoHelper.TerrainExt);
        if (ext == null) return;
        mode ??= AoHelper.G(ext, "mode")?.ToString();
        fixedTemperature ??= AoHelper.F(ext, "fixedTemperature");
        minTemperature ??= AoHelper.F(ext, "minTemperature");
        maxTemperature ??= AoHelper.F(ext, "maxTemperature");
        albedo ??= NaNToNull(AoHelper.F(ext, "albedo"));
        thermalRate ??= NaNToNull(AoHelper.F(ext, "thermalRate"));
    }

    /// <summary>AO 用 NaN 表示"没配，用推导值"。直接读出来会让界面显示 NaN、清空行为也错。</summary>
    private static float? NaNToNull(float? v) => v is float f && !float.IsNaN(f) ? f : null;

    public override void Apply()
    {
        if (this.def is not TerrainDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefStats(def);
        ApplyV<int>(nameof(pathCost), v => def.pathCost = v);
        ApplyV<float>(nameof(destroyOnBombDamageThreshold), v => def.destroyOnBombDamageThreshold = v);
        ApplyV<float>(nameof(fertility), v => def.fertility = v);
        ApplyV<float>(nameof(extraDeteriorationFactor), v => def.extraDeteriorationFactor = v);
        ApplyV<float>(nameof(toxicBuildupFactor), v => def.toxicBuildupFactor = v);
        ApplyV<bool>(nameof(changeable), v => def.changeable = v);
        ApplyV<bool>(nameof(holdSnowOrSand), v => def.holdSnowOrSand = v);
        ApplyV<bool>(nameof(layerable), v => def.layerable = v);
        ApplyV<bool>(nameof(canEverTerraform), v => def.canEverTerraform = v);
        ApplyV<bool>(nameof(extinguishesFire), v => def.extinguishesFire = v);

        ApplyL<string>(nameof(tags), v => def.tags = v);
        ApplyL<TerrainAffordanceDef>(nameof(affordances), v => def.affordances = v);
        ApplyL<ResearchProjectDef>(nameof(researchPrerequisites), v => def.researchPrerequisites = v);
        ApplyL<ThingDefCountClass>(nameof(costList), v => def.costList = v);
        ApplyL<StuffCategoryDef>(nameof(stuffCategories), v => def.stuffCategories = v);
        ApplyAoExtension(def);
    }

    /// <summary>
    /// 写回 AO 的地形扩展。
    /// <para>
    /// 六格全部被 <c>GroundMaterialTable.ResolveCache</c> 固化成结构体（首次查询后不再重算），
    /// 所以只要动过其中任何一格，就必须清缓存 —— 否则"把 HotSpring 从 Fixed 45℃ 改成 60℃"
    /// 这种最直观的操作，地温连一摄氏度都不会变，而且日志里一句话都没有。
    /// </para>
    /// </summary>
    private void ApplyAoExtension(TerrainDef def)
    {
        var ext = AoHelper.Ext(def, AoHelper.TerrainExt);
        if (ext == null) return;

        // 与 Apply* 门控同源的判据：这一格这次会不会被写。
        bool Touched(string f) => IsForceWriteField(f) || StateOf(f) != FieldState.Unset;

        bool wrote = Touched(nameof(mode)) | Touched(nameof(fixedTemperature))
                   | Touched(nameof(minTemperature)) | Touched(nameof(maxTemperature))
                   | Touched(nameof(albedo)) | Touched(nameof(thermalRate));

        ApplyS(nameof(mode), name =>
        {
            // 跨 Mod 枚举：写入前按 AO 的枚举类型解析。手输的脏值（或旧版本存档）直接跳过，
            // 不做"猜一个最接近的"—— 猜错等于静默改掉地面温度模型。
            if (name.NullOrEmpty()) return;
            var enumType = AoHelper.T(AoHelper.GroundMode);
            if (enumType != null && System.Enum.TryParse(enumType, name, out var parsed)
                && System.Enum.IsDefined(enumType, parsed))
                AoHelper.S(ext, "mode", parsed);
        });

        ApplyV<float>(nameof(fixedTemperature), v => AoHelper.S(ext, "fixedTemperature", v));
        ApplyV<float>(nameof(minTemperature), v => AoHelper.S(ext, "minTemperature", v));
        ApplyV<float>(nameof(maxTemperature), v => AoHelper.S(ext, "maxTemperature", v));

        ApplyNaN(ext, "albedo", nameof(albedo), albedo);
        ApplyNaN(ext, "thermalRate", nameof(thermalRate), thermalRate);

        if (wrote) AoHelper.ClearGroundCache();
    }

    /// <summary>
    /// 写回「NaN = 用推导值」的字段：Set / 重置写字段值（空 → NaN），Cleared 先还原原值、原值为空才写 NaN，
    /// Unset 不动。
    /// <para>
    /// 不能直接用 <c>ApplyV</c>：它的 Cleared 分支是从原始快照还原，而快照里这一格可能正是 null
    /// ⇒ 等于什么都不做，"清空"看起来毫无反应；反过来，不分青红皂白一律写 NaN 又会把 XML 里配过的值抹掉。
    /// </para>
    /// </summary>
    private void ApplyNaN(object ext, string member, string field, float? value)
    {
        bool force = IsForceWriteField(field);
        var state = StateOf(field);
        if (!force && state == FieldState.Unset) return;

        if (!force && state == FieldState.Cleared
            && TweakDatabase.GetOriginal(id) is TerrainData orig && orig.RawVal(field) is float original)
        {
            // 清空 = 还原原值（XML 里配过就还原它，没配过则落到下面的 NaN）。
            AoHelper.S(ext, member, original);
            return;
        }
        AoHelper.S(ext, member, value ?? float.NaN);
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(TerrainCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum TerrainCategory
    {
        Foundation,
        Water,
        SandSoil,
        Stone,
        Special,
        Floor,
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is TerrainDef def)
        {
            //if (def.bridge) return (int)TerrainCategory.Bridge;
            if (def.isFoundation || def.bridge) return (int)TerrainCategory.Foundation;
            if (def.HasTag("Water")) return (int)TerrainCategory.Water;
            if (def.categoryType == TerrainDef.TerrainCategoryType.Sand || def.categoryType == TerrainDef.TerrainCategoryType.Soil) return (int)TerrainCategory.SandSoil;
            if (def.categoryType == TerrainDef.TerrainCategoryType.Stone) return (int)TerrainCategory.Stone;
            if (def.natural) return (int)TerrainCategory.Special;
            if (def.layerable) return (int)TerrainCategory.Floor;
        }
        return (int)TerrainCategory.Other;
    }
}