using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(MemeDef), SettingType.Meme)]
class MemeData : TweakData<MemeData>
{
    //// 字段定义
    [TweakField(Style = ColumnStyle.Int)]
    public int? impact = null;
    [TweakField()]
    public float? selectionWeightFactor = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    //public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is MemeDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description ?? "";
            uiIcon = d.Icon;
            searchString = label + d.defName;
            selectionWeightFactor ??= d.randomizationSelectionWeightFactor;
            impact ??= d.impact;
        }
    }

    public override void Apply()
    {
        if (this.def is not MemeDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<float>(nameof(selectionWeightFactor), v => def.randomizationSelectionWeightFactor = v);
        ApplyV<int>(nameof(impact), v => def.impact = v);
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(MemeType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum MemeType
    {
        Normal,
        Structure
    }

    public override int GetPropType()
    {
        if (this.def is MemeDef def)
        {
            if (def.category == MemeCategory.Structure)
            {
                return (int)MemeType.Structure;
            }
        }
        return (int)MemeType.Normal;
    }
}


