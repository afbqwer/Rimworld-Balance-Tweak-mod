using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

/// <summary>
/// 战斗力参考值：攻击 / 防御 / 总体，按类别锚定到 100。
/// <para>
/// 设计见 <c>Docs/CombatPower_Design.md</c>。这里只列四条实现约束：
/// </para>
/// <list type="number">
/// <item><b>刻度在首次被读时定格</b>（<see cref="EnsureAnchors"/>）。锚是「尺子」不是「现值」，
/// 跟着活实例走会让"改一只巨兽 → 所有动物读数漂移"重演武器强度那次的坑。</item>
/// <item><b>远程 DPS 一律读 <see cref="WeaponData.MaxRangedDPS"/></b>，不在这里重算 ——
/// 光束（<c>verb.beamDamageDef</c>）、电弧喷射（<c>Verb_ArcSprayIncinerator</c>）、
/// 建筑炮塔节奏这几个分支 <c>GenRangeDPS</c> 已经处理过，重写必然漏。</item>
/// <item><b>覆盖全部兵种</b>：动物、昆虫、机械、实体（Anomaly）、其它、Humanlike。
/// 装备差异全部收进 <see cref="CombatGear"/>，这里只负责「拿到装备之后怎么算」。
/// 锚按类别分：动物/昆虫锚巨兽，其余锚蜈蚣·爆裂。</item>
/// <item><b>攻击 = max(近战, 远程) + Σ炮塔</b>。主输出的两条路共用一个 verb，互斥；
/// 炮塔组件是独立通道，能和它们同时开火，所以相加。</item>
/// </list>
/// </summary>
internal static class CombatPower
{
    #region 常量

    /// <summary>参考来袭穿透：突击步枪量级。防御端用它算护甲期望减伤。</summary>
    private const float RefArmorPenetration = 0.15f;

    /// <summary>参考交火距离（格）。所有远程单位按这一个距离估价，不按各自最优射程 ——
    /// 武器强度那边已经有 <c>log10(射程)</c> 的"够得着"奖励，再按距离扣一次命中是双重计价。</summary>
    private const float RefEngageDistance = 12f;

    /// <summary>招式 / 弹丸没写穿透时，原版按伤害的 1.5% 推导（<c>ProjectileProperties.GetArmorPenetration</c> /
    /// <c>VerbProperties.AdjustedArmorPenetration</c> 的最后一段）。</summary>
    private const float DerivedApPerDamage = 0.015f;

    /// <summary>
    /// 接敌损耗：近战值乘 0.75。
    /// <para>
    /// 乘在<b>近战值</b>上而不是"近战单位"上 —— 远程单位也能打近战，只是不能同时做两件事：
    /// 贴上去的那几秒它没在开枪。所以这是"近战这条输出路径"的折扣，对谁都一样。
    /// </para>
    /// </summary>
    private const float MeleeEngagePenalty = 0.75f;

    /// <summary>人形兵种没声明技能时的默认等级（射击/近战各一条）。</summary>
    private const int DefaultHumanlikeSkill = 5;

    /// <summary>动物 / 昆虫的锚：巨兽（Core），原版 combatPower 500。</summary>
    private const string AnimalAnchorKind = "Thrumbo";

    /// <summary>机械 / 人形 / 实体的锚：蜈蚣·爆裂（Core），原版 combatPower 400。选 Core 内的单位当尺子 ——
    /// 拿 DLC 单位当基准，没装该 DLC 的玩家刻度就无从校准。</summary>
    private const string MechAnchorKind = "Mech_CentipedeBlaster";

    #endregion

    #region 状态

    private static bool anchorsReady;
    private static float animalAnchorAttack;
    private static float animalAnchorDefense;
    private static float mechAnchorAttack;
    private static float mechAnchorDefense;

    /// <summary>已告警过的炮塔枪（避免每帧刷日志）。</summary>
    private static readonly HashSet<string> turretCooldownWarned = [];

