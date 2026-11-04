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
/// <b>第二条回归</b>：索引里的 <c>TweakID</c> 必须在这里现拼。`data.id` 是在随后的
/// <c>SetDef</c> 里才赋值的，此刻还是 `default`（defName 为空、settingType 为 `None`）——
/// 用它建表会把所有条目塌缩到同一个 `":None"` 键上：武器解析不到弹丸（DPS 全 0），
/// 炮塔枪拿到空链接（点一下就 `KeyNotFoundException`）。
/// </para>
/// </summary>
public class LookupIndexTests
{
    [Fact]
    public void 反查索引不依赖分类被读取且用def现拼id()
    {
        var gun = new ThingDef { defName = "BT_TestTurretGun" };
        var turret = new ThingDef { defName = "BT_TestTurret", building = new BuildingProperties { turretGunDef = gun } };
        var race = new ThingDef { defName = "BT_TestRace" };
        var bullet = new ThingDef { defName = "BT_TestBullet" };

        // 刻意不调 SetDef、不给 id：这与 Init 里的真实时序一致（索引先于建快照）。
        var bd = new BuildingData();
        var rd = new RaceData();
        var pd = new ProjectileData();
        Assert.Equal(default, bd.id);   // 前提断言：此刻 id 确实是空的

        var items = new List<(Def, TweakData, SettingType)>
        {
            (turret, bd, SettingType.Building),
            (race, rd, SettingType.Race),
            (bullet, pd, SettingType.Projectile),
        };

        try
        {
            TweakDatabase.BuildLookupIndices(items);

            Assert.Equal(new TweakData.TweakID(turret.defName, SettingType.Building),
                TweakDatabase.turretBuildingDatas[gun.defName]);
            Assert.Equal(new TweakData.TweakID(race.defName, SettingType.Race),
                TweakDatabase.raceDatas[race.defName]);
            Assert.Equal(new TweakData.TweakID(bullet.defName, SettingType.Projectile),
                TweakDatabase.projectileDatas[bullet.defName]);
        }
        finally
        {
            // 这三个字典是全局静态的：测试里喂进去的假 defName 不能留给别的用例。
            TweakDatabase.BuildLookupIndices(new List<(Def, TweakData, SettingType)>());
        }
    }
}
