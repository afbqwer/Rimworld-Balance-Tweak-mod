using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(TraitDef), SettingType.Trait)]
class TraitData : TweakData<TraitData>, ISubItemHost
{
    /// <summary>特性程度子项（原 Init() 里的硬编码块）。</summary>
    public IEnumerable<TweakData> CreateSubItems(TweakData parent, bool tweaked)
    {
        if (parent.def is not TraitDef d || d.degreeDatas.NullOrEmpty()) yield break;
        for (int i = 0; i < d.degreeDatas.Count; i++)
        {
            if (d.degreeDatas[i] == null) continue;
            var nd = new TraitDegreeNodeData { index = i };
            nd.SetParentTweak(parent, SettingType.TraitDegree, tweaked);
            yield return nd;
        }
    }

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? FirstDegree = null;

    [TweakField(Style = ColumnStyle.Float)]
    public float? commonality = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? commonalityFemale = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canBeSuppressed = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? allowOnHostileSpawn = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public FactionDef? disableHostilityFromFaction = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(AnimalType))]
    public AnimalType? disableHostilityFromAnimalType = null;

    [TweakField(Style = ColumnStyle.DefList)]
    public List<TraitDef>? conflictingTraits = null;
    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? exclusionTags = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<SkillDef>? conflictingPassions = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<SkillDef>? forcedPassions = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<WorkTypeDef>? requiredWorkTypes = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<WorkTypeDef>? disabledWorkTypes = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(WorkTags))]
    public WorkTags? requiredWorkTags = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(WorkTags))]
    public WorkTags? disabledWorkTags = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    private static readonly FieldInfo CommonalityField = AccessTools.Field(typeof(TraitDef), "commonality");
    private static readonly FieldInfo CommonalityFemaleField = AccessTools.Field(typeof(TraitDef), "commonalityFemale");

    public override int LoadingOrd => 100;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is TraitDef d)
        {
            label = d.degreeDatas.FirstOrDefault()?.LabelCap ?? "";
            defLabel ??= def?.label;
            desc = d.defName + '\n' + (d.degreeDatas.FirstOrDefault()?.description ?? "");
            searchString = label + d.defName;
            commonality ??= (float?)CommonalityField.GetValue(d);
            commonalityFemale ??= (float?)CommonalityFemaleField.GetValue(d);
            canBeSuppressed ??= d.canBeSuppressed;
            allowOnHostileSpawn ??= d.allowOnHostileSpawn;
            disableHostilityFromFaction ??= d.disableHostilityFromFaction;
            disableHostilityFromAnimalType ??= d.disableHostilityFromAnimalType;
            if (!IsFieldModified(nameof(conflictingTraits))) conflictingTraits ??= d.conflictingTraits;
            if (!IsFieldModified(nameof(exclusionTags))) exclusionTags ??= d.exclusionTags;
            if (!IsFieldModified(nameof(conflictingPassions))) conflictingPassions ??= d.conflictingPassions;
            if (!IsFieldModified(nameof(forcedPassions))) forcedPassions ??= d.forcedPassions;
            if (!IsFieldModified(nameof(requiredWorkTypes))) requiredWorkTypes ??= d.requiredWorkTypes;
            if (!IsFieldModified(nameof(disabledWorkTypes))) disabledWorkTypes ??= d.disabledWorkTypes;
            requiredWorkTags ??= d.requiredWorkTags;
            disabledWorkTags ??= d.disabledWorkTags;
        }
    }

    public override void Apply()
    {
        if (this.def is not TraitDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyV<float>(nameof(commonality), v => CommonalityField.SetValue(def, v));
        ApplyV<float>(nameof(commonalityFemale), v => CommonalityFemaleField.SetValue(def, v));
        ApplyV<bool>(nameof(canBeSuppressed), v => def.canBeSuppressed = v);
        ApplyV<bool>(nameof(allowOnHostileSpawn), v => def.allowOnHostileSpawn = v);
        ApplyD<FactionDef>(nameof(disableHostilityFromFaction), v => def.disableHostilityFromFaction = v);
        ApplyN<AnimalType>(nameof(disableHostilityFromAnimalType), v => def.disableHostilityFromAnimalType = v);
        ApplyL<TraitDef>(nameof(conflictingTraits), v => def.conflictingTraits = v);
        ApplyL<string>(nameof(exclusionTags), v => def.exclusionTags = v);
        ApplyL<SkillDef>(nameof(conflictingPassions), v => def.conflictingPassions = v);
        ApplyL<SkillDef>(nameof(forcedPassions), v => def.forcedPassions = v);
        ApplyL<WorkTypeDef>(nameof(requiredWorkTypes), v => def.requiredWorkTypes = v);
        ApplyL<WorkTypeDef>(nameof(disabledWorkTypes), v => def.disabledWorkTypes = v);
        ApplyV<WorkTags>(nameof(requiredWorkTags), v => def.requiredWorkTags = v);
        ApplyV<WorkTags>(nameof(disabledWorkTags), v => def.disabledWorkTags = v);
    }


    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(TraitCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    public enum TraitCategory
    {
        SingleDegree,
        MultiDegree,
    }

    public override int GetPropType()
    {
        if (this.def is TraitDef d && d.degreeDatas.Count > 1)
        {
            return (int)TraitCategory.MultiDegree;
        }
        return (int)TraitCategory.SingleDegree;
    }
}