    /// <summary>defName → 市价。非人形的候选武器筛选要用，而市价与兵种无关，缓存掉省一次 StatWorker。</summary>
    private static readonly Dictionary<string, float> marketValueCache = [];

    /// <summary>「defName|stuff」→ 武器招式价值。人形的近战候选逐条求值太贵，缓存掉。</summary>
    private static readonly Dictionary<string, float> meleeValueCache = [];

    /// <summary>defName → 武器固有远程价值。候选池里同一个 def 会因材料不同重复出现。</summary>
    private static readonly Dictionary<string, float> rangedValueCache = [];

    #endregion

    #region 入口

    /// <summary>
    /// 填 <see cref="PawnKindData"/> 的三个派生列。由 <c>PawnKindData.EnsureDisplayValues</c> 调用。
    /// </summary>
    public static void Fill(PawnKindData data)
    {
        data.attackPower = null;
        data.defensePower = null;
        data.overallPower = null;

        if (data.def is not PawnKindDef kind || kind.race?.race == null) return;

        var race = kind.race;
        Gear gear = CombatGear.Of(kind);

        float attack = Offense(kind, race, gear);
        // 防御要遍历几十个部位，按 (种族, 装备画像) 缓存：同一种族的人形兵种通常共享画像。
        float defense = CombatGear.CachedDefense(race.defName, gear, () => Defense(race, gear.Armor));

        EnsureAnchors();
        bool mechScale = UsesMechAnchor(data);
        float anchorAttack = mechScale ? mechAnchorAttack : animalAnchorAttack;
        float anchorDefense = mechScale ? mechAnchorDefense : animalAnchorDefense;

        // 0 表示"这条不存在"，不是"这条很弱"：没有物理招式的实体（靠 abilities 输出）攻击就是 0，
        // 显示成 0.00 会被误读成"弱得离谱"。写成 null 让列显示 Null。
        data.attackPower = anchorAttack > 0f && attack > 0f ? attack / anchorAttack * 100f : null;
        data.defensePower = anchorDefense > 0f ? defense / anchorDefense * 100f : 0f;

        // 任一轴缺席，开方就没有意义 —— 一起写成 null。
        data.overallPower = data.attackPower is float atk && data.defensePower is float def && def > 0f
            ? Mathf.Sqrt(atk * def)
            : null;
    }

    /// <summary>
    /// 依赖行被改后，把受影响的 pawnkind 行显示缓存置脏。
    /// <para>
    /// 粗粒度是刻意的：一个 pawnkind 依赖"一集合"武器/服装，一件装备又反依赖一堆 pawnkind，
    /// 两个方向都建反向索引的维护成本远大于收益。这里只是写 bool，几百行是纳秒级。
    /// </para>
    /// <para>
    /// 但<b>只对真会影响战斗力的类型动手</b>：改一栋建筑、一个配方不该让整张兵种表重算 ——
    /// 重算不是免费的（要重建装备池）。
    /// </para>
    /// </summary>
    public static void Invalidate(TweakData changed)
    {
        var type = changed.id.settingType;

        // 建筑和杂项不改兵种，但会改市价 / 炮塔节奏，所以装备池要跟着清；行本身不必置脏。
        bool combat = AffectsCombat(type);
        if (!combat && type is not (SettingType.Building or SettingType.Misc)) return;

        // 所有派生缓存一起清：市价、武器招式、武器固有价值、装备画像、防御。
        // 它们互相依赖（改一件服装 → 画像变 → 防御变），分开清迟早漏一个。
        marketValueCache.Clear();
        meleeValueCache.Clear();
        rangedValueCache.Clear();
        CombatGear.Clear();

        if (!combat) return;
        if (!cachedData.TryGetValue(SettingType.PawnKind, out var rows)) return;
        foreach (var data in rows.Values) data.InvalidateDisplayValues();
    }

