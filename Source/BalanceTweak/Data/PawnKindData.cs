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

    // === 战斗力参考值（只读派生列，见 CombatPower.cs / Docs/CombatPower_Design.md） ===
    // 攻击 = 近战 / 远程取高值；防御 = 核心血量 ÷ 护甲减伤 ÷ 体型；总体 = sqrt(攻击 × 防御)。
    // 三者都以「该类别的锚单位 = 100」为刻度：动物/昆虫锚巨兽，机械锚蜈蚣·爆裂。
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfCombatRated))]
    public float? attackPower = null;
    [TweakField(DataType = ColumnDataType.Display, Available = nameof(AvailableIfCombatRated))]
    public float? defensePower = null;
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int, Available = nameof(AvailableIfCombatRated))]
    public float? overallPower = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? collidesWithPawns = null;
    [TweakField(Style = ColumnStyle.Bool, Available = nameof(AvailableIfMech))]
    public bool? allowInMechClusters = null;

    /// <summary>技能等级区间（<c>PawnKindDef.skills</c>）：每项 = 技能 + 生成时的等级范围。</summary>
    [TweakField(Style = ColumnStyle.SkillRangeList)]
    public List<SkillRange>? skills = null;


    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? initialWillRange = null;
    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHuman))]
    public FloatRange? initialResistanceRange = null;

    [TweakField(Style = ColumnStyle.Range, Available = nameof(AvailableIfHumanOrMech))]
    public FloatRange? weaponMoney = null;

    /// <summary>
    /// 手持武器反查入口（只读触发列）。
    /// <para>
    /// 兵种身上<b>没有</b>武器这个字段 —— 原版是按 <c>weaponTags</c> + <c>weaponMoney</c> 现抽一把，
    /// 所以"能拿哪些枪"只能反查：扫全库武器，逐个套 <c>PawnWeaponGenerator</c> 的判定。
    /// 字段本身不存也不读任何值，窗口打开时由 <see cref="FindWeaponLinks"/> 实时计算。
    /// </para>
    /// </summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.PawnWeaponLinks,
        Hidden = nameof(HiddenIfNoWeaponTags))]
    public TweakID? weaponLinks = null;

    /// <summary>没有武器标签的兵种（动物、昆虫、徒手机械）没有可列的候选武器 → 该格留白。</summary>
    public static bool HiddenIfNoWeaponTags(TweakData data)
        => data.def is PawnKindDef kind && kind.weaponTags.NullOrEmpty();
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
    [TweakField(Style = ColumnStyle.StringList, Available = nameof(AvailableIfHumanOrMech))]
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
                // 只借图标，不借 desc：RaceData.desc 的第一行是**种族的** defName，
                // 直接拿来会让兵种行的悬浮提示显示成 race 的 defName。
                // 兵种自己没有描述时，取种族的描述正文（不含 defName 那一行）。
                string body = d.description.NullOrEmpty()
                    ? d.race?.description ?? ""
                    : d.description;
                desc = $"{d.defName}\n{body}";
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

            // Skills：技能等级区间（元素是 private 字段的 SkillRange，编解码走字段表）
            if (!IsFieldModified(nameof(skills))) skills ??= d.skills;

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
        ApplyDefLabel(nameof(defLabel), def);
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

        // Skills
        ApplyL<SkillRange>(nameof(skills), v => def.skills = v);

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

        // 参考列按"当前 def 现算"：改完种族 / 招式 / 武器装备后，下一次读就该是新值。
        displayValuesEnsured = false;
    }

    /// <summary>
    /// 战斗力参考值三列。计算推迟到 UI 第一次要它们时 —— 那时 def 已经是 Apply 之后的值。
    /// 详见 <see cref="CombatPower"/>。
    /// </summary>
    public override void EnsureDisplayValues()
    {
        if (displayValuesEnsured) return;
        // def 还没绑定就先别算，也别置标志：那一刻算出来的 null 会被永久冻住。
        if (def == null) return;
        displayValuesEnsured = true;
        CombatPower.Fill(this);
    }

    public static bool AvailableIfHuman(TweakData data) => data.propType switch
    {
        (int)RaceType.Humanlike => true,
        _ => false,
    };

    /// <summary>武器标签 / 武器预算：人类与机械共用（机械没有 ThingDef 侧的武器字段，只能在这里改）。</summary>
    public static bool AvailableIfHumanOrMech(TweakData data) => data.propType switch
    {
        (int)RaceType.Humanlike => true,
        (int)RaceType.Mechanoid => true,
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

    /// <summary>
    /// 战斗力参考值：全部兵种适用。
    /// <para>
    /// 谓词是按页签第一行求值的（<c>TweakUtility</c> 只用 firstData 调一次），所以不能在这里
    /// 按行返回 false —— 那会让整个列在"第一行恰好是人形"时消失。
    /// </para>
    /// </summary>
    public static bool AvailableIfCombatRated(TweakData data) => true;

    /// <summary>
    /// 该兵种可能抽到的手持武器（供 <see cref="TweakLinksWindow"/> 使用）。
    /// <para>
    /// 判定逐条对齐 <c>PawnWeaponGenerator.TryGenerateWeaponFor</c>：
    /// 本兵种的某个标签出现在武器标签里、价格不超过 <c>weaponMoney.max</c>、
    /// 材料被 <c>weaponStuffOverride</c> 钉死时这把武器能用上那种材料。
    /// 预算取<b>上界</b>而不是随机掷一次 —— 这里要的是"最宽的可抽范围"，掷一次只会随机砍掉一半候选。
    /// </para>
    /// <para>
    /// 只在窗口打开时调用一次，故不做缓存：数据可能刚被改过（改了标签 / 预算，这份列表就该立刻变）。
    /// 市价走 <see cref="CombatPower.MarketValue"/> 的缓存，与攻击列用同一份口径。
    /// </para>
    /// </summary>
    public static List<TweakID> FindWeaponLinks(PawnKindDef kind)
        => kind == null ? [] : CollectWeaponLinks([kind]);

    /// <summary>
    /// 一组兵种能抽到的全部手持武器（去重，按市价升序）。种族页用它把所有关联兵种并起来。
    /// <para>
    /// 只扫<b>一遍</b>武器库，每把武器问"这组兵种里有没有人抽得到它" —— 逐兵种各扫一遍在 Human
    /// （125 个兵种）上会是十几万次重复遍历。判定看 <see cref="Takes"/>。
    /// </para>
    /// </summary>
    internal static List<TweakID> CollectWeaponLinks(List<PawnKindDef> kinds)
    {
        var result = new List<TweakID>();
        if (kinds.NullOrEmpty()) return result;

        var picked = new List<(float price, TweakID id)>();
        foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            // 候选池的构成条件（PawnWeaponGenerator.Reset 里的 IsWeapon）：主手装备 + 有武器标签。
            if (thing.equipmentType != EquipmentType.Primary) continue;
            if (thing.weaponTags.NullOrEmpty()) continue;
            // generateAllowChance == 0 表示永不自然生成（独特变体），抽不到，也就不算候选。
            if (thing.generateAllowChance <= 0f) continue;

            PawnKindDef? owner = null;
            for (int i = 0; i < kinds.Count; i++)
            {
                if (!Takes(kinds[i], thing)) continue;
                owner = kinds[i];
                break;
            }
            if (owner == null) continue;

            var id = new TweakID(thing.defName, SettingType.Weapon);
            // 只列真的在武器页签里有数据行的 —— 否则点了跳不过去，等于给个死链接。
            if (TweakDatabase.GetOriginal(id) == null) continue;
            picked.Add((PriceOf(owner, thing), id));
        }

        // 按价格升序：一眼能看出这个兵种的装备档次，也方便对照 weaponMoney。
        picked.Sort((a, b) => a.price.CompareTo(b.price));
        for (int i = 0; i < picked.Count; i++) result.Add(picked[i].id);
        return result;
    }

    /// <summary>
    /// 这个兵种抽不抽得到这把武器。逐条对齐 <c>PawnWeaponGenerator.TryGenerateWeaponFor</c>：
    /// 标签交集、材料锁定时能用上那种材料、价格不超过 <c>weaponMoney.max</c>。
    /// <para>
    /// 预算取<b>上界</b>而不是随机掷一次 —— 这里要的是"最宽的可抽范围"，掷一次只会随机砍掉一半候选。
    /// </para>
    /// </summary>
    private static bool Takes(PawnKindDef kind, ThingDef thing)
    {
        var tags = kind.weaponTags;
        if (tags.NullOrEmpty()) return false;
        if (!tags.Any(tag => thing.weaponTags.Contains(tag))) return false;

        // 材料钉死时，原版只保留"材料正好是它"的那些 pair。这里对应判"这把武器用不用得上这种材料"：
        // 用不上（没有材料类别、或类别不匹配）就一对都凑不出来，等于抽不到。
        var stuff = kind.weaponStuffOverride;
        if (stuff != null)
        {
            var cats = stuff.stuffProps?.categories;
            if (cats == null || thing.stuffCategories.NullOrEmpty()) return false;
            if (!thing.stuffCategories.Any(cats.Contains)) return false;
        }

        return PriceOf(kind, thing) <= kind.weaponMoney.max;
    }

    /// <summary>
    /// 这把武器在该兵种手上的市价。材料被钉死时要按那种材料算，否则"预算够不够"会判错；
    /// 这条路径少见，不值得另开一份缓存。
    /// </summary>
    private static float PriceOf(PawnKindDef kind, ThingDef thing)
    {
        var stuff = kind.weaponStuffOverride;
        return stuff != null
            ? StatWorker_MarketValue.CalculatedBaseMarketValue(thing, stuff)
            : CombatPower.MarketValue(thing);
    }

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
