using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.RaceData;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;



[TweakFor(typeof(PawnKindDef), SettingType.PawnKind)]
class PawnKindData : TweakData<PawnKindData>
{

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? Race = null;

    [TweakField(Style = ColumnStyle.Int)]
    public float? combatPower = null;
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? collidesWithPawns = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMech))]
    public bool? allowInMechClusters = null;


    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? initialWillRange = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? initialResistanceRange = null;

    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? weaponMoney = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? apparelMoney = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? gearHealthRange = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(QualityCategory), Available = nameof(AvailableIfHuman))]
    public QualityCategory? itemQuality = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(QualityCategory), Available = nameof(AvailableIfHuman))]
    public QualityCategory? forceWeaponQuality = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(QualityCategory), Available = nameof(AvailableIfHuman))]
    public QualityCategory? minApparelQuality = null;
    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(QualityCategory), Available = nameof(AvailableIfHuman))]
    public QualityCategory? maxApparelQuality = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? forceNormalGearQuality = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? nakedChance = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? biocodeWeaponChance = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? destroyGearOnDrop = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canStrip = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? apparelAllowHeadgearChance = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? ignoreApparelAllowChance = null;

    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? maxPerGroup = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canBeScattered = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? appearsRandomlyInCombatGroups = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfBeast))]
    public IntRange? wildGroupSize = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfBeast))]
    public float? ecoSystemWeight = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(Gender), Available = nameof(AvailableIfHuman))]
    public Gender? fixedGender = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? chronologicalAgeRange = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? minGenerationAge = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? maxGenerationAge = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canOpenAnyDoor = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canOpenDoors = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? isBoss = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? preventIdeo = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? humanPregnancyChance = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? allowOldAgeInjuries = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? forceNoDeathNotification = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? controlGroupPortraitZoom = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? showInDebugSpawner = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? royalTitleChance = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? useFactionXenotypes = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? allowRoyalRoomRequirements = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? allowRoyalApparelRequirements = null;

    // === Second Priority: Combat & AI ===
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? isFighter = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? forceDeathOnDowned = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? overrideDeathOnDownedChance = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? fleeHealthThresholdRange = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? ignoresPainShock = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canMeleeAttack = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? aiAvoidCover = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? canBeSapper = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? isGoodBreacher = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? defendPointRadius = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfBeast))]
    public bool? canArriveManhunter = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? immuneToTraps = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? immuneToGameConditionEffects = null;

    // === Second Priority: Capture & Prison ===
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? basePrisonBreakMtbDays = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? acceptArrestChanceFactor = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? skipResistant = null;

    // === Second Priority: Faction & Hostility ===
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? factionHostileOnKill = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? factionHostileOnDeath = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? hostileToAll = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? factionLeader = null;

    // === Second Priority: List<string> Fields ===
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHuman))]
    public List<string>? weaponTags = null;
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHuman))]
    public List<string>? apparelTags = null;
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHuman))]
    public List<string>? apparelDisallowTags = null;
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHuman))]
    public List<string>? techHediffsTags = null;
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHuman))]
    public List<string>? techHediffsDisallowTags = null;

    // === Second Priority: List<Def> Fields ===
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<ThingDef>? apparelRequired = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<AbilityDef>? abilities = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<TraitDef>? disallowedTraits = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<ChemicalDef>? forcedAddictions = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<ThingDef>? techHediffsRequired = null;
    [TweakField(Style = ColumnStyle.DefList, Available = nameof(AvailableIfHuman))]
    public List<RoyalTitleDef>? titleSelectOne = null;

    // === Second Priority: Def Selector Fields ===
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public FactionDef? defaultFactionDef = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public ThingDef? weaponStuffOverride = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public ThingDef? invFoodDef = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public RoyalTitleDef? titleRequired = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public RoyalTitleDef? minTitleRequired = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public MutantDef? mutant = null;
    [TweakField(Style = ColumnStyle.DefSelector, Available = nameof(AvailableIfHuman))]
    public ColorDef? favoriteColor = null;

    // === Second Priority: Numeric Fields ===
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? techHediffsMoney = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? techHediffsChance = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? techHediffsMaxAmount = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? invNutrition = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? chemicalAddictionChance = null;
    [TweakField(Style = ColumnStyle.Float, Available = nameof(AvailableIfHuman))]
    public float? combatEnhancingDrugsChance = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public IntRange? combatEnhancingDrugsCount = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? extraSkillLevels = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? minTotalSkillLevels = null;
    [TweakField(Style = ColumnStyle.Int, Available = nameof(AvailableIfHuman))]
    public int? minBestSkillLevel = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? isGoodPsychicRitualInvoker = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? apparelIgnoreSeasons = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? apparelIgnorePollution = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? ignoreFactionApparelStuffRequirements = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? ignoreIdeoApparelColors = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? trader = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfHuman))]
    public bool? generateInitialNonFamilyRelations = null;

    [TweakField(Style = ColumnStyle.String)]
    public string? defLabel = null;

    public override int LoadingOrd => 300;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        defLabel ??= def?.label;
        if (this.def is PawnKindDef d)
        {
            if (GetData(Race) is RaceData rd)
            {
                uiIcon = rd.uiIcon;
                uiIconColor = rd.uiIconColor;
                desc = rd.desc;
            }
            combatPower ??= d.combatPower;
            allowInMechClusters ??= d.allowInMechClusters;
            collidesWithPawns ??= d.collidesWithPawns;
            initialWillRange ??= d.initialWillRange;
            initialResistanceRange ??= d.initialResistanceRange;

            weaponMoney ??= d.weaponMoney;
            apparelMoney ??= d.apparelMoney;
            gearHealthRange ??= d.gearHealthRange;
            itemQuality ??= d.itemQuality;
            forceWeaponQuality ??= d.forceWeaponQuality;
            minApparelQuality ??= d.minApparelQuality;
            maxApparelQuality ??= d.maxApparelQuality;
            forceNormalGearQuality ??= d.forceNormalGearQuality;
            nakedChance ??= d.nakedChance;
            biocodeWeaponChance ??= d.biocodeWeaponChance;
            destroyGearOnDrop ??= d.destroyGearOnDrop;
            canStrip ??= d.canStrip;
            apparelAllowHeadgearChance ??= d.apparelAllowHeadgearChance;
            ignoreApparelAllowChance ??= d.ignoreApparelAllowChance;

            maxPerGroup ??= d.maxPerGroup;
            canBeScattered ??= d.canBeScattered;
            appearsRandomlyInCombatGroups ??= d.appearsRandomlyInCombatGroups;
            wildGroupSize ??= d.wildGroupSize;
            ecoSystemWeight ??= d.ecoSystemWeight;

            fixedGender ??= d.fixedGender;
            chronologicalAgeRange ??= d.chronologicalAgeRange;
            minGenerationAge ??= d.minGenerationAge;
            maxGenerationAge ??= d.maxGenerationAge;
            canOpenAnyDoor ??= d.canOpenAnyDoor;
            canOpenDoors ??= d.canOpenDoors;
            isBoss ??= d.isBoss;
            preventIdeo ??= d.preventIdeo;
            humanPregnancyChance ??= d.humanPregnancyChance;
            allowOldAgeInjuries ??= d.allowOldAgeInjuries;
            forceNoDeathNotification ??= d.forceNoDeathNotification;
            controlGroupPortraitZoom ??= d.controlGroupPortraitZoom;
            showInDebugSpawner ??= d.showInDebugSpawner;
            royalTitleChance ??= d.royalTitleChance;
            useFactionXenotypes ??= d.useFactionXenotypes;
            allowRoyalRoomRequirements ??= d.allowRoyalRoomRequirements;
            allowRoyalApparelRequirements ??= d.allowRoyalApparelRequirements;

            // Second Priority: Combat & AI
            isFighter ??= d.isFighter;
            forceDeathOnDowned ??= d.forceDeathOnDowned;
            overrideDeathOnDownedChance ??= d.overrideDeathOnDownedChance;
            fleeHealthThresholdRange ??= d.fleeHealthThresholdRange;
            ignoresPainShock ??= d.ignoresPainShock;
            canMeleeAttack ??= d.canMeleeAttack;
            aiAvoidCover ??= d.aiAvoidCover;
            canBeSapper ??= d.canBeSapper;
            isGoodBreacher ??= d.isGoodBreacher;
            defendPointRadius ??= d.defendPointRadius;
            canArriveManhunter ??= d.canArriveManhunter;
            immuneToTraps ??= d.immuneToTraps;
            immuneToGameConditionEffects ??= d.immuneToGameConditionEffects;

            // Second Priority: Capture & Prison
            basePrisonBreakMtbDays ??= d.basePrisonBreakMtbDays;
            acceptArrestChanceFactor ??= d.acceptArrestChanceFactor;
            skipResistant ??= d.skipResistant;

            // Second Priority: Faction & Hostility
            factionHostileOnKill ??= d.factionHostileOnKill;
            factionHostileOnDeath ??= d.factionHostileOnDeath;
            hostileToAll ??= d.hostileToAll;
            factionLeader ??= d.factionLeader;

            // Second Priority: List<string>
            weaponTags ??= d.weaponTags;
            apparelTags ??= d.apparelTags;
            apparelDisallowTags ??= d.apparelDisallowTags;
            techHediffsTags ??= d.techHediffsTags;
            techHediffsDisallowTags ??= d.techHediffsDisallowTags;

            // Second Priority: List<Def>
            apparelRequired ??= d.apparelRequired;
            abilities ??= d.abilities;
            disallowedTraits ??= d.disallowedTraits;
            forcedAddictions ??= d.forcedAddictions;
            techHediffsRequired ??= d.techHediffsRequired;
            titleSelectOne ??= d.titleSelectOne;

            // Second Priority: Def Selector
            defaultFactionDef ??= d.defaultFactionDef;
            weaponStuffOverride ??= d.weaponStuffOverride;
            invFoodDef ??= d.invFoodDef;
            titleRequired ??= d.titleRequired;
            minTitleRequired ??= d.minTitleRequired;
            mutant ??= d.mutant;
            favoriteColor ??= d.favoriteColor;

            // Second Priority: Numeric
            techHediffsMoney ??= d.techHediffsMoney;
            techHediffsChance ??= d.techHediffsChance;
            techHediffsMaxAmount ??= d.techHediffsMaxAmount;
            invNutrition ??= d.invNutrition;
            chemicalAddictionChance ??= d.chemicalAddictionChance;
            combatEnhancingDrugsChance ??= d.combatEnhancingDrugsChance;
            combatEnhancingDrugsCount ??= d.combatEnhancingDrugsCount;
            extraSkillLevels ??= d.extraSkillLevels;
            minTotalSkillLevels ??= d.minTotalSkillLevels;
            minBestSkillLevel ??= d.minBestSkillLevel;
            isGoodPsychicRitualInvoker ??= d.isGoodPsychicRitualInvoker;
            apparelIgnoreSeasons ??= d.apparelIgnoreSeasons;
            apparelIgnorePollution ??= d.apparelIgnorePollution;
            ignoreFactionApparelStuffRequirements ??= d.ignoreFactionApparelStuffRequirements;
            ignoreIdeoApparelColors ??= d.ignoreIdeoApparelColors;
            trader ??= d.trader;
            generateInitialNonFamilyRelations ??= d.generateInitialNonFamilyRelations;
        }
    }

    public override void Apply()
    {
        if (this.def is not PawnKindDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        if (defLabel != null) this.def.label = defLabel;
        if (combatPower.HasValue) def.combatPower = combatPower.Value;
        if (allowInMechClusters.HasValue) def.allowInMechClusters = allowInMechClusters.Value;
        if (collidesWithPawns.HasValue) def.collidesWithPawns = collidesWithPawns.Value;
        if (initialWillRange.HasValue) def.initialWillRange = initialWillRange.Value;
        if (initialResistanceRange.HasValue) def.initialResistanceRange = initialResistanceRange.Value;

        if (weaponMoney.HasValue) def.weaponMoney = weaponMoney.Value;
        if (apparelMoney.HasValue) def.apparelMoney = apparelMoney.Value;
        if (gearHealthRange.HasValue) def.gearHealthRange = gearHealthRange.Value;
        if (itemQuality.HasValue) def.itemQuality = itemQuality.Value;
        if (forceWeaponQuality.HasValue) def.forceWeaponQuality = forceWeaponQuality.Value;
        if (minApparelQuality.HasValue) def.minApparelQuality = minApparelQuality.Value;
        if (maxApparelQuality.HasValue) def.maxApparelQuality = maxApparelQuality.Value;
        if (forceNormalGearQuality.HasValue) def.forceNormalGearQuality = forceNormalGearQuality.Value;
        if (nakedChance.HasValue) def.nakedChance = nakedChance.Value;
        if (biocodeWeaponChance.HasValue) def.biocodeWeaponChance = biocodeWeaponChance.Value;
        if (destroyGearOnDrop.HasValue) def.destroyGearOnDrop = destroyGearOnDrop.Value;
        if (canStrip.HasValue) def.canStrip = canStrip.Value;
        if (apparelAllowHeadgearChance.HasValue) def.apparelAllowHeadgearChance = apparelAllowHeadgearChance.Value;
        if (ignoreApparelAllowChance.HasValue) def.ignoreApparelAllowChance = ignoreApparelAllowChance.Value;

        if (maxPerGroup.HasValue) def.maxPerGroup = maxPerGroup.Value;
        if (canBeScattered.HasValue) def.canBeScattered = canBeScattered.Value;
        if (appearsRandomlyInCombatGroups.HasValue) def.appearsRandomlyInCombatGroups = appearsRandomlyInCombatGroups.Value;
        if (wildGroupSize.HasValue) def.wildGroupSize = wildGroupSize.Value;
        if (ecoSystemWeight.HasValue) def.ecoSystemWeight = ecoSystemWeight.Value;

        if (fixedGender.HasValue) def.fixedGender = fixedGender.Value;
        if (chronologicalAgeRange.HasValue) def.chronologicalAgeRange = chronologicalAgeRange.Value;
        if (minGenerationAge.HasValue) def.minGenerationAge = minGenerationAge.Value;
        if (maxGenerationAge.HasValue) def.maxGenerationAge = maxGenerationAge.Value;
        if (canOpenAnyDoor.HasValue) def.canOpenAnyDoor = canOpenAnyDoor.Value;
        if (canOpenDoors.HasValue) def.canOpenDoors = canOpenDoors.Value;
        if (isBoss.HasValue) def.isBoss = isBoss.Value;
        if (preventIdeo.HasValue) def.preventIdeo = preventIdeo.Value;
        if (humanPregnancyChance.HasValue) def.humanPregnancyChance = humanPregnancyChance.Value;
        if (allowOldAgeInjuries.HasValue) def.allowOldAgeInjuries = allowOldAgeInjuries.Value;
        if (forceNoDeathNotification.HasValue) def.forceNoDeathNotification = forceNoDeathNotification.Value;
        if (controlGroupPortraitZoom.HasValue) def.controlGroupPortraitZoom = controlGroupPortraitZoom.Value;
        if (showInDebugSpawner.HasValue) def.showInDebugSpawner = showInDebugSpawner.Value;
        if (royalTitleChance.HasValue) def.royalTitleChance = royalTitleChance.Value;
        if (useFactionXenotypes.HasValue) def.useFactionXenotypes = useFactionXenotypes.Value;
        if (allowRoyalRoomRequirements.HasValue) def.allowRoyalRoomRequirements = allowRoyalRoomRequirements.Value;
        if (allowRoyalApparelRequirements.HasValue) def.allowRoyalApparelRequirements = allowRoyalApparelRequirements.Value;

        // Second Priority: Combat & AI
        if (isFighter.HasValue) def.isFighter = isFighter.Value;
        if (forceDeathOnDowned.HasValue) def.forceDeathOnDowned = forceDeathOnDowned.Value;
        if (overrideDeathOnDownedChance.HasValue) def.overrideDeathOnDownedChance = overrideDeathOnDownedChance.Value;
        if (fleeHealthThresholdRange.HasValue) def.fleeHealthThresholdRange = fleeHealthThresholdRange.Value;
        if (ignoresPainShock.HasValue) def.ignoresPainShock = ignoresPainShock.Value;
        if (canMeleeAttack.HasValue) def.canMeleeAttack = canMeleeAttack.Value;
        if (aiAvoidCover.HasValue) def.aiAvoidCover = aiAvoidCover.Value;
        if (canBeSapper.HasValue) def.canBeSapper = canBeSapper.Value;
        if (isGoodBreacher.HasValue) def.isGoodBreacher = isGoodBreacher.Value;
        if (defendPointRadius.HasValue) def.defendPointRadius = defendPointRadius.Value;
        if (canArriveManhunter.HasValue) def.canArriveManhunter = canArriveManhunter.Value;
        if (immuneToTraps.HasValue) def.immuneToTraps = immuneToTraps.Value;
        if (immuneToGameConditionEffects.HasValue) def.immuneToGameConditionEffects = immuneToGameConditionEffects.Value;

        // Second Priority: Capture & Prison
        if (basePrisonBreakMtbDays.HasValue) def.basePrisonBreakMtbDays = basePrisonBreakMtbDays.Value;
        if (acceptArrestChanceFactor.HasValue) def.acceptArrestChanceFactor = acceptArrestChanceFactor.Value;
        if (skipResistant.HasValue) def.skipResistant = skipResistant.Value;

        // Second Priority: Faction & Hostility
        if (factionHostileOnKill.HasValue) def.factionHostileOnKill = factionHostileOnKill.Value;
        if (factionHostileOnDeath.HasValue) def.factionHostileOnDeath = factionHostileOnDeath.Value;
        if (hostileToAll.HasValue) def.hostileToAll = hostileToAll.Value;
        if (factionLeader.HasValue) def.factionLeader = factionLeader.Value;

        // Second Priority: List<string>
        if (weaponTags != null) def.weaponTags = weaponTags;
        if (apparelTags != null) def.apparelTags = apparelTags;
        if (apparelDisallowTags != null) def.apparelDisallowTags = apparelDisallowTags;
        if (techHediffsTags != null) def.techHediffsTags = techHediffsTags;
        if (techHediffsDisallowTags != null) def.techHediffsDisallowTags = techHediffsDisallowTags;

        // Second Priority: List<Def>
        if (apparelRequired != null) def.apparelRequired = apparelRequired;
        if (abilities != null) def.abilities = abilities;
        if (disallowedTraits != null) def.disallowedTraits = disallowedTraits;
        if (forcedAddictions != null) def.forcedAddictions = forcedAddictions;
        if (techHediffsRequired != null) def.techHediffsRequired = techHediffsRequired;
        if (titleSelectOne != null) def.titleSelectOne = titleSelectOne;

        // Second Priority: Def Selector
        if (defaultFactionDef != null) def.defaultFactionDef = defaultFactionDef;
        if (weaponStuffOverride != null) def.weaponStuffOverride = weaponStuffOverride;
        if (invFoodDef != null) def.invFoodDef = invFoodDef;
        if (titleRequired != null) def.titleRequired = titleRequired;
        if (minTitleRequired != null) def.minTitleRequired = minTitleRequired;
        if (mutant != null) def.mutant = mutant;
        if (favoriteColor != null) def.favoriteColor = favoriteColor;

        // Second Priority: Numeric
        if (techHediffsMoney.HasValue) def.techHediffsMoney = techHediffsMoney.Value;
        if (techHediffsChance.HasValue) def.techHediffsChance = techHediffsChance.Value;
        if (techHediffsMaxAmount.HasValue) def.techHediffsMaxAmount = techHediffsMaxAmount.Value;
        if (invNutrition.HasValue) def.invNutrition = invNutrition.Value;
        if (chemicalAddictionChance.HasValue) def.chemicalAddictionChance = chemicalAddictionChance.Value;
        if (combatEnhancingDrugsChance.HasValue) def.combatEnhancingDrugsChance = combatEnhancingDrugsChance.Value;
        if (combatEnhancingDrugsCount.HasValue) def.combatEnhancingDrugsCount = combatEnhancingDrugsCount.Value;
        if (extraSkillLevels.HasValue) def.extraSkillLevels = extraSkillLevels.Value;
        if (minTotalSkillLevels.HasValue) def.minTotalSkillLevels = minTotalSkillLevels.Value;
        if (minBestSkillLevel.HasValue) def.minBestSkillLevel = minBestSkillLevel.Value;
        if (isGoodPsychicRitualInvoker.HasValue) def.isGoodPsychicRitualInvoker = isGoodPsychicRitualInvoker.Value;
        if (apparelIgnoreSeasons.HasValue) def.apparelIgnoreSeasons = apparelIgnoreSeasons.Value;
        if (apparelIgnorePollution.HasValue) def.apparelIgnorePollution = apparelIgnorePollution.Value;
        if (ignoreFactionApparelStuffRequirements.HasValue) def.ignoreFactionApparelStuffRequirements = ignoreFactionApparelStuffRequirements.Value;
        if (ignoreIdeoApparelColors.HasValue) def.ignoreIdeoApparelColors = ignoreIdeoApparelColors.Value;
        if (trader.HasValue) def.trader = trader.Value;
        if (generateInitialNonFamilyRelations.HasValue) def.generateInitialNonFamilyRelations = generateInitialNonFamilyRelations.Value;
    }

    public static bool AvailableIfHuman(TweakData data) => data.propType switch
    {
        (int)RaceType.Humanlike => true,
        _ => false,
    };

    public static bool AvailableIfMech(TweakData data) => data.propType switch
    {
        (int)RaceType.Mechanoid => true,
        _ => false,
    };

    public static bool AvailableIfLive(TweakData data) => data.propType switch
    {
        (int)RaceType.Mechanoid => false,
        _ => true,
    };
    public static bool AvailableIfBeast(TweakData data) => data.propType switch
    {
        (int)RaceType.Animal => true,
        (int)RaceType.Insect => true,
        _ => false,
    };

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(RaceType).GetEnumNames().Select(s => "MST." + s).ToList();

    public override int GetPropType()
    {
        if (this.def is PawnKindDef def && def.race?.defName != null)
        {
            var id = TweakDatabase.raceDatas.TryGetValue(def.race.defName);
            var l = GetAllData(id);
            if (l.Count > 0)
            {
                var f = l[0];
                Race = f.id;
                foreach (var item in l)
                {
                    ((RaceData)item).PawnKind = this.id;
                }
                return f.propType;
            }
        }
        return (int)RaceType.Other;
    }
}
