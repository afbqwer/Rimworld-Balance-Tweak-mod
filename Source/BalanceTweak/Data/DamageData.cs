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
        if (defaultDamage.HasValue) { def.defaultDamage = defaultDamage.Value; }
        if (defaultArmorPenetration.HasValue) { def.defaultArmorPenetration = defaultArmorPenetration.Value; }
        if (defaultStoppingPower.HasValue) { def.defaultStoppingPower = defaultStoppingPower.Value; }
        if (minDamageToFragment.HasValue) { def.minDamageToFragment = minDamageToFragment.Value; }
        if (scaleDamageToBuildingsBasedOnFlammability.HasValue) { def.scaleDamageToBuildingsBasedOnFlammability = scaleDamageToBuildingsBasedOnFlammability.Value; }
        if (buildingDamageFactor.HasValue) { def.buildingDamageFactor = buildingDamageFactor.Value; }
        if (buildingDamageFactorPassable.HasValue) { def.buildingDamageFactorPassable = buildingDamageFactorPassable.Value; }
        if (buildingDamageFactorImpassable.HasValue) { def.buildingDamageFactorImpassable = buildingDamageFactorImpassable.Value; }
        if (plantDamageFactor.HasValue) { def.plantDamageFactor = plantDamageFactor.Value; }
        if (corpseDamageFactor.HasValue) { def.corpseDamageFactor = corpseDamageFactor.Value; }
        if (harmsHealth.HasValue) { def.harmsHealth = harmsHealth.Value; }
        if (makesBlood.HasValue) { def.makesBlood = makesBlood.Value; }
        if (isRanged.HasValue) { def.isRanged = isRanged.Value; }
        if (execution.HasValue) { def.execution = execution.Value; }
        if (ignoreShields.HasValue) { def.ignoreShields = ignoreShields.Value; }
        if (harmAllLayersUntilOutside.HasValue) { def.harmAllLayersUntilOutside = harmAllLayersUntilOutside.Value; }
        if (overkillPctToDestroyPart.HasValue) { def.overkillPctToDestroyPart = overkillPctToDestroyPart.Value; }
        if (causeStun.HasValue) { def.causeStun = causeStun.Value; }
        if (stunAdaptationTicks.HasValue) { def.stunAdaptationTicks = stunAdaptationTicks.Value; }
        if (constantStunDurationTicks.HasValue) { def.constantStunDurationTicks = constantStunDurationTicks.Value; }
        if (stunResistStat != null) { def.stunResistStat = stunResistStat; }
        if (bluntStunDuration.HasValue) { def.bluntStunDuration = bluntStunDuration.Value; }
        if (armorCategory != null) { def.armorCategory = armorCategory; }
        if (hediff != null) { def.hediff = hediff; }
        if (hediffSkin != null) { def.hediffSkin = hediffSkin; }
        if (hediffSolid != null) { def.hediffSolid = hediffSolid; }
        if (cutExtraTargetsCurve != null) { def.cutExtraTargetsCurve = cutExtraTargetsCurve; }
        if (bluntStunChancePerDamagePctOfCorePartToHeadCurve != null) { def.bluntStunChancePerDamagePctOfCorePartToHeadCurve = bluntStunChancePerDamagePctOfCorePartToHeadCurve; }
        if (bluntStunChancePerDamagePctOfCorePartToBodyCurve != null) { def.bluntStunChancePerDamagePctOfCorePartToBodyCurve = bluntStunChancePerDamagePctOfCorePartToBodyCurve; }
        if (stabChanceOfForcedInternal.HasValue) { def.stabChanceOfForcedInternal = stabChanceOfForcedInternal.Value; }
        if (cutCleaveBonus.HasValue) { def.cutCleaveBonus = cutCleaveBonus.Value; }
        if (bluntInnerHitChance.HasValue) { def.bluntInnerHitChance = bluntInnerHitChance.Value; }
        if (bluntInnerHitDamageFractionToConvert.HasValue) { def.bluntInnerHitDamageFractionToConvert = bluntInnerHitDamageFractionToConvert.Value; }
        if (bluntInnerHitDamageFractionToAdd.HasValue) { def.bluntInnerHitDamageFractionToAdd = bluntInnerHitDamageFractionToAdd.Value; }
        if (scratchSplitPercentage.HasValue) { def.scratchSplitPercentage = scratchSplitPercentage.Value; }
        if (isExplosive.HasValue) { def.isExplosive = isExplosive.Value; }
        if (explosionAffectOutsidePartsOnly.HasValue) { def.explosionAffectOutsidePartsOnly = explosionAffectOutsidePartsOnly.Value; }
        if (explosionHeatEnergyPerCell.HasValue) { def.explosionHeatEnergyPerCell = explosionHeatEnergyPerCell.Value; }
        if (expolosionPropagationSpeed.HasValue) { def.expolosionPropagationSpeed = expolosionPropagationSpeed.Value; }
        if (explosionInteriorCellCountMultiplier.HasValue) { def.explosionInteriorCellCountMultiplier = explosionInteriorCellCountMultiplier.Value; }
        if (explosionInteriorCellDistanceMultiplier.HasValue) { def.explosionInteriorCellDistanceMultiplier = explosionInteriorCellDistanceMultiplier.Value; }
        if (igniteChanceByTargetFlammability != null) { def.igniteChanceByTargetFlammability = igniteChanceByTargetFlammability; }
        if (igniteCellChance.HasValue) { def.igniteCellChance = igniteCellChance.Value; }
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
