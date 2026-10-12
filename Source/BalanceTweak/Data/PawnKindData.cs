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
            if (!IsFieldModified(nameof(weaponTags))) weaponTags ??= d.weaponTags;
            if (!IsFieldModified(nameof(apparelTags))) apparelTags ??= d.apparelTags;
            if (!IsFieldModified(nameof(apparelDisallowTags))) apparelDisallowTags ??= d.apparelDisallowTags;
            if (!IsFieldModified(nameof(techHediffsTags))) techHediffsTags ??= d.techHediffsTags;
            if (!IsFieldModified(nameof(techHediffsDisallowTags))) techHediffsDisallowTags ??= d.techHediffsDisallowTags;

            // Second Priority: List<Def>
            if (!IsFieldModified(nameof(apparelRequired))) apparelRequired ??= d.apparelRequired;
            if (!IsFieldModified(nameof(abilities))) abilities ??= d.abilities;
            if (!IsFieldModified(nameof(disallowedTraits))) disallowedTraits ??= d.disallowedTraits;
            if (!IsFieldModified(nameof(forcedAddictions))) forcedAddictions ??= d.forcedAddictions;
            if (!IsFieldModified(nameof(techHediffsRequired))) techHediffsRequired ??= d.techHediffsRequired;
            if (!IsFieldModified(nameof(titleSelectOne))) titleSelectOne ??= d.titleSelectOne;

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
        ApplyS(nameof(defLabel), v => this.def.label = v);
        ApplyV<float>(nameof(combatPower), v => def.combatPower = v);
        ApplyV<bool>(nameof(allowInMechClusters), v => def.allowInMechClusters = v);
        ApplyV<bool>(nameof(collidesWithPawns), v => def.collidesWithPawns = v);
        ApplyN<FloatRange>(nameof(initialWillRange), v => def.initialWillRange = v);
        ApplyN<FloatRange>(nameof(initialResistanceRange), v => def.initialResistanceRange = v);

        ApplyV<FloatRange>(nameof(weaponMoney), v => def.weaponMoney = v);
        ApplyV<FloatRange>(nameof(apparelMoney), v => def.apparelMoney = v);
        ApplyV<FloatRange>(nameof(gearHealthRange), v => def.gearHealthRange = v);
        ApplyV<QualityCategory>(nameof(itemQuality), v => def.itemQuality = v);
        ApplyN<QualityCategory>(nameof(forceWeaponQuality), v => def.forceWeaponQuality = v);
        ApplyV<QualityCategory>(nameof(minApparelQuality), v => def.minApparelQuality = v);
        ApplyV<QualityCategory>(nameof(maxApparelQuality), v => def.maxApparelQuality = v);
        ApplyV<bool>(nameof(forceNormalGearQuality), v => def.forceNormalGearQuality = v);
        ApplyV<float>(nameof(nakedChance), v => def.nakedChance = v);
        ApplyV<float>(nameof(biocodeWeaponChance), v => def.biocodeWeaponChance = v);
        ApplyV<bool>(nameof(destroyGearOnDrop), v => def.destroyGearOnDrop = v);
        ApplyV<bool>(nameof(canStrip), v => def.canStrip = v);
        ApplyV<float>(nameof(apparelAllowHeadgearChance), v => def.apparelAllowHeadgearChance = v);
        ApplyV<bool>(nameof(ignoreApparelAllowChance), v => def.ignoreApparelAllowChance = v);

        ApplyV<int>(nameof(maxPerGroup), v => def.maxPerGroup = v);
        ApplyV<bool>(nameof(canBeScattered), v => def.canBeScattered = v);
        ApplyV<bool>(nameof(appearsRandomlyInCombatGroups), v => def.appearsRandomlyInCombatGroups = v);
        ApplyV<IntRange>(nameof(wildGroupSize), v => def.wildGroupSize = v);
        ApplyV<float>(nameof(ecoSystemWeight), v => def.ecoSystemWeight = v);

        ApplyN<Gender>(nameof(fixedGender), v => def.fixedGender = v);
        ApplyN<FloatRange>(nameof(chronologicalAgeRange), v => def.chronologicalAgeRange = v);
        ApplyV<int>(nameof(minGenerationAge), v => def.minGenerationAge = v);
        ApplyV<int>(nameof(maxGenerationAge), v => def.maxGenerationAge = v);
        ApplyV<bool>(nameof(canOpenAnyDoor), v => def.canOpenAnyDoor = v);
        ApplyV<bool>(nameof(canOpenDoors), v => def.canOpenDoors = v);
        ApplyV<bool>(nameof(isBoss), v => def.isBoss = v);
        ApplyV<bool>(nameof(preventIdeo), v => def.preventIdeo = v);
        ApplyV<float>(nameof(humanPregnancyChance), v => def.humanPregnancyChance = v);
        ApplyV<bool>(nameof(allowOldAgeInjuries), v => def.allowOldAgeInjuries = v);
        ApplyV<bool>(nameof(forceNoDeathNotification), v => def.forceNoDeathNotification = v);
        ApplyV<float>(nameof(controlGroupPortraitZoom), v => def.controlGroupPortraitZoom = v);
        ApplyV<bool>(nameof(showInDebugSpawner), v => def.showInDebugSpawner = v);
        ApplyV<float>(nameof(royalTitleChance), v => def.royalTitleChance = v);
        ApplyV<bool>(nameof(useFactionXenotypes), v => def.useFactionXenotypes = v);
        ApplyV<bool>(nameof(allowRoyalRoomRequirements), v => def.allowRoyalRoomRequirements = v);
        ApplyV<bool>(nameof(allowRoyalApparelRequirements), v => def.allowRoyalApparelRequirements = v);

        // Second Priority: Combat & AI
        ApplyV<bool>(nameof(isFighter), v => def.isFighter = v);
        ApplyV<bool>(nameof(forceDeathOnDowned), v => def.forceDeathOnDowned = v);
        ApplyN<float>(nameof(overrideDeathOnDownedChance), v => def.overrideDeathOnDownedChance = v);
        ApplyV<FloatRange>(nameof(fleeHealthThresholdRange), v => def.fleeHealthThresholdRange = v);
        ApplyV<bool>(nameof(ignoresPainShock), v => def.ignoresPainShock = v);
        ApplyV<bool>(nameof(canMeleeAttack), v => def.canMeleeAttack = v);
        ApplyV<bool>(nameof(aiAvoidCover), v => def.aiAvoidCover = v);
        ApplyV<bool>(nameof(canBeSapper), v => def.canBeSapper = v);
        ApplyV<bool>(nameof(isGoodBreacher), v => def.isGoodBreacher = v);
        ApplyV<float>(nameof(defendPointRadius), v => def.defendPointRadius = v);
        ApplyV<bool>(nameof(canArriveManhunter), v => def.canArriveManhunter = v);
        ApplyV<bool>(nameof(immuneToTraps), v => def.immuneToTraps = v);
        ApplyV<bool>(nameof(immuneToGameConditionEffects), v => def.immuneToGameConditionEffects = v);

        // Second Priority: Capture & Prison
        ApplyV<float>(nameof(basePrisonBreakMtbDays), v => def.basePrisonBreakMtbDays = v);
        ApplyV<float>(nameof(acceptArrestChanceFactor), v => def.acceptArrestChanceFactor = v);
        ApplyV<bool>(nameof(skipResistant), v => def.skipResistant = v);

        // Second Priority: Faction & Hostility
        ApplyV<bool>(nameof(factionHostileOnKill), v => def.factionHostileOnKill = v);
        ApplyV<bool>(nameof(factionHostileOnDeath), v => def.factionHostileOnDeath = v);
        ApplyV<bool>(nameof(hostileToAll), v => def.hostileToAll = v);
        ApplyV<bool>(nameof(factionLeader), v => def.factionLeader = v);

        // Second Priority: List<string>
        ApplyL<string>(nameof(weaponTags), v => def.weaponTags = v);
        ApplyL<string>(nameof(apparelTags), v => def.apparelTags = v);
        ApplyL<string>(nameof(apparelDisallowTags), v => def.apparelDisallowTags = v);
        ApplyL<string>(nameof(techHediffsTags), v => def.techHediffsTags = v);
        ApplyL<string>(nameof(techHediffsDisallowTags), v => def.techHediffsDisallowTags = v);

        // Second Priority: List<Def>
        ApplyL<ThingDef>(nameof(apparelRequired), v => def.apparelRequired = v);
        ApplyL<AbilityDef>(nameof(abilities), v => def.abilities = v);
        ApplyL<TraitDef>(nameof(disallowedTraits), v => def.disallowedTraits = v);
        ApplyL<ChemicalDef>(nameof(forcedAddictions), v => def.forcedAddictions = v);
        ApplyL<ThingDef>(nameof(techHediffsRequired), v => def.techHediffsRequired = v);
        ApplyL<RoyalTitleDef>(nameof(titleSelectOne), v => def.titleSelectOne = v);

        // Second Priority: Def Selector
        ApplyD<FactionDef>(nameof(defaultFactionDef), v => def.defaultFactionDef = v);
        ApplyD<ThingDef>(nameof(weaponStuffOverride), v => def.weaponStuffOverride = v);
        ApplyD<ThingDef>(nameof(invFoodDef), v => def.invFoodDef = v);
        ApplyD<RoyalTitleDef>(nameof(titleRequired), v => def.titleRequired = v);
        ApplyD<RoyalTitleDef>(nameof(minTitleRequired), v => def.minTitleRequired = v);
        ApplyD<MutantDef>(nameof(mutant), v => def.mutant = v);
        ApplyD<ColorDef>(nameof(favoriteColor), v => def.favoriteColor = v);

        // Second Priority: Numeric
        ApplyV<FloatRange>(nameof(techHediffsMoney), v => def.techHediffsMoney = v);
        ApplyV<float>(nameof(techHediffsChance), v => def.techHediffsChance = v);
        ApplyV<int>(nameof(techHediffsMaxAmount), v => def.techHediffsMaxAmount = v);
        ApplyV<float>(nameof(invNutrition), v => def.invNutrition = v);
        ApplyV<float>(nameof(chemicalAddictionChance), v => def.chemicalAddictionChance = v);
        ApplyV<float>(nameof(combatEnhancingDrugsChance), v => def.combatEnhancingDrugsChance = v);
        ApplyV<IntRange>(nameof(combatEnhancingDrugsCount), v => def.combatEnhancingDrugsCount = v);
        ApplyV<int>(nameof(extraSkillLevels), v => def.extraSkillLevels = v);
        ApplyV<int>(nameof(minTotalSkillLevels), v => def.minTotalSkillLevels = v);
        ApplyV<int>(nameof(minBestSkillLevel), v => def.minBestSkillLevel = v);
        ApplyV<bool>(nameof(isGoodPsychicRitualInvoker), v => def.isGoodPsychicRitualInvoker = v);
        ApplyV<bool>(nameof(apparelIgnoreSeasons), v => def.apparelIgnoreSeasons = v);
        ApplyV<bool>(nameof(apparelIgnorePollution), v => def.apparelIgnorePollution = v);
        ApplyV<bool>(nameof(ignoreFactionApparelStuffRequirements), v => def.ignoreFactionApparelStuffRequirements = v);
        ApplyV<bool>(nameof(ignoreIdeoApparelColors), v => def.ignoreIdeoApparelColors = v);
        ApplyV<bool>(nameof(trader), v => def.trader = v);
        ApplyV<bool>(nameof(generateInitialNonFamilyRelations), v => def.generateInitialNonFamilyRelations = v);
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
