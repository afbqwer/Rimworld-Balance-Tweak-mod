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
            uiIcon = GetIcon(d.iconPath);
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

            statOffsets ??= d.statOffsets;
            statFactors ??= d.statFactors;
            capMods ??= d.capMods;
            damageFactors ??= d.damageFactors;

            abilities ??= d.abilities;
            forcedTraits ??= d.forcedTraits;
            suppressedTraits ??= d.suppressedTraits;
            aptitudes ??= d.aptitudes;
            enablesNeeds ??= d.enablesNeeds;
            disablesNeeds ??= d.disablesNeeds;
            disabledWorkTags ??= d.disabledWorkTags;
            makeImmuneTo ??= d.makeImmuneTo;
            hediffGiversCannotGive ??= d.hediffGiversCannotGive;
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
        if (defLabel != null) this.def.label = defLabel;
        if (biostatMet.HasValue) { def.biostatMet = biostatMet.Value; }
        if (biostatArc.HasValue) { def.biostatArc = biostatArc.Value; }
        if (biostatCpx.HasValue) { def.biostatCpx = biostatCpx.Value; }

        if (selectionWeight.HasValue) { def.selectionWeight = selectionWeight.Value; }
        if (canGenerateInGeneSet.HasValue) { def.canGenerateInGeneSet = canGenerateInGeneSet.Value; }
        if (marketValueFactor.HasValue) { def.marketValueFactor = marketValueFactor.Value; }
        if (minAgeActive.HasValue) { def.minAgeActive = minAgeActive.Value; }
        if (randomChosen.HasValue) { def.randomChosen = randomChosen.Value; }
        if (passOnDirectly.HasValue) { def.passOnDirectly = passOnDirectly.Value; }
        if (removeOnRedress.HasValue) { def.removeOnRedress = removeOnRedress.Value; }

        if (statOffsets != null) { def.statOffsets = statOffsets; }
        if (statFactors != null) { def.statFactors = statFactors; }
        if (capMods != null) { def.capMods = capMods; }
        if (damageFactors != null) { def.damageFactors = damageFactors; }

        if (abilities != null) { def.abilities = abilities; }
        if (forcedTraits != null) { def.forcedTraits = forcedTraits; }
        if (suppressedTraits != null) { def.suppressedTraits = suppressedTraits; }
        if (aptitudes != null) { def.aptitudes = aptitudes; }
        if (enablesNeeds != null) { def.enablesNeeds = enablesNeeds; }
        if (disablesNeeds != null) { def.disablesNeeds = disablesNeeds; }
        if (disabledWorkTags.HasValue) { def.disabledWorkTags = disabledWorkTags.Value; }
        if (makeImmuneTo != null) { def.makeImmuneTo = makeImmuneTo; }
        if (hediffGiversCannotGive != null) { def.hediffGiversCannotGive = hediffGiversCannotGive; }
        if (passionModSkill != null || passionModType != null)
        {
            def.passionMod = new PassionMod
            {
                skill = passionModSkill ?? def.passionMod?.skill,
                modType = passionModType ?? def.passionMod?.modType ?? PassionMod.PassionModType.None,
            };
        }
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


