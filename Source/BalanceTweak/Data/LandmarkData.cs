using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


/// <summary>
/// 世界地图地标（<see cref="LandmarkDef"/>，Odyssey）。只收与玩法数值有关的字段：
/// 出现权重 + 两组地块突变器（后者决定这块地长成什么样：动植物、气候、遗迹）。
///
/// <para>
/// <b>写入生效时机</b>：<c>commonality</c> 与两组突变器只在<b>世界生成</b>时读一次
/// （<c>WorldGenStep_Landmarks</c> / <c>WorldLandmarks.AddLandmark</c>），所以改完对<b>已生成</b>的世界没有影响，
/// 只影响之后新开的地图。
/// </para>
/// <para>
/// <b>刻意不开放的字段</b>（都与平衡无关，纯表现 / 实现细节，且各自会带来额外风险）：
/// </para>
/// <list type="bullet">
/// <item><c>iconTexturePath</c> / <c>drawScale</c> / <c>drawType</c> / <c>coastRotateMode</c> /
/// <c>drawAboveRoads</c> / <c>atlasSize</c>：世界地图贴图怎么画。写错路径只是贴图变粉方块，
/// 但 <c>atlasSize</c> 打错会在 <c>WorldDrawLayer_Terrain</c> 里除零 —— 为几个纯视觉参数背这个风险不值得。</item>
/// <item><c>workerClass</c>：地标的 C# 实现类型，改它等于换掉行为，只读展示也没多大意义，干脆不给列。</item>
/// <item><c>nameMaker</c> / <c>category</c>：命名与筛选标签，不影响任何数值（<c>category</c> 仍用于本页签的类型分组，
/// 但那是读 Def、不是改 Def —— 见 <see cref="GetPropType"/>）。</item>
/// <item><c>terrainParameters</c>（<c>List&lt;ShaderParameter&gt;</c>）：字段全 private，值有
/// <c>float</c> / <c>Vector4</c> / <c>Texture2D</c> / <c>Matrix</c> 四种形态且类型靠 XML 文本首字符推断，
/// 没有可无损往返的文本表示。</item>
/// </list>
/// </summary>
[TweakFor(typeof(LandmarkDef), SettingType.Landmark)]
class LandmarkData : TweakData<LandmarkData>
{
    /// <summary>世界生成时抽取该地标的权重（同一步骤里按权重随机）。这是地标出现频率的总开关。</summary>
    [TweakField()]
    public float? commonality = null;

    /// <summary>放置时按几率附加的突变器（决定这块地的动植物、气候、遗迹等）。</summary>
    [TweakField(Style = ColumnStyle.MutatorChanceList)]
    public List<MutatorChance>? mutatorChances = null;

    /// <summary>组合地标突变器：命中一个即停止（<c>landmark.isComboLandmark</c>）。</summary>
    [TweakField(Style = ColumnStyle.MutatorChanceList)]
    public List<MutatorChance>? comboLandmarkMutators = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (def is not LandmarkDef d) return;

