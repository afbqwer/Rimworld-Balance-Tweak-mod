using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;


[TweakFor(typeof(ThingDef), SettingType.Projectile, Priority = 400, MatchMethod = nameof(DataUtility.MatchProjectile))]
class ProjectileData : TweakData<ProjectileData>
{
    // 字段定义
    /// <summary>
    /// 关联列：点击打开"使用此投射物的武器"列表窗口。
    /// <para>
    /// 字段本身<b>不保存也不读取任何值</b>，窗口打开时由 <see cref="FindWeaponLinks"/> 实时反查 ——
    /// 与 <c>BodyData.raceLinks</c> 同一种做法。原来的单向 <c>Link</c> 只能挂一把武器，
    /// 而弹丸是多对一（原版 <c>Bullet_Shotgun</c> 同时供泵动与链式霰弹枪），
    /// 单一槽位既表达不全、又会在多把武器轮流 GenValues 时被最后一把覆盖。
    /// </para>
    /// <para>字段名保持 <c>RangedWeapon</c> 不变：改名会让旧档里这个 XML 节点失配（Readme 硬约束）。</para>
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.WeaponLinks)]
    public TweakID? RangedWeapon = null;
    [TweakField(Style = ColumnStyle.Int, GetAbstract = nameof(GetdamageAmountBaseAbstract))]
    public int? damageAmountBase = null;// -1
    [TweakField(GetAbstract = nameof(GetArmorPenetrationAbstract))]
    public float? armorPenetrationBase = null;// -1
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? damageType = null;
    [TweakField()]
    public float? stoppingPower = null;
    [TweakField(Available = nameof(AvailableIfExplosion))]
    public float? explosionRadius = null;

    [TweakField()]
    public float? projectileSpeed = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statBases = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    //[TweakField( DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    //public VerbData? ProjectileVerb = null;

    /// <summary>
    /// 共用同一弹丸的武器（运行期索引，不序列化）。
    /// <para>
    /// 反查列 <see cref="FindWeaponLinks"/> 是"看"，这里解决"改"：弹丸的伤害/穿透一改，
    /// 所有共用它的武器（原版 <c>Bullet_Shotgun</c> 供泵动霰弹枪与链式霰弹枪，
    /// <c>Bullet_ChargeBlasterHeavy</c> 供三把电荷爆裂枪）的镜像与 DPS 都得重算 ——
    /// 只刷一把等于放任其余显示与实际脱节。
    /// </para>
    /// <para>
    /// 按弹丸 defName 归组，因此克隆 / 原始 / 已存三层实例天然共享同一份登记，不受克隆层数影响。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, List<TweakID>> sharedWeapons = new();

    /// <summary>登记一把引用本弹丸的武器（幂等）。由 <see cref="WeaponData"/> 解析出弹丸时调用。</summary>
    public void RegisterSharedWeapon(TweakID weaponId)
    {
        if (string.IsNullOrEmpty(id.defName)) return;
        if (!sharedWeapons.TryGetValue(id.defName, out var list))
        {
            list = new List<TweakID>();
            sharedWeapons.Add(id.defName, list);
        }
        if (!list.Contains(weaponId)) list.Add(weaponId);
    }

    /// <summary>
    /// 刷新全部共用本弹丸的武器（镜像伤害/穿透 + DPS + 强度）。
    /// <para>取数走 <see cref="TweakData.GetLiveData"/>：初始化期 UI 缓存还是空的，<c>GetData</c> 只会返回原始快照。</para>
    /// </summary>
    public void RefreshSharedWeapons()
    {
        if (string.IsNullOrEmpty(id.defName)) return;
        if (!sharedWeapons.TryGetValue(id.defName, out var list)) return;
        // 先取长度：武器在 GenValues 里可能顺带解析并登记新的共享者（Projectile 为 null 的行），
        // 边遍历边扩容不安全，本次只刷已有的，新登记的下一轮自然跟上。
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            if (GetLiveData(list[i]) is WeaponData wd) wd.GenValues();
        }
    }

    /// <summary>
    /// 找出所有使用该弹丸的武器数据链接（供 <see cref="TweakLinksWindow"/> 使用）。
    /// <para>
    /// 实时反查，不依赖 <see cref="sharedWeapons"/> 索引 —— 那份索引只收"初始化时解析过"的武器，
    /// 窗口要的是全量真相（包括当前存档里没编辑过的）。
    /// 只在窗口打开时调用一次，故不做缓存。
    /// </para>
    /// </summary>
    public static List<TweakID> FindWeaponLinks(ThingDef projectile)
    {
        var result = new List<TweakID>();
        if (projectile == null) return result;
        foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!thing.IsWeapon || thing.Verbs.NullOrEmpty()) continue;
            bool uses = false;
            foreach (var verb in thing.Verbs)
            {
                if (verb == null || verb.IsMeleeAttack) continue;
                if (verb.defaultProjectile != projectile) continue;
                uses = true;
                break;
            }
            if (!uses) continue;

            var id = new TweakID(thing.defName, SettingType.Weapon);
            // 只列真的在武器页签里有数据行的 —— 否则点了跳不过去，等于给个死链接。
            if (TweakDatabase.GetOriginal(id) == null) continue;
            result.Add(id);
        }
        return result;
    }

    public static float GetdamageAmountBaseAbstract(TweakData? data)
    {
        if (data is ProjectileData pd && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetDamageAmount(null, null, null);
        }
        return -1f;
    }
    private static float GetArmorPenetrationAbstract(TweakData? data)
    {
        if (data is ProjectileData pd && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            if (p.damageDef == null) { return -1f; }
            return p.GetArmorPenetration(null, null);
        }
        return -1f;
    }
    public static bool AvailableIfExplosion(TweakData data) => data.propType switch
    {
        (int)ProjectileType.ExplosionProjectile => true,
        _ => false,
    };

    static FieldInfo damageAmountBaseField = Reflect.Field(typeof(ProjectileProperties), "damageAmountBase");
    static FieldInfo armorPenetrationBaseField = Reflect.Field(typeof(ProjectileProperties), "armorPenetrationBase");

    //private static void OnChangeDamage(TweakData data)
    //{
    //    if (data is ProjectileData pd && pd.def is ThingDef d && d.projectile is ProjectileProperties p)
    //    {
    //        if (p.damageDef == null) { return; }
    //        pd.normaldamageAmountBase = p.GetDamageAmount(null, null, null);
    //        pd.normalarmorPenetrationBase = p.GetArmorPenetration(null, null);
    //    }
    //}
    public override void Apply()
    {
        if (this.def != null) ApplyS(nameof(defLabel), v => this.def.label = v);
        SetProjectileProp(def);
        if (def is ThingDef d)
        {
            ApplyDefStats(d);
        }

    }

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (def is ThingDef d)
        {
            if (!IsFieldModified(nameof(statBases))) statBases ??= d.statBases;
        }
        // 基线回填<b>不分 tweaked</b>。
        // 存档里只写"用户改过的字段"，其余字段连 XML 节点都没有 —— 读档走 tweaked=true 时若跳过回填，
        // 那条数据的其它字段就永远是 null。踩过的坑：弹丸条目由武器侧推送创建（只带 armorPenetrationBase），
        // 读档后 damageAmountBase 是 null → 武器行的镜像跟着变 null（显示 Null、DPS/批量/复制全拿不到值）。
        GetProjectileProp(def);
    }

    private void GetProjectileProp(Def? def)
    {
        if (def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            // 只填"用户没动过"的字段：Set 的不能被 def 覆盖，Cleared 的必须保持空
            // （Readme 硬约束：回填点不带 IsFieldModified 守卫，清空意图重启即失效）。
            if (!IsFieldModified(nameof(projectileSpeed))) projectileSpeed ??= p.speed;
            if (!IsFieldModified(nameof(stoppingPower))) stoppingPower ??= p.stoppingPower;
            if (!IsFieldModified(nameof(explosionRadius))) explosionRadius ??= p.explosionRadius;
            if (!IsFieldModified(nameof(damageAmountBase))) damageAmountBase ??= damageAmountBaseField.GetValue(p) as int?;
            if (!IsFieldModified(nameof(armorPenetrationBase))) armorPenetrationBase ??= armorPenetrationBaseField.GetValue(p) as float?;
            // damageType 原来是裸赋值：读档时会拿 def 的值盖掉刚 Resolve 出来的用户改动。
            if (!IsFieldModified(nameof(damageType))) damageType = p.damageDef;
        }
        else
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
    }

    /// <param name="refreshWeapons">
    /// 写完是否刷新共用本弹丸的所有武器。武器行只在"真的动了这两格镜像"时才需要刷，
    /// 否则每改一次射程/冷却都会把整组枪的强度重算一遍（GetEquipPower 不便宜）。
    /// </param>
    public void SetProjectileProp(Def? def, bool refreshWeapons = true)
    {
        if (def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            ApplyV<float>(nameof(projectileSpeed), v => p.speed = v);
            ApplyV<float>(nameof(stoppingPower), v => p.stoppingPower = v);
            ApplyV<float>(nameof(explosionRadius), v => p.explosionRadius = v);
            ApplyV<int>(nameof(damageAmountBase), v => damageAmountBaseField.SetValue(p, v));
            ApplyV<float>(nameof(armorPenetrationBase), v => armorPenetrationBaseField.SetValue(p, v));
            ApplyD<DamageDef>(nameof(damageType), v => p.damageDef = v);

            // Cleared（含"重置此格/此列"）写的是"还原原值"，落在 def 上；字段里留的还是旧值。
            // 武器行的镜像是从字段同步的，不回读一次的话，重置完武器行会立刻被刷回改后的值。
            if (StateOf(nameof(damageAmountBase)) == FieldState.Cleared)
                damageAmountBase = damageAmountBaseField.GetValue(p) as int?;
            if (StateOf(nameof(armorPenetrationBase)) == FieldState.Cleared)
                armorPenetrationBase = armorPenetrationBaseField.GetValue(p) as float?;

            if (refreshWeapons) RefreshSharedWeapons();
        }
        else
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ProjectileType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum ProjectileType
    {
        BulletProjectile,
        ExplosionProjectile,
        BeamProjectile,
        Projectile,
        SpecialProjectile,
    }

    public override int GetPropType()
    {
        if (this.def is ThingDef def)
        {
            TweakDatabase.projectileDatas.SetOrAdd(def.defName, id);
            if (def.thingClass == typeof(Beam))
            {
                return (int)ProjectileType.BeamProjectile;
            }
            if (def.projectile.damageDef == DamageDefOf.Bullet)
            {
                return (int)ProjectileType.BulletProjectile;
            }
            if (def.projectile != null && def.projectile.explosionRadius > 0)
            {
                return (int)ProjectileType.ExplosionProjectile;
            }
            if (def.projectile == null || (def.projectile != null && def.projectile.damageDef == null))
            {
                return (int)ProjectileType.SpecialProjectile;
            }
        }
        return (int)ProjectileType.Projectile;
    }





}


