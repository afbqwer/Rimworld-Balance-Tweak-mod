#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using LudeonTK;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 序列化/反序列化关键路径的单元测试
/// 测试策略：模拟 Scribe 加载后的 _savedLoadData 状态，调用 ResolveDefs() 验证结果。
/// </summary>
[StaticConstructorOnStartup]
public static class TweakDataSerializationTests
{
    static TweakDataSerializationTests(){
        // 取消注释以在游戏启动时运行测试
        //LongEventHandler.QueueLongEvent(RunSerializationTests, "BalanceTweak testing", doAsynchronously: false, null);
    }

    private static void RunSerializationTests()
    {
        int passed = 0, failed = 0;

        // ─── ResolveDef 测试 ───
        RunTest("ResolveDef 有效 Def", () => testResolveDef_Valid(), ref passed, ref failed);
        RunTest("ResolveDef 无效 Def → 返回 false", () => testResolveDef_Invalid(), ref passed, ref failed);
        RunTest("ResolveDef null/空 defName → 保持 null", () => testResolveDef_Null(), ref passed, ref failed);
        RunTest("ResolveDef 无 _savedLoadData → 返回 true", () => testResolveDef_NoSavedData(), ref passed, ref failed);

        // ─── ResolveDefList 测试 ───
        RunTest("ResolveDefList 有效列表", () => testResolveDefList_Valid(), ref passed, ref failed);
        RunTest("ResolveDefList 空列表 → 保持 null", () => testResolveDefList_Empty(), ref passed, ref failed);
        RunTest("ResolveDefList 含无效 defName → 返回 false", () => testResolveDefList_InvalidItem(), ref passed, ref failed);

        // ─── ResolveStatModifierList 测试 ───
        RunTest("ResolveStatModifierList 有效数据", () => testResolveStatModList_Valid(), ref passed, ref failed);
        RunTest("ResolveStatModifierList 无效 stat → 返回 false", () => testResolveStatModList_InvalidStat(), ref passed, ref failed);
        RunTest("ResolveStatModifierList 空列表 → 保持 null", () => testResolveStatModList_Empty(), ref passed, ref failed);

        // ─── ResolvePawnCapacityList 测试 ───
        RunTest("ResolvePawnCapacityList 有效数据", () => testResolvePawnCapList_Valid(), ref passed, ref failed);
        RunTest("ResolvePawnCapacityList 无效 capacity → 返回 false", () => testResolvePawnCapList_Invalid(), ref passed, ref failed);
        RunTest("ResolvePawnCapacityList 完整格式（含 statFactorMod）", () => testResolvePawnCapList_FullFormat(), ref passed, ref failed);

        // ─── ResolveDamageFactorList 测试 ───
        RunTest("ResolveDamageFactorList 有效数据", () => testResolveDamageFactorList_Valid(), ref passed, ref failed);
        RunTest("ResolveDamageFactorList 无效 damageDef → 返回 false", () => testResolveDamageFactorList_Invalid(), ref passed, ref failed);

        // ─── ResolveSkillGainList 测试 ───
        RunTest("ResolveSkillGainList 有效数据", () => testResolveSkillGainList_Valid(), ref passed, ref failed);
        RunTest("ResolveSkillGainList 无效 skill → 返回 false", () => testResolveSkillGainList_Invalid(), ref passed, ref failed);

        // ─── ResolveAptitudeList 测试 ───
        RunTest("ResolveAptitudeList 有效数据", () => testResolveAptitudeList_Valid(), ref passed, ref failed);
        RunTest("ResolveAptitudeList 无效 skill → 返回 false", () => testResolveAptitudeList_Invalid(), ref passed, ref failed);

        // ─── ResolveGeneticTraitDataList 测试 ───
        RunTest("ResolveGeneticTraitDataList 有效数据", () => testResolveGeneticTraitList_Valid(), ref passed, ref failed);
        RunTest("ResolveGeneticTraitDataList 无效 trait → 返回 false", () => testResolveGeneticTraitList_Invalid(), ref passed, ref failed);

        // ─── ResolveThingDefCountList 测试 ───
        RunTest("ResolveThingDefCountList 有效数据（无 stuff）", () => testResolveThingDefCountList_Valid(), ref passed, ref failed);
        RunTest("ResolveThingDefCountList 含 stuff", () => testResolveThingDefCountList_WithStuff(), ref passed, ref failed);
        RunTest("ResolveThingDefCountList 无效 thingDef → 返回 false", () => testResolveThingDefCountList_Invalid(), ref passed, ref failed);

        // ─── ResolveSkillReqList 测试 ───
        RunTest("ResolveSkillReqList 有效数据", () => testResolveSkillReqList_Valid(), ref passed, ref failed);
        RunTest("ResolveSkillReqList 无效 skill → 返回 false", () => testResolveSkillReqList_Invalid(), ref passed, ref failed);

        // ─── ResolveIngredientList 测试 ───
        RunTest("ResolveIngredientList 有效数据", () => testResolveIngredientList_Valid(), ref passed, ref failed);
        RunTest("ResolveIngredientList 无效格式 → 跳过单条", () => testResolveIngredientList_BadFormat(), ref passed, ref failed);

        // ─── ResolveIngredientFilter 测试 ───
        RunTest("ResolveIngredientFilter 有效数据", () => testResolveIngredientFilter_Valid(), ref passed, ref failed);
        RunTest("ResolveIngredientFilter null/空 → 返回 true", () => testResolveIngredientFilter_Null(), ref passed, ref failed);

        // ─── ResolveIntList 测试 ───
        RunTest("ResolveIntList 有效数据", () => testResolveIntList_Valid(), ref passed, ref failed);
        RunTest("ResolveIntList 空列表 → 保持 null", () => testResolveIntList_Empty(), ref passed, ref failed);

        // ─── ResolveProcessIngredientList 测试 ───
        RunTest("ResolveProcessIngredientList 有效数据（仅 thing）", () => testResolveProcessIngredientList_Valid(), ref passed, ref failed);
        RunTest("ResolveProcessIngredientList 含 disallowed", () => testResolveProcessIngredientList_WithDisallowed(), ref passed, ref failed);

        // ─── ResolveProcessResultList 测试 ───
        RunTest("ResolveProcessResultList 有效数据", () => testResolveProcessResultList_Valid(), ref passed, ref failed);

        // ─── 备份/回滚测试 ───
        RunTest("ResolveDefs 单字段失败 → 回滚所有字段", () => testBackupRestore_SingleFieldFail(), ref passed, ref failed);
        RunTest("ResolveDefs 多类型字段，单个失败 → 全部回滚", () => testBackupRestore_MixedTypes(), ref passed, ref failed);
        RunTest("ResolveDefs 无失败 → 正常保留已解析字段", () => testBackupRestore_NoFailure(), ref passed, ref failed);

        // ─── BackCompat PostLoadInit 字段跟踪测试 ───
        RunTest("BackCompat PostLoadInit 跟踪简单类型字段", () => testBackCompat_TracksSimpleFields(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 跟踪 Def 字段", () => testBackCompat_TracksDefField(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 跟踪列表类型字段", () => testBackCompat_TracksListFields(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 混合类型全部跟踪", () => testBackCompat_TracksAllMixedTypes(), ref passed, ref failed);

        Log.Message("[BalanceTweak 测试] 完成.");
        if (failed > 0)
            Log.Warning($"[BalanceTweak 测试] 通过: {passed}, 失败: {failed}");
        else
            Log.Message($"[BalanceTweak 测试] 全部通过 ({passed})");
    }

    // ═══════════════════════ ResolveDef 测试 ═══════════════════════

    /// <summary>ResolveDef: 设置有效 defName → 应正确解析为 Def 对象</summary>
    private static void testResolveDef_Valid()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "Shooting"
        });
        bool ok = gene.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(gene.passionModSkill != null, "passionModSkill 不应为 null");
        Assert(gene.passionModSkill?.defName == "Shooting", $"应解析为 Shooting，实际为 {gene.passionModSkill?.defName}");
    }

