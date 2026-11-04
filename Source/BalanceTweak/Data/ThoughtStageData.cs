using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.ThoughtData;

namespace BalanceTweak;


class ThoughtStageData : TweakData<ThoughtStageData>
{
    // 字段定义
    [TweakField(DataType = ColumnDataType.Field, Style = ColumnStyle.String, Available = nameof(NotAvailable))]
    public string? stageLabel = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? Thought = null;

    [TweakField()]
    public float? baseMoodEffect = null;

    [TweakField()]
    public float? baseOpinionOffset = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? visible = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? subdefLabel = null;

    public override int LoadingOrd => 300;
    private ThoughtStage? stage = null;

    public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false)
    {
        base.SetParentTweak(data, type, tweaked);
        if (data.def is ThoughtDef def && index.HasValue)
        {

            stage = def.stages[index.Value];
            if (stage == null) { return false; }
            if (!stageLabel.NullOrEmpty() && stage.untranslatedLabel != stageLabel)
            {
                Log.Warning($"[BalanceTweak]{def} tool的名称错误：{stageLabel}");
                return false;
            }
            stageLabel = stage.untranslatedLabel;
            subdefLabel ??= stage.label;
            id = new($"{def.defName}_{index}", type);
            var li = GetAllData(data.id);
            foreach (var item in li)
            {
                ((ThoughtData)item).FirstStage ??= this.id;
            }
            Thought = data.id;
            baseMoodEffect ??= stage.baseMoodEffect;
            baseOpinionOffset ??= stage.baseOpinionOffset;
            visible ??= stage.visible;
        }
        return true;
    }

    /// <summary>阶段的抽象标签（<c>LabelAbstractCap</c>）就是原版 UI 显示的那行字，空则回落 defName。</summary>
    protected override string ResolveLabel()
    {
        if (stage == null) return base.ResolveLabel();
        var l = stage.LabelAbstractCap;
        return l.NullOrEmpty() ? (def?.defName ?? string.Empty) : l;
    }

    protected override string ResolveSearchString()
        => $"{def?.defName}{index}{label}";

    /// <summary>阶段描述优先，没有就借父级（想法本身）的 desc —— 与原先 SetParentTweak 里的赋值等价。</summary>
    protected override string ResolveDesc()
        => stage?.description ?? GetData(parentTweakId)?.desc ?? base.ResolveDesc();

    /// <summary>阶段没有自己的图标，直接借父级那一行的。</summary>
    protected override Texture2D? ResolveUiIcon()
        => GetData(parentTweakId)?.uiIcon ?? base.ResolveUiIcon();

    protected override Color ResolveUiIconColor()
        => GetData(parentTweakId)?.uiIconColor ?? base.ResolveUiIconColor();

    public override void Apply()
    {
        if (this.stage is not ThoughtStage stage)
        {
            Log.Error($"[BalanceTweak]{this}的stage为{this.stage}!");
            return;
        }
        // 阶段 label 的 memo 在 ThoughtStage 自己身上（无公开清除入口），且 ThoughtDef.LabelCap 在
        // def.label 为空时直接用 stages[0].label —— 两处缓存都要清，否则改名在界面上不生效。
        ApplyS(nameof(subdefLabel), v =>
        {
            stage.label = v;
            LabelCache.Clear(stage);
            this.def?.ClearCachedData();
        });
        if (stage != null)
        {
            ApplyV<float>(nameof(baseMoodEffect), v => stage.baseMoodEffect = v);
            ApplyV<float>(nameof(baseOpinionOffset), v => stage.baseOpinionOffset = v);
            ApplyV<bool>(nameof(visible), v => stage.visible = v);
        }
    }

    public static bool NotAvailable(TweakData data) => data.propType switch
    {
        _ => false,
    };
    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ThoughtType).GetEnumNames().Select(s => "MST." + s).ToList();


    public override int GetPropType()
    {
        // 与原先 `propType = data.propType` 等价：阶段跟随父级（想法本身）的分类。
        if (parentTweakId.HasValue && GetData(parentTweakId.Value) is { } parent) return parent.propType;
        return (int)ThoughtType.Situational;
    }

}


