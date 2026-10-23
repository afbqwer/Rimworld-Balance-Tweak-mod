using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(XenotypeDef), SettingType.Xenotype)]
class XenotypeData : TweakData<XenotypeData>
{
    // 核心构成
    [TweakField(Style = ColumnStyle.DefList)]
    public List<GeneDef>? genes = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? inheritable = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? descriptionShort = null;

    // 生成与战斗
    [TweakField(Style = ColumnStyle.Float)]
    public float? combatPowerFactor = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canGenerateAsCombatant = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? factionlessGenerationWeight = null;

    // 限制与异种复制
    [TweakField(Style = ColumnStyle.DefList)]
    public List<WeaponClassDef>? forbiddenWeaponClasses = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? generateWithXenogermReplicatingHediffChance = null;

    [TweakField(Style = ColumnStyle.Range)]
    public FloatRange? xenogermReplicatingDurationLeftDaysRange = null;

    [TweakField(Style = ColumnStyle.XenotypeChanceList)]
    public List<XenotypeChance>? doubleXenotypeChances = null;

    // 显示
    [TweakField(Style = ColumnStyle.Float)]
    public float? displayPriority = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is XenotypeDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= d.label;
            desc = d.description ?? "";
            searchString = label + d.defName;
            uiIcon = d.Icon;

            inheritable ??= d.inheritable;
            descriptionShort ??= d.descriptionShort;
            combatPowerFactor ??= d.combatPowerFactor;
            canGenerateAsCombatant ??= d.canGenerateAsCombatant;
            factionlessGenerationWeight ??= d.factionlessGenerationWeight;
            generateWithXenogermReplicatingHediffChance ??= d.generateWithXenogermReplicatingHediffChance;
            xenogermReplicatingDurationLeftDaysRange ??= d.xenogermReplicatingDurationLeftDaysRange;
            displayPriority ??= d.displayPriority;

            // 列表字段：已改/已清空过就不得用 Def 回填（硬约束第 8 行，SC2 会校验）
            if (!IsFieldModified(nameof(genes))) genes ??= d.genes;
            if (!IsFieldModified(nameof(forbiddenWeaponClasses))) forbiddenWeaponClasses ??= d.forbiddenWeaponClasses;
            if (!IsFieldModified(nameof(doubleXenotypeChances))) doubleXenotypeChances ??= d.doubleXenotypeChances;
        }
    }

    public override void Apply()
    {
        if (this.def is not XenotypeDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyS(nameof(descriptionShort), v => def.descriptionShort = v);
        // genes 在游戏里被 AllGenes/Archite 直接解引用（无 null 判断）：
        // Cleared 语义仍由 ApplyL 判定，但写入时用空列表代替 null，避免运行期 NRE。
        ApplyL<GeneDef>(nameof(genes), v => def.genes = v ?? new List<GeneDef>());

        ApplyV<bool>(nameof(inheritable), v => def.inheritable = v);
        ApplyV<float>(nameof(combatPowerFactor), v => def.combatPowerFactor = v);
        ApplyV<bool>(nameof(canGenerateAsCombatant), v => def.canGenerateAsCombatant = v);
        ApplyV<float>(nameof(factionlessGenerationWeight), v => def.factionlessGenerationWeight = v);

        ApplyL<WeaponClassDef>(nameof(forbiddenWeaponClasses), v => def.forbiddenWeaponClasses = v);
        ApplyV<float>(nameof(generateWithXenogermReplicatingHediffChance), v => def.generateWithXenogermReplicatingHediffChance = v);
        ApplyV<FloatRange>(nameof(xenogermReplicatingDurationLeftDaysRange), v => def.xenogermReplicatingDurationLeftDaysRange = v);
        ApplyL<XenotypeChance>(nameof(doubleXenotypeChances), v => def.doubleXenotypeChances = v);

        ApplyV<float>(nameof(displayPriority), v => def.displayPriority = v);
    }

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(XenotypeType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum XenotypeType
    {
        Inheritable,
        NonInheritable,
    }

    public override int GetPropType()
        => this.def is XenotypeDef d && !d.inheritable
            ? (int)XenotypeType.NonInheritable
            : (int)XenotypeType.Inheritable;
}
