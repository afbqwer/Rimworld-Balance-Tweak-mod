using System.Reflection;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 子 def（<see cref="ThoughtStage"/> / <see cref="TraitDegreeData"/>）的 <c>LabelCap</c> 缓存清理。
///
/// <para>
/// 这两类不走 <see cref="Def.ClearCachedData"/>：它们的 label 是普通字段，<c>LabelCap</c> 各自在自己的
/// 私有字段里 memo（<c>cachedLabelCap</c> / <c>cachedLabelAbstractCap</c> / <c>cachedLabelMaleCap</c> /
/// <c>cachedLabelFemaleCap</c>），游戏没有提供公开的清除入口，只能反射置 null。
/// </para>
/// <para>
/// 不清的后果与 Def.label 那次是同一个：<c>ThoughtStageData.SetParentTweak</c> 用
/// <c>stage.LabelAbstractCap</c> 组行名与搜索串，<c>TraitDegreeNodeData</c> 用 <c>degreeData.LabelCap</c> 同理 ——
/// 缓存一旦在数据层建表时被填过，之后改 label 界面上仍是旧名。
/// </para>
/// <para>
/// 字段名解析走 <see cref="Reflect.Field"/>：跨版本改名时启动日志会给一条明确的 Error，而不是静默失效。
/// </para>
/// </summary>
internal static class LabelCache
{
    private static readonly FieldInfo? ThoughtLabelCap = Reflect.Field(typeof(ThoughtStage), "cachedLabelCap");
    private static readonly FieldInfo? ThoughtLabelAbstractCap = Reflect.Field(typeof(ThoughtStage), "cachedLabelAbstractCap");
    private static readonly FieldInfo? DegreeLabelCap = Reflect.Field(typeof(TraitDegreeData), "cachedLabelCap");
    private static readonly FieldInfo? DegreeLabelCapMale = Reflect.Field(typeof(TraitDegreeData), "cachedLabelMaleCap");
    private static readonly FieldInfo? DegreeLabelCapFemale = Reflect.Field(typeof(TraitDegreeData), "cachedLabelFemaleCap");

    /// <summary>
    /// 清 ThoughtStage 的两份缓存。<c>labelAbstract</c> 为空时回落到 <c>label</c>，
    /// 所以改 <c>label</c> 必须连带清 <c>cachedLabelAbstractCap</c>，否则 <c>LabelAbstractCap</c> 仍是旧名。
    /// </summary>
    public static void Clear(ThoughtStage stage)
    {
        ThoughtLabelCap?.SetValue(stage, null);
        ThoughtLabelAbstractCap?.SetValue(stage, null);
    }

    /// <summary>
    /// 清 TraitDegreeData 的三份缓存。<c>labelMale</c> / <c>labelFemale</c> 为空时回落到 <c>label</c>，
    /// 故改 <c>label</c> 时三份都要清。
    /// </summary>
    public static void Clear(TraitDegreeData degree)
    {
        DegreeLabelCap?.SetValue(degree, null);
        DegreeLabelCapMale?.SetValue(degree, null);
        DegreeLabelCapFemale?.SetValue(degree, null);
    }
}
