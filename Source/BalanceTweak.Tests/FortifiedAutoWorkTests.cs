using System.Collections.Generic;
using RimWorld;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <see cref="FortifiedAutoWork"/> 的技能列表转换。
/// 扩展上存的是 <c>Dictionary&lt;SkillDef,int&gt;</c>，界面用 <see cref="SkillGain"/> 列表；
/// 空列表必须得到空字典（= 清空），不能留下半截数据。
/// </summary>
public class FortifiedAutoWorkTests
{
    [Fact]
    public void ToSkillMap_跳过空技能且同一技能后者覆盖前者()
    {
        var crafting = new SkillDef { defName = "Crafting" };
        var shooting = new SkillDef { defName = "Shooting" };

        var map = FortifiedAutoWork.ToSkillMap(new List<SkillGain>
        {
            new() { skill = crafting, amount = 3 },
            new() { skill = shooting, amount = 5 },
            new() { skill = crafting, amount = 7 },
            new() { skill = null, amount = 9 },
        });

        Assert.Equal(2, map.Count);
        Assert.Equal(7, map[crafting]);
        Assert.Equal(5, map[shooting]);
    }

    [Fact]
    public void ToSkillMap_空输入得到空字典()
    {
        Assert.Empty(FortifiedAutoWork.ToSkillMap(null));
        Assert.Empty(FortifiedAutoWork.ToSkillMap(new List<SkillGain>()));
    }
}
