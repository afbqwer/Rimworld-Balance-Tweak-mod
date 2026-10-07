using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(BodyPartDef), SettingType.BodyPart)]
class BodyPartData : TweakData<BodyPartData>
{
    // skinCovered / solid 在 BodyPartDef 中是私有字段（无公开访问器），只能反射读写。
    private static readonly FieldInfo SkinCoveredField = AccessTools.Field(typeof(BodyPartDef), "skinCovered");
    private static readonly FieldInfo SolidField = AccessTools.Field(typeof(BodyPartDef), "solid");

    /// <summary>
    /// 关联列：仅在 <see cref="SettingType.BodyPart"/> 页签提供“点击打开链接列表窗口”的入口，
    /// 字段本身不保存值，窗口打开时由 <see cref="FindRaceLinks"/> 实时计算。
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.RaceLinks)]
    public TweakID? raceLinks = null;

    /// <summary>
    /// 关联列：提供“点击打开包含该部位的 BodyData 链接列表窗口”的入口，
    /// 字段本身不保存值，窗口打开时由 <see cref="FindBodyLinks"/> 实时计算。
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.BodyLinks)]
    public TweakID? bodyLinks = null;
    // ── 基础 ──
    [TweakField(Style = ColumnStyle.Int)]
    public int? hitPoints = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<BodyPartTagDef>? tags = null;

    // ── 受伤与流血 ──
    [TweakField()]
    public float? permanentInjuryChanceFactor = null;
    [TweakField()]
    public float? bleedRate = null;
    [TweakField()]
    public float? frostbiteVulnerability = null;

    // ── 结构属性 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? skinCovered = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? solid = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? alive = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? delicate = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? beautyRelated = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? conceptual = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? socketed = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? destroyableByDamage = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canBeVacuumBurnt = null;

    // ── 手术与掉落 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canScarify = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? pawnGeneratorCanAmputate = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canSuggestAmputation = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? forceAlwaysRemovable = null;
    [TweakField()]
    public float? executionPartPriority = null;
    // ── 命中权重（def.hitChanceFactors 为 Dictionary<DamageDef, float>，用 DamageFactor 列表承载）──
    [TweakField(Style = ColumnStyle.DamageModList)]
    public List<DamageFactor>? hitChanceFactors = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is BodyPartDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= d.label;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            hitPoints ??= d.hitPoints;
            tags ??= d.tags;
            permanentInjuryChanceFactor ??= d.permanentInjuryChanceFactor;
            bleedRate ??= d.bleedRate;
            frostbiteVulnerability ??= d.frostbiteVulnerability;
            skinCovered ??= (bool)SkinCoveredField.GetValue(d);
            solid ??= (bool)SolidField.GetValue(d);
            alive ??= d.alive;
            delicate ??= d.delicate;
            beautyRelated ??= d.beautyRelated;
            conceptual ??= d.conceptual;
            socketed ??= d.socketed;
            destroyableByDamage ??= d.destroyableByDamage;
            canBeVacuumBurnt ??= d.canBeVacuumBurnt;
            canScarify ??= d.canScarify;
            pawnGeneratorCanAmputate ??= d.pawnGeneratorCanAmputate;
            canSuggestAmputation ??= d.canSuggestAmputation;
            forceAlwaysRemovable ??= d.forceAlwaysRemovable;
            executionPartPriority ??= d.executionPartPriority;
            hitChanceFactors ??= d.hitChanceFactors?
                .Select(kv => new DamageFactor { damageDef = kv.Key, factor = kv.Value })
                .ToList();
        }
    }

    public override void Apply()
    {
        if (this.def is not BodyPartDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) { def.label = defLabel; }
        if (hitPoints.HasValue) { def.hitPoints = hitPoints.Value; }
        if (tags != null) { def.tags = tags; }
        if (permanentInjuryChanceFactor.HasValue) { def.permanentInjuryChanceFactor = permanentInjuryChanceFactor.Value; }
        if (bleedRate.HasValue) { def.bleedRate = bleedRate.Value; }
        if (frostbiteVulnerability.HasValue) { def.frostbiteVulnerability = frostbiteVulnerability.Value; }
        if (skinCovered.HasValue) { SkinCoveredField.SetValue(def, skinCovered.Value); }
        if (solid.HasValue) { SolidField.SetValue(def, solid.Value); }
        if (alive.HasValue) { def.alive = alive.Value; }
        if (delicate.HasValue) { def.delicate = delicate.Value; }
        if (beautyRelated.HasValue) { def.beautyRelated = beautyRelated.Value; }
        if (conceptual.HasValue) { def.conceptual = conceptual.Value; }
        if (socketed.HasValue) { def.socketed = socketed.Value; }
        if (destroyableByDamage.HasValue) { def.destroyableByDamage = destroyableByDamage.Value; }
        if (canBeVacuumBurnt.HasValue) { def.canBeVacuumBurnt = canBeVacuumBurnt.Value; }
        if (canScarify.HasValue) { def.canScarify = canScarify.Value; }
        if (pawnGeneratorCanAmputate.HasValue) { def.pawnGeneratorCanAmputate = pawnGeneratorCanAmputate.Value; }
        if (canSuggestAmputation.HasValue) { def.canSuggestAmputation = canSuggestAmputation.Value; }
        if (forceAlwaysRemovable.HasValue) { def.forceAlwaysRemovable = forceAlwaysRemovable.Value; }
        if (executionPartPriority.HasValue) { def.executionPartPriority = executionPartPriority.Value; }
        if (hitChanceFactors != null)
        {
            var dict = new Dictionary<DamageDef, float>();
            foreach (var f in hitChanceFactors)
                if (f.damageDef != null) dict[f.damageDef] = f.factor;
            def.hitChanceFactors = dict;
        }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(BodyPartCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum BodyPartCategory
    {
        /// <summary>有皮肤覆盖的部位（头、躯干、四肢、耳、鼻……）。</summary>
        Skin,
        /// <summary>无皮肤覆盖且会流血的软组织（心、肺、肝、脑、眼……）。</summary>
        Organ,
        /// <summary>不会流血的硬质/非活体部位（骨骼、壳、角、蹄、机械体部件……）。</summary>
        Bone,
        /// <summary>其余部位（腰）。</summary>
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is BodyPartDef def)
        {
            if (def.conceptual)
            {
                return (int)BodyPartCategory.Other;
            }
            if ((bool)SkinCoveredField.GetValue(def))
            {
                return (int)BodyPartCategory.Skin;
            }
            if (def.bleedRate > 0f)
            {
                return (int)BodyPartCategory.Organ;
            }
            return (int)BodyPartCategory.Bone;
        }
        return (int)BodyPartCategory.Other;
    }

    #region 反向查询（种族 / 身体）

    /// <summary>
    /// 找出所有“身体包含该部位”的种族数据链接（供 TweakLinksWindow 使用）。
    /// 仅在窗口打开时调用一次，故不做缓存。
    /// </summary>
    public static List<TweakID> FindRaceLinks(BodyPartDef part)
    {
        var result = new List<TweakID>();
        foreach (var raceId in TweakDatabase.raceDatas.Values)
        {
            var race = DefDatabase<ThingDef>.GetNamed(raceId.defName, false);
            var body = race?.race?.body;
            if (body?.corePart == null) continue;
            if (BodyHasPart(body.corePart, part) && !result.Contains(raceId))
                result.Add(raceId);
        }
        return result;
    }

    private static bool BodyHasPart(BodyPartRecord rec, BodyPartDef part)
    {
        if (rec.def == part) return true;
        if (rec.parts == null) return false;
        foreach (var child in rec.parts)
            if (child != null && BodyHasPart(child, part)) return true;
        return false;
    }

    /// <summary>
    /// 找出所有部位树包含该部位的 BodyDef（供 TweakLinksWindow 使用）。
    /// 仅在窗口打开时调用一次，故不做缓存。
    /// </summary>
    public static List<TweakID> FindBodyLinks(BodyPartDef part)
    {
        var result = new List<TweakID>();
        foreach (var body in DefDatabase<BodyDef>.AllDefsListForReading)
        {
            if (body.corePart != null && BodyHasPart(body.corePart, part))
                result.Add(new TweakID(body.defName, SettingType.BodyDef));
        }
        return result;
    }

    #endregion
}
