using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(FactionDef), SettingType.Faction)]
class FactionData : TweakData<FactionData>
{
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? raidCommonalityFromPointsCurve = null;

    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? maxPawnCostPerTotalPointsCurve = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(TechLevel))]
    public TechLevel? techLevel = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? settlementGenerationWeight = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? naturalEnemy = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? permanentEnemy = null;

    /// <summary>
    /// 兵种池：袭击编成的**结构**（哪个 <see cref="PawnGroupKindDef"/>、以多大 commonality 出、
    /// 每类兵种的出现权重、点数上限、禁用的袭击策略）。字段值是与 Def 不共享实例的副本
    /// （<see cref="PawnGroupMakerCodec.CloneAll"/>），取消编辑不会污染 Def。
    /// </summary>
    [TweakField(Style = ColumnStyle.PawnGroupMakerList)]
    public List<PawnGroupMaker>? pawnGroupMakers = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is FactionDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description;
            // FactionIcon 首次访问会 ContentFinder 加载纹理，延迟到真正显示时再取。
            uiIconColor = d.DefaultColor;
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            raidCommonalityFromPointsCurve ??= d.raidCommonalityFromPointsCurve;
            maxPawnCostPerTotalPointsCurve ??= d.maxPawnCostPerTotalPointsCurve;
            techLevel ??= d.techLevel;
            settlementGenerationWeight ??= d.settlementGenerationWeight;
            naturalEnemy ??= d.naturalEnemy;
            permanentEnemy ??= d.permanentEnemy;
            // 列表字段必须带守卫：否则用户清空后读档会被 Def 回填"复活"（且这里是深克隆，
            // 裸 ??= 还会让已编辑的副本被 Def 覆盖）。
            if (!IsFieldModified(nameof(pawnGroupMakers)))
                pawnGroupMakers ??= PawnGroupMakerCodec.CloneAll(d.pawnGroupMakers);
        }
    }

    public override void Apply()
    {
        if (this.def is not FactionDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyDefLabel(nameof(defLabel), def);
        ApplyV<SimpleCurve>(nameof(raidCommonalityFromPointsCurve), v => def.raidCommonalityFromPointsCurve = v);
        ApplyV<SimpleCurve>(nameof(maxPawnCostPerTotalPointsCurve), v => def.maxPawnCostPerTotalPointsCurve = v);
        ApplyV<TechLevel>(nameof(techLevel), v => def.techLevel = v);
        ApplyV<float>(nameof(settlementGenerationWeight), v => def.settlementGenerationWeight = v);
        ApplyV<bool>(nameof(naturalEnemy), v => def.naturalEnemy = v);
        ApplyV<bool>(nameof(permanentEnemy), v => def.permanentEnemy = v);
        // 写回前再克隆一次：字段里的对象与 Def 永不共享实例，所以"编辑 → Apply → 再编辑 → 再 Apply"
        // 不会互相污染。非原版子类不克隆，按"类型 + 次序"从 Def 里找回原实例（见 MergeWithDef）。
        ApplyL<PawnGroupMaker>(nameof(pawnGroupMakers),
            v => def.pawnGroupMakers = PawnGroupMakerCodec.MergeWithDef(v, def.pawnGroupMakers));
    }

    protected override Texture2D? ResolveUiIcon() => (def as FactionDef)?.FactionIcon;

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(FactionType).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (this.def is FactionDef def)
        {
            if (def.hidden)
            {
                return (int)FactionType.Hidden;
            }
        }
        return (int)FactionType.Normal;
    }

    private enum FactionType
    {
        Normal,
        Hidden
    }
}
