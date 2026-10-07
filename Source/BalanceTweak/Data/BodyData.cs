using System.Collections.Generic;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// 整套身体（<see cref="BodyDef"/>）。一个 BodyDef 一条数据；
/// 其部位构成一棵树，通过 <see cref="ColumnStyle.BodyPartTree"/> 树形编辑窗口编辑。
/// </summary>
[TweakFor(typeof(BodyDef), SettingType.BodyDef)]
class BodyData : TweakData<BodyData>
{
    /// <summary>
    /// 整棵部位树的编码（前缀序遍历，见 <see cref="BodyPartTreeCodec"/>）。
    /// 以 <c>List&lt;string&gt;</c> 承载，直接复用现有的列表序列化（ExposeStringList/ResolveStringList）。
    /// </summary>
    [TweakField(Style = ColumnStyle.BodyPartTree)]
    public List<string>? parts = null;

    /// <summary>
    /// 关联列：提供“点击打开使用该身体的种族链接列表窗口”的入口，
    /// 字段本身不保存值，窗口打开时由 <see cref="FindRaceLinks"/> 实时计算。
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.RaceLinks)]
    public TweakID? raceLinks = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is BodyDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            desc = d.description;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            parts ??= BodyPartTreeCodec.Encode(d.corePart);
        }
    }

    public override void Apply()
    {
        if (this.def is not BodyDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (parts != null) { BodyPartTreeCodec.SetCorePart(def, parts); }
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = new() { "MST.BodyDef" };

    public override int GetPropType() => 0;

    /// <summary>
    /// 找出所有使用该身体的种族数据链接（供 TweakLinksWindow 使用）。
    /// 仅在窗口打开时调用一次，故不做缓存。
    /// </summary>
    public static List<TweakID> FindRaceLinks(BodyDef body)
    {
        var result = new List<TweakID>();
        foreach (var raceId in TweakDatabase.raceDatas.Values)
        {
            var race = DefDatabase<ThingDef>.GetNamed(raceId.defName, false);
            if (race?.race?.body == body && !result.Contains(raceId))
                result.Add(raceId);
        }
        return result;
    }
}