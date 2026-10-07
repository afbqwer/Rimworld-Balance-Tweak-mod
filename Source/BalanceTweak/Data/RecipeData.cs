using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// PipeSystem types are accessed via RecipeProcessHelper to avoid load failures
// when PipeSystem is not present.
using HarmonyLib;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

[TweakFor(typeof(RecipeDef), SettingType.Recipe)]
[TweakFor(typeof(RecipeDef), SettingType.Recipe, TypeName = "PipeSystem.ProcessDef", MayRequire = TweakDatabase.VEF)]
public class RecipeData : TweakData<RecipeData>
{
    public bool isProcess = false;

    // ── RecipeDef fields ──
    [TweakField(Style = ColumnStyle.Bool, DataType = ColumnDataType.Display, Available = nameof(AvailableIfNonProcess))]
    public bool? generated = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfNonProcess), GetAbstract = nameof(GetWorkAmountAbstract))]
    public float? workAmount = null;// -1 时由产物物品的工作量决定

    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfCrafting))]
    public int? targetCountAdjustment = null;

    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonSurgeryAndVEF))]
    public ThingDef? unfinishedThingDef = null;
    [TweakField(Style = ColumnStyle.ThingDefCountList, Available = nameof(AvailableIfNonSurgeryAndVEF))]
    public List<ThingDefCountClass>? products = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfNonSurgeryAndVEF))]
    public List<ThingDef>? recipeUsers = null;
    [TweakField(Style = ColumnStyle.IngredientList, Available = nameof(AvailableIfNonProcess))]
    public List<IngredientCount>? ingredients = null;
    [TweakField(Style = ColumnStyle.IngredientFilter, Available = nameof(AvailableIfNonProcess))]
    public ThingFilter? fixedIngredientFilter = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonProcess))]
    public StatDef? workSpeedStat = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonMechAndVEF))]
    public SkillDef? workSkill = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfNonMechAndVEF))]
    public float? workSkillLearnFactor = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfNonProcess))]
    public ResearchProjectDef? researchPrerequisite = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfNonProcess))]
    public List<ResearchProjectDef>? researchPrerequisites = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonSurgeryAndVEF))]
    public bool? mechanitorOnlyRecipe = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfNonMechAndVEF))]
    public bool? isViolation = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfSurgery))]
    public bool? anesthetize = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfSurgery))]
    public float? surgerySuccessChanceFactor = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfSurgery))]
    public float? deathOnFailedSurgeryChance = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMechanoid))]
    public int? formingTicks = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfMechanoid))]
    public int? gestationCycles = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMechanoid))]
    public bool? mechResurrection = null;


    [TweakField(Style = ColumnStyle.IngredientFilter, Available = nameof(AvailableIfNonProcess))]
    public ThingFilter? defaultIngredientFilter = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfSurgery))]
    public HediffDef? addsHediff = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfSurgery))]
    public List<BodyPartDef>? appliedOnFixedBodyParts = null;
    [TweakField(Style = ColumnStyle.SkillReqList, Available = nameof(AvailableIfNonMechAndVEF))]
    public List<SkillRequirement>? skillRequirements = null;

    // ── PipeSystem.ProcessDef fields (only when isProcess) ──

    [TweakField(Style = ColumnStyle.ProcessIngredientList, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public List<ProcessIngredientItem>? processIngredients = null;
    [TweakField(Style = ColumnStyle.Int, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public int? ticks = null;
    [TweakField(Style = ColumnStyle.IntList, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableOnlyProcess))]
    public List<int>? ticksQuality = null;
    [TweakField(Style = ColumnStyle.ProcessResultList, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public List<ProcessResultItem>? processResults = null;
    [TweakField(Style = ColumnStyle.Bool, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public bool? isLightDependingProcess = null;
    [TweakField(Style = ColumnStyle.Float, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public float? minLight = null;
    [TweakField(Style = ColumnStyle.Float, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public float? maxLight = null;
    [TweakField(Style = ColumnStyle.Bool, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public bool? temperatureRuinable = null;
    [TweakField(Style = ColumnStyle.Float, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public float? minSafeTemperature = null;
    [TweakField(Style = ColumnStyle.Float, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public float? maxSafeTemperature = null;
    [TweakField(Style = ColumnStyle.Float, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public float? maxOutputCount = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.String, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public string? processUser = null;
    [TweakField(Style = ColumnStyle.Bool, MayRequire = TweakDatabase.VEF, Available = nameof(AvailableIfProcess))]
    public bool? hidden = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 200;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        if (def is RecipeDef d)
        {
            generated = d.generated;
            label = d.LabelCap.NullOrEmpty() ? d.defName : d.LabelCap;
            defLabel ??= def?.label;
            desc = $"{d.defName}\n{d.description}";
            uiIcon = d.UIIcon ?? d.UIIconThing?.uiIcon;
            var c = d.UIIconThing?.uiIconColor;
            if(c != null){
                uiIconColor = c.Value;
            }
            if (desc.NullOrEmpty()) { desc = d.defName; }
            searchString = label + d.defName;
            workAmount ??= d.workAmount;
            workSpeedStat ??= d.workSpeedStat;
            targetCountAdjustment ??= d.targetCountAdjustment;
            unfinishedThingDef ??= d.unfinishedThingDef;
            workSkill ??= d.workSkill;
            workSkillLearnFactor ??= d.workSkillLearnFactor;
            products ??= d.products;
            recipeUsers ??= d.recipeUsers;
            researchPrerequisite ??= d.researchPrerequisite;
            researchPrerequisites ??= d.researchPrerequisites;
            mechanitorOnlyRecipe ??= d.mechanitorOnlyRecipe;
            isViolation ??= d.isViolation;
            anesthetize ??= d.anesthetize;
            surgerySuccessChanceFactor ??= d.surgerySuccessChanceFactor;
            deathOnFailedSurgeryChance ??= d.deathOnFailedSurgeryChance;
            formingTicks ??= d.formingTicks;
            gestationCycles ??= d.gestationCycles;
            mechResurrection ??= d.mechResurrection;
            ingredients ??= d.ingredients;
            fixedIngredientFilter ??= d.fixedIngredientFilter;
            defaultIngredientFilter ??= d.defaultIngredientFilter;
            addsHediff ??= d.addsHediff;
            appliedOnFixedBodyParts ??= d.appliedOnFixedBodyParts;
            skillRequirements = d.skillRequirements;
        }
        else
        {
            if (DataUtility.MayRequire(TweakDatabase.VEF))
            {
                RecipeProcessHelper.SetProcessDef(def, this);
                return;
            }
        }

    }

    public override void Apply()
    {
        if (this.def is RecipeDef def)
        {
            if (defLabel != null) this.def.label = defLabel;
            if (workAmount.HasValue) { def.workAmount = workAmount.Value; }
            if (workSpeedStat != null) { def.workSpeedStat = workSpeedStat; }
            if (targetCountAdjustment.HasValue) { def.targetCountAdjustment = targetCountAdjustment.Value; }
            if (unfinishedThingDef != null) { def.unfinishedThingDef = unfinishedThingDef; }
            if (workSkill != null) { def.workSkill = workSkill; }
            if (workSkillLearnFactor.HasValue) { def.workSkillLearnFactor = workSkillLearnFactor.Value; }
            if (products != null) { def.products = products; }
            if (recipeUsers != null)
            {
                var oldUsers = def.recipeUsers?.ToList();
                def.recipeUsers = recipeUsers;
                InvalidateCaches(def, oldUsers, recipeUsers);
            }
            if (researchPrerequisite != null) { def.researchPrerequisite = researchPrerequisite; }
            if (researchPrerequisites != null) { def.researchPrerequisites = researchPrerequisites; }
            if (mechanitorOnlyRecipe.HasValue) { def.mechanitorOnlyRecipe = mechanitorOnlyRecipe.Value; }
            if (isViolation.HasValue) { def.isViolation = isViolation.Value; }
            if (anesthetize.HasValue) { def.anesthetize = anesthetize.Value; }
            if (surgerySuccessChanceFactor.HasValue) { def.surgerySuccessChanceFactor = surgerySuccessChanceFactor.Value; }
            if (deathOnFailedSurgeryChance.HasValue) { def.deathOnFailedSurgeryChance = deathOnFailedSurgeryChance.Value; }
            if (formingTicks.HasValue) { def.formingTicks = formingTicks.Value; }
            if (gestationCycles.HasValue) { def.gestationCycles = gestationCycles.Value; }
            if (mechResurrection.HasValue) { def.mechResurrection = mechResurrection.Value; }
            if (ingredients != null) { def.ingredients = ingredients?.ToList(); }
            if (fixedIngredientFilter != null) { def.fixedIngredientFilter = fixedIngredientFilter; }
            if (defaultIngredientFilter != null) { def.defaultIngredientFilter = defaultIngredientFilter; }
            if (addsHediff != null) { def.addsHediff = addsHediff; }
            if (appliedOnFixedBodyParts != null) { def.appliedOnFixedBodyParts = appliedOnFixedBodyParts; }

            if (skillRequirements != null) { def.skillRequirements = skillRequirements; }
        }

        if (this.def != null && DataUtility.MayRequire(TweakDatabase.VEF))
        {
            RecipeProcessHelper.ApplyProcessDef(this.def, this);
            if (hidden.HasValue)
            {
                RecipeProcessHelper.ApplyProcessHidden(this.def, hidden.Value);
            }
            return;
        }

    }

    private static void InvalidateCaches(RecipeDef def, List<ThingDef>? oldUsers, List<ThingDef>? newUsers)
    {
        // Reset RecipeDef.isSurgeryCached — IsSurgery iterates AllRecipeUsers (includes recipeUsers)
        var surgeryField = AccessTools.Field(typeof(RecipeDef), "isSurgeryCached");
        surgeryField?.SetValue(def, null);

        // Reset ThingDef.allRecipesCached for all affected recipe users
        var recipesField = AccessTools.Field(typeof(ThingDef), "allRecipesCached");
        if (oldUsers != null)
            foreach (var thing in oldUsers)
                recipesField?.SetValue(thing, null);
        if (newUsers != null)
            foreach (var thing in newUsers)
                recipesField?.SetValue(thing, null);
    }

    /// <summary>
    /// 工作量抽象值：当 workAmount 为 -1 时，由第一个产物的 WorkToMake 决定（与游戏内 RecipeDef.WorkAmountForStuff 逻辑一致）。
    /// </summary>
    public static float GetWorkAmountAbstract(TweakData data)
    {
        if (data is RecipeData rd && rd.def is RecipeDef d && d.products is { Count: > 0 })
        {
            return d.products[0].thingDef?.GetStatValueAbstract(StatDefOf.WorkToMake) ?? -1f;
        }
        return -1f;
    }

    public static bool AvailableIfCrafting(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Crafting => true,
        _ => false,
    };
    public static bool AvailableIfSurgery(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Surgery => true,
        _ => false,
    };
    public static bool AvailableIfMechanoid(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Mechanoid => true,
        _ => false,
    };
    public static bool AvailableIfNonSurgeryAndVEF(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Surgery => false,
        (int)RecipeCategory.Factory => false,
        (int)RecipeCategory.Process => false,
        _ => true,
    };
    public static bool AvailableIfNonMechAndVEF(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Mechanoid => false,
        (int)RecipeCategory.Factory => false,
        (int)RecipeCategory.Process => false,
        _ => true,
    };

    public static bool AvailableIfProcess(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Factory => true,
        (int)RecipeCategory.Process => true,
        _ => false,
    };

    public static bool AvailableIfNonProcess(TweakData data) => !AvailableIfProcess(data);

    public static bool AvailableOnlyProcess(TweakData data) => data.propType switch
    {
        (int)RecipeCategory.Process => true,
        _ => false,
    };

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(RecipeCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    public enum RecipeCategory
    {
        CostList,
        Crafting,
        Surgery,
        Mechanoid,
        Other,
        Factory,
        Process,
    }

    public override int GetPropType()
    {
        if (this.def is RecipeDef def)
        {
            if (def.mechanitorOnlyRecipe && def.products.FirstOrDefault()?.thingDef?.race != null)
            {
                return (int)RecipeCategory.Mechanoid;
            }
            if (def.IsSurgery || def.addsHediff != null)
            {
                return (int)RecipeCategory.Surgery;
            }
            if (def.products?.Count > 0)
            {
                if (def.generated)
                    return (int)RecipeCategory.CostList;
                else
                    return (int)RecipeCategory.Crafting;
            }
            return (int)RecipeCategory.Other;
        }
        if (this.def != null && DataUtility.MayRequire(TweakDatabase.VEF))
        {
            int pt = RecipeProcessHelper.GetProcessDefPropType(this.def);
            if (pt >= 0) return pt;
        }
        return (int)RecipeCategory.Other;
    }
}