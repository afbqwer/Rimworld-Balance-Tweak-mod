using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(ThoughtDef), SettingType.Thought)]
class ThoughtData : TweakData<ThoughtData>, ISubItemHost
{
    /// <summary>想法阶段子项（原 Init() 里的硬编码块）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not ThoughtDef d || d.stages.NullOrEmpty()) yield break;
        for (int i = 0; i < d.stages.Count; i++)
        {
            if (d.stages[i] == null) continue;
            var sd = new ThoughtStageData { index = i };
            sd.SetParentTweak(parent, SettingType.ThoughtStage, tweaked);
            yield return sd;
        }
    }

    // 字段定义
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? FirstStage = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int)]
    public int? StageNum = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? stackLimit = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? stackedEffectMultiplier = null;
    [TweakField()]
    public float? durationDays = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? stackLimitForSameOtherPawn = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? lerpMoodToZero = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is ThoughtDef d)
        {
            var fs = d.stages?.FirstOrDefault();
            label = d.label ?? fs?.LabelAbstractCap ?? d.defName;
            defLabel ??= def?.label;
            desc = d.description ?? fs?.description ?? "";
            if (d.Icon != null && d.Icon != BaseContent.BadTex)
            {
                uiIcon = d.Icon;
            }
            searchString = label + d.defName;
            StageNum ??= d.stages?.Count ?? 0;
            stackLimit ??= d.stackLimit;
            stackedEffectMultiplier ??= d.stackedEffectMultiplier;
            durationDays ??= d.durationDays;
            stackLimitForSameOtherPawn ??= d.stackLimitForSameOtherPawn;
            lerpMoodToZero ??= d.lerpMoodToZero;
        }
    }

    public override void Apply()
    {
        if (this.def is not ThoughtDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (stackLimit.HasValue) { def.stackLimit = stackLimit.Value; }
        if (stackedEffectMultiplier.HasValue) { def.stackedEffectMultiplier = stackedEffectMultiplier.Value; }
        if (durationDays.HasValue) { def.durationDays = durationDays.Value; }
        if (stackLimitForSameOtherPawn.HasValue) { def.stackLimitForSameOtherPawn = stackLimitForSameOtherPawn.Value; }
        if (lerpMoodToZero.HasValue) { def.lerpMoodToZero = lerpMoodToZero.Value; }
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ThoughtType).GetEnumNames().Select(s => "MST." + s).ToList();

    public enum ThoughtType
    {
        Situational,
        Memory,
        Social
    }

    public override int GetPropType()
    {
        if (this.def is ThoughtDef def)
        {
            if (def.IsSocial)
            {
                return (int)ThoughtType.Social;
            }
            if (def.IsMemory)
            {
                return (int)ThoughtType.Memory;
            }
        }
        return (int)ThoughtType.Situational;
    }
}


