using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// 镜像列（<c>[TweakField(IsMirror = true)]</c>）的相等判定语义。
///
/// <para>
/// 背景：武器行的伤害/穿透是关联弹丸的<b>副本</b>，值属于弹丸那一行。原版 <c>Bullet_Shotgun</c>
/// 同时供泵动霰弹枪与链式霰弹枪 —— 任一把改了弹丸，共用它的每一把枪的副本都会被派生同步刷新。
/// </para>
/// <para>
/// 若副本参与 <see cref="TweakData.EqualsData"/>，"我这一行什么都没改、只是副本跟着源行变了"
/// 就会被判成"不等于原始数据"，于是 <c>ResetSingleItem</c> / <c>ResetSelectedColumn</c> 末尾的
/// <c>RemoveData</c> 再也不触发：脏行常驻已修改表，白占存档条目、每次读档重放一遍。
/// </para>
/// </summary>
[Collection(DefDatabaseProbeCollection.Name)]
public class MirrorColumnTests
{
    /// <summary>探针数据类：一个镜像字段 + 一个自有字段，写法与 <c>WeaponData</c> 的伤害/穿透一致。</summary>
    private class MirrorProbeData : TweakData<MirrorProbeData>
    {
        [TweakField(Style = ColumnStyle.Int, IsMirror = true)]
        public int? mirrorValue = null;

        [TweakField(Style = ColumnStyle.Int)]
        public int? ownValue = null;

        public override void Apply() { }
        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    private static MirrorProbeData New() => new()
    {
        id = new TweakData.TweakID("__BT_MirrorProbe", SettingType.Weapon),
    };

    [Fact]
    public void 标记会传播到列配置()
    {
        var cols = New().AllColumn;
        Assert.True(cols.Single(c => c.fieldName == nameof(MirrorProbeData.mirrorValue)).isMirror);
        Assert.False(cols.Single(c => c.fieldName == nameof(MirrorProbeData.ownValue)).isMirror);
    }

    /// <summary>★核心：共享弹丸被别的武器改了，本行副本跟着变 —— 不算"我改过"。</summary>
    [Fact]
    public void 只有镜像值不同时视为已恢复原样()
    {
        var original = New();
        original.mirrorValue = 15;

        var live = New();
        live.mirrorValue = 20;   // 派生同步刷进来的现值

        Assert.True(live.EqualsData(original), "镜像值属于源行，不该让本行判成「未恢复」");
    }

    /// <summary>镜像上的"已修改"标记同样不参与判定：改动归属已转移到源行。</summary>
    [Fact]
    public void 镜像的修改标记不影响相等判定()
    {
        var original = New();
        original.mirrorValue = 15;

        var live = New();
        live.mirrorValue = 20;
        live.MarkFieldModified(nameof(MirrorProbeData.mirrorValue));

        Assert.True(live.EqualsData(original));
    }

    /// <summary>反向保证：真正属于本行的字段照常参与判定，否则重置会连用户意图一起丢掉。</summary>
    [Fact]
    public void 自有字段不同时不算恢复原样()
    {
        var original = New();
        original.mirrorValue = 15;
        original.ownValue = 7;

        var live = New();
        live.mirrorValue = 20;
        live.ownValue = 9;

        Assert.False(live.EqualsData(original));
    }

    /// <summary>自有字段的"清空"意图也不能被镜像规则顺带抹掉。</summary>
    [Fact]
    public void 自有字段的清空意图仍参与判定()
    {
        var original = New();
        original.ownValue = 7;

        var live = New();
        live.ownValue = 7;
        live.MarkFieldCleared(nameof(MirrorProbeData.ownValue));

        Assert.False(live.EqualsData(original));
    }

    /// <summary>探针：Def 类型的镜像字段（招式行的 <c>damageType</c> 就是这种）。</summary>
    private class DefMirrorProbeData : TweakData<DefMirrorProbeData>
    {
        [TweakField(Style = ColumnStyle.DefSelector, IsMirror = true)]
        public DamageDef? defMirror = null;

        [TweakField(Style = ColumnStyle.Int)]
        public int? ownValue = null;

        public override void Apply() { }
        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    private static DefMirrorProbeData NewDefProbe() => new()
    {
        id = new TweakData.TweakID("__BT_DefMirrorProbe", SettingType.MeleeTool),
    };

    /// <summary>
    /// 镜像判定不看字段类型：Def 引用（招式行的 <c>damageType</c>）同样是"值属于另一行"，
    /// 共用同一招式的行在共享行被改后副本必然不同 —— 参与判定会让它们永远"未恢复原样"。
    /// </summary>
    [Fact]
    public void Def类型的镜像字段同样不参与相等判定()
    {
        var cut = new DamageDef { defName = "BT_TestCut" };
        var stab = new DamageDef { defName = "BT_TestStab" };

        var original = NewDefProbe();
        original.defMirror = cut;

        var live = NewDefProbe();
        live.defMirror = stab;   // 共享行被别的行改了，派生的副本跟着变
        live.MarkFieldModified(nameof(DefMirrorProbeData.defMirror));

        Assert.True(live.EqualsData(original), "Def 镜像列也不该让本行判成「未恢复」");
        Assert.True(live.AllColumn.Single(c => c.fieldName == nameof(DefMirrorProbeData.defMirror)).isMirror);
    }

    /// <summary>真实用例：招式行的伤害类型已转成共享招式类型行的镜像。</summary>
    [Fact]
    public void 招式行的伤害类型是镜像()
    {
        var col = new ToolData().AllColumn.Single(c => c.fieldName == nameof(ToolData.damageType));
        Assert.True(col.isMirror);
    }