    /// <summary>哪些页签的改动会让兵种战斗力失效。</summary>
    private static bool AffectsCombat(SettingType type) => type switch
    {
        SettingType.PawnKind => true,     // 武器预算 / 标签 / combatPower 本身
        SettingType.Weapon => true,       // DPS、穿透、射程
        SettingType.Apparel => true,      // 护甲值、部位覆盖
        SettingType.Stuff => true,        // 材料倍率会改服装护甲与武器招式
        SettingType.Race => true,         // 血量、护甲、体型、招式、炮塔组件
        SettingType.MeleeTool => true,    // 招式数值
        SettingType.BodyDef => true,      // 核心部位血量、部位覆盖率
        _ => false,
    };

    /// <summary>测试用：丢掉锚的定格值与所有派生缓存。</summary>
    internal static void ResetAnchors()
    {
        anchorsReady = false;
        animalAnchorAttack = animalAnchorDefense = 0f;
        mechAnchorAttack = mechAnchorDefense = 0f;
        turretCooldownWarned.Clear();
        marketValueCache.Clear();
        meleeValueCache.Clear();
        rangedValueCache.Clear();
        CombatGear.Clear();
    }

    #endregion

    #region 攻击

    /// <summary>
    /// 攻击 = 主输出 + 炮塔。主输出的两条路（贴上去砍 / 站远点打）共用一个 verb，取高值；
    /// 炮塔是独立通道，相加。
    /// </summary>
    private static float Offense(PawnKindDef kind, ThingDef race, in Gear gear)
    {
        // 技能：只有 Humanlike 有技能追踪器，其余走 noSkillOffset（动物/机械的固定值）。
        float meleeHit = Accuracy(StatDefOf.MeleeHitChance, SkillOf(kind, SkillDefOf.Melee));
        float melee = Mathf.Max(UnarmedMelee(race), gear.Melee) * meleeHit * MeleeEngagePenalty;

        float accuracy = Mathf.Pow(
            Accuracy(StatDefOf.ShootingAccuracyPawn, SkillOf(kind, SkillDefOf.Shooting)),
            RefEngageDistance);

        return Mathf.Max(melee, gear.Ranged * accuracy) + TurretIntrinsic(race) * accuracy;
    }

    /// <summary>
    /// 空手 / 种族自带招式。人形拿着武器时这条会被武器招式顶掉，所以由调用方取 <c>max</c>。
    /// <para>
    /// DPS 直接复用 <see cref="DataUtility.CalculateExpectedDPS"/>（<c>weaponMode: true</c>），
    /// 与武器强度的近战刻度同源 —— 那边把负穿透折算成 <c>1.5 × damage × 0.015</c> 的加成，
    /// 这里不另立一套，否则同一只镰在武器页和兵种页会有两种算不出来的关系。
    /// </para>
    /// </summary>
    private static float UnarmedMelee(ThingDef race)
    {
        if (race.tools.NullOrEmpty()) return 0f;

        float damageFactor = StatOrDefault(race, StatDefOf.MeleeDamageFactor);
        float cooldownFactor = StatOrDefault(race, StatDefOf.MeleeCooldownFactor);

        return DataUtility.CalculateExpectedDPS(
            race.tools, null, damageFactor, cooldownFactor, weaponMode: true);
    }

    /// <summary>
    /// 炮塔输出：<c>CompTurretGun</c> 是<b>独立</b>的射击通道 —— 它自己 tick、自己算冷却，
    /// 不与装备武器的 verb 抢同一个槽位，所以可以和近战 / 射击<b>同时</b>进行。
    /// 因此这一项是<b>相加</b>，不是取高值。
    /// <para>
    /// 注意不要按 <c>CompProperties_TurretGun.autoAttack</c> 过滤：那个字段在原版里只有
    /// <c>CompTurretGun.AutoAttack</c> 一个只读属性转发，<b>没有任何行为读它</b>（Warqueen 的
    /// <c>autoAttack=false</c> 照样会自动开火）。按它过滤会把战争女王的炮塔白白扣掉。
    /// </para>
    /// </summary>
    private static float TurretIntrinsic(ThingDef race)
    {
        float sum = 0f;
        if (race.comps == null) return sum;
        foreach (var comp in race.comps)
        {
            if (comp is CompProperties_TurretGun { turretDef: not null } turret)
                sum += RangedIntrinsic(turret.turretDef);
        }
        return sum;
    }

