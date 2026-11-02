using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 武器特性（<see cref="WeaponTraitDef"/>）：人格武器（Royalty Bladelink）与武器改装特性（Odyssey 枪管/瞄具/弹药）共用同一个 Def 类型。
///
/// <para>
/// <b>生效时机分两类，改动前后的表现不一样，列上的 tooltip 已逐条写明</b>：
/// </para>
/// <list type="bullet">
/// <item><b>立即生效</b> —— <c>equippedStatOffsets</c>（<c>StatWorker</c> 每次取值都遍历特性的
/// <c>TraitsListForReading</c>）、<c>statOffsets</c> / <c>statFactors</c>（<c>CompUniqueWeapon.GetStatOffset/GetStatFactor</c>）、
/// <c>extraDamages</c>（<c>Verb_LaunchProjectile</c> 发射瞬间合并进弹丸）、
/// <c>burstShot*Multiplier</c> / <c>additionalStoppingPower</c>（<c>ThingDef</c> 的统计报告与弹丸计算现读）。</item>
/// <item><b>只影响之后生成/命名/配色</b> —— <c>commonality</c> / <c>canGenerateAlone</c> / <c>exclusionTags</c>
/// （<c>CompUniqueWeapon.PostPostMake</c> 抽特性时用）、<c>traitAdjectives</c>（生成名字时用）、
/// <c>forcedColor</c>（生成时写进 comp 的 <c>color</c> 字段，之后不再读 def）。</item>
/// </list>
///
/// <para>
/// <b>不暴露的两个字段</b>：<c>workerClass</c>（<see cref="System.Type"/>，改了还要反射清
/// <see cref="WeaponTraitDef.Worker"/> 里的实例缓存，属于"改代码"而不是"改数值"）、
/// <c>abilityProps</c>（<c>CompProperties</c> 子对象，需要整套 Comp 编辑器，不是一列能表达的）。
/// </para>
/// </summary>
[TweakFor(typeof(WeaponTraitDef), SettingType.WeaponTrait)]
class WeaponTraitData : TweakData<WeaponTraitData>
{
    // ── 标识与生成 ──
    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public WeaponCategoryDef? weaponCategory = null;

    [TweakField()]
    public float? commonality = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canGenerateAlone = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? exclusionTags = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? traitAdjectives = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public ColorDef? forcedColor = null;

    // ── 射击与伤害 ──
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? damageDefOverride = null;

    [TweakField(Style = ColumnStyle.ExtraDamageList)]
    public List<ExtraDamage>? extraDamages = null;

    [TweakField()]
    public float? additionalStoppingPower = null;

    [TweakField(Style = ColumnStyle.Prec)]
    public float? burstShotSpeedMultiplier = null;

    [TweakField(Style = ColumnStyle.Prec)]
    public float? burstShotCountMultiplier = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? ignoresAccuracyMaluses = null;

    [TweakField()]
    public float? marketValueOffset = null;

    // ── 属性修正 ──
    // 这三个都是 List<StatModifier>，但 WeaponTraitDef 既不是 ThingDef 也不是 BuildableDef，
    // ApplyDefStats 的 Phase 1/2/3 都到不了这里 —— 所以 Apply 里按普通集合走 ApplyL（先例：GeneData）。
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;

    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? equippedStatOffsets = null;

    // ── 心情与健康（人格武器）──
    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThoughtDef? bondedThought = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public ThoughtDef? killThought = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? equippedHediffs = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? bondedHediffs = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? neverBond = null;

