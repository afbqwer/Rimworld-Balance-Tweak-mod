using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


[TweakFor(typeof(GeneDef), SettingType.Gene)]
class GeneData : TweakData<GeneData>
{
    // 字段定义
    [TweakField(Style = ColumnStyle.String)]
    public string? displayCategory = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? biostatMet = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? biostatCpx = null;
    [TweakField(Style = ColumnStyle.Int)]
    public int? biostatArc = null;

    // 基础属性（第一优先级）
    [TweakField(Style = ColumnStyle.Float)]
    public float? selectionWeight = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canGenerateInGeneSet = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? marketValueFactor = null;
    [TweakField(Style = ColumnStyle.Float)]
    public float? minAgeActive = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? randomChosen = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? passOnDirectly = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? removeOnRedress = null;

    // 属性修正（第二优先级）
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statOffsets = null;
    [TweakField(Style = ColumnStyle.StatModList)]
    public List<StatModifier>? statFactors = null;
    [TweakField(Style = ColumnStyle.CapModList)]
    public List<PawnCapacityModifier>? capMods = null;
    [TweakField(Style = ColumnStyle.DamageModList)]
    public List<DamageFactor>? damageFactors = null;

    // 能力与特性（第二优先级）
    [TweakField(Style = ColumnStyle.DefList)]
    public List<AbilityDef>? abilities = null;
    [TweakField(Style = ColumnStyle.GeneticTraitList)]
    public List<GeneticTraitData>? forcedTraits = null;
    [TweakField(Style = ColumnStyle.GeneticTraitList)]
    public List<GeneticTraitData>? suppressedTraits = null;
    [TweakField(Style = ColumnStyle.AptitudeList)]
    public List<Aptitude>? aptitudes = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public SkillDef? passionModSkill = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(PassionMod.PassionModType))]
    public PassionMod.PassionModType? passionModType = null;

    // 需求、工作与免疫
    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? enablesNeeds = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<NeedDef>? disablesNeeds = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(WorkTags))]
    public WorkTags? disabledWorkTags = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? makeImmuneTo = null;
    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? hediffGiversCannotGive = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 100;

    private string? iconPath;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is GeneDef d)
        {
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = d.description ?? "";
            displayCategory = d.displayCategory?.LabelCap ?? "";
            iconPath = d.iconPath;
            uiIconColor = d.IconColor;
            searchString = displayCategory + label + d.defName;
            biostatMet ??= d.biostatMet;
            biostatArc ??= d.biostatArc;
            biostatCpx ??= d.biostatCpx;

            selectionWeight ??= d.selectionWeight;
            canGenerateInGeneSet ??= d.canGenerateInGeneSet;
            marketValueFactor ??= d.marketValueFactor;
            minAgeActive ??= d.minAgeActive;
            randomChosen ??= d.randomChosen;
            passOnDirectly ??= d.passOnDirectly;
            removeOnRedress ??= d.removeOnRedress;

            if (!IsFieldModified(nameof(statOffsets))) statOffsets ??= d.statOffsets;
            if (!IsFieldModified(nameof(statFactors))) statFactors ??= d.statFactors;
            if (!IsFieldModified(nameof(capMods))) capMods ??= d.capMods;
            if (!IsFieldModified(nameof(damageFactors))) damageFactors ??= d.damageFactors;

            if (!IsFieldModified(nameof(abilities))) abilities ??= d.abilities;
            if (!IsFieldModified(nameof(forcedTraits))) forcedTraits ??= d.forcedTraits;
            if (!IsFieldModified(nameof(suppressedTraits))) suppressedTraits ??= d.suppressedTraits;
            if (!IsFieldModified(nameof(aptitudes))) aptitudes ??= d.aptitudes;
            if (!IsFieldModified(nameof(enablesNeeds))) enablesNeeds ??= d.enablesNeeds;
            if (!IsFieldModified(nameof(disablesNeeds))) disablesNeeds ??= d.disablesNeeds;
            disabledWorkTags ??= d.disabledWorkTags;
            if (!IsFieldModified(nameof(makeImmuneTo))) makeImmuneTo ??= d.makeImmuneTo;
            if (!IsFieldModified(nameof(hediffGiversCannotGive))) hediffGiversCannotGive ??= d.hediffGiversCannotGive;
            if (d.passionMod != null)
            {
                passionModSkill ??= d.passionMod.skill;
                passionModType ??= d.passionMod.modType;
            }
        }
    }

    public override void Apply()
    {
        if (this.def is not GeneDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyV<int>(nameof(biostatMet), v => def.biostatMet = v);
        ApplyV<int>(nameof(biostatArc), v => def.biostatArc = v);
        ApplyV<int>(nameof(biostatCpx), v => def.biostatCpx = v);

        ApplyV<float>(nameof(selectionWeight), v => def.selectionWeight = v);
        ApplyV<bool>(nameof(canGenerateInGeneSet), v => def.canGenerateInGeneSet = v);
        ApplyV<float>(nameof(marketValueFactor), v => def.marketValueFactor = v);
        ApplyV<float>(nameof(minAgeActive), v => def.minAgeActive = v);
        ApplyV<bool>(nameof(randomChosen), v => def.randomChosen = v);
        ApplyV<bool>(nameof(passOnDirectly), v => def.passOnDirectly = v);
        ApplyV<bool>(nameof(removeOnRedress), v => def.removeOnRedress = v);

        ApplyL<StatModifier>(nameof(statOffsets), v => def.statOffsets = v);
        ApplyL<StatModifier>(nameof(statFactors), v => def.statFactors = v);
        ApplyL<PawnCapacityModifier>(nameof(capMods), v => def.capMods = v);
        ApplyL<DamageFactor>(nameof(damageFactors), v => def.damageFactors = v);

        ApplyL<AbilityDef>(nameof(abilities), v => def.abilities = v);
        ApplyL<GeneticTraitData>(nameof(forcedTraits), v => def.forcedTraits = v);
        ApplyL<GeneticTraitData>(nameof(suppressedTraits), v => def.suppressedTraits = v);
        ApplyL<Aptitude>(nameof(aptitudes), v => def.aptitudes = v);
        ApplyL<NeedDef>(nameof(enablesNeeds), v => def.enablesNeeds = v);
        ApplyL<NeedDef>(nameof(disablesNeeds), v => def.disablesNeeds = v);
        ApplyV<WorkTags>(nameof(disabledWorkTags), v => def.disabledWorkTags = v);
        ApplyL<HediffDef>(nameof(makeImmuneTo), v => def.makeImmuneTo = v);
        ApplyL<HediffDef>(nameof(hediffGiversCannotGive), v => def.hediffGiversCannotGive = v);
        ApplyD<SkillDef>(nameof(passionModSkill), v => def.passionMod = new PassionMod
        {
            skill = v,
            modType = passionModType ?? def.passionMod?.modType ?? PassionMod.PassionModType.None,
        });
        ApplyV<PassionMod.PassionModType>(nameof(passionModType), v => def.passionMod = new PassionMod
        {
            skill = passionModSkill ?? def.passionMod?.skill,
            modType = v,
        });
    }


    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(GeneType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum GeneType
    {
        Appearance,
        Metabolism,
        NoMetabolism,
        Archite,
    }

    public override int GetPropType()
    {
        if (this.def is GeneDef def)
        {
            if (def.endogeneCategory != EndogeneCategory.None || def.bodyType.HasValue)
            {
                return (int)GeneType.Appearance;
            }
            if (def.biostatArc > 0)
            {
                return (int)GeneType.Archite;
            }
            if (def.biostatMet == 0)
            {
                return (int)GeneType.NoMetabolism;
            }
        }
        return (int)GeneType.Metabolism;
    }

    protected override Texture2D? ResolveUiIcon() => GetIcon(iconPath);

    private static Texture2D? GetIcon(string? iconPath)
    {
        if (iconPath.NullOrEmpty())
        {
            return null;
        }
        else
        {
            return ContentFinder<Texture2D>.Get(iconPath, false) ?? BaseContent.BadTex;
        }
    }
}


