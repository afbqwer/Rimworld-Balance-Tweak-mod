using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(DamageDef), SettingType.Damage)]
class DamageData : TweakData<DamageData>
{
    // ── 基础数值 ──
    [TweakField(Style = ColumnStyle.Int)]
    public int? defaultDamage = null;
    [TweakField()]
    public float? defaultArmorPenetration = null;
    [TweakField()]
    public float? defaultStoppingPower = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? minDamageToFragment = null;

    // ── 建筑与植物伤害 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? scaleDamageToBuildingsBasedOnFlammability = null;
    [TweakField()]
    public float? buildingDamageFactor = null;
    [TweakField()]
    public float? buildingDamageFactorPassable = null;
    [TweakField()]
    public float? buildingDamageFactorImpassable = null;
    [TweakField()]
    public float? plantDamageFactor = null;
    [TweakField()]
    public float? corpseDamageFactor = null;

    // ── 通用开关与行为 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? harmsHealth = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? makesBlood = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isRanged = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? execution = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? ignoreShields = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? harmAllLayersUntilOutside = null;
    [TweakField(Style = ColumnStyle.Range)]
    public FloatRange? overkillPctToDestroyPart = null;

    // ── 眩晕 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? causeStun = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfStun))]
    public int? stunAdaptationTicks = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfStun))]
    public int? constantStunDurationTicks = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfStun))]
    public StatDef? stunResistStat = null;
    [TweakField(Available = nameof(AvailableIfMelee))]
    public float? bluntStunDuration = null;

    // ── Def 引用 ──
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageArmorCategoryDef? armorCategory = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public HediffDef? hediff = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public HediffDef? hediffSkin = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public HediffDef? hediffSolid = null;

    // ── 近战 ──
    [TweakField(Style = ColumnStyle.Curve, Available = nameof(AvailableIfMelee))]
    public SimpleCurve? cutExtraTargetsCurve = null;
    [TweakField(Style = ColumnStyle.Curve, Available = nameof(AvailableIfMelee))]
    public SimpleCurve? bluntStunChancePerDamagePctOfCorePartToHeadCurve = null;
    [TweakField(Style = ColumnStyle.Curve, Available = nameof(AvailableIfMelee))]
    public SimpleCurve? bluntStunChancePerDamagePctOfCorePartToBodyCurve = null;
    [TweakField(Available = nameof(AvailableIfMelee))]
    public float? stabChanceOfForcedInternal = null;
    [TweakField(Available = nameof(AvailableIfMelee))]
    public float? cutCleaveBonus = null;
    [TweakField(Available = nameof(AvailableIfMelee))]
    public float? bluntInnerHitChance = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfMelee))]
    public FloatRange? bluntInnerHitDamageFractionToConvert = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfMelee))]
    public FloatRange? bluntInnerHitDamageFractionToAdd = null;
    [TweakField(Available = nameof(AvailableIfMelee))]
    public float? scratchSplitPercentage = null;

    // ── 爆炸 ──
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isExplosive = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfExplosion))]
    public bool? explosionAffectOutsidePartsOnly = null;
    [TweakField(Available = nameof(AvailableIfExplosion))]
    public float? explosionHeatEnergyPerCell = null;
    [TweakField(Available = nameof(AvailableIfExplosion))]
    public float? expolosionPropagationSpeed = null;
    [TweakField(Available = nameof(AvailableIfExplosion))]
    public float? explosionInteriorCellCountMultiplier = null;
    [TweakField(Available = nameof(AvailableIfExplosion))]
    public float? explosionInteriorCellDistanceMultiplier = null;

    // ── 火焰（以下字段仅对火焰伤害有效）──
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? igniteChanceByTargetFlammability = null;
    [TweakField()]
    public float? igniteCellChance = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is DamageDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            defaultDamage ??= d.defaultDamage;
            defaultArmorPenetration ??= d.defaultArmorPenetration;
            defaultStoppingPower ??= d.defaultStoppingPower;
            minDamageToFragment ??= d.minDamageToFragment;
            scaleDamageToBuildingsBasedOnFlammability ??= d.scaleDamageToBuildingsBasedOnFlammability;
            buildingDamageFactor ??= d.buildingDamageFactor;
            buildingDamageFactorPassable ??= d.buildingDamageFactorPassable;
            buildingDamageFactorImpassable ??= d.buildingDamageFactorImpassable;
            plantDamageFactor ??= d.plantDamageFactor;
            corpseDamageFactor ??= d.corpseDamageFactor;
            harmsHealth ??= d.harmsHealth;
            makesBlood ??= d.makesBlood;
            isRanged ??= d.isRanged;
            execution ??= d.execution;
            ignoreShields ??= d.ignoreShields;
            harmAllLayersUntilOutside ??= d.harmAllLayersUntilOutside;
            overkillPctToDestroyPart ??= d.overkillPctToDestroyPart;
            causeStun ??= d.causeStun;
            stunAdaptationTicks ??= d.stunAdaptationTicks;
            constantStunDurationTicks ??= d.constantStunDurationTicks;
            stunResistStat ??= d.stunResistStat;
            bluntStunDuration ??= d.bluntStunDuration;
            armorCategory ??= d.armorCategory;
            hediff ??= d.hediff;
            hediffSkin ??= d.hediffSkin;
            hediffSolid ??= d.hediffSolid;
            cutExtraTargetsCurve ??= d.cutExtraTargetsCurve;
            bluntStunChancePerDamagePctOfCorePartToHeadCurve ??= d.bluntStunChancePerDamagePctOfCorePartToHeadCurve;
            bluntStunChancePerDamagePctOfCorePartToBodyCurve ??= d.bluntStunChancePerDamagePctOfCorePartToBodyCurve;
            stabChanceOfForcedInternal ??= d.stabChanceOfForcedInternal;
            cutCleaveBonus ??= d.cutCleaveBonus;
            bluntInnerHitChance ??= d.bluntInnerHitChance;
            bluntInnerHitDamageFractionToConvert ??= d.bluntInnerHitDamageFractionToConvert;
            bluntInnerHitDamageFractionToAdd ??= d.bluntInnerHitDamageFractionToAdd;
            scratchSplitPercentage ??= d.scratchSplitPercentage;
            isExplosive ??= d.isExplosive;
            explosionAffectOutsidePartsOnly ??= d.explosionAffectOutsidePartsOnly;
            explosionHeatEnergyPerCell ??= d.explosionHeatEnergyPerCell;
            expolosionPropagationSpeed ??= d.expolosionPropagationSpeed;
            explosionInteriorCellCountMultiplier ??= d.explosionInteriorCellCountMultiplier;
            explosionInteriorCellDistanceMultiplier ??= d.explosionInteriorCellDistanceMultiplier;
            igniteChanceByTargetFlammability ??= d.igniteChanceByTargetFlammability;
            igniteCellChance ??= d.igniteCellChance;
        }
    }

    public override void Apply()
    {
        if (this.def is not DamageDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyV<int>(nameof(defaultDamage), v => def.defaultDamage = v);
        ApplyV<float>(nameof(defaultArmorPenetration), v => def.defaultArmorPenetration = v);
        ApplyV<float>(nameof(defaultStoppingPower), v => def.defaultStoppingPower = v);
        ApplyV<int>(nameof(minDamageToFragment), v => def.minDamageToFragment = v);
        ApplyV<bool>(nameof(scaleDamageToBuildingsBasedOnFlammability), v => def.scaleDamageToBuildingsBasedOnFlammability = v);
        ApplyV<float>(nameof(buildingDamageFactor), v => def.buildingDamageFactor = v);
        ApplyV<float>(nameof(buildingDamageFactorPassable), v => def.buildingDamageFactorPassable = v);
        ApplyV<float>(nameof(buildingDamageFactorImpassable), v => def.buildingDamageFactorImpassable = v);
        ApplyV<float>(nameof(plantDamageFactor), v => def.plantDamageFactor = v);
        ApplyV<float>(nameof(corpseDamageFactor), v => def.corpseDamageFactor = v);
        ApplyV<bool>(nameof(harmsHealth), v => def.harmsHealth = v);
        ApplyV<bool>(nameof(makesBlood), v => def.makesBlood = v);
        ApplyV<bool>(nameof(isRanged), v => def.isRanged = v);
        ApplyV<bool>(nameof(execution), v => def.execution = v);
        ApplyV<bool>(nameof(ignoreShields), v => def.ignoreShields = v);
        ApplyV<bool>(nameof(harmAllLayersUntilOutside), v => def.harmAllLayersUntilOutside = v);
        ApplyV<FloatRange>(nameof(overkillPctToDestroyPart), v => def.overkillPctToDestroyPart = v);
        ApplyV<bool>(nameof(causeStun), v => def.causeStun = v);
        ApplyV<int>(nameof(stunAdaptationTicks), v => def.stunAdaptationTicks = v);
        ApplyV<int>(nameof(constantStunDurationTicks), v => def.constantStunDurationTicks = v);
        ApplyD<StatDef>(nameof(stunResistStat), v => def.stunResistStat = v);
        ApplyV<float>(nameof(bluntStunDuration), v => def.bluntStunDuration = v);
        ApplyD<DamageArmorCategoryDef>(nameof(armorCategory), v => def.armorCategory = v);
        ApplyD<HediffDef>(nameof(hediff), v => def.hediff = v);
        ApplyD<HediffDef>(nameof(hediffSkin), v => def.hediffSkin = v);
        ApplyD<HediffDef>(nameof(hediffSolid), v => def.hediffSolid = v);
        ApplyV<SimpleCurve>(nameof(cutExtraTargetsCurve), v => def.cutExtraTargetsCurve = v);
        ApplyV<SimpleCurve>(nameof(bluntStunChancePerDamagePctOfCorePartToHeadCurve), v => def.bluntStunChancePerDamagePctOfCorePartToHeadCurve = v);
        ApplyV<SimpleCurve>(nameof(bluntStunChancePerDamagePctOfCorePartToBodyCurve), v => def.bluntStunChancePerDamagePctOfCorePartToBodyCurve = v);
        ApplyV<float>(nameof(stabChanceOfForcedInternal), v => def.stabChanceOfForcedInternal = v);
        ApplyV<float>(nameof(cutCleaveBonus), v => def.cutCleaveBonus = v);
        ApplyV<float>(nameof(bluntInnerHitChance), v => def.bluntInnerHitChance = v);
        ApplyV<FloatRange>(nameof(bluntInnerHitDamageFractionToConvert), v => def.bluntInnerHitDamageFractionToConvert = v);
        ApplyV<FloatRange>(nameof(bluntInnerHitDamageFractionToAdd), v => def.bluntInnerHitDamageFractionToAdd = v);
        ApplyV<float>(nameof(scratchSplitPercentage), v => def.scratchSplitPercentage = v);
        ApplyV<bool>(nameof(isExplosive), v => def.isExplosive = v);
        ApplyV<bool>(nameof(explosionAffectOutsidePartsOnly), v => def.explosionAffectOutsidePartsOnly = v);
        ApplyV<float>(nameof(explosionHeatEnergyPerCell), v => def.explosionHeatEnergyPerCell = v);
        ApplyV<float>(nameof(expolosionPropagationSpeed), v => def.expolosionPropagationSpeed = v);
        ApplyV<float>(nameof(explosionInteriorCellCountMultiplier), v => def.explosionInteriorCellCountMultiplier = v);
        ApplyV<float>(nameof(explosionInteriorCellDistanceMultiplier), v => def.explosionInteriorCellDistanceMultiplier = v);
        ApplyV<SimpleCurve>(nameof(igniteChanceByTargetFlammability), v => def.igniteChanceByTargetFlammability = v);
        ApplyV<float>(nameof(igniteCellChance), v => def.igniteCellChance = v);
    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(DamageType).GetEnumNames().Select(s => "MST." + s).ToList();

    // 简化分类：只依赖游戏自带的稳定标志位与 worker 类型继承关系，
    // 不按 defName 字符串硬编码，避免 Mod 自定义伤害被误分类。
    private enum DamageType
    {
        Projectile,
        Melee,
        Explosion,
        Stun,
        Special,
    }

    public override int GetPropType()
    {
        if (this.def is DamageDef def)
        {
            if (def.isExplosive)
            {
                return (int)DamageType.Explosion;
            }
            if ((def.workerClass != null && typeof(DamageWorker_Stun).IsAssignableFrom(def.workerClass)) || def.causeStun || def.constantStunDurationTicks.HasValue)
            {
                return (int)DamageType.Stun;
            }
            if (def.isRanged)
            {
                return (int)DamageType.Projectile;
            }
            if (def.harmsHealth)
            {
                return (int)DamageType.Melee;
            }
        }
        return (int)DamageType.Special;
    }

    public static bool AvailableIfExplosion(TweakData data) => data.propType == (int)DamageType.Explosion;
    public static bool AvailableIfStun(TweakData data) => true;//data.propType == (int)DamageType.Stun;
    public static bool AvailableIfMelee(TweakData data) => data.propType == (int)DamageType.Melee;
}