    /// <summary>ResolveDef: 设置无效 defName → 应返回 false</summary>
    private static void testResolveDef_Invalid()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "NonExistentSkillDef_12345"
        });
        bool ok = gene.ResolveDefs();
        Assert(!ok, "ResolveDefs 应返回 false");
    }

    /// <summary>ResolveDef: 设置 null/空 defName → 应保持字段为 null</summary>
    private static void testResolveDef_Null()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = ""
        });
        bool ok = gene.ResolveDefs();
        Assert(ok, "空 defName 应返回 true（保持 null）");
        Assert(gene.passionModSkill == null, "passionModSkill 应为 null");

        // 验证 null 值（string? 类型为 null 而非空字符串）
        var gene2 = new GeneData();
        SetSavedLoadData(gene2, new Dictionary<string, object?>
        {
            ["passionModSkill"] = null
        });
        bool ok2 = gene2.ResolveDefs();
        Assert(ok2, "null defName 应返回 true（跳過）");
        Assert(gene2.passionModSkill == null, "passionModSkill 应为 null");
    }

    /// <summary>ResolveDef: _savedLoadData 为 null → 立即返回 true</summary>
    private static void testResolveDef_NoSavedData()
    {
        var gene = new GeneData();
        // 不设置 _savedLoadData
        bool ok = gene.ResolveDefs();
        Assert(ok, "_savedLoadData 为 null 时应返回 true");
    }

    // ═══════════════════════ ResolveDefList 测试 ═══════════════════════

    /// <summary>ResolveDefList: 有效 defNames 列表 → 应正确解析</summary>
    private static void testResolveDefList_Valid()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string> { "EntitySkip", "Bloodfeed" }
        });
        bool ok = gene.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(gene.abilities != null, "abilities 不应为 null");
        Assert(gene.abilities?.Count == 2, $"应包含 2 项，实际 {gene.abilities?.Count}");
        Assert(gene.abilities?[0].defName == "EntitySkip", $"第一项应为 EntitySkip，实际 {gene.abilities?[0].defName}");
        Assert(gene.abilities?[1].defName == "Bloodfeed", $"第二项应为 Bloodfeed，实际 {gene.abilities?[1].defName}");
    }

    /// <summary>ResolveDefList: 空列表 → 保持 null</summary>
    private static void testResolveDefList_Empty()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string>()
        });
        bool ok = gene.ResolveDefs();
        Assert(ok, "空列表应返回 true");
        Assert(gene.abilities == null, "abilities 应为 null（空列表不设置）");
    }

    /// <summary>ResolveDefList: 含无效 defName → 返回 false</summary>
    private static void testResolveDefList_InvalidItem()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string> { "EntitySkip", "NonExistentAbility_XYZ" }
        });
        bool ok = gene.ResolveDefs();
        Assert(!ok, "含无效 defName 时应返回 false");
    }

    // ═══════════════════════ ResolveStatModifierList 测试 ═══════════════════════

    /// <summary>ResolveStatModifierList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveStatModList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["statOffsets"] = new List<string> { "MoveSpeed|0.2", "MeleeDamageFactor|1.5" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(hediff.statOffsets != null, "statOffsets 不应为 null");
        Assert(hediff.statOffsets?.Count == 2, $"应包含 2 项，实际 {hediff.statOffsets?.Count}");
        Assert(hediff.statOffsets?[0].stat.defName == "MoveSpeed", $"第一项 stat 应为 MoveSpeed，实际 {hediff.statOffsets?[0].stat?.defName}");
        Assert(Math.Abs(hediff.statOffsets![0].value - 0.2f) < 0.001f, $"第一项 value 应为 0.2");
        Assert(hediff.statOffsets?[1].stat.defName == "MeleeDamageFactor", $"第二项 stat 应为 MeleeDamageFactor");
        Assert(Math.Abs(hediff.statOffsets![1].value - 1.5f) < 0.001f, $"第二项 value 应为 1.5");
    }

    /// <summary>ResolveStatModifierList: 无效 stat def → 返回 false</summary>
    private static void testResolveStatModList_InvalidStat()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["statOffsets"] = new List<string> { "NonExistentStat_ABC|0.5" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(!ok, "无效 stat 时应返回 false");
    }

    /// <summary>ResolveStatModifierList: 空列表 → 保持 null</summary>
    private static void testResolveStatModList_Empty()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["statOffsets"] = new List<string>()
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "空列表应返回 true");
        Assert(hediff.statOffsets == null, "statOffsets 应为 null");
    }

    // ═══════════════════════ ResolvePawnCapacityList 测试 ═══════════════════════

    /// <summary>ResolvePawnCapacityList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolvePawnCapList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = new List<string> { "Consciousness|0|0.5|1||" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(hediff.capMods != null, "capMods 不应为 null");
        Assert(hediff.capMods?.Count == 1, $"应包含 1 项，实际 {hediff.capMods?.Count}");
        Assert(hediff.capMods?[0].capacity.defName == "Consciousness", $"capacity 应为 Consciousness，实际 {hediff.capMods![0].capacity.defName}");
        Assert(Math.Abs(hediff.capMods[0].offset) < 0.001f, "offset 应为 0");
        Assert(Math.Abs(hediff.capMods[0].setMax - 0.5f) < 0.001f, "setMax 应为 0.5");
        Assert(Math.Abs(hediff.capMods[0].postFactor - 1f) < 0.001f, "postFactor 应为 1（默认）");
        Assert(hediff.capMods[0].statFactorMod == null, "statFactorMod 应为 null（留空）");
        Assert(hediff.capMods[0].setMaxCurveEvaluateStat == null, "setMaxCurveEvaluateStat 应为 null（留空）");
    }

    /// <summary>ResolvePawnCapacityList: 无效 capacity → 返回 false</summary>
    private static void testResolvePawnCapList_Invalid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = new List<string> { "NonExistentCapacity_XYZ|0|0.5|1||" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(!ok, "无效 capacity 时应返回 false");
    }

    /// <summary>ResolvePawnCapacityList: 完整 6 段格式（含 statFactorMod + setMaxCurveEvaluateStat）</summary>
    private static void testResolvePawnCapList_FullFormat()
    {
        var hediff = new HediffStageData();
        // 格式: capacity|offset|setMax|postFactor|statFactorMod|setMaxCurveEvaluateStat
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = new List<string> { "Moving|0.2|-1|0.8|MoveSpeed|ShootingAccuracyPawn" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "完整格式 ResolveDefs 应返回 true");
        Assert(hediff.capMods != null, "capMods 不应为 null");
        Assert(hediff.capMods?.Count == 1, $"应包含 1 项，实际 {hediff.capMods?.Count}");
        Assert(hediff.capMods?[0].capacity.defName == "Moving", $"capacity 应为 Moving");
        Assert(Math.Abs(hediff.capMods![0].offset - 0.2f) < 0.001f, "offset 应为 0.2");
        Assert(Math.Abs(hediff.capMods[0].setMax - (-1f)) < 0.001f, "setMax 应为 -1");
        Assert(Math.Abs(hediff.capMods[0].postFactor - 0.8f) < 0.001f, "postFactor 应为 0.8");
        Assert(hediff.capMods[0].statFactorMod != null, "statFactorMod 不应为 null");
        Assert(hediff.capMods[0].statFactorMod?.defName == "MoveSpeed", $"statFactorMod 应为 MoveSpeed，实际 {hediff.capMods[0].statFactorMod?.defName}");
        Assert(hediff.capMods[0].setMaxCurveEvaluateStat != null, "setMaxCurveEvaluateStat 不应为 null");
        Assert(hediff.capMods[0].setMaxCurveEvaluateStat?.defName == "ShootingAccuracyPawn", $"setMaxCurveEvaluateStat 应为 ShootingAccuracyPawn");
    }

    // ═══════════════════════ ResolveDamageFactorList 测试 ═══════════════════════

    /// <summary>ResolveDamageFactorList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveDamageFactorList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["damageFactors"] = new List<string> { "Arrow|0.5", "Bullet|0.75" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(hediff.damageFactors != null, "damageFactors 不应为 null");
        Assert(hediff.damageFactors?.Count == 2, $"应包含 2 项，实际 {hediff.damageFactors?.Count}");
        Assert(hediff.damageFactors?[0].damageDef.defName == "Arrow", $"第一项应为 Arrow，实际 {hediff.damageFactors?[0].damageDef.defName}");
        Assert(Math.Abs(hediff.damageFactors![0].factor - 0.5f) < 0.001f, "第一项 factor 应为 0.5");
        Assert(hediff.damageFactors[1].damageDef.defName == "Bullet", $"第二项应为 Bullet，实际 {hediff.damageFactors?[1].damageDef.defName}");
        Assert(Math.Abs(hediff.damageFactors![1].factor - 0.75f) < 0.001f, "第二项 factor 应为 0.75");
    }

    /// <summary>ResolveDamageFactorList: 无效 damageDef → 返回 false</summary>
    private static void testResolveDamageFactorList_Invalid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["damageFactors"] = new List<string> { "NonExistentDamage_XYZ|0.5" }
        });
        bool ok = hediff.ResolveDefs();
        Assert(!ok, "无效 damageDef 时应返回 false");
    }

    // ═══════════════════════ ResolveSkillGainList 测试 ═══════════════════════

    /// <summary>ResolveSkillGainList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveSkillGainList_Valid()
    {
        var traitDeg = new TraitDegreeNodeData();
        SetSavedLoadData(traitDeg, new Dictionary<string, object?>
        {
            ["skillGains"] = new List<string> { "Shooting|5", "Cooking|3" }
        });
        bool ok = traitDeg.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(traitDeg.skillGains != null, "skillGains 不应为 null");
        Assert(traitDeg.skillGains?.Count == 2, $"应包含 2 项，实际 {traitDeg.skillGains?.Count}");
        Assert(traitDeg.skillGains?[0].skill?.defName == "Shooting", $"第一项 skill 应为 Shooting，实际 {traitDeg.skillGains?[0].skill?.defName}");
        Assert(traitDeg.skillGains?[0].amount == 5, $"第一项 amount 应为 5，实际 {traitDeg.skillGains?[0].amount}");
        Assert(traitDeg.skillGains?[1].skill?.defName == "Cooking", $"第二项 skill 应为 Cooking");
        Assert(traitDeg.skillGains?[1].amount == 3, $"第二项 amount 应为 3");
    }

    /// <summary>ResolveSkillGainList: 无效 skill → 返回 false</summary>
    private static void testResolveSkillGainList_Invalid()
    {
        var traitDeg = new TraitDegreeNodeData();
        SetSavedLoadData(traitDeg, new Dictionary<string, object?>
        {
            ["skillGains"] = new List<string> { "NonExistentSkill_ZZZ|5" }
        });
        bool ok = traitDeg.ResolveDefs();
        Assert(!ok, "无效 skill 时应返回 false");
    }

    // ═══════════════════════ ResolveAptitudeList 测试 ═══════════════════════

    /// <summary>ResolveAptitudeList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveAptitudeList_Valid()
    {
        var traitDeg = new TraitDegreeNodeData();
        SetSavedLoadData(traitDeg, new Dictionary<string, object?>
        {
            ["aptitudes"] = new List<string> { "Shooting|2", "Crafting|1" }
        });
        bool ok = traitDeg.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(traitDeg.aptitudes != null, "aptitudes 不应为 null");
        Assert(traitDeg.aptitudes?.Count == 2, $"应包含 2 项，实际 {traitDeg.aptitudes?.Count}");
        Assert(traitDeg.aptitudes?[0].skill?.defName == "Shooting", $"第一项 skill 应为 Shooting，实际 {traitDeg.aptitudes?[0].skill?.defName}");
        Assert(traitDeg.aptitudes?[0].level == 2, $"第一项 level 应为 2，实际 {traitDeg.aptitudes?[0].level}");
        Assert(traitDeg.aptitudes?[1].skill?.defName == "Crafting", $"第二项 skill 应为 Crafting");
        Assert(traitDeg.aptitudes?[1].level == 1, $"第二项 level 应为 1");
    }

    /// <summary>ResolveAptitudeList: 无效 skill → 返回 false</summary>
    private static void testResolveAptitudeList_Invalid()
    {
        var traitDeg = new TraitDegreeNodeData();
        SetSavedLoadData(traitDeg, new Dictionary<string, object?>
        {
            ["aptitudes"] = new List<string> { "NonExistentSkill_ZZZ|1" }
        });
        bool ok = traitDeg.ResolveDefs();
        Assert(!ok, "无效 skill 时应返回 false");
    }

    // ═══════════════════════ ResolveGeneticTraitDataList 测试 ═══════════════════════

    /// <summary>ResolveGeneticTraitDataList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveGeneticTraitList_Valid()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["forcedTraits"] = new List<string> { "Kind|0", "Industriousness|2" }
        });
        bool ok = gene.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(gene.forcedTraits != null, "forcedTraits 不应为 null");
        Assert(gene.forcedTraits?.Count == 2, $"应包含 2 项，实际 {gene.forcedTraits?.Count}");
        Assert(gene.forcedTraits?[0].def?.defName == "Kind", $"第一项 trait 应为 Kind，实际 {gene.forcedTraits?[0].def?.defName}");
        Assert(gene.forcedTraits?[0].degree == 0, $"第一项 degree 应为 0，实际 {gene.forcedTraits?[0].degree}");
        Assert(gene.forcedTraits?[1].def?.defName == "Industriousness", $"第二项 trait 应为 Industriousness");
        Assert(gene.forcedTraits?[1].degree == 2, $"第二项 degree 应为 2");
    }

    /// <summary>ResolveGeneticTraitDataList: 无效 trait → 返回 false</summary>
    private static void testResolveGeneticTraitList_Invalid()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["forcedTraits"] = new List<string> { "NonExistentTrait_ZZZ|0" }
        });
        bool ok = gene.ResolveDefs();
        Assert(!ok, "无效 trait 时应返回 false");
    }

    // ═══════════════════════ ResolveThingDefCountList 测试 ═══════════════════════

    /// <summary>ResolveThingDefCountList: 格式 thingDef|stuff|count → 应正确解析</summary>
    private static void testResolveThingDefCountList_Valid()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["products"] = new List<string> { "Steel||10", "Cloth||5" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(recipe.products != null, "products 不应为 null");
        Assert(recipe.products?.Count == 2, $"应包含 2 项，实际 {recipe.products?.Count}");
        Assert(recipe.products?[0].thingDef?.defName == "Steel", $"第一项 thingDef 应为 Steel，实际 {recipe.products?[0].thingDef?.defName}");
        Assert(recipe.products?[0].stuff == null, "第一项 stuff 应为 null（未指定）");
        Assert(recipe.products?[0].count == 10, $"第一项 count 应为 10，实际 {recipe.products?[0].count}");
        Assert(recipe.products?[1].thingDef?.defName == "Cloth", $"第二项 thingDef 应为 Cloth");
        Assert(recipe.products?[1].count == 5, $"第二项 count 应为 5");
    }

    /// <summary>ResolveThingDefCountList: 含 stuff 字段</summary>
    private static void testResolveThingDefCountList_WithStuff()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["products"] = new List<string> { "Plasteel|Steel|5" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(recipe.products != null, "products 不应为 null");
        Assert(recipe.products?.Count == 1, $"应包含 1 项，实际 {recipe.products?.Count}");
        Assert(recipe.products?[0].thingDef?.defName == "Plasteel", $"thingDef 应为 Plasteel");
        Assert(recipe.products?[0].stuff?.defName == "Steel", $"stuff 应为 Steel，实际 {recipe.products?[0].stuff?.defName}");
        Assert(recipe.products?[0].count == 5, $"count 应为 5");
    }

    /// <summary>ResolveThingDefCountList: 无效 thingDef → 返回 false</summary>
    private static void testResolveThingDefCountList_Invalid()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["products"] = new List<string> { "NonExistentThing_XYZ||10" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(!ok, "无效 thingDef 时应返回 false");
    }

    // ═══════════════════════ ResolveSkillReqList 测试 ═══════════════════════

    /// <summary>ResolveSkillReqList: 管道分隔符格式 → 应正确解析</summary>
    private static void testResolveSkillReqList_Valid()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["skillRequirements"] = new List<string> { "Crafting|8", "Medicine|6" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(recipe.skillRequirements != null, "skillRequirements 不应为 null");
        Assert(recipe.skillRequirements?.Count == 2, $"应包含 2 项，实际 {recipe.skillRequirements?.Count}");
        Assert(recipe.skillRequirements?[0].skill?.defName == "Crafting", $"第一项 skill 应为 Crafting，实际 {recipe.skillRequirements?[0].skill?.defName}");
        Assert(recipe.skillRequirements?[0].minLevel == 8, $"第一项 minLevel 应为 8");
        Assert(recipe.skillRequirements?[1].skill?.defName == "Medicine", $"第二项 skill 应为 Medicine");
        Assert(recipe.skillRequirements?[1].minLevel == 6, $"第二项 minLevel 应为 6");
    }

    /// <summary>ResolveSkillReqList: 无效 skill → 返回 false</summary>
    private static void testResolveSkillReqList_Invalid()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["skillRequirements"] = new List<string> { "NonExistentSkill_ZZZ|5" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(!ok, "无效 skill 时应返回 false");
    }

    // ═══════════════════════ ResolveIngredientList 测试 ═══════════════════════

    /// <summary>ResolveIngredientList: 格式 filterStr|count → 应正确解析</summary>
    private static void testResolveIngredientList_Valid()
    {
        var recipe = new RecipeData();
        // 过滤器格式来自 ThingFilterHelper.IngredientFilterToString
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["ingredients"] = new List<string> { "categories=FoodRaw|5", "thingDefs=Steel|10" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(recipe.ingredients != null, "ingredients 不应为 null");
        Assert(recipe.ingredients?.Count > 0, "ingredients 应包含条目");
        // 具体验证：应该至少有一个 ingredient 且 count 正确
        bool foundRawFood = false;
        bool foundSteel = false;
        foreach (var ic in recipe.ingredients!)
        {
            if (Math.Abs(ic.GetBaseCount() - 5f) < 0.001f) foundRawFood = true;
            if (Math.Abs(ic.GetBaseCount() - 10f) < 0.001f) foundSteel = true;
        }
        Assert(foundRawFood, "应找到 count=5 的 IngredientCount（FoodRaw）");
        Assert(foundSteel, "应找到 count=10 的 IngredientCount（Steel）");
    }

    /// <summary>ResolveIngredientList: 无效格式（无管道符）→ 触发污染机制，字段回滚</summary>
    private static void testResolveIngredientList_BadFormat()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["ingredients"] = new List<string> { "categories=FoodRaw|5", "no-pipe-separator" }
        });
        bool ok = recipe.ResolveDefs();
        Assert(!ok, "含无效条目时污染机制应使 ResolveDefs 返回 false");
        Assert(recipe.ingredients == null, "含无效条目时 ingredients 应回滚为 null");
    }

    // ═══════════════════════ ResolveIngredientFilter 测试 ═══════════════════════

    /// <summary>ResolveIngredientFilter: 有效过滤器字符串 → 应正确解析</summary>
    private static void testResolveIngredientFilter_Valid()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["fixedIngredientFilter"] = "categories=Foods; thingDefs=Steel,Plasteel"
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(recipe.fixedIngredientFilter != null, "fixedIngredientFilter 不应为 null");
    }

    /// <summary>ResolveIngredientFilter: null/空字符串 → 返回 true，保持 null</summary>
    private static void testResolveIngredientFilter_Null()
    {
        var recipe = new RecipeData();

        // 空字符串
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["fixedIngredientFilter"] = ""
        });
        bool ok = recipe.ResolveDefs();
        Assert(ok, "空字符串应返回 true");
        Assert(recipe.fixedIngredientFilter == null, "空字符串时 fixedIngredientFilter 应为 null");

        // null 值（不在 _savedLoadData 中）
        var recipe2 = new RecipeData();
        SetSavedLoadData(recipe2, new Dictionary<string, object?>
        {
            ["fixedIngredientFilter"] = null
        });
        bool ok2 = recipe2.ResolveDefs();
        Assert(ok2, "null 值应返回 true");
    }

    // ═══════════════════════ ResolveIntList 测试 ═══════════════════════

    /// <summary>ResolveIntList: int 列表 → 应正确解析（需要 VEF 模组）</summary>
    private static void testResolveIntList_Valid()
    {
        var recipe = new RecipeData();
        // 检查 ticksQuality 字段是否在 Fields 中（取决于 VEF 模组是否加载）
        bool fieldAvailable = false;
        foreach (var meta in RecipeData.Fields)
        {
            if (meta.Field.Name == "ticksQuality") { fieldAvailable = true; break; }
        }

        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["ticksQuality"] = new List<int> { 6000, 12000, 24000 }
        });

        if (!fieldAvailable)
        {
            // VEF 未加载，字段不在 Fields 中 → 验证不崩溃即可
            bool ok = recipe.ResolveDefs();
            Assert(ok, "ResolveDefs 应返回 true（字段不可用，无操作）");
            Log.Message("[BalanceTweak 测试]   ⚠ (ticksQuality 字段因 VEF 未加载不可用，跳过值验证)");
            return;
        }

        bool ok2 = recipe.ResolveDefs();
        Assert(ok2, "ResolveDefs 应返回 true");
        Assert(recipe.ticksQuality != null, "ticksQuality 不应为 null");
        Assert(recipe.ticksQuality?.Count == 3, $"应包含 3 项，实际 {recipe.ticksQuality?.Count}");
        Assert(recipe.ticksQuality?[0] == 6000, "第一项应为 6000");
        Assert(recipe.ticksQuality?[1] == 12000, "第二项应为 12000");
        Assert(recipe.ticksQuality?[2] == 24000, "第三项应为 24000");
    }

    /// <summary>ResolveIntList: 空列表 → 保持 null</summary>
    private static void testResolveIntList_Empty()
    {
        var recipe = new RecipeData();
        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["ticksQuality"] = new List<int>()
        });
        // 即使字段不可用也不应崩溃
        bool ok = recipe.ResolveDefs();
        Assert(ok, "空列表应返回 true（字段不可用时也不崩溃）");
    }

    // ═══════════════════════ ResolveProcessIngredientList 测试 ═══════════════════════

    /// <summary>ResolveProcessIngredientList: 有效数据（仅 thing 或 category）</summary>
    private static void testResolveProcessIngredientList_Valid()
    {
        var recipe = new RecipeData();
        // processIngredients 需要 VEF 模组
        bool fieldAvailable = false;
        foreach (var meta in RecipeData.Fields)
        {
            if (meta.Field.Name == "processIngredients") { fieldAvailable = true; break; }
        }

        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["processIngredients"] = new List<string> { "Steel|||5", "|ResourcesRaw||3" }
        });

        if (!fieldAvailable)
        {
            bool ok = recipe.ResolveDefs();
            Assert(ok, "VEF 未加载时不应崩溃");
            Log.Message("[BalanceTweak 测试]   ⚠ (processIngredients 字段因 VEF 未加载不可用，跳过验证)");
            return;
        }

        bool ok2 = recipe.ResolveDefs();
        Assert(ok2, "ResolveDefs 应返回 true");
        Assert(recipe.processIngredients != null, "processIngredients 不应为 null");
        Assert(recipe.processIngredients?.Count == 2, $"应包含 2 项，实际 {recipe.processIngredients?.Count}");
        Assert(recipe.processIngredients?[0].thing?.defName == "Steel", $"第一项 thing 应为 Steel，实际 {recipe.processIngredients?[0].thing?.defName}");
        Assert(Math.Abs(recipe.processIngredients![0].countNeeded - 5f) < 0.001f, $"第一项 countNeeded 应为 5");
        Assert(recipe.processIngredients[1].thingCategory?.defName == "ResourcesRaw", $"第二项 category 应为 ResourcesRaw，实际 {recipe.processIngredients[1].thingCategory?.defName}");
        Assert(Math.Abs(recipe.processIngredients[1].countNeeded - 3f) < 0.001f, $"第二项 countNeeded 应为 3");
    }

    /// <summary>ResolveProcessIngredientList: 含 disallowedThingDefs</summary>
    private static void testResolveProcessIngredientList_WithDisallowed()
    {
        var recipe = new RecipeData();
        bool fieldAvailable = false;
        foreach (var meta in RecipeData.Fields)
        {
            if (meta.Field.Name == "processIngredients") { fieldAvailable = true; break; }
        }

        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["processIngredients"] = new List<string> { "Steel||WoodLog,Cloth|10" }
        });

        if (!fieldAvailable)
        {
            bool ok = recipe.ResolveDefs();
            Assert(ok, "VEF 未加载时不应崩溃");
            return;
        }

        bool ok2 = recipe.ResolveDefs();
        Assert(ok2, "ResolveDefs 应返回 true");
        Assert(recipe.processIngredients != null, "processIngredients 不应为 null");
        Assert(recipe.processIngredients?.Count == 1, $"应包含 1 项，实际 {recipe.processIngredients?.Count}");
        Assert(recipe.processIngredients?[0].disallowedThingDefs != null, "disallowedThingDefs 不应为 null");
        Assert(recipe.processIngredients?[0].disallowedThingDefs?.Count == 2, $"应包含 2 个 disallowed，实际 {recipe.processIngredients?[0].disallowedThingDefs?.Count}");
        Assert(recipe.processIngredients?[0].disallowedThingDefs?[0].defName == "WoodLog", $"第一项应为 WoodLog");
        Assert(recipe.processIngredients?[0].disallowedThingDefs?[1].defName == "Cloth", $"第二项应为 Cloth");
    }

    // ═══════════════════════ ResolveProcessResultList 测试 ═══════════════════════

    /// <summary>ResolveProcessResultList: 格式 thingDef|count → 应正确解析</summary>
    private static void testResolveProcessResultList_Valid()
    {
        var recipe = new RecipeData();
        bool fieldAvailable = false;
        foreach (var meta in RecipeData.Fields)
        {
            if (meta.Field.Name == "processResults") { fieldAvailable = true; break; }
        }

        SetSavedLoadData(recipe, new Dictionary<string, object?>
        {
            ["processResults"] = new List<string> { "Steel|5", "Plasteel|3" }
        });

        if (!fieldAvailable)
        {
            bool ok = recipe.ResolveDefs();
            Assert(ok, "VEF 未加载时不应崩溃");
            Log.Message("[BalanceTweak 测试]   ⚠ (processResults 字段因 VEF 未加载不可用，跳过验证)");
            return;
        }

        bool ok2 = recipe.ResolveDefs();
        Assert(ok2, "ResolveDefs 应返回 true");
        Assert(recipe.processResults != null, "processResults 不应为 null");
        Assert(recipe.processResults?.Count == 2, $"应包含 2 项，实际 {recipe.processResults?.Count}");
        Assert(recipe.processResults?[0].thing?.defName == "Steel", $"第一项 thing 应为 Steel，实际 {recipe.processResults?[0].thing?.defName}");
        Assert(recipe.processResults?[0].count == 5, $"第一项 count 应为 5");
        Assert(recipe.processResults?[1].thing?.defName == "Plasteel", $"第二项 thing 应为 Plasteel");
        Assert(recipe.processResults?[1].count == 3, $"第二项 count 应为 3");
    }

    // ═══════════════════════ 备份/回滚测试 ═══════════════════════

    /// <summary>
    /// 单个字段解析失败 → 仅回滚失败字段，成功字段保留解析值
    /// </summary>
    private static void testBackupRestore_SingleFieldFail()
    {
        var gene = new GeneData();

        // 一个有效 + 一个无效
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string> { "Bloodfeed" },     // 有效
            ["passionModSkill"] = "NonExistentSkill_XYZ"          // 无效
        });
        bool ok = gene.ResolveDefs();
        Assert(!ok, "ResolveDefs 应返回 false（部分失败）");
        // 修复后：有效字段不再被回滚，abilities 应成功解析
        Assert(gene.abilities != null, "abilities 应成功解析为非 null");
        Assert(gene.abilities?.Count == 1, "abilities 应包含 1 项");
        Assert(gene.abilities?[0].defName == "Bloodfeed", "abilities[0] 应为 Bloodfeed");
        // passionModSkill 解析失败，应为 null
        Assert(gene.passionModSkill == null, "passionModSkill 应为 null（解析失败）");
    }

    /// <summary>
    /// 多种列表类型混合，其中一个无效 → 仅回滚失败字段，成功字段保留解析值
    /// </summary>
    private static void testBackupRestore_MixedTypes()
    {
        var gene = new GeneData();

        // 预设置字段值（模拟 Scribe 已加载了旧值）
        gene.forcedTraits = new List<GeneticTraitData>
        {
            new() { def = DefDatabase<TraitDef>.GetNamedSilentFail("Kind"), degree = 0 }
        };
        gene.statOffsets = new List<StatModifier>
        {
            new() { stat = StatDefOf.MoveSpeed, value = 0.2f }
        };

        // 多个有效字段 + 一个无效
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["statOffsets"] = new List<string> { "MoveSpeed|0.5", "MeleeDamageFactor|2.0" },  // 有效
            ["aptitudes"] = new List<string> { "Shooting|3" },                                 // 有效
            ["abilities"] = new List<string> { "NonExistentAbility_ZZZ" }                      // 无效
        });

        bool ok = gene.ResolveDefs();
        Assert(!ok, "部分失败时应返回 false");

        // 修复后：statOffsets 应成功解析为新值（不再回滚）
        Assert(gene.statOffsets != null, "statOffsets 不应为 null");
        Assert(gene.statOffsets?.Count == 2, "statOffsets 应包含 2 项（新值）");
        Assert(Math.Abs(gene.statOffsets![0].value - 0.5f) < 0.001f, "statOffsets[0].value 应为 0.5（新值）");

        // aptitudes 应成功解析
        Assert(gene.aptitudes != null, "aptitudes 不应为 null（应成功解析）");
        Assert(gene.aptitudes?.Count == 1, "aptitudes 应包含 1 项");
        Assert(gene.aptitudes?[0].skill?.defName == "Shooting", "aptitudes[0].skill 应为 Shooting");

        // abilities 解析失败，应为 null
        Assert(gene.abilities == null, "abilities 应为 null（解析失败）");

        // forcedTraits 保留旧值（未在 _savedLoadData 中，不应受影响）
        Assert(gene.forcedTraits != null, "forcedTraits 应保留旧值（未参与本次解析）");
        Assert(gene.forcedTraits?.Count == 1, "forcedTraits 应保留 1 项");
    }

    /// <summary>
    /// 所有字段解析成功 → 保留解析后的值，不回滚
    /// </summary>
    private static void testBackupRestore_NoFailure()
    {
        var gene = new GeneData();

        // 预设置旧值
        gene.passionModSkill = DefDatabase<SkillDef>.GetNamed("Melee");

        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "Shooting",   // 有效，应覆盖旧值
            ["abilities"] = new List<string> { "EntitySkip" }  // 有效
        });

        bool ok = gene.ResolveDefs();
        Assert(ok, "全部成功时应返回 true");

        // 验证解析后的新值
        Assert(gene.passionModSkill?.defName == "Shooting", $"passionModSkill 应被覆盖为 Shooting，实际为 {gene.passionModSkill?.defName}");
        Assert(gene.abilities?.Count == 1, "abilities 应包含 1 项");
        Assert(gene.abilities?[0].defName == "EntitySkip", "abilities[0] 应为 EntitySkip");
    }

    // ═══════════════════════ BackCompat PostLoadInit 字段跟踪测试 ═══════════════════════

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段：验证简单类型字段（float?, int?, bool?）能被正确跟踪。
    /// 简单类型在 ExposeField LoadingVars 中已通过 SetValue 设置值，value != null 应捕获它们。
    /// </summary>
    private static void testBackCompat_TracksSimpleFields()
    {
        var gene = new GeneData();
        // 预设置字段值（模拟 LoadingVars 通过 ExposeSimple 已设置值）
        gene.selectionWeight = 1.5f;
        gene.biostatMet = 2;
        gene.canGenerateInGeneSet = true;

        // _savedLoadData 中不应包含这些字段（简单类型不通过 _savedLoadData 恢复）
        SetSavedLoadData(gene, new Dictionary<string, object?>());

        // 模拟 BackCompat PostLoadInit 逻辑
        SimulateBackCompatPostLoadInit(gene);

        // 验证简单类型字段被跟踪
        Assert(gene.IsFieldModified("selectionWeight"), "_modifiedFieldNames 应包含 selectionWeight");
        Assert(gene.IsFieldModified("biostatMet"), "_modifiedFieldNames 应包含 biostatMet");
        Assert(gene.IsFieldModified("canGenerateInGeneSet"), "_modifiedFieldNames 应包含 canGenerateInGeneSet");
        Assert(gene.CountFieldModified() >= 3, $"_modifiedFieldNames 应至少有 3 个字段，实际 {gene.CountFieldModified()}");
    }

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段：验证 Def 类型字段（通过 _savedLoadData 恢复）能被正确跟踪。
    /// Def 字段在 ExposeField LoadingVars 中未调用 SetValue（值由 ResolveDefs 设置），
    /// 必须通过 _savedLoadData.ContainsKey 捕获。
    /// </summary>
    private static void testBackCompat_TracksDefField()
    {
        var gene = new GeneData();
        // 设置 _savedLoadData（模拟 LoadingVars 阶段 ExposeDef 存储了 defName）
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "Shooting"
        });
        // 不预设置字段值（模拟 LoadingVars 未调用 SetValue 的状态）

        // 模拟 BackCompat PostLoadInit 逻辑
        SimulateBackCompatPostLoadInit(gene);

        // 验证 Def 字段被跟踪（即使 field value 为 null，但 _savedLoadData 包含它）
        Assert(gene.IsFieldModified("passionModSkill"), $"_modifiedFieldNames 应包含 passionModSkill（通过 _savedLoadData），IsFieldModified 返回 false");
    }

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段：验证列表类型字段（通过 _savedLoadData 恢复）能被正确跟踪。
    /// 列表字段在 ExposeField LoadingVars 中未调用 SetValue（值由 ResolveDefs 设置），
    /// 必须通过 _savedLoadData.ContainsKey 捕获。
    /// </summary>
    private static void testBackCompat_TracksListFields()
    {
        var gene = new GeneData();
        // 设置 _savedLoadData（模拟 LoadingVars 阶段 ExposeList 存储了字符串列表）
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["statOffsets"] = new List<string> { "MoveSpeed|0.2" },
            ["abilities"] = new List<string> { "EntitySkip" },
            ["capMods"] = new List<string> { "Consciousness|0|0.5|1||" },
            ["damageFactors"] = new List<string> { "Arrow|0.5" }
        });

        // 模拟 BackCompat PostLoadInit 逻辑
        SimulateBackCompatPostLoadInit(gene);

        // 验证所有列表类型字段被跟踪
        Assert(gene.IsFieldModified("statOffsets"), "_modifiedFieldNames 应包含 statOffsets（列表）");
        Assert(gene.IsFieldModified("abilities"), "_modifiedFieldNames 应包含 abilities（DefList）");
        Assert(gene.IsFieldModified("capMods"), "_modifiedFieldNames 应包含 capMods（列表）");
        Assert(gene.IsFieldModified("damageFactors"), "_modifiedFieldNames 应包含 damageFactors（列表）");
        Assert(gene.CountFieldModified() >= 4, $"_modifiedFieldNames 应至少有 4 个字段，实际 {gene.CountFieldModified()}");
    }

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段：验证混合类型（简单 + Def + 列表）全部被跟踪。
    /// 简单类型靠 value != null，Def/列表靠 _savedLoadData.ContainsKey，互不遗漏。
    /// </summary>
    private static void testBackCompat_TracksAllMixedTypes()
    {
        var gene = new GeneData();
        // 预设置简单类型字段值
        gene.selectionWeight = 1.0f;
        // 设置 _savedLoadData 含复杂类型
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "Shooting",
            ["statOffsets"] = new List<string> { "MoveSpeed|0.2" },
            ["abilities"] = new List<string> { "EntitySkip" }
        });

        SimulateBackCompatPostLoadInit(gene);

        // 全部应被跟踪
        Assert(gene.IsFieldModified("selectionWeight"), "简单类型 selectionWeight 应被跟踪");
        Assert(gene.IsFieldModified("passionModSkill"), "Def 类型 passionModSkill 应被跟踪（通过 _savedLoadData）");
        Assert(gene.IsFieldModified("statOffsets"), "列表类型 statOffsets 应被跟踪（通过 _savedLoadData）");
        Assert(gene.IsFieldModified("abilities"), "DefList 类型 abilities 应被跟踪（通过 _savedLoadData）");
        Assert(gene.CountFieldModified() >= 4, $"_modifiedFieldNames 应至少有 4 个字段，实际 {gene.CountFieldModified()}");
    }

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段的字段跟踪逻辑。
    /// 代码路径与 TweakData.Serialization.cs 中 LoadAllFieldsForBackCompat() 的 PostLoadInit 部分保持一致。
    /// </summary>
    private static void SimulateBackCompatPostLoadInit(TweakData instance)
    {
        // 获取具体类型的 Fields 静态属性（静态属性不沿继承链搜索，需要 FlattenHierarchy）
        var fieldsProp = instance.GetType().GetProperty("Fields",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        var fields = (List<StatFieldMeta>)fieldsProp!.GetValue(null)!;

        // 获取 _savedLoadData
        var savedLoadDataField = typeof(TweakData).GetField("_savedLoadData",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var savedLoadData = (Dictionary<string, object?>?)savedLoadDataField!.GetValue(instance);

        // 遍历所有字段，执行与 LoadAllFieldsForBackCompat PostLoadInit 完全相同的逻辑
        foreach (var meta in fields)
        {
            if (meta.Attr.DataType != StatColumnConfig.ColumnDataType.Field) continue;
            var value = meta.Field.GetValue(instance);
            // 必须同时检查 value != null 和 _savedLoadData.ContainsKey
            if (value != null || savedLoadData?.ContainsKey(meta.Field.Name) == true)
                instance.MarkFieldModified(meta.Field.Name);
        }

        instance._backCompatResolved = true;
    }

    // ═══════════════════════ 辅助方法 ═══════════════════════

    private static void SetSavedLoadData(TweakData instance, Dictionary<string, object?> data)
    {
        var field = typeof(TweakData).GetField("_savedLoadData",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(instance, data);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void RunTest(string name, Action test, ref int passed, ref int failed)
    {
        try
        {
            test();
            passed++;
            Log.Message($"[BalanceTweak 测试] ✓ {name}");
        }
        catch (Exception ex)
        {
            failed++;
            Log.Error($"[BalanceTweak 测试] ✗ {name}: {ex.Message}");
        }
    }
}
#endif