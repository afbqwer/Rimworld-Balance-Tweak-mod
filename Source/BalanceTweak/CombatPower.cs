using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;

namespace BalanceTweak;

/// <summary>
/// 战斗力参考值：攻击 / 防御 / 总体，按类别锚定到 100。
/// <para>
/// 设计见 <c>Docs/CombatPower_Design.md</c>。这里只列三条实现约束：
/// </para>
/// <list type="number">
/// <item><b>刻度在首次被读时定格</b>（<see cref="EnsureAnchors"/>）。锚是「尺子」不是「现值」，
/// 跟着活实例走会让"改一只巨兽 → 所有动物读数漂移"重演武器强度那次的坑。</item>
/// <item><b>远程 DPS 一律读 <see cref="WeaponData.MaxRangedDPS"/></b>，不在这里重算 ——
/// 光束（<c>verb.beamDamageDef</c>）、电弧喷射（<c>Verb_ArcSprayIncinerator</c>）、
/// 建筑炮塔节奏这几个分支 <c>GenRangeDPS</c> 已经处理过，重写必然漏。</item>
/// <item><b>只对非人形生效</b>：动物、昆虫、机械、实体（Anomaly）、其它。
/// Humanlike 要建模装备、技能、基因、药物，不在这一轮。锚按类别分：动物/昆虫锚巨兽，
/// 机械/实体/其它锚蜈蚣·爆裂（三类都没有"人类式装备体系"，用动物锚会全挤在低端）。</item>
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

    /// <summary>动物 / 昆虫的锚：巨兽（Core），原版 combatPower 500。</summary>
    private const string AnimalAnchorKind = "Thrumbo";

    /// <summary>机械的锚：蜈蚣·爆裂（Core），原版 combatPower 400。选 Core 内的单位当尺子 ——
    /// 拿 DLC 单位当基准，没装该 DLC 的玩家刻度就无从校准。</summary>
    private const string MechAnchorKind = "Mech_CentipedeBlaster";

    #endregion

    #region 状态

    private static bool anchorsReady;
    private static float animalAnchorAttack;
    private static float animalAnchorDefense;
    private static float mechAnchorAttack;
    private static float mechAnchorDefense;

    /// <summary>无技能单位的近战命中率（<c>MeleeHitChance</c> 曲线在 <c>noSkillOffset</c> 处的值）。</summary>
    private static float? meleeHitChance;

    /// <summary>无技能单位的射击精度（<c>ShootingAccuracyPawn</c> 同理）。</summary>
    private static float? shootingAccuracy;

    /// <summary>已告警过的炮塔枪（避免每帧刷日志）。</summary>
    private static readonly HashSet<string> turretCooldownWarned = [];

    /// <summary>defName → 市价。候选武器筛选对每一行都要用，而市价与兵种无关，缓存掉省一次 StatWorker。</summary>
    private static readonly Dictionary<string, float> marketValueCache = [];

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
        // 列虽然只对非人形显示，但 EnsureDisplayValues 对每一行都会跑 —— 人类兵种有几百行，
        // 在入口就挡掉，别让它们白跑一遍候选武器枚举。
        if (!IsCovered(data)) return;

        var race = kind.race;
        float attack = Mathf.Max(MeleeAttack(race) * MeleeEngagePenalty, RangedAttack(kind, race));
        float defense = Defense(race);

        EnsureAnchors();
        bool mechScale = UsesMechAnchor(data);
        float anchorAttack = mechScale ? mechAnchorAttack : animalAnchorAttack;
        float anchorDefense = mechScale ? mechAnchorDefense : animalAnchorDefense;

        data.attackPower = anchorAttack > 0f ? attack / anchorAttack * 100f : 0f;
        data.defensePower = anchorDefense > 0f ? defense / anchorDefense * 100f : 0f;
        data.overallPower = Mathf.Sqrt(Mathf.Max(0f, data.attackPower.Value) * Mathf.Max(0f, data.defensePower.Value));
    }

    /// <summary>
    /// 依赖行被改后，把受影响的 pawnkind 行显示缓存置脏。
    /// <para>
    /// 粗粒度是刻意的：一个 pawnkind 依赖"一集合"武器，一把武器又反依赖一堆 pawnkind，
    /// 两个方向都建反向索引的维护成本远大于收益。这里只是写 bool，几百行是纳秒级。
    /// </para>
    /// <para>
    /// 但<b>只对真会影响战斗力的类型动手</b>：改一栋建筑、一个配方不该让整张兵种表重算 ——
    /// 重算不是免费的（每行要扫一遍武器库）。
    /// </para>
    /// </summary>
    public static void Invalidate(TweakData changed)
    {
        // 价格缓存跟着武器 / 建筑改动失效：改 costList 会改市价，进而改"武器预算放不放得下"。
        switch (changed.id.settingType)
        {
            case SettingType.Weapon:
            case SettingType.Building:
            case SettingType.Misc:
                marketValueCache.Clear();
                break;
        }

        if (!AffectsCombat(changed.id.settingType)) return;
        if (!cachedData.TryGetValue(SettingType.PawnKind, out var rows)) return;
        foreach (var data in rows.Values) data.InvalidateDisplayValues();
    }

    /// <summary>哪些页签的改动会让兵种战斗力失效。</summary>
    private static bool AffectsCombat(SettingType type) => type switch
    {
        SettingType.PawnKind => true,     // 武器预算 / 武器标签 / combatPower 本身
        SettingType.Weapon => true,       // DPS、穿透、射程
        SettingType.Race => true,         // 血量、护甲、体型、招式、炮塔组件
        SettingType.MeleeTool => true,    // 招式数值
        SettingType.BodyDef => true,      // 核心部位血量
        _ => false,
    };

    /// <summary>测试用：丢掉锚的定格值。</summary>
    internal static void ResetAnchors()
    {
        anchorsReady = false;
        animalAnchorAttack = animalAnchorDefense = 0f;
        mechAnchorAttack = mechAnchorDefense = 0f;
        turretCooldownWarned.Clear();
        marketValueCache.Clear();
    }

    #endregion

    #region 攻击：近战

    /// <summary>
    /// 近战有效输出 = 招式加权 DPS × 命中 × 穿透因子。
    /// <para>
    /// DPS 直接复用 <see cref="DataUtility.CalculateExpectedDPS"/>（<c>weaponMode: true</c>），
    /// 与武器强度的近战刻度同源 —— 那边把负穿透折算成 <c>1.5 × damage × 0.015</c> 的加成，
    /// 这里不另立一套，否则同一只镰在武器页和兵种页会有两种算不出来的关系。
    /// </para>
    /// </summary>
    private static float MeleeAttack(ThingDef race)
    {
        if (race.tools.NullOrEmpty()) return 0f;

        float damageFactor = StatOrDefault(race, StatDefOf.MeleeDamageFactor);
        float cooldownFactor = StatOrDefault(race, StatDefOf.MeleeCooldownFactor);

        float dps = DataUtility.CalculateExpectedDPS(
            race.tools, null, damageFactor, cooldownFactor, weaponMode: true);

        return dps * MeleeHitChance;
    }

    #endregion

    #region 攻击：远程

    /// <summary>
    /// 远程有效输出：装备武器通道与炮塔通道取高值。
    /// 不求和 —— 一个单位在同一时刻只能用一个 verb。
    /// </summary>
    private static float RangedAttack(PawnKindDef kind, ThingDef race)
    {
        float best = kind.weaponTags.NullOrEmpty() ? 0f : EquippedGunValue(kind);

        foreach (var gun in TurretGuns(race))
            best = Mathf.Max(best, GunValue(gun));

        return best;
    }

    /// <summary>
    /// 按原版权重求候选武器的期望值。
    /// <para>
    /// 筛选条件逐条对齐 <c>PawnWeaponGenerator.TryGenerateWeaponFor</c>：标签交集、价格上限、
    /// <c>generateAllowChance</c>、derp 判定。其中 <c>generateAllowChance</c> 一条是验证时撞出来的 ——
    /// 原版 <c>Gun_ChargeLance_Unique</c> 与普通版共用 <c>MechanoidGunMedium</c> 标签，
    /// 漏了这条会把"永不自然生成"的独特变体算进期望值。
    /// </para>
    /// </summary>
    private static float EquippedGunValue(PawnKindDef kind)
    {
        var tags = kind.weaponTags;
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

            if (TweakData.GetLiveData(new TweakID(def.defName, SettingType.Weapon)) is not WeaponData wd) continue;

            float value = GunValue(wd);
            if (value <= 0f) continue;

            float weight = Mathf.Max(def.generateCommonality, 0.0001f) * Mathf.Max(price, 0.0001f);
            sumWeight += weight;
            sumWeighted += weight * value;
            best = Mathf.Max(best, value);
        }

        return sumWeight > 0f ? sumWeighted / sumWeight : best;
    }

    /// <summary>
    /// 单位自带炮塔（<c>CompProperties_TurretGun</c>）：原版 <c>Mech_Centurion</c> /
    /// <c>Mech_Warqueen</c> / <c>Mech_Diabolus</c> 的 race 上都挂了这个组件。
    /// <c>autoAttack = false</c> 的是玩家手动控制用，敌方不会自动开火，不计入。
    /// </summary>
    private static IEnumerable<ThingDef> TurretGuns(ThingDef race)
    {
        if (race.comps == null) yield break;
        foreach (var comp in race.comps)
        {
            if (comp is CompProperties_TurretGun turret && turret.autoAttack && turret.turretDef != null)
                yield return turret.turretDef;
        }
    }

    /// <summary>
    /// 单把枪的价值 = 期望 DPS × 武器命中 × 射手折扣 × 穿透因子 × 射程系数。
    /// </summary>
    private static float GunValue(ThingDef gunDef)
    {
        if (TweakData.GetLiveData(new TweakID(gunDef.defName, SettingType.Weapon)) is not WeaponData wd)
            return 0f;
        return GunValue(wd);
    }

    private static float GunValue(WeaponData wd)
    {
        if (wd.def is not ThingDef gunDef) return 0f;

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

        return dps
            * hit
            * Mathf.Pow(ShootingAccuracy, RefEngageDistance)
            * ApFactor(ap, derived)
            * rangeFactor;
    }

    /// <summary>
    /// 护甲穿透按原版三段判定还原（<c>Verse/ProjectileProperties.cs:160</c>）：
    /// <code>
    /// if (damageAmountBase != -1 || armorPenetrationBase >= 0) ap = armorPenetrationBase;
    /// else                                                     ap = damageDef.defaultArmorPenetration;
    /// if (ap &lt; 0)                                             ap = 伤害 × 0.015;
    /// ap *= RangedWeapon_ArmorPenetrationMultiplier;
    /// </code>
    /// <paramref name="derived"/> 为 true 表示走了最后那段"按伤害推导"。
    /// <para>
    /// 这段不能省：<c>armorPenetrationBase</c> 的字段默认值是 <b>-1</b> 而不是 0，
    /// 原版绝大多数弹丸都没写这一项，直接当 0 会把电荷长枪的 0.45 读成 0。
    /// </para>
    /// </summary>
    private static float ResolvedArmorPenetration(WeaponData wd, out bool derived)
    {
        float rawAp = wd.armorPenetrationBase ?? -1f;
        float damage = wd.damageAmountBase ?? -1f;
        float ap;

        if (damage >= 0f || rawAp >= 0f)
        {
            ap = rawAp;
        }
        else
        {
            var projectile = TweakData.GetLiveData(wd.Projectile) as ProjectileData;
            ap = projectile?.damageType?.defaultArmorPenetration ?? -1f;
        }

        derived = ap < 0f;
        if (derived)
        {
            if (damage < 0f)
            {
                var projectile = TweakData.GetLiveData(wd.Projectile) as ProjectileData;
                damage = projectile?.damageType?.defaultDamage ?? 0f;
            }
            ap = damage * DerivedApPerDamage;
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
    /// 等效生命值 = 核心部位血量 ÷ 护甲期望减伤 ÷ 体型惩罚 ÷ 来袭伤害系数。
    /// <para>
    /// <b>用核心部位而不是总血量</b>：击杀判据是核心部位被摧毁，而总血量里大头是器官和手脚
    /// （人 1084 对 core 40，差 27 倍），那些部位被打烂既不致命也不影响输出。
    /// </para>
    /// </summary>
    private static float Defense(ThingDef race)
    {
        var props = race.race;
        var corePart = props.body?.corePart?.def;
        if (corePart == null) return 0f;

        float hp = corePart.hitPoints * props.baseHealthScale;

        float sharp = StatOrDefault(race, StatDefOf.ArmorRating_Sharp);
        float blunt = StatOrDefault(race, StatDefOf.ArmorRating_Blunt);
        if (TweakDatabase.VCTAMDown)
        {
            sharp = CompressArmor(sharp);
            blunt = CompressArmor(blunt);
        }

        float mitigation = Mathf.Sqrt(Mitigate(sharp) * Mitigate(blunt));
        if (mitigation <= 0f) return 0f;

        // 体型越大越容易被远程打中（ShotReport.factorFromTargetSize），等效血量反着除。
        float size = Mathf.Clamp(props.baseBodySize, 0.5f, 2f);

        float incoming = StatOrDefault(race, StatDefOf.IncomingDamageFactor);
        if (incoming <= 0f) incoming = 1f;

        return hp / mitigation / size / incoming;
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

    /// <summary>
    /// 用机械那把尺子的类别：机械、实体（Anomaly）、其它。
    /// <para>
    /// 这三类都没有"人类式的装备 / 技能"体系，用动物锚（巨兽）会全部挤在低端 ——
    /// 巨兽本身就不是它们的同侪。统一挂到机械刻度上，至少和同场出现的单位可比。
    /// </para>
    /// </summary>
    private static bool UsesMechAnchor(TweakData data) => data.propType switch
    {
        (int)RaceData.RaceType.Mechanoid => true,
        (int)RaceData.RaceType.Entity => true,
        (int)RaceData.RaceType.Other => true,
        _ => false,
    };

    /// <summary>本轮覆盖范围：除 Humanlike 以外的全部（含实体与"其它"）。</summary>
    private static bool IsCovered(TweakData data) => data.propType != (int)RaceData.RaceType.Humanlike;

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
        // 必须和 Fill 走同一个式子（含接敌损耗），否则锚与行不同口径，刻度就假了。
        return Mathf.Max(MeleeAttack(kind.race) * MeleeEngagePenalty, RangedAttack(kind, kind.race));
    }

    private static float AnchorDefense(string kindName)
    {
        if (DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName) is not PawnKindDef kind) return 0f;
        return Defense(kind.race);
    }

    /// <summary>近战命中率：从 <c>MeleeHitChance</c> 的曲线取值，不写死常数（曲线被 mod 改了会自动跟随）。</summary>
    private static float MeleeHitChance => meleeHitChance ??= NoSkillStatValue(StatDefOf.MeleeHitChance);

    /// <summary>射击精度：同上。动物和机械都没有 <c>Pawn_SkillTracker</c>，所以走 <c>noSkillOffset</c>。</summary>
    private static float ShootingAccuracy => shootingAccuracy ??= NoSkillStatValue(StatDefOf.ShootingAccuracyPawn);

    /// <summary>
    /// 复刻 <c>StatWorker.GetValueUnfinalized</c> 在「没有技能」时的取值：
    /// <c>base + noSkillOffset</c>，再过 <c>postProcessCurve</c>。
    /// </summary>
    private static float NoSkillStatValue(StatDef stat)
    {
        if (stat == null) return 1f;
        float raw = stat.defaultBaseValue + stat.noSkillOffset;
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