    public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is not WeaponTraitDef d) return;

        label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
        defLabel ??= d.label;
        // 特性描述就是玩家在枪械信息里看到的那段说明 —— 它比 "defName + description" 更有用。
        desc = d.description.NullOrEmpty() ? d.defName : d.description;
        searchString = label + d.defName;

        weaponCategory ??= d.weaponCategory;
        commonality ??= d.commonality;
        canGenerateAlone ??= d.canGenerateAlone;
        forcedColor ??= d.forcedColor;

        damageDefOverride ??= d.damageDefOverride;
        additionalStoppingPower ??= d.additionalStoppingPower;
        burstShotSpeedMultiplier ??= d.burstShotSpeedMultiplier;
        burstShotCountMultiplier ??= d.burstShotCountMultiplier;
        ignoresAccuracyMaluses ??= d.ignoresAccuracyMaluses;
        marketValueOffset ??= d.marketValueOffset;

        bondedThought ??= d.bondedThought;
        killThought ??= d.killThought;
        neverBond ??= d.neverBond;

        // 集合字段必须带守卫：已清空的字段（Cleared）读档后不得被 def 回填"复活"。
        if (!IsFieldModified(nameof(exclusionTags))) exclusionTags ??= d.exclusionTags;
        if (!IsFieldModified(nameof(traitAdjectives))) traitAdjectives ??= d.traitAdjectives;
        if (!IsFieldModified(nameof(extraDamages))) extraDamages ??= d.extraDamages;
        if (!IsFieldModified(nameof(statOffsets))) statOffsets ??= d.statOffsets;
        if (!IsFieldModified(nameof(statFactors))) statFactors ??= d.statFactors;
        if (!IsFieldModified(nameof(equippedStatOffsets))) equippedStatOffsets ??= d.equippedStatOffsets;
        if (!IsFieldModified(nameof(equippedHediffs))) equippedHediffs ??= d.equippedHediffs;
        if (!IsFieldModified(nameof(bondedHediffs))) bondedHediffs ??= d.bondedHediffs;
    }

    public override void Apply()
    {
        if (this.def is not WeaponTraitDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }

        ApplyDefLabel(nameof(defLabel), def);
        ApplyD<WeaponCategoryDef>(nameof(weaponCategory), v => def.weaponCategory = v);
        ApplyV<float>(nameof(commonality), v => def.commonality = v);
        ApplyV<bool>(nameof(canGenerateAlone), v => def.canGenerateAlone = v);
        ApplyL<string>(nameof(exclusionTags), v => def.exclusionTags = v);
        ApplyL<string>(nameof(traitAdjectives), v => def.traitAdjectives = v);
        ApplyD<ColorDef>(nameof(forcedColor), v => def.forcedColor = v);

        ApplyD<DamageDef>(nameof(damageDefOverride), v => def.damageDefOverride = v);
        ApplyL<ExtraDamage>(nameof(extraDamages), v => def.extraDamages = v);
        ApplyV<float>(nameof(additionalStoppingPower), v => def.additionalStoppingPower = v);
        ApplyV<float>(nameof(burstShotSpeedMultiplier), v => def.burstShotSpeedMultiplier = v);
        ApplyV<float>(nameof(burstShotCountMultiplier), v => def.burstShotCountMultiplier = v);
        ApplyV<bool>(nameof(ignoresAccuracyMaluses), v => def.ignoresAccuracyMaluses = v);
        ApplyV<float>(nameof(marketValueOffset), v => def.marketValueOffset = v);

        ApplyL<StatModifier>(nameof(statOffsets), v => def.statOffsets = v);
        ApplyL<StatModifier>(nameof(statFactors), v => def.statFactors = v);
        ApplyL<StatModifier>(nameof(equippedStatOffsets), v => def.equippedStatOffsets = v);

        ApplyD<ThoughtDef>(nameof(bondedThought), v => def.bondedThought = v);
        ApplyD<ThoughtDef>(nameof(killThought), v => def.killThought = v);
        ApplyL<HediffDef>(nameof(equippedHediffs), v => def.equippedHediffs = v);
        ApplyL<HediffDef>(nameof(bondedHediffs), v => def.bondedHediffs = v);
        ApplyV<bool>(nameof(neverBond), v => def.neverBond = v);
    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(WeaponTraitKind).GetEnumNames().Select(s => "MST." + s).ToList();

    /// <summary>
    /// 两族特性：武器改装（Odyssey）与人格武器（Royalty Bladelink）。
    /// 页签里混在一起时，"改改装件"和"改人格"是两件完全不同的事，筛一下省事。
    /// <para>顺序即默认筛选值（<c>filterPropType</c> 初值 0）：改装特性数量远多于人格特性，放第一位。</para>
    /// </summary>
    private enum WeaponTraitKind
    {
        Attachment,
        Bladelink,
    }

    /// <summary>
    /// 判定只看"是不是人格武器特性"，不按 defName 硬编码：
    /// 原版用 <c>weaponCategory == BladeLink</c>，但第三方 Mod 的人格特性可能不填分类却填了绑定/击杀想法，
    /// 所以把 <c>bondedThought</c> / <c>killThought</c> / <c>neverBond</c> 也算进来。
    /// </summary>
    public override int GetPropType()
    {
        if (this.def is WeaponTraitDef d)
        {
            if (d.weaponCategory?.defName == "BladeLink"
                || d.bondedThought != null
                || d.killThought != null
                || d.neverBond)
            {
                return (int)WeaponTraitKind.Bladelink;
            }
        }
        return (int)WeaponTraitKind.Attachment;
    }
}
