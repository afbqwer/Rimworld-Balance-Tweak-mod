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
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
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
        if (tweaked == false)
        {
            GetProjectileProp(def);
        }
    }

    private void GetProjectileProp(Def? def)
    {
        if (def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            projectileSpeed ??= p.speed;
            stoppingPower ??= p.stoppingPower;
            explosionRadius ??= p.explosionRadius;
            //OnChangeDamage(this);
            damageAmountBase ??= damageAmountBaseField.GetValue(p) as int?;
            armorPenetrationBase ??= armorPenetrationBaseField.GetValue(p) as float?;
            damageType = p.damageDef;
        }
        else
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
    }

    public void SetProjectileProp(Def? def)
    {
        if (def is ThingDef d && d.projectile is ProjectileProperties p)
        {
            ApplyV<float>(nameof(projectileSpeed), v => p.speed = v);
            ApplyV<float>(nameof(stoppingPower), v => p.stoppingPower = v);
            ApplyV<float>(nameof(explosionRadius), v => p.explosionRadius = v);
            ApplyV<int>(nameof(damageAmountBase), v => damageAmountBaseField.SetValue(p, v));
            ApplyV<float>(nameof(armorPenetrationBase), v => armorPenetrationBaseField.SetValue(p, v));
            ApplyD<DamageDef>(nameof(damageType), v => p.damageDef = v);
            if (RangedWeapon != null)
            {
                var w = (WeaponData?)GetData(RangedWeapon);
                w?.GenValues();
            }
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


