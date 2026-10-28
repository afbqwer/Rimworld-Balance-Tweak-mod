using System.Collections.Generic;
using System.Linq;
using BalanceTweak;
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
}