    /// <summary>
    /// 招式行能按 <c>tool.capacities[0]</c> 解析到正确的招式类型行，并把伤害类型同步成共享行的值。
    /// <para>
    /// 守着两条：① 索引键是「能力」而不是 maneuver 名；② 镜像用赋值而不是 <c>??=</c>
    /// （后者会让共享行之后的改动永远刷不进来）。
    /// </para>
    /// </summary>
    [Fact]
    public void 招式行解析到对应的招式类型行()
    {
        var capacity = new ToolCapacityDef { defName = "__BT_TestCutCap" };
        var maneuverId = new TweakData.TweakID("__BT_TestSlashManeuver", SettingType.Maneuver);
        var cut = new DamageDef { defName = "BT_TestCutDamage" };
        var tool = new Tool { untranslatedLabel = "Cut", power = 10f, cooldownTime = 2f };
        tool.capacities.Add(capacity);
        var weaponDef = new ThingDef { defName = "__BT_TestManeuverWeapon", tools = new List<Tool> { tool } };
        var parent = new WeaponData
        {
            def = weaponDef,
            id = new TweakData.TweakID(weaponDef.defName, SettingType.Weapon),
        };

        try
        {
            TweakDatabase.maneuverDatas[capacity.defName] = maneuverId;
            TweakDatabase.AllOriginals[maneuverId] = new ManeuverData
            {
                id = maneuverId,
                def = new ManeuverDef { defName = maneuverId.defName, requiredCapacity = capacity },
                meleeDamageDef = cut,
            };

            var td = new ToolData { index = 0 };
            Assert.True(td.SetParentTweak(parent, SettingType.MeleeTool));

            Assert.Equal(maneuverId, td.Maneuver);
            Assert.Same(cut, td.damageType);
        }
        finally
        {
            TweakDatabase.maneuverDatas.Remove(capacity.defName);
            TweakDatabase.AllOriginals.Remove(maneuverId);
        }
    }

    /// <summary>
    /// 旧档迁移：招式行上"已修改"的伤害类型搬到招式类型行，且第二次加载不再搬（幂等）。
    /// <para>
    /// 守着的是"静默失效"：<c>damageType</c> 转镜像后 <c>Apply</c> 不再写 verb、
    /// <c>EqualsData</c> 也不再比较它 —— 不搬的话老存档里那条修改会永远不落盘。
    /// </para>
    /// </summary>
    [Fact]
    public void 旧档的招式伤害类型迁移到招式类型行且幂等()
    {
        var capacity = new ToolCapacityDef { defName = "__BT_MigCap" };
        var maneuverId = new TweakData.TweakID("__BT_MigManeuver", SettingType.Maneuver);
        var cut = new DamageDef { defName = "BT_MigCut" };
        var blunt = new DamageDef { defName = "BT_MigBlunt" };

        var tool = new Tool { power = 8f, cooldownTime = 2f };
        tool.capacities.Add(capacity);
        var weaponDef = new ThingDef { defName = "__BT_MigWeapon", tools = new List<Tool> { tool } };
        var parent = new WeaponData
        {
            def = weaponDef,
            id = new TweakData.TweakID(weaponDef.defName, SettingType.Weapon),
        };
        var toolId = new TweakData.TweakID(weaponDef.defName + "_0", SettingType.MeleeTool);
        // 旧档形态：工具行自己是 Set，招式类型行还不存在。
        var toolRow = new ToolData { id = toolId, index = 0, parentTweakId = parent.id, damageType = cut };
        toolRow.MarkFieldModified(nameof(ToolData.damageType));

        try
        {
            // 迁移按父项的原始快照取 Tool（不绑定实例的 def）。
            TweakDatabase.AllOriginals[parent.id] = parent;
            TweakDatabase.AllOriginals[maneuverId] = new ManeuverData
            {
                id = maneuverId,
                def = new ManeuverDef { defName = maneuverId.defName, requiredCapacity = capacity },
                meleeDamageDef = blunt,
            };
            TweakDatabase.maneuverDatas[capacity.defName] = maneuverId;
            BalanceTweakSettings.tweakDatas[toolId] = toolRow;

            TweakDatabase.MigrateToolDamageTypes();

            Assert.True(BalanceTweakSettings.tweakDatas.TryGetValue(maneuverId, out var migrated));
            Assert.Same(cut, ((ManeuverData)migrated!).meleeDamageDef);
            Assert.False(toolRow.IsFieldModified(nameof(ToolData.damageType)), "迁移后工具行不该再持有这一格");
            Assert.False(BalanceTweakSettings.tweakDatas.ContainsKey(toolId), "工具行只剩这一格修改，迁移后整行不该再占存档条目");

            // 幂等：第二次加载时工具行已不是 Set，什么都不该发生。
            var before = ((ManeuverData)BalanceTweakSettings.tweakDatas[maneuverId]!).meleeDamageDef;
            TweakDatabase.MigrateToolDamageTypes();
            Assert.Same(before, ((ManeuverData)BalanceTweakSettings.tweakDatas[maneuverId]!).meleeDamageDef);
        }
        finally
        {
            BalanceTweakSettings.tweakDatas.Remove(toolId);
            BalanceTweakSettings.tweakDatas.Remove(maneuverId);
            BalanceTweakSettings.cachedData[SettingType.Maneuver].Remove(maneuverId);
            TweakDatabase.AllOriginals.Remove(maneuverId);
            TweakDatabase.AllOriginals.Remove(parent.id);
            TweakDatabase.maneuverDatas.Remove(capacity.defName);
        }
    }
}
