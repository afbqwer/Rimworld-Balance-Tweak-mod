using System.Collections.Generic;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;

namespace BalanceTweak.Tests;

/// <summary>
/// 「defName → 数据行」反查索引（<see cref="TweakDatabase.turretBuildingDatas"/> /
/// <see cref="TweakDatabase.projectileDatas"/> / <see cref="TweakDatabase.raceDatas"/>）
/// 必须在建表阶段一次性填好。
///
/// <para>
/// <b>被守着的那条回归</b>：这三个索引原先是在各自的 <c>GetPropType</c> 里顺手写的，
/// 依赖"初始化期每行都会被求值一次分类"。分类改成懒加载后，谁先被读谁才写 ——
/// 于是武器解析弹丸时表是空的（伤害/穿透镜像全空、强度除以 0 变成 NaN / ∞），
/// 判炮塔时表也是空的（炮塔枪回落成"内置武器"）。
/// </para>
/// <para>
/// 这里直接喂 <c>BuildLookupIndices</c>：它只应该看 Def 与数据行的类型，
/// <b>不需要任何人读过 propType</b>。
/// </para>
/// </summary>
public class LookupIndexTests
{
    [Fact]
    public void 反查索引不依赖分类被读取()
    {
        var gun = new ThingDef { defName = "BT_TestTurretGun" };
        var turret = new ThingDef { defName = "BT_TestTurret", building = new BuildingProperties { turretGunDef = gun } };
        var race = new ThingDef { defName = "BT_TestRace" };
        var bullet = new ThingDef { defName = "BT_TestBullet" };

        // 只给 id，不调 SetDef / 不读 propType —— 索引的来源是"有哪些行"，不该牵扯分类的求值时机。
        var bd = new BuildingData { id = new TweakData.TweakID(turret.defName, SettingType.Building) };
        var rd = new RaceData { id = new TweakData.TweakID(race.defName, SettingType.Race) };
        var pd = new ProjectileData { id = new TweakData.TweakID(bullet.defName, SettingType.Projectile) };

        var items = new List<(Def, TweakData, SettingType)>
        {
            (turret, bd, SettingType.Building),
            (race, rd, SettingType.Race),
            (bullet, pd, SettingType.Projectile),
        };

        try
        {
            TweakDatabase.BuildLookupIndices(items);

            Assert.Equal(bd.id, TweakDatabase.turretBuildingDatas[gun.defName]);
            Assert.Equal(rd.id, TweakDatabase.raceDatas[race.defName]);
            Assert.Equal(pd.id, TweakDatabase.projectileDatas[bullet.defName]);
        }
        finally
        {
            // 这三个字典是全局静态的：测试里喂进去的假 defName 不能留给别的用例。
            TweakDatabase.BuildLookupIndices(new List<(Def, TweakData, SettingType)>());
        }
    }
}