    /// <summary>
    /// 非人形：按原版权重求候选武器的期望值。
    /// <para>
    /// 筛选条件逐条对齐 <c>PawnWeaponGenerator.TryGenerateWeaponFor</c>：标签交集、价格上限、
    /// <c>generateAllowChance</c>、derp 判定。其中 <c>generateAllowChance</c> 一条是验证时撞出来的 ——
    /// 原版 <c>Gun_ChargeLance_Unique</c> 与普通版共用 <c>MechanoidGunMedium</c> 标签，
    /// 漏了这条会把"永不自然生成"的独特变体算进期望值。
    /// </para>
    /// </summary>
    internal static float ExpectedGunValue(PawnKindDef kind)
    {
        var tags = kind.weaponTags;
        if (tags.NullOrEmpty()) return 0f;

        float budget = kind.weaponMoney.max;
        float sumWeight = 0f;
        float sumWeighted = 0f;
        float best = 0f;

        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.equipmentType != EquipmentType.Primary) continue;
            if (def.weaponTags.NullOrEmpty()) continue;
            if (!def.weaponTags.Any(tags.Contains)) continue;
            if (def.generateAllowChance < 1f) continue;

            var stuff = DataUtility.GetDefaultStuffFromDef(def);
            if (PawnWeaponGenerator.IsDerpWeapon(def, stuff)) continue;

            float price = MarketValue(def);
            if (price > budget) continue;

            float value = RangedIntrinsic(def);
            if (value <= 0f) continue;

