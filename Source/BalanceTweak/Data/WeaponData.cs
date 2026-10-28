using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.DataUtility;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Weapon, Priority = 200, MatchMethod = nameof(DataUtility.MatchWeapon))]
public class WeaponData : TweakData<WeaponData>, ISubItemHost
{
    /// <summary>武器招式子项（原 Init() 里 "data is RaceData or WeaponData" 那段硬编码）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not ThingDef t || t.tools.NullOrEmpty()) yield break;
        for (int i = 0; i < t.tools.Count; i++)
        {
            var td = new ToolData { index = i };
            td.SetParentTweak(parent, SettingType.MeleeTool, tweaked);
            yield return td;
        }
    }

    // 字段定义
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfTurret))]
    public TweakID? GunTurret = null;

    [TweakField(StatDef = "MarketValue", Available = nameof(AvailableIfNonBuildin))]
    public float? marketValue = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNonBuildin))]
    public float? normalMarketValue = null;

    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? forcedMissRadius = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyTouch", Available = nameof(AvailableIfRanged))]
    public float? accuracyTouch = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyShort", Available = nameof(AvailableIfRanged))]
    public float? accuracyShort = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyMedium", Available = nameof(AvailableIfRanged))]
    public float? accuracyMedium = null;
    [TweakField(Style = ColumnStyle.Prec, StatDef = "AccuracyLong", Available = nameof(AvailableIfRanged))]
    public float? accuracyLong = null;
    [TweakField(StatDef = "RangedWeapon_Cooldown", Available = nameof(AvailableIfRanged))]
    public float? cooldown = null;

    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged), GetAbstract = nameof(GetdamageAmountBaseAbstract))]
    public int? damageAmountBase = null;// -1
    [TweakField(Available = nameof(AvailableIfRanged), GetAbstract = nameof(GetArmorPenetrationAbstract))]
    public float? armorPenetrationBase = null;// -1
    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? warmup = null;// -1
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged))]
    public int? ticksBetweenBurstShots = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfRanged))]
    public int? burstShotCount = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? OptimalRange = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? MaxRangedDPS = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? AverageRangedDPS = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfRanged))]
    public float? LRangedDPS = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link, Available = nameof(AvailableIfRanged))]
    public TweakID? Projectile = null;

    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfMelee))]
    public float? MeleeDPS = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? LMeleeDPS = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? WeaponTool = null;

    [TweakField(DataType = ColumnDataType.Display)]
    public float? equipPower = null;
    //[TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfNonBuildin))]
    //public float? equipCP = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? isCompQuality = null;

    //[TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool, Default = nameof(GetIsMadeofStuff), Available = nameof(AvailableIfNonBuildin))]
    //public bool? isMadeofStuff = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public int? costStuffCount = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList, Available = nameof(AvailableIfNonBuildin))]
    public List<ThingDefCountClass>? costList = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfNonBuildin))]
    public List<StuffCategoryDef>? stuffCategories = null;
    [TweakField(StatDef = "WorkToMake", Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public float? workToMake = null;
    [TweakField(StatDef = "MaxHitPoints", Style = ColumnStyle.Int, Available = nameof(AvailableIfNonBuildin))]
    public float? maxHitPoints = null;
    [TweakField(StatDef = "Flammability", Style = ColumnStyle.Prec, Available = nameof(AvailableIfNonBuildin))]
    public float? flammability = null;

    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? minRange = null;
    [TweakField(Available = nameof(AvailableIfRanged))]
    public float? maxRange = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;

    [TweakField(StatDef = "MeleeWeapon_DamageMultiplier", Style = ColumnStyle.Prec)]
    public float? meleeDamageMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_WarmupMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? warmupMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_DamageMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeDamageMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_ArmorPenetrationMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeArmorPenetrationMultiplier = null;
    [TweakField(StatDef = "RangedWeapon_RangeMultiplier", Style = ColumnStyle.Prec, Available = nameof(AvailableIfRanged))]
    public float? rangeMultiplier = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? thingSetMakerTags = null;
    /// <summary>
    /// 武器自身的标签（<c>ThingDef.weaponTags</c>，原版允许为 null）。
    /// 角色生成时按 <c>PawnKindDef.weaponTags</c> 与这里的交集挑武器，交易与随机生成也按它归类。
    /// </summary>
    [TweakField(Style = ColumnStyle.ThingSetTags)]
    public List<string>? weaponTagList = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? equippedStatOffsets = null;

    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonMelee))]
    public bool? removeReloadableComp = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableMaxCharges = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonMelee))]
    public ThingDef? reloadableAmmoDef = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableAmmoCountToRefill = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableAmmoCountPerCharge = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonMelee))]
    public int? reloadableBaseReloadTicks = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonMelee))]
    public bool? reloadableReplenishAfterCooldown = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;


    private float? LMAXRangedDPS = null;
    private VerbProperties? verb = null;
    static FieldInfo ForcedMissRadius = Reflect.Field(typeof(VerbProperties), "forcedMissRadius");
    public override int LoadingOrd => 300;

    /// <summary>
    /// 解析本武器关联的弹丸。GenValues 与 Apply 共用同一个入口：
    /// SetDef 阶段通常已经能解析（<see cref="ProjectileData"/> 用的是默认 LoadingOrd 100，先于武器的 300），
    /// 但读档的 <c>Projectile</c> 可能缺席或指向已换掉的实例，Apply 时还要按"当前生效"的那一层再取一次。
    /// </summary>
    /// <param name="forWrite">
    /// 要往弹丸上写东西时传 true：此时必须拿"可写实例"（<see cref="TweakData.GetEditableData"/>），
    /// 初始化期 UI 缓存还没建，只读路径拿到的原始快照写进去既不落盘、又会污染原值。
    /// </param>
    private ProjectileData? ResolveProjectile(VerbProperties? v, bool forWrite = false)
    {
        if (Projectile == null && v?.defaultProjectile != null
            && TweakDatabase.projectileDatas.TryGetValue(v.defaultProjectile.defName, out var pid))
        {
            Projectile = pid;
        }
        return (forWrite ? GetEditableData(Projectile) : GetLiveData(Projectile)) as ProjectileData;
    }

    /// <summary>
    /// 这一格是否正被「还原」（<see cref="StatColumnConfig.ResetTo"/>：右键重置此格 / 重置整列）。
    /// <para>
    /// <c>ResetTo</c> 把原值写回字段并清掉修改标记，字段状态因此必然回到 <see cref="FieldState.Unset"/> ——
    /// 只看三态的话，"重置"和"没动过"没有区别，镜像列的重置就会静默失效。
    /// </para>
    /// <para>
    /// 整行重置（<see cref="TweakData.forceWriteAll"/>）不算：那是"我这一行全恢复"，
    /// 不该顺手把别人对同一颗弹丸的修改一起撤销。
    /// </para>
    /// </summary>
    private bool IsFieldBeingReset(string field)
        => !forceWriteAll && forceWriteFields?.Contains(field) == true;

    /// <summary>用户是否显式改过武器行的伤害 / 穿透（这两格是弹丸的镜像，改它们等于改弹丸）。</summary>
    private bool HasWeaponDamageEdit
        => StateOf(nameof(damageAmountBase)) != FieldState.Unset || IsFieldBeingReset(nameof(damageAmountBase))
        || StateOf(nameof(armorPenetrationBase)) != FieldState.Unset || IsFieldBeingReset(nameof(armorPenetrationBase));

    /// <summary>
    /// 登记"这把武器引用这个弹丸"，供 <see cref="ProjectileData.RefreshSharedWeapons"/> 反查刷新。
    /// <para>
    /// 弹丸页的武器关联列是<b>实时反查</b>（<see cref="ProjectileData.FindWeaponLinks"/>），不靠这个索引，
    /// 所以这里只维护"改动弹丸时该刷新谁"。
    /// </para>
    /// <para>
    /// 必须每次 GenValues 都登记，不能只在首次解析时做 —— 读了存档的武器行 <c>Projectile</c> 已序列化，
    /// 走不到"首次解析"分支，漏登记就会在弹丸改动时不被刷新。
    /// </para>
    /// </summary>
    private void LinkProjectile(ProjectileData projectile) => projectile.RegisterSharedWeapon(id);

    private void ApplyWeaponProp(ThingDef def)
    {
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);
        ApplyL<string>(nameof(thingSetMakerTags), v => def.thingSetMakerTags = v);
        if (IsFieldModified(nameof(thingSetMakerTags))) DataUtility.RefreshThingSetMakerFilters();
        ApplyL<string>(nameof(weaponTagList), v => def.weaponTags = v);
        bool costChanged = false;
        ApplyV<int>(nameof(costStuffCount), v => { def.costStuffCount = v; costChanged = true; });
        ApplyL<ThingDefCountClass>(nameof(costList), v => { def.costList = v?.ToList(); costChanged = true; });
        ApplyL<StuffCategoryDef>(nameof(stuffCategories), v => { def.stuffCategories = v?.ToList(); costChanged = true; });

        if (costChanged)
        {
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.Where(r =>
                r.generated && r.products != null && r.products.Count == 1 && r.products[0].thingDef == def))
            {
                RecipeDefGenerator.SetIngredients(recipe, def, recipe.adjustedCount);
            }
            CostListCalculator.Reset();
        }

        ApplyV<bool>(nameof(isCompQuality), v =>
        {
            if (!v)
            {
                def.comps.RemoveWhere(c => c.compClass == typeof(CompQuality));
            }
            else if (!def.comps.Any(c => c.compClass == typeof(CompQuality)))
            {
                def.comps.Add(new CompProperties(typeof(CompQuality)));
            }
        });

        // 移除可重装组件：仅当用户显式设 true（Set 且值为 true）。判据用 StateOf 而非裸 HasValue。
        if (StateOf(nameof(removeReloadableComp)) == FieldState.Set && (bool)RawVal(nameof(removeReloadableComp))!)
        {
            def.comps.RemoveWhere(c => c is CompProperties_ApparelReloadable);
        }
        else
        {
            var reloadable = def.GetCompProperties<CompProperties_ApparelReloadable>();
            if (reloadable != null)
            {
                ApplyD<ThingDef>(nameof(reloadableAmmoDef), v => reloadable.ammoDef = v);
                ApplyV<int>(nameof(reloadableAmmoCountToRefill), v => reloadable.ammoCountToRefill = v);
                ApplyV<int>(nameof(reloadableAmmoCountPerCharge), v => reloadable.ammoCountPerCharge = v);
                ApplyV<int>(nameof(reloadableBaseReloadTicks), v => reloadable.baseReloadTicks = v);
                ApplyV<bool>(nameof(reloadableReplenishAfterCooldown), v => reloadable.replenishAfterCooldown = v);
                ApplyV<int>(nameof(reloadableMaxCharges), v => reloadable.maxCharges = v);
            }
        }
        if (verb != null)
        {
            ApplyV<float>(nameof(maxRange), v => verb.range = v);
            ApplyV<float>(nameof(minRange), v => verb.minRange = v);
            ApplyV<float>(nameof(warmup), v => verb.warmupTime = v);
            ApplyV<int>(nameof(ticksBetweenBurstShots), v => verb.ticksBetweenBurstShots = v);
            ApplyV<int>(nameof(burstShotCount), v => verb.burstShotCount = v);
            ApplyV<float>(nameof(forcedMissRadius), v => ForcedMissRadius.SetValue(verb, v));
            // 用户动过伤害/穿透这两格时直接取"可写实例"，省掉一次只读解析；
            // 没动过就只读，弹丸自己的 Apply 才是它那些字段的权威写入方，这里重复写只会制造竞态。
            bool damageEdited = HasWeaponDamageEdit;
            var p = ResolveProjectile(verb, forWrite: damageEdited);
            if (p != null)
            {
                LinkProjectile(p);
                if (damageEdited && p.def is ThingDef pdef)
                {
                    PushWeaponEditsToProjectile(p);
                    p.SetProjectileProp(pdef, refreshWeapons: true);
                }
            }
        }
    }

    /// <summary>
    /// 把武器行上的伤害 / 穿透（本质是关联弹丸的镜像）写回弹丸。
    /// <para>
    /// <b>只在用户显式动过这两格时写</b>：改过（Set）就写新值，清空或"重置此格/此列"（Cleared / 还原）
    /// 就让弹丸自己恢复原值。<b>没动过就什么都不写</b> —— 这两列在 <see cref="GenValues"/> 里被无条件
    /// 同步成弹丸的值，属于派生镜像；若每次 Apply 都照镜像盖回去，任何一把共用同一弹丸的武器（原版
    /// <c>Bullet_Shotgun</c> 就同时供泵动霰弹枪与链式霰弹枪）在改自身其它字段时，
    /// 都会把自己那份陈旧镜像盖回共享弹丸，把另一把武器的修改静默撤销。
    /// </para>
    /// <para>
    /// 改动还要标记到弹丸那一行并登记进已修改表：否则弹丸的 <c>ApplyV</c> 会因三态未改而跳过，
    /// 值只停在内存里，def 不落盘、存档也带不走。
    /// </para>
    /// <para>
    /// 写成功后武器行会主动放弃这两格的修改标记（见方法末尾）：改动既然存进了弹丸，
    /// "已修改"与撤销入口就都该出现在弹丸行 —— 否则武器行那个重置按钮按下去不会回写弹丸，是假的。
    /// </para>
    /// </summary>
    /// <returns>是否真的动过弹丸（决定要不要重算整组枪的强度）。</returns>
    private bool PushWeaponEditsToProjectile(ProjectileData p)
    {
        bool touched = false;

        if (IsFieldBeingReset(nameof(damageAmountBase))
            || StateOf(nameof(damageAmountBase)) == FieldState.Cleared)
        {
            // 让弹丸自己按"显式清空 / 还原"恢复原值（ApplyV 会走 Revert），不必在这里抄原值。
            p.MarkFieldCleared(nameof(damageAmountBase));
            touched = true;
        }
        else if (StateOf(nameof(damageAmountBase)) == FieldState.Set && damageAmountBase.HasValue)
        {
            p.damageAmountBase = damageAmountBase;
            p.MarkFieldModified(nameof(damageAmountBase));
            touched = true;
        }

        if (IsFieldBeingReset(nameof(armorPenetrationBase))
            || StateOf(nameof(armorPenetrationBase)) == FieldState.Cleared)
        {
            p.MarkFieldCleared(nameof(armorPenetrationBase));
            touched = true;
        }
        else if (StateOf(nameof(armorPenetrationBase)) == FieldState.Set && armorPenetrationBase.HasValue)
        {
            p.armorPenetrationBase = armorPenetrationBase;
            p.MarkFieldModified(nameof(armorPenetrationBase));
            touched = true;
        }

        if (!touched) return false;

        p.MarkIndirectlyModified();

        // 改动归属已经转移到弹丸那一行，武器行自己不再持有这两格的修改标记。
        // 不撤的话武器行会亮"已修改"并给出重置入口，而那个重置按"只重置本行"的规则不会回写弹丸
        // —— 点了没反应，是个假按钮。可见归属与可撤销入口都必须落在弹丸行。
        RemoveFieldModified(nameof(damageAmountBase));
        RemoveFieldModified(nameof(armorPenetrationBase));
        if (CountFieldModified() == 0)
        {
            tweaked = false;
            // 一行都没改了，就别再占一个存档条目 —— 否则读档时 SetDef(tweaked: true) 又会把"已修改"点亮。
            tweakDatas.Remove(id);
        }
        return true;
    }

    static float? meleeLQualityMult = null;
    static float? AccuracyMediumLMult = null;
    static float? DamageLMultiplier = null;
    static float? APLMultiplier = null;
    static List<Tool>? HumanTool = null;
    /// <param name="includeEquipPower">SetDef 首次建数据时为 false，避免启动期跑昂贵的 GetEquipPower；
    /// Apply / 弹丸 / 招式刷新路径保持默认 true，行为与改动前一致。</param>
    public void GenValues(bool includeEquipPower = true)
    {
        if (!meleeLQualityMult.HasValue || !AccuracyMediumLMult.HasValue || !DamageLMultiplier.HasValue)
        {
            meleeLQualityMult = GetLegendaryFactor(StatDefOf.MeleeWeapon_DamageMultiplier);
            AccuracyMediumLMult = GetLegendaryFactor(StatDefOf.AccuracyMedium);
            DamageLMultiplier = GetLegendaryFactor(StatDefOf.RangedWeapon_DamageMultiplier);
            APLMultiplier = GetLegendaryFactor(StatDefOf.RangedWeapon_ArmorPenetrationMultiplier);
        }
        if (HumanTool == null)
        {
            var humanreference = DefDatabase<ThingDef>.GetNamed("Human");
            HumanTool = humanreference.tools;
        }
        // 只在 def 已绑定时推断，且不要用 ?? false 兜底：
        // def 为 null 时 GetIsCompQuality 返回 null，兜成 false 会把这个派生字段永久写坏
        // （详见 SetDef 里的说明）。
        if (!isCompQuality.HasValue && def is ThingDef)
            isCompQuality = (bool?)GetIsCompQuality(this) ?? false;
        if (def is ThingDef d)
        {
            techLevel ??= d.techLevel;
            verb ??= d.Verbs.FirstOrDefault(v => !v.IsMeleeAttack);
            if (verb != null)
            {
                var projectile = ResolveProjectile(verb);
                if (projectile != null)
                {
                    LinkProjectile(projectile);
                    // 与关联弹丸保持同步：弹丸的伤害/穿透被编辑后，武器行需跟随刷新（DPS 由这两个字段算出）。
                    // 不能用 ??=：武器在 SetDef 阶段已填过值，??= 会让弹丸改动永远刷不进来。
                    damageAmountBase = projectile.damageAmountBase;
                    armorPenetrationBase = projectile.armorPenetrationBase;
                }
                warmup ??= verb.warmupTime;
                minRange ??= verb.minRange;
                maxRange ??= verb.range;
                ticksBetweenBurstShots ??= verb.ticksBetweenBurstShots;
                burstShotCount ??= verb.burstShotCount;
                forcedMissRadius ??= (float?)ForcedMissRadius.GetValue(verb);
                GenRangeDPS(d, verb);
            }
            var stuff = GetDefaultStuffFromDef(d);
            var md = CalculateExpectedDPS(null, d.tools, meleeDamageMultiplier ?? 1f, 1f, false, stuff);
            MeleeDPS = md;
            if (d.HasAssignableCompFrom(typeof(CompQuality)))
            {
                LMeleeDPS = CalculateExpectedDPS(null, d.tools, meleeLQualityMult.Value * (meleeDamageMultiplier ?? 1f), 1f, false, stuff);
            }
            else
            {
                LMeleeDPS = md;
            }
            //var mm = TryGetStat(d, StatDefOf.MeleeWeapon_DamageMultiplier);
            //if (mm.HasValue)
            //{
            //    MeleeDPS *= mm.Value;
            //    LMeleeDPS *= mm.Value;
            //}

            // SetDef 首次建数据时跳过（启动期不跑昂贵计算）；Apply / 弹丸 / 招式刷新时照常重算。
            if (includeEquipPower)
                equipPower = (float?)GetEquipPower(this, HumanTool, isCompQuality.GetValueOrDefault(), stuff);
            //equipCP = (float?)GetEquipCP(this, equipPower!.Value, isCompQuality);
        }
    }

    private void GenRangeDPS(ThingDef d, VerbProperties verb)
    {
        var dmg = damageAmountBase;
        var cd = cooldown;
        var aim = warmup;
        if (!dmg.HasValue || dmg.Value == -1)
        {
            if (d.projectile?.damageDef is DamageDef dd && !dd.causeStun && !dd.harmsHealth)
            {
                MaxRangedDPS = 0f;
                AverageRangedDPS = 0f;
                LRangedDPS = 0f;
                return;
            }
            if (Projectile != null)
            {
                var p = GetData(Projectile);
                dmg = (int)ProjectileData.GetdamageAmountBaseAbstract(p);
            }
            else if (verb.beamDamageDef is DamageDef bid)
            {
                dmg = bid.defaultDamage;
            }
        }
        if (!cd.HasValue && verb.defaultCooldownTime > 0) { cd = verb.defaultCooldownTime; }
        if (aim == 0f && GunTurret != null && GetData(GunTurret) is BuildingData bd && bd.def is ThingDef builddef)
        {
            aim = builddef.building?.turretBurstWarmupTime.Average;
        }
        if (!dmg.HasValue) { dmg = 0; } else { dmg = Math.Max(dmg.Value, 0); }
        if (cd.HasValue && aim.HasValue && burstShotCount.HasValue && cd > 0f)
        {
            MaxRangedDPS = (dmg * burstShotCount * (rangeDamageMultiplier ?? 1f))
                / (aim * (warmupMultiplier ?? 1f) + cd + (burstShotCount - 1) * (ticksBetweenBurstShots ?? 0) / 60f);
        }
        else
        {
            return;
        }
        if (verb.verbClass == typeof(Verb_ArcSprayIncinerator))
        {
            MaxRangedDPS /= burstShotCount;
        }
        if (!minRange.HasValue || !maxRange.HasValue)
        {
            AverageRangedDPS = MaxRangedDPS;
            LRangedDPS = AverageRangedDPS * DamageLMultiplier ?? 1f;
        }
        else
        {
            OptimalRange = FindOptimalRange(d, minRange.Value, maxRange.Value);
            var hit = GetHitChanceFactor(d, OptimalRange.Value);
            AverageRangedDPS = MaxRangedDPS * hit;
            if (isCompQuality.GetValueOrDefault())
            {
                var dmgl = (int?)(dmg.Value * DamageLMultiplier ?? 1f);
                var hitl = Mathf.Clamp01(hit * AccuracyMediumLMult ?? 1f);
                var maxdps = (dmgl * burstShotCount * (rangeDamageMultiplier ?? 1f))
                    / (aim * (warmupMultiplier ?? 1f) + cd + (burstShotCount - 1) * ticksBetweenBurstShots / 60f);
                LRangedDPS = maxdps * hitl;
                LMAXRangedDPS = maxdps;
            }
            else { LRangedDPS = AverageRangedDPS; LMAXRangedDPS = MaxRangedDPS; }
        }
    }

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        costStuffCount ??= (int?)GetCostStuffCount(this);
        if (def is ThingDef td)
        {
            if (!IsFieldModified(nameof(costList))) costList ??= td.costList;
            if (!IsFieldModified(nameof(stuffCategories))) stuffCategories ??= td.stuffCategories;
            if (!IsFieldModified(nameof(statBases))) statBases ??= td.statBases;
            if (!IsFieldModified(nameof(thingSetMakerTags))) thingSetMakerTags ??= td.thingSetMakerTags;
            if (!IsFieldModified(nameof(weaponTagList))) weaponTagList ??= td.weaponTags;
            if (!IsFieldModified(nameof(equippedStatOffsets))) equippedStatOffsets ??= td.equippedStatOffsets;
            var reloadable = td.GetCompProperties<CompProperties_ApparelReloadable>();
            if (reloadable != null)
            {
                reloadableAmmoDef ??= reloadable.ammoDef;
                reloadableAmmoCountToRefill ??= reloadable.ammoCountToRefill;
                reloadableAmmoCountPerCharge ??= reloadable.ammoCountPerCharge;
                reloadableBaseReloadTicks ??= reloadable.baseReloadTicks;
                reloadableReplenishAfterCooldown ??= reloadable.replenishAfterCooldown;
                reloadableMaxCharges ??= reloadable.maxCharges;
            }
        }
        // 品质影响是**派生值**（def 上有没有 CompQuality），未修改时一律按 def 重算，不信存档里的旧值。
        // 为什么不能只靠 GenValues 的"没有值才填"：那个值一旦是错的就会被永久固化。
        // 实测路径：共享投射物的弹丸行（LoadingOrd 100）先于武器行（300）应用，它触发的
        // RefreshSharedWeapons 会调到尚未 SetDef、def 还是 null 的武器行 —— 那里 GetIsCompQuality
        // 返回 null 被兜成 false，此后 SetDef 见到"已有值"就不再重新推断。
        // 症状：UI 的"品质影响"显示关闭，而 def 从未被写（三态仍是 Unset），游戏里品质其实还在。
        if (!IsFieldModified(nameof(isCompQuality)))
            isCompQuality = (bool?)GetIsCompQuality(this) ?? false;

        GenValues(includeEquipPower: false);
    }

    private bool _displayValuesEnsured;
    public override void EnsureDisplayValues()
    {
        if (_displayValuesEnsured) return;
        _displayValuesEnsured = true;
        normalMarketValue ??= (float?)GetNormalMarketValue(this);
        if (def is ThingDef td)
        {
            var stuff = GetDefaultStuffFromDef(td);
            equipPower ??= (float?)GetEquipPower(this, HumanTool!, isCompQuality ?? false, stuff);
        }
    }

    public override void Apply()
    {
        if (this.def is not ThingDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyDefStats(def);
        ApplyWeaponProp(def);
        GenValues();
    }

    static float? RDPS = null;
    static float? HumanDPS = null;

    /// <summary>强度刻度的参照物：原版电荷步枪。</summary>
    private static readonly TweakID ChargeRifleId = new("Gun_ChargeRifle", SettingType.Weapon);

    /// <summary>电荷步枪数据缺席时的兜底强度（19.52134 × log10(30)，与旧实现同一个常量）。</summary>
    private static readonly float FallbackChargePower =
        Mathf.Sqrt(20.33f * 21.18f) * Mathf.Pow(1f + 0.18f * 1.5f, 2f) * Mathf.Log10(30f);

    /// <summary>
    /// 远程强度的<b>唯一取数口</b>：分子（被测武器）与基准（电荷步枪）都走它。
    /// <para>
    /// 强度是「以电荷步枪 = 100」的相对刻度，两边必须<b>同源同时刻</b>：DPS 取数据层字段，
    /// AP 取数据层的镜像值，射程同理。
    /// </para>
    /// <para>
    /// 旧实现两边不同源 —— 分子用 <c>LRangedDPS</c> + 现读 def 的 AP，基准却用
    /// <c>CalculateRangeDPS(电荷步枪的 def)</c> + 现读 def 的 AP，而且只算一次（<c>CRPower</c> 静态缓存）。
    /// 共享投射物（原版 <c>Bullet_ChargeRifle</c> 被多把枪共用）在 apply 期间被别的武器写进 def 之后，
    /// 基准停在旧值、分子已经是新值：未修改的电荷步枪于是不再等于 100，偏差量还取决于
    /// 「同时改了哪些共用该弹的武器」—— 这就是强度随 mod 环境漂移的来源。
    /// </para>
    /// </summary>
    private static float? RangedPower(WeaponData wd, bool legendary)
    {
        if (!wd.LRangedDPS.HasValue) return null;

        // AP 走数据层镜像（共享弹丸的当前值）。-1 表示弹丸没写 AP，回落到 def 的
        // GetArmorPenetration，保证与基准同一口径（那边也是 GetArmorPenetration）。
        float ap = wd.armorPenetrationBase is float mirror && mirror != -1f
            ? mirror
            : GetArmorPenetrationAbstract(wd);
        if (ap == -1f) ap = 0f;
        if (legendary) ap *= APLMultiplier ?? 1f;
        if (TweakDatabase.VCTAPDown && ap > 0.5f) ap = 0.5f + Mathf.Log(ap + 0.5f, 5f);

        float explosionFactor = 1f;
        if (GetLiveData(wd.Projectile) is ProjectileData pd && pd.explosionRadius > 0f)
            explosionFactor = 1f + Mathf.Log(1f + pd.explosionRadius.Value);

        float power = Mathf.Sqrt(wd.LRangedDPS.Value * wd.LMAXRangedDPS.GetValueOrDefault(wd.LRangedDPS.Value))
            * Mathf.Pow(1f + ap, 2f) * explosionFactor;
        if (wd.maxRange > 0f) power *= Mathf.Log10(wd.maxRange.Value);
        return power;
    }

    private static object? GetEquipPower(TweakData data, List<Tool> humantool, bool? isQ, ThingDef? stuff)
    {
        isQ = (isQ ?? false);
        if (data is WeaponData wd && wd.def is ThingDef def)
        {
            if (!RDPS.HasValue || !HumanDPS.HasValue)
            {
                var reference = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
                if (MayRequire("ludeon.rimworld.royalty"))
                {
                    reference = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_MonoSword");
                }
                var rtool = reference.tools;
                RDPS = CalculateExpectedDPS(humantool, rtool, meleeLQualityMult ?? 1.65f, 1f, true);
                HumanDPS = CalculateExpectedDPS(humantool, null, 1f, 1f, true);
            }
            var mdps = CalculateExpectedDPS(def.tools, humantool, isQ.Value ? ((wd.meleeDamageMultiplier ?? 1f) * meleeLQualityMult ?? 1.65f) : (wd.meleeDamageMultiplier ?? 1f), 1f, true, stuff);

            var meleePower = Mathf.Max(0f, (mdps - HumanDPS.Value)
                / (RDPS.Value - HumanDPS.Value)
                * 100f);
            if (wd.propType == (int)WeaponType.MeleeWeapon) //近战则不计算远程
            {
                return meleePower;
            }

            var rangePower = 0f;
            if (RangedPower(wd, isQ.Value) is float selfPower)
            {
                // 基准取「当前生效」的电荷步枪数据（与 wd 同一层），且**不缓存**：
                // 缓存会让刻度停在"第一次算的那一版"，那是强度漂移的另一半原因。
                float baseline = GetLiveData(ChargeRifleId) is WeaponData refWd
                    ? RangedPower(refWd, true) ?? FallbackChargePower
                    : FallbackChargePower;
                rangePower = Mathf.Max(0f, selfPower / baseline * 100f);
            }
            return Mathf.Max(meleePower, rangePower);

        }
        return 0f;
    }

    private static float GetdamageAmountBaseAbstract(TweakData data)
    {
        if (data is WeaponData wd && GetData(wd.Projectile) is ProjectileData pd
            && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetDamageAmount(null, null, null);
        }
        return -1f;
    }
    private static float GetArmorPenetrationAbstract(TweakData data)
    {
        if (data is WeaponData wd && GetData(wd.Projectile) is ProjectileData pd
            && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetArmorPenetration(null, null);
        }
        return -1f;
    }

    public static bool AvailableIfMelee(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => true,
        _ => false,
    };
    public static bool AvailableIfRanged(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => false,
        _ => true,
    };
    public static bool AvailableIfTurret(TweakData data) => data.propType switch
    {
        (int)WeaponType.TurretWeapon => true,
        _ => false,
    };

    public static bool AvailableIfNonBuildin(TweakData data) => data.propType switch
    {
        (int)WeaponType.TurretWeapon => false,
        (int)WeaponType.BuildinWeapon => false,
        _ => true,
    };
    public static bool AvailableIfNonMelee(TweakData data) => data.propType switch
    {
        (int)WeaponType.MeleeWeapon => false,
        _ => true,
    };

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(WeaponType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum WeaponType
    {
        MeleeWeapon = 0,
        RangedWeapon,
        TurretWeapon,
        BuildinWeapon,
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            if (def.IsMeleeWeapon)
            {
                return (int)WeaponType.MeleeWeapon;
            }
            if (TweakDatabase.turretBuildingDatas.TryGetValue(def.defName, out var d) &&
                (def.statBases?.Find(s => s.stat == StatDefOf.WorkToMake)?.value ?? -1) <= 0)
            {
                var l = GetAllData(d);
                if (l.Count > 0) GunTurret = d;
                foreach (var item in l)
                {
                    ((BuildingData)item).TurretGun = id;
                }
                return (int)WeaponType.TurretWeapon;
            }
            if (def.destroyOnDrop == true)
            {
                return (int)WeaponType.BuildinWeapon;
            }
        }
        return (int)WeaponType.RangedWeapon;
    }
}