        defLabel ??= d.label;
        commonality ??= d.commonality;
        // 列表字段必须带守卫：已清空的字段读档后不能被 Def 的现列表"复活"（L0 Cleared 语义，SC2 自检会报）。
        if (!IsFieldModified(nameof(mutatorChances))) mutatorChances ??= d.mutatorChances;
        if (!IsFieldModified(nameof(comboLandmarkMutators))) comboLandmarkMutators ??= d.comboLandmarkMutators;
    }

    public override void Apply()
    {
        if (this.def is not LandmarkDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<float>(nameof(commonality), v => def.commonality = v);
        ApplyL<MutatorChance>(nameof(mutatorChances), v => def.mutatorChances = SanitizeMutatorChances(v));
        ApplyL<MutatorChance>(nameof(comboLandmarkMutators), v => def.comboLandmarkMutators = SanitizeMutatorChances(v));
    }

    /// <summary>
    /// 写回前清掉没有 <c>mutator</c> 的条目，并逐条换成新实例。
    /// <para>
    /// <b>为什么要清</b>：<see cref="LandmarkDef.IsValidTile"/> 与 <c>WorldLandmarks.AddLandmark</c> 都直接
    /// 解引用 <c>mutatorChance.mutator</c>，留一条空引用等于在世界生成期埋一颗 NRE。
    /// 字段里仍保留用户填的内容（编辑窗口里那行还在，补一个突变器就能救回来）。
    /// </para>
    /// <para>
    /// <b>为什么换新实例</b>：<see cref="SetDef"/> 的 <c>??=</c> 让字段与 Def 的活列表共用元素，
    /// 直接写回去会变成"字段改一次、Def 也跟着变"，违反"字段与 Def 永不共享可变对象"的约定。
    /// </para>
    /// </summary>
    private List<MutatorChance> SanitizeMutatorChances(List<MutatorChance>? list)
    {
        var result = new List<MutatorChance>();
        if (list == null) return result;
        foreach (var mc in list)
        {
            if (mc?.mutator == null)
            {
                Log.Warning($"[BalanceTweak] {id} 的突变器列表里有条目没有指定突变器，已跳过（原版读它时会空引用）。");
                continue;
            }
            result.Add(new MutatorChance { mutator = mc.mutator, chance = mc.chance, required = mc.required });
        }
        return result;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(LandmarkCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    /// <summary>
    /// 按「这块地长在哪」分组。两级判据，都是原版自己写下的数据，不按 defName 硬编码：
    ///
    /// <para>
    /// <b>① <c>LandmarkDef.category</c></b>：原版 45 个地标里只有 33 个标了它（lake / coastal / mountain / structure）。
    /// </para>
    /// <para>
    /// <b>② 必需突变器的类别</b>：剩下 12 个（DryLake / Crevasse / HotSprings / AncientSmokeVent……）的
    /// <c>category</c> 是空的，但它们都挂了 <c>Required="True"</c> 的突变器，而 <see cref="TileMutatorDef.categories"/>
    /// 用的正是同一套口径（Lake / Coast / Mountain / AncientStructure / AncientVent）。少了这一步，这 12 条会全部
    /// 挤进「其它」，而页签默认显示的就是第 0 组 —— 用户会以为这些地标根本不存在。
    /// </para>
    /// <para>
    /// 两级都判不出来的（原版只有 Wetland / Dunes 两个）归「其它」：它们的突变器也没标类别，
    /// 与其猜一个，不如如实放一起。
    /// </para>
    /// </summary>
    public override int GetPropType()
    {
        if (this.def is not LandmarkDef def) return (int)LandmarkCategory.Other;

        switch (def.category)
        {
            case "lake": return (int)LandmarkCategory.Lake;
            case "coastal": return (int)LandmarkCategory.Coastal;
            case "mountain": return (int)LandmarkCategory.Mountain;
            case "structure": return (int)LandmarkCategory.Structure;
        }

        if (def.mutatorChances == null) return (int)LandmarkCategory.Other;
        foreach (var mc in def.mutatorChances)
        {
            // 只认必需突变器：非必需的（0.05 几率那种）与地标类型无因果关系，拿它推断只会让分类随机漂移。
            if (mc?.mutator == null || !mc.required) continue;
            var categories = mc.mutator.categories;
            if (categories == null) continue;
            foreach (var category in categories)
            {
                switch (category)
                {
                    case "Lake": return (int)LandmarkCategory.Lake;
                    case "Coast": return (int)LandmarkCategory.Coastal;
                    case "Mountain": return (int)LandmarkCategory.Mountain;
                    case "AncientStructure":
                    case "AncientVent": return (int)LandmarkCategory.Structure;
                }
            }
        }
        return (int)LandmarkCategory.Other;
    }

    private enum LandmarkCategory
    {
        Lake,
        Coastal,
        Mountain,
        Structure,
        Other,
    }
}