            float weight = Mathf.Max(def.generateCommonality, 0.0001f) * Mathf.Max(price, 0.0001f);
            sumWeight += weight;
            sumWeighted += weight * value;
            best = Mathf.Max(best, value);
        }

        return sumWeight > 0f ? sumWeighted / sumWeight : best;
    }

    #endregion

    #region 单件装备价值

    /// <summary>
    /// 一把枪的固有价值 = 期望 DPS × 武器命中 × 穿透因子 × 射程系数。
    /// <b>不含射手精度</b> —— 那是兵种的属性，由 <see cref="Offense"/> 统一乘。
    /// </summary>
    internal static float RangedIntrinsic(ThingDef gunDef)
    {
        // 一把枪会以多个 ThingStuffPair 出现在候选池里（同一个 def），按 defName 缓存掉重复求解。
        if (rangedValueCache.TryGetValue(gunDef.defName, out float cached)) return cached;
        float value = ComputeRangedIntrinsic(gunDef);
        rangedValueCache[gunDef.defName] = value;
        return value;
    }

    private static float ComputeRangedIntrinsic(ThingDef gunDef)
    {
        if (TweakData.GetLiveData(new TweakID(gunDef.defName, SettingType.Weapon)) is not WeaponData wd)
            return 0f;

        // 同时被建筑炮塔挂载的枪：WeaponData.MaxRangedDPS 走的是 BuildingProperties 的节奏
        // （turretBurstCooldownTime），pawn 挂载用的是 verb 的 defaultCooldownTime —— 两个数不一样。
        // 这种枪在 pawn 侧无法复用该字段，宁可不计也不给一个错的数。
        if (wd.GunTurret != null)
        {
            if (turretCooldownWarned.Add(gunDef.defName))
                Log.Warning($"[BalanceTweak] {gunDef.defName} 同时被建筑炮塔挂载，"
                    + "其 DPS 按建筑节奏计算，pawn 侧不可用，战斗力参考值已跳过该武器。");
            return 0f;
        }

        wd.EnsureDisplayValues();
        float dps = wd.MaxRangedDPS ?? 0f;
        if (dps <= 0f) return 0f;

        float hit = DataUtility.GetHitChanceFactor(gunDef, RefEngageDistance);
        float ap = ResolvedArmorPenetration(wd, out bool derived);
        float range = wd.maxRange ?? RefEngageDistance;
        float rangeFactor = 1f + Mathf.Max(0f,
            Mathf.Log10(Mathf.Max(range, RefEngageDistance) / RefEngageDistance));

        return dps * hit * ApFactor(ap, derived) * rangeFactor;
    }

    /// <summary>
    /// 一件武器作为近战武器时的招式价值（含材料倍率、含穿透因子，不含命中率）。
    /// 没有人形时这条只用于「枪托 / 刀剑」，因为动物和机械用的是种族招式。
    /// </summary>
    internal static float MeleeIntrinsic(ThingDef def, ThingDef? stuff)
    {
        if (def.tools.NullOrEmpty()) return 0f;

        string key = def.defName + "|" + (stuff?.defName ?? "-");
        if (meleeValueCache.TryGetValue(key, out float cached)) return cached;

        float value = DataUtility.CalculateExpectedDPS(
            null, def.tools, 1f, 1f, weaponMode: true, stuff: stuff);
        meleeValueCache[key] = value;
        return value;
    }

    /// <summary>
    /// 护甲穿透：数值直接走原版 <see cref="ProjectileProperties.GetArmorPenetration"/>，
    /// 不在这里重写三段判定。
    /// <para>
    /// 三段是 `damageAmountBase != -1 || armorPenetrationBase >= 0` 取显式值、
    /// 否则取 `damageDef.defaultArmorPenetration`、再否则按 `伤害 × 0.015` 推导，
    /// 外加 `damageDef.armorCategory == null → 0` 的前置短路。这些分支原版方法里都有，
    /// 抄一遍就有第二份定义 —— 而且 mod 里已经有 `ProjectileData.GetArmorPenetrationAbstract`
    /// 走了同一个入口，再抄一份就是第三份。
    /// </para>
    /// <para>
    /// 这里只额外算 <paramref name="derived"/>：那是 "AP 来自伤害推导" 的标记，
    /// 用来决定 <see cref="ApFactor"/> 要不要给 1.5 的补偿。原版方法不吐这个标记，只能自己判一次。
    /// </para>
    /// </summary>
    private static float ResolvedArmorPenetration(WeaponData wd, out bool derived)
    {
        float rawAp = wd.armorPenetrationBase ?? -1f;
        float damage = wd.damageAmountBase ?? -1f;

        var props = (TweakData.GetLiveData(wd.Projectile)?.def as ThingDef)?.projectile;

        // "最终会不会落到按伤害推导"：显式值 ≥ 0 就不是；否则看 damageDef 有没有给默认穿透。
        float beforeDerive = damage >= 0f || rawAp >= 0f
            ? rawAp
            : props?.damageDef?.defaultArmorPenetration ?? -1f;
        derived = beforeDerive < 0f;

        float ap;
        if (props != null)
        {
            ap = props.GetArmorPenetration(null, null);
        }
        else if (derived)
        {
            // 没有弹丸对象（光束类走 beamDamageDef），退回"按伤害推导"的等价式。
            ap = Mathf.Max(damage, 0f) * DerivedApPerDamage;
        }
        else
        {
            ap = Mathf.Max(rawAp, 0f);
        }

        ap *= wd.rangeArmorPenetrationMultiplier ?? 1f;
        if (TweakDatabase.VCTAPDown && ap > 0.5f) ap = 0.5f + Mathf.Log(ap + 0.5f, 5f);
        return ap;
    }

    /// <summary>穿透因子，与 <c>DataUtility.CalculateToolDPS</c> 的规则一致。</summary>
    private static float ApFactor(float ap, bool derived)
    {
        float apm = derived ? 1.5f : 1f;
        float a = Mathf.Min(ap * apm, ap + 1f);
        return (1f + a) * (1f + a);
    }

    #endregion

    #region 防御

    /// <summary>
    /// 等效生命值 = 核心部位血量 ÷ 覆盖率加权减伤 ÷ 体型惩罚 ÷ 来袭伤害系数。
    /// <para>
    /// <b>用核心部位而不是总血量</b>：击杀判据是核心部位被摧毁，而总血量里大头是器官和手脚
    /// （人 1084 对 core 40，差 27 倍），那些部位被打烂既不致命也不影响输出。
    /// </para>
    /// <para>
    /// <b>减伤按部位覆盖率加权</b>：服装只护住它覆盖的部位（背心护 42.7% 的命中，头盔护头部 6.0%），
    /// 所以「躯干 1.0 护甲 + 头盔 0.72」不等于「全身 1.0」。非人形没有服装，各部位护甲一致，
    /// 加权退化成原来的单一值，读数不变。
    /// </para>
    /// <para>
    /// <b>不建模累计伤害阈值（150 × HealthScale）</b>：原版确实有这条死因，但实战里核心部位 / 头部
    /// 被打掉才是主因；把两条都算进来会让「部位饱和度」那套模型压过真实分布（镰会算出比蜈蚣还耐打）。
    /// </para>
    /// </summary>
    private static float Defense(ThingDef race, ArmorSet armor)
    {
        var props = race.race;
        var body = props.body;
        var corePart = body?.corePart?.def;
        if (corePart == null) return 0f;

        float hp = corePart.hitPoints * props.baseHealthScale;

        float raceSharp = StatOrDefault(race, StatDefOf.ArmorRating_Sharp);
        float raceBlunt = StatOrDefault(race, StatDefOf.ArmorRating_Blunt);

        float sumCoverage = 0f;
        float sumMitigation = 0f;

        var parts = body!.AllParts;
        for (int i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            float coverage = part.coverageAbs;
            if (coverage <= 0f) continue;

            // 部位护甲 = max(种族天然护甲, 覆盖该部位的最高一件服装)，不叠层。
            float sharp = Mathf.Max(raceSharp, armor.Sharp(part));
            float blunt = Mathf.Max(raceBlunt, armor.Blunt(part));

            if (TweakDatabase.VCTAMDown)
            {
                sharp = CompressArmor(sharp);
                blunt = CompressArmor(blunt);
            }

            float mitigation = Mathf.Sqrt(Mitigate(sharp) * Mitigate(blunt));
            sumCoverage += coverage;
            sumMitigation += coverage * mitigation;
        }

        if (sumCoverage <= 0f) return 0f;
        float averageMitigation = sumMitigation / sumCoverage;
        if (averageMitigation <= 0.0001f) return 0f;

        // 体型越大越容易被远程打中（ShotReport.factorFromTargetSize），等效血量反着除。
        float size = Mathf.Clamp(props.baseBodySize, 0.5f, 2f);

        float incoming = StatOrDefault(race, StatDefOf.IncomingDamageFactor);
        if (incoming <= 0f) incoming = 1f;

        return hp / averageMitigation / size / incoming;
    }

    /// <summary>
    /// 护甲期望减伤。原版 <c>ArmorUtility.ApplyArmor</c>：<c>rand &lt; eff/2</c> 归零、
    /// <c>rand &lt; eff</c> 半伤、否则全额，于是 <c>E[伤害倍率] = 1 − 0.75 × eff</c>。
    /// </summary>
    private static float Mitigate(float armor)
    {
        float eff = Mathf.Clamp(armor - RefArmorPenetration, 0f, 1f);
        return 1f - 0.75f * eff;
    }

    /// <summary>VCT 的 <c>ArmorUnlimitNerf</c>：护甲超过 2 之后按对数压缩（与 ApparelData 同一处理）。</summary>
    private static float CompressArmor(float armor)
        => armor > 2f ? 2f + Mathf.Log(armor - 1f, 5f) : armor;

    #endregion

    #region 锚与曲线

    /// <summary>用机械那把尺子的类别：机械、人形、实体（Anomaly）、其它。</summary>
    private static bool UsesMechAnchor(TweakData data) => data.propType is
        (int)RaceData.RaceType.Mechanoid or
        (int)RaceData.RaceType.Humanlike or
        (int)RaceData.RaceType.Entity or
        (int)RaceData.RaceType.Other;

    /// <summary>
    /// 锚只在第一次用到时算一次并定格。冻结之后，用户改巨兽 / 蜈蚣的数值不会让整张表跟着漂 ——
    /// 这是「尺子」和「现值」的区别，与武器强度取原始电荷步枪当基准同一个理由。
    /// <para>
    /// 代价：如果用户在第一次打开兵种页签<b>之前</b>就改了锚单位，刻度会带着那处改动。
    /// 这是可接受的（只影响显示口径，不影响存档），要完全固定得把原始数值另存一份，不值得。
    /// </para>
    /// </summary>
    private static void EnsureAnchors()
    {
        if (anchorsReady) return;
        anchorsReady = true;

        animalAnchorAttack = AnchorAttack(AnimalAnchorKind);
        animalAnchorDefense = AnchorDefense(AnimalAnchorKind);
        mechAnchorAttack = AnchorAttack(MechAnchorKind);
        mechAnchorDefense = AnchorDefense(MechAnchorKind);
    }

    private static float AnchorAttack(string kindName)
    {
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName) is not PawnKindDef kind) return 0f;
        // 必须和 Fill 走同一个式子（装备画像 + 接敌损耗 + 炮塔相加），否则锚与行不同口径，刻度就假了。
        return Offense(kind, kind.race, CombatGear.Of(kind));
    }

    private static float AnchorDefense(string kindName)
    {
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName) is not PawnKindDef kind) return 0f;
        return Defense(kind.race, ArmorSet.Empty);
    }

    /// <summary>
    /// 该兵种的技能等级；非 Humanlike 返回 null（走 <c>noSkillOffset</c>）。
    /// 原版只有 Humanlike 建 <c>Pawn_SkillTracker</c>，动物和机械没有技能这回事。
    /// </summary>
    private static int? SkillOf(PawnKindDef kind, SkillDef skill)
    {
        if (kind.race?.race?.Humanlike != true) return null;

        if (kind.skills != null)
        {
            for (int i = 0; i < kind.skills.Count; i++)
            {
                if (kind.skills[i].Skill != skill) continue;
                var range = kind.skills[i].Range;
                return Mathf.RoundToInt((range.min + range.max) * 0.5f);
            }
        }
        return DefaultHumanlikeSkill;
    }

    /// <summary>
    /// 命中 / 精度曲线求值。技能存在时按 <c>defaultBaseValue + 技能等级</c>（<c>SkillNeed_BaseBonus</c>
    /// 每级 +1），没有技能时按 <c>noSkillOffset</c>，两者都过 <c>postProcessCurve</c> ——
    /// 不写死 0.62 / 0.96，曲线被 mod 改了会自动跟随。
    /// </summary>
    private static float Accuracy(StatDef? stat, int? skill)
    {
        if (stat == null) return 1f;
        float raw = stat.defaultBaseValue + (skill ?? stat.noSkillOffset);
        return stat.postProcessCurve?.Evaluate(raw) ?? raw;
    }

    private static float StatOrDefault(ThingDef def, StatDef stat)
        => stat == null ? 1f : def.GetStatValueAbstract(stat);

    private static float MarketValue(ThingDef def)
    {
        if (marketValueCache.TryGetValue(def.defName, out var cached)) return cached;
        float value = DataUtility.GetDefaultMarketValue(def);
        marketValueCache[def.defName] = value;
        return value;
    }

    #endregion
}
