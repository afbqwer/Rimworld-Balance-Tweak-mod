#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        LongEventHandler.QueueLongEvent(RunSerializationTests, "BalanceTweak testing", doAsynchronously: false, null);
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

        // ─── TraderKindDef.stockGenerators 的字段编解码与往返 ───
        RunTest("Trader stockGenerators 字段表：原版全部类型/字段都在表里", () => testStockGen_FieldTableIntegrity(), ref passed, ref failed);
        RunTest("Trader stockGenerators 往返：全部已加载商人类型", () => testStockGen_RoundTripAllDefs(), ref passed, ref failed);
        RunTest("Trader stockGenerators 往返：每个原版类型逐字段（全部字段填非默认值）", () => testStockGen_AllTypesFieldRoundTrip(), ref passed, ref failed);
        RunTest("Trader stockGenerators 往返：Def/区间/枚举/嵌套对象列表/字符串列表", () => testStockGen_FieldValuesRoundTrip(), ref passed, ref failed);
        RunTest("Trader stockGenerators 转义：值里含分隔符也能原样往返", () => testStockGen_EscapingRoundTrip(), ref passed, ref failed);
        RunTest("Trader stockGenerators 文本格式：列表两端的转义与省略默认值", () => testStockGen_Format(), ref passed, ref failed);
        RunTest("Trader stockGenerators 克隆：改副本不影响原对象 / 非原版沿用原实例", () => testStockGen_CloneIsDeep(), ref passed, ref failed);
        RunTest("Trader stockGenerators 空/null 输入", () => testStockGen_EmptyAndNull(), ref passed, ref failed);
        RunTest("Trader stockGenerators 未知类型/字段/缺 Def → 解析失败", () => testStockGen_BadInputFails(), ref passed, ref failed);
        RunTest("Trader stockGenerators 字段表分类与摘要", () => testStockGen_FieldsAndSummary(), ref passed, ref failed);
        RunTest("Trader stockGenerators 类型菜单只列原版 / 非原版只读透传", () => testStockGen_NonVanillaRules(), ref passed, ref failed);
        RunTest("Trader stockGenerators 非原版占位标记：按类型名找回 Def 原实例（字段不丢）", () => testStockGen_NonVanillaPassthrough(), ref passed, ref failed);
        RunTest("Trader stockGenerators 表单区间文本：半成品（1~ / ~4）与千位分隔符必须被拒", () => testStockGen_RangeTextValidation(), ref passed, ref failed);

        // ─── 备份/回滚测试 ───
        RunTest("ResolveDefs 单字段失败 → 回滚所有字段", () => testBackupRestore_SingleFieldFail(), ref passed, ref failed);
        RunTest("ResolveDefs 多类型字段，单个失败 → 全部回滚", () => testBackupRestore_MixedTypes(), ref passed, ref failed);
        RunTest("ResolveDefs 无失败 → 正常保留已解析字段", () => testBackupRestore_NoFailure(), ref passed, ref failed);

        // ─── BackCompat PostLoadInit 字段跟踪测试 ───
        RunTest("BackCompat PostLoadInit 跟踪简单类型字段", () => testBackCompat_TracksSimpleFields(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 跟踪 Def 字段", () => testBackCompat_TracksDefField(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 跟踪列表类型字段", () => testBackCompat_TracksListFields(), ref passed, ref failed);
        RunTest("BackCompat PostLoadInit 混合类型全部跟踪", () => testBackCompat_TracksAllMixedTypes(), ref passed, ref failed);
        // ─── ★T1 反破坏红线 ───
        RunTest("★T1 代际 A + 未动过的列表/ThingFilter 字段 → 不进 _modifiedFieldNames", () => testBackCompat_GenA_UntouchedListFields(), ref passed, ref failed);

        // ─── L0 三态内核判据 ───
        RunTest("IsEmptyValue 空值判定（ThingFilter 优先于 ICollection）", () => testIsEmptyValue_EmptyRules(), ref passed, ref failed);

        // ─── B 组：Resolve 显式置空 + Apply 门控 ───
        RunTest("★B1 已改但空 → ResolveList 显式置空（Cleared）", () => testB1_ResolveList_ExplicitClear(), ref passed, ref failed);
        RunTest("B 未改字段不触发 setter（旧行为）", () => testB_UnsetNotWritten(), ref passed, ref failed);
        RunTest("B 列表/可空 Cleared → setter 收到 null", () => testB_ClearedSetsNull(), ref passed, ref failed);
        RunTest("B 标量/Def/string Cleared → 还原原值", () => testB_ClearedReverts(), ref passed, ref failed);
        RunTest("B 原值缺席 → 保持现状 + 不抛异常（Revert 兜底）", () => testB_RevertFallback(), ref passed, ref failed);

        // ─── C 组：三态接线 / 自检 ───
        RunTest("C 跨类型三态入口 StateOfField", () => testC_StateOfField_CrossType(), ref passed, ref failed);
        RunTest("★T8 EqualsData 三态（状态不同即视为已改，保护清空意图）", () => testC_EqualsData_ThreeState(), ref passed, ref failed);
        RunTest("★SC2 复活检测判据（未回填不报，回填必报）", () => testC_SC2_RevivedDetection(), ref passed, ref failed);
        RunTest("★T11 自检 RunAll 不抛异常", () => testC_SelfCheck_RunAll_NoThrow(), ref passed, ref failed);

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
    /// ★T1（反破坏红线）：代际 A 存档（没有 modifiedFieldNames 节点）+ 用户**从未动过**的
    /// 列表 / Def 字段 ⇒ 绝不能进 _modifiedFieldNames。
    ///
    /// 背景：代际 A 的 ExposeList / ExposeDefList / ExposeStringList / ExposeIntList /
    /// ExposeIngredientFilter 在 LoadingVars 分支里**无条件写 key**（值为 null / 空列表时也写）。
    /// 旧实现用 `_savedLoadData.ContainsKey` 判定"改过"，于是所有未动过的列表字段都被误标，
    /// L0 新语义一开就会把它们整片清空 → 旧档数据丢失。这是 A 组的最高风险项。
    /// </summary>
    private static void testBackCompat_GenA_UntouchedListFields()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            // 未动过的列表字段：key 在、值为 null 或空列表（Expose* 加载分支的必然产物）
            ["statOffsets"] = null,
            ["abilities"] = new List<string>(),
            ["capMods"] = new List<string>(),
            ["damageFactors"] = null,
            // 未动过的 Def 字段：ExposeDef 只在非空时才写 key，这里顺带覆盖空串的边角情形
            ["passionModSkill"] = "",
        });

        SimulateBackCompatPostLoadInit(gene);

        Assert(!gene.IsFieldModified("statOffsets"), "未动过的 List<StatModifier> statOffsets 不得进 _modifiedFieldNames（红线）");
        Assert(!gene.IsFieldModified("abilities"), "未动过的 List<AbilityDef> abilities 不得进 _modifiedFieldNames（红线）");
        Assert(!gene.IsFieldModified("capMods"), "未动过的 List<PawnCapacityModifier> capMods 不得进 _modifiedFieldNames（红线）");
        Assert(!gene.IsFieldModified("damageFactors"), "未动过的 List<DamageFactor> damageFactors 不得进 _modifiedFieldNames（红线）");
        Assert(!gene.IsFieldModified("passionModSkill"), "空 defName 的 Def 字段不得进 _modifiedFieldNames");
        Assert(gene.CountFieldModified() == 0, $"_modifiedFieldNames 应保持为空，实际 {gene.CountFieldModified()} 个");

        // 反向校验：真正改过的字段仍必须被跟踪（别把启发式收过头）
        var gene2 = new GeneData();
        SetSavedLoadData(gene2, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string> { "EntitySkip" },
            ["passionModSkill"] = "Shooting",
        });
        SimulateBackCompatPostLoadInit(gene2);
        Assert(gene2.IsFieldModified("abilities"), "非空列表 abilities 必须被跟踪");
        Assert(gene2.IsFieldModified("passionModSkill"), "有效 defName 的 Def 字段必须被跟踪");
    }

    /// <summary>
    /// L0 三态内核的判据 IsEmptyValue：null / 空串 / 空集合 / 无允许项的 ThingFilter 判空；
    /// 其它一律不判空。ThingFilter 的分支必须早于 ICollection（否则过滤器会被当普通集合误判）。
    /// </summary>
    private static void testIsEmptyValue_EmptyRules()
    {
        Assert(TweakData.IsEmptyValue(null), "null 应判空");
        Assert(TweakData.IsEmptyValue(""), "空串应判空");
        Assert(!TweakData.IsEmptyValue("x"), "非空串不判空");
        Assert(TweakData.IsEmptyValue(new List<string>()), "空列表应判空");
        Assert(!TweakData.IsEmptyValue(new List<string> { "a" }), "非空列表不判空");
        Assert(TweakData.IsEmptyValue(new ThingFilter()), "无允许项的 ThingFilter 应判空");
        var tf = new ThingFilter();
        tf.SetAllow(ThingDefOf.Steel, true);
        Assert(!TweakData.IsEmptyValue(tf), "有允许项的 ThingFilter 不判空");
        Assert(!TweakData.IsEmptyValue(1f), "值类型不判空");
        Assert(!TweakData.IsEmptyValue(false), "false 不判空");
    }

    // ═══════════════════════ B 组：Resolve 显式置空 + Apply 门控 ═══════════════════════

    /// <summary>
    /// ★B1：显式清空（Cleared）的列表字段，ResolveList 必须显式置空 —— 否则字段会残留
    /// SetDef 回填值（Cleared 的实例值必须是空）。
    /// </summary>
    private static void testB1_ResolveList_ExplicitClear()
    {
        var gene = new GeneData();
        gene.statOffsets = null; // 用户清空
        gene.MarkFieldCleared("statOffsets");
        // 模拟读档：ExposeList 对空/null 列表不写 key，加载后 _savedLoadData 里该字段值为 null
        SetSavedLoadData(gene, new Dictionary<string, object?> { ["statOffsets"] = null });

        bool ok = gene.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(gene.statOffsets == null, "B1：已改但空的列表字段应被显式置空（Cleared）");
    }

    /// <summary>
    /// 未改字段不触发 setter；已改非空 → 写入；"已改但值空"（未显式清空）⇒ 退化为 Unset，不写。
    /// ApplyS 兼容旧版判据（非 null 即照写）：空串在未 Cleared 时仍照写。
    /// </summary>
    private static void testB_UnsetNotWritten()
    {
        var g1 = new GeneData();
        bool called = false, listCalled = false;
        g1.ApplyV<float>("selectionWeight", v => called = true);
        g1.ApplyL<StatModifier>("statOffsets", v => listCalled = true);
        Assert(!called && !listCalled, "未改字段不应触发 setter");

        var g2 = new GeneData { selectionWeight = 3f };
        g2.MarkFieldModified("selectionWeight");
        float captured = 0f;
        g2.ApplyV<float>("selectionWeight", v => captured = v);
        Assert(Math.Abs(captured - 3f) < 0.001f, "已改非空字段应写入");

        var g3 = new GeneData { selectionWeight = null };
        g3.MarkFieldModified("selectionWeight");
        bool called3 = false;
        g3.ApplyV<float>("selectionWeight", v => called3 = true);
        Assert(!called3, "已改但值空（未显式清空）⇒ Unset，不写");

        var g4 = new GeneData { defLabel = "" };
        g4.MarkFieldModified("defLabel");
        string? capS = null; bool calledS = false;
        g4.ApplyS("defLabel", v => { capS = v; calledS = true; });
        Assert(calledS && capS == "", "空串在未 Cleared 时仍照写（与旧版 != null 一致）");
    }

    /// <summary>集合 / 可空目标 Cleared ⇒ setter 收到 null（清空）。</summary>
    private static void testB_ClearedSetsNull()
    {
        var g1 = new GeneData();
        g1.statOffsets = null; // 用户清空
        g1.MarkFieldCleared("statOffsets");
        List<StatModifier>? capL = new(); bool calledL = false;
        g1.ApplyL<StatModifier>("statOffsets", v => { capL = v; calledL = true; });
        Assert(calledL && capL == null, "列表 Cleared ⇒ setter 收到 null（清空）");

        var g2 = new GeneData { selectionWeight = null };
        g2.MarkFieldCleared("selectionWeight");
        float? capN = 1f; bool calledN = false;
        g2.ApplyN<float>("selectionWeight", v => { capN = v; calledN = true; });
        Assert(calledN && capN == null, "可空目标 Cleared ⇒ setter 收到 null");
    }

    /// <summary>标量 / Def 引用 / string 的 Cleared ⇒ 还原 GetOriginal(id) 的同字段原值。</summary>
    private static void testB_ClearedReverts()
    {
        var id = new TweakData.TweakID("__BT_Test_Original__", BalanceTweakSettings.SettingType.Gene);
        try
        {
            var orig = new GeneData { id = id };
            orig.passionModSkill = DefDatabase<SkillDef>.GetNamed("Shooting");
            orig.selectionWeight = 7f;
            orig.defLabel = "OrigLabel";
            TweakDatabase.AllOriginals[id] = orig;

            var g1 = new GeneData { id = id, passionModSkill = null };
            g1.MarkFieldCleared("passionModSkill");
            SkillDef? capD = null; bool calledD = false;
            g1.ApplyD<SkillDef>("passionModSkill", v => { capD = v; calledD = true; });
            Assert(calledD && capD == orig.passionModSkill, "Def 引用 Cleared ⇒ 还原原引用");

            var g2 = new GeneData { id = id, selectionWeight = null };
            g2.MarkFieldCleared("selectionWeight");
            float capV = 0f; bool calledV = false;
            g2.ApplyV<float>("selectionWeight", v => { capV = v; calledV = true; });
            Assert(calledV && Math.Abs(capV - 7f) < 0.001f, "标量 Cleared ⇒ 还原原值");

            var g3 = new GeneData { id = id, defLabel = "" };
            g3.MarkFieldCleared("defLabel");
            string? capS = null; bool calledS = false;
            g3.ApplyS("defLabel", v => { capS = v; calledS = true; });
            Assert(calledS && capS == "OrigLabel", "string Cleared ⇒ 还原原字符串");
        }
        finally { TweakDatabase.AllOriginals.Remove(id); }
    }

    /// <summary>原值缺席（老档 / 原始数据被换掉）：保持现状、不抛异常、不写坏值（Revert 兜底）。</summary>
    private static void testB_RevertFallback()
    {
        var id = new TweakData.TweakID("__BT_Test_NoOriginal__", BalanceTweakSettings.SettingType.Gene);
        TweakDatabase.AllOriginals.Remove(id); // 确保无原值
        var g = new GeneData { id = id, selectionWeight = null };
        g.MarkFieldCleared("selectionWeight");
        bool called = false;
        g.ApplyV<float>("selectionWeight", v => called = true); // 不应抛异常
        Assert(!called, "原值缺席：保持现状、不写、不抛异常");
    }

    // ═══════════════════════ C 组：三态 / 自检 ═══════════════════════

    /// <summary>跨类型只读入口 StateOfField：Cleared 只来自显式清空集合。</summary>
    private static void testC_StateOfField_CrossType()
    {
        var g = new GeneData { selectionWeight = null };
        g.MarkFieldCleared("selectionWeight");
        Assert(g.StateOfField("selectionWeight") == FieldState.Cleared, "显式清空 ⇒ Cleared");
        Assert(g.StateOfField("biostatMet") == FieldState.Unset, "未改字段 ⇒ Unset");

        var g2 = new GeneData { selectionWeight = 2f };
        g2.MarkFieldModified("selectionWeight");
        Assert(g2.StateOfField("selectionWeight") == FieldState.Set, "已改且非空 ⇒ Set");
    }

    /// <summary>
    /// ★T8：EqualsData 先比状态、再比值 —— 状态不同（Set vs Unset）即视为"已改"，
    /// 保护用户的修改/清空意图不被重置流程误删。
    /// </summary>
    private static void testC_EqualsData_ThreeState()
    {
        var modified = new GeneData { selectionWeight = 1f };
        modified.MarkFieldModified("selectionWeight");
        var untouched = new GeneData { selectionWeight = 1f };
        Assert(!modified.EqualsData(untouched),
            "Set vs Unset ⇒ 视为已改（值虽相同，仍保护用户意图）");
    }

    /// <summary>
    /// ★SC2 判据：ResolveDefs 末尾记录的"modified ∧ 空"字段，回填前不报复活、被回填后必报。
    /// </summary>
    private static void testC_SC2_RevivedDetection()
    {
        var gene = new GeneData();
        gene.MarkFieldCleared("statOffsets");
        SetSavedLoadData(gene, new Dictionary<string, object?> { ["statOffsets"] = null });
        bool ok = gene.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");

        Assert(gene.DetectRevivedFields().Count == 0, "未回填时不得报复活");

        // 模拟 SetDef / GenValues 的 ??= 回填（漏了 !IsFieldModified 守卫的那种）
        gene.statOffsets = new List<StatModifier> { new() { stat = StatDefOf.MoveSpeed, value = 1f } };
        Assert(gene.DetectRevivedFields().Contains("statOffsets"), "被回填后必须报出复活字段");
    }

    /// <summary>★T11：运行期自检 RunAll 覆盖 SC1–SC6，必须不抛异常（异常由 Guard 记 error 但测试视为失败）。</summary>
    private static void testC_SelfCheck_RunAll_NoThrow()
    {
        bool origin = TweakSelfCheck.selfCheckEnabled;
        try
        {
            TweakSelfCheck.selfCheckEnabled = true;
            TweakSelfCheck.RunAll(); // 空库也不得抛
            var id = new TweakData.TweakID("__BT_Test_SelfCheck__", BalanceTweakSettings.SettingType.Gene);
            try
            {
                var g = new GeneData { id = id };
                g.MarkFieldCleared("statOffsets");
                g.statOffsets = null;
                BalanceTweakSettings.tweakDatas[id] = g;
                TweakSelfCheck.RunAll(); // 有数据（含 Cleared）也不得抛
            }
            finally { BalanceTweakSettings.tweakDatas.Remove(id); }
        }
        finally { TweakSelfCheck.selfCheckEnabled = origin; }
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
            // 直接复用真实判据（A1 收紧后：只认"值非 null"或"key 存在且值非空"），
            // 避免测试里再抄一份逻辑、日后两处漂移。
            if (TweakData.IsBackCompatModified(value, meta.Field.Name, savedLoadData))
                instance.MarkFieldModified(meta.Field.Name);
        }

        instance._backCompatResolved = true;
    }

    // ═══════════════════════ TraderKindDef.stockGenerators 字段表 / 编解码 ═══════════════════════
    // 字段值是真实对象（List<StockGenerator>），文本只在存档/剪贴板时由 StockGeneratorCodec 生成。
    // 字段集完全来自 StockGeneratorCodec 里"显式模式匹配原版类型"的那张字段表（不用反射遍历）：
    // 核心不变量：编码 → 解码 → 再编码必须逐行一致，字段值也要逐字段相等。一旦不成立，用户每次
    // 存档读档都会无声地丢掉或改写库存生成器的内容。

    /// <summary>编码一次再解析回来（测试里最常用的往返）。</summary>
    private static StockGenerator RoundTripGen(StockGenerator gen)
    {
        string line = StockGeneratorCodec.ToItemString(gen);
        Assert(!line.Contains('\n'), "一行必须是一条（不含换行）");
        Assert(StockGeneratorCodec.TryParse(line, out var back) && back != null, $"解析不了自己编码出来的行：{line}");
        return back!;
    }

    /// <summary>反射设字段（原版不少生成器字段是 private，测试造样本只能这么设）。</summary>
    private static void SetGenField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert(field != null, $"找不到字段 {target.GetType().Name}.{name}");
        field!.SetValue(target, value);
    }

    /// <summary>反射读字段（同上，配合 <see cref="SetGenField"/> 校验 private 字段的往返）。</summary>
    private static object? GetGenField(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert(field != null, $"找不到字段 {target.GetType().Name}.{name}");
        return field!.GetValue(target);
    }

    /// <summary>游戏本体程序集里的全部 StockGenerator 子类（含官方 DLC 的类型；原版与 DLC 编在同一个 Assembly-CSharp.dll 里）。</summary>
    private static IEnumerable<Type> VanillaGeneratorSubclasses()
        => typeof(StockGenerator).Assembly.GetTypes()
            .Where(t => t != typeof(StockGenerator) && typeof(StockGenerator).IsAssignableFrom(t) && !t.IsAbstract);

    /// <summary>原版会写进 Def XML 的实例字段：跳过 static / 编译器生成 / <c>[Unsaved]</c> / 运行期临时表。</summary>
    private static IEnumerable<FieldInfo> XmlInstanceFields(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsDefined(typeof(UnsavedAttribute), false)) continue;
                if (f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)) continue;
                if (f.Name.IndexOf('<') >= 0) continue;
                if (f.Name == "tmpGenerated") continue; // 生成库存时的临时收集表（生成完就 Clear），故意不入字段表
                yield return f;
            }
        }
    }

    private static void testStockGen_RoundTripAllDefs()
    {
        var problems = new List<string>();
        int checkedCount = 0;

        foreach (var def in DefDatabase<TraderKindDef>.AllDefsListForReading)
        {
            if (def.stockGenerators.NullOrEmpty()) continue;
            checkedCount++;

            var lines1 = StockGeneratorCodec.Serialize(def.stockGenerators);
            if (lines1 == null) { problems.Add($"{def.defName}: 编码返回 null"); continue; }

            var parsed = StockGeneratorCodec.Deserialize(lines1);
            if (parsed == null)
            {
                problems.Add($"{def.defName}: 解析失败\n{string.Join("\n", lines1)}");
                continue;
            }
            if (parsed.Count != def.stockGenerators.Count)
            {
                problems.Add($"{def.defName}: 数量不一致 {parsed.Count} != {def.stockGenerators.Count}");
                continue;
            }

            var lines2 = StockGeneratorCodec.Serialize(parsed);
            if (lines2 == null || !lines1.SequenceEqual(lines2))
            {
                problems.Add($"{def.defName}: 往返后不一致\n--- 第一次 ---\n{string.Join("\n", lines1)}"
                    + $"\n--- 第二次 ---\n{string.Join("\n", lines2 ?? new List<string>())}");
            }
        }

        Assert(checkedCount > 0, "未检查到任何商人类型（TraderKindDef 缺失？）");
        Assert(problems.Count == 0, $"{problems.Count} 个商人类型往返失败（列出前 2 个）：\n{string.Join("\n", problems.Take(2))}");
    }

    /// <summary>
    /// 字段表与原版对得上：表里声明的字段都能解析到（没有改名 / 拼错），原版每个生成器类型与每个字段
    /// 都在表里，且字段类型都被支持。原版一旦加了新类型 / 新字段 / 新字段类型，这条会先炸出来 ——
    /// 而不是等用户存档时静默丢字段。
    /// </summary>
    private static void testStockGen_FieldTableIntegrity()
    {
        Assert(StockGeneratorCodec.UnresolvedFields.Count == 0,
            $"字段表里有解析不到的字段（原版改名 / 移除？）：{string.Join(", ", StockGeneratorCodec.UnresolvedFields)}");

        var problems = new List<string>();
        foreach (var type in VanillaGeneratorSubclasses())
        {
            if (!StockGeneratorCodec.IsVanilla(type))
            {
                problems.Add($"{type.Name} 不在字段表里（原版加了新生成器类型？）");
                continue;
            }

            var declared = StockGeneratorCodec.FieldsFor(type).Select(f => f.Name).ToHashSet();
            foreach (var field in XmlInstanceFields(type))
                if (!declared.Contains(field.Name))
                    problems.Add($"{type.Name}.{field.Name}（{field.FieldType.Name}）没写进字段表");

            foreach (var field in StockGeneratorCodec.FieldsFor(type))
                if (field.Kind == StockGeneratorCodec.Kind.Unsupported)
                    problems.Add($"{type.Name}.{field.Name} 的字段类型不受支持");
        }

        Assert(problems.Count == 0, $"{problems.Count} 处字段表与原版对不上（列出前 8 条）：\n{string.Join("\n", problems.Take(8))}");
    }

    /// <summary>每个原版类型的每个字段都填上非默认值，往返后必须逐字段相等（覆盖各类型的全部读写路径）。</summary>
    private static void testStockGen_AllTypesFieldRoundTrip()
    {
        var problems = new List<string>();
        foreach (var type in StockGeneratorCodec.VanillaGeneratorTypes())
        {
            var gen = (StockGenerator)Activator.CreateInstance(type)!;
            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                var sample = SampleValueOf(field, StockGeneratorCodec.ReadField(field, gen));
                Assert(sample != null, $"{type.Name}.{field.Name}（{field.Kind}）造不出样本值");
                StockGeneratorCodec.WriteField(field, gen, sample);
            }

            string line = StockGeneratorCodec.ToItemString(gen);
            Assert(!line.Contains('\n'), $"{type.Name}：一行不得含换行");
            Assert(StockGeneratorCodec.TryParse(line, out var back) && back != null, $"{type.Name}：解析不了自己编码出来的行：{line}");
            if (back!.GetType() != type) { problems.Add($"{type.Name}: 往返后类型变成 {back.GetType().Name}"); continue; }
            if (StockGeneratorCodec.ToItemString(back) != line) { problems.Add($"{type.Name}: 再编码不一致\n{line}\n{StockGeneratorCodec.ToItemString(back)}"); continue; }

            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                var expected = StockGeneratorCodec.ReadField(field, gen);
                var actual = StockGeneratorCodec.ReadField(field, back);
                if (!SameValue(expected, actual))
                    problems.Add($"{type.Name}.{field.Name}: {DescribeValueOf(expected)} != {DescribeValueOf(actual)}");
            }
        }
        Assert(problems.Count == 0, $"{problems.Count} 处字段没往返一致（列出前 6 条）：\n{string.Join("\n", problems.Take(6))}");
    }

    /// <summary>按字段表的种类造一个"非默认"的样本值（<paramref name="baselineValue"/> 是新实例上的初始值）。</summary>
    private static object? SampleValueOf(StockGeneratorCodec.SgField field, object? baselineValue)
    {
        switch (field.Kind)
        {
            case StockGeneratorCodec.Kind.Float: return 7.25f;
            case StockGeneratorCodec.Kind.Int: return 3;
            case StockGeneratorCodec.Kind.Bool: return !(baselineValue is bool b && b);
            case StockGeneratorCodec.Kind.Text: return "示例|标签=值;[x]\\y";
            case StockGeneratorCodec.Kind.IntRange: return new IntRange(2, 5);
            case StockGeneratorCodec.Kind.FloatRange: return new FloatRange(1.5f, 9.75f);
            case StockGeneratorCodec.Kind.Enum:
            {
                foreach (var value in Enum.GetValues(field.EnumType!))
                    if (!Equals(value, baselineValue)) return value;
                return null;
            }
            case StockGeneratorCodec.Kind.Def:
                return GenDefDatabase.GetAllDefsInDatabaseForDef(field.ElementType!).FirstOrDefault();
            case StockGeneratorCodec.Kind.DefList:
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(field.ElementType!))!;
                var def = GenDefDatabase.GetAllDefsInDatabaseForDef(field.ElementType!).FirstOrDefault();
                if (def != null) list.Add(def);
                return list.Count > 0 ? list : null;
            }
            case StockGeneratorCodec.Kind.StringList:
                return new List<string> { "first", "second|x" };
            case StockGeneratorCodec.Kind.ComplexList:
            {
                var elementType = field.ElementType!;
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                var element = Activator.CreateInstance(elementType)!;
                foreach (var sub in StockGeneratorCodec.FieldsFor(elementType))
                {
                    var sample = SampleValueOf(sub, StockGeneratorCodec.ReadField(sub, element));
                    if (sample != null) StockGeneratorCodec.WriteField(sub, element, sample);
                }
                list.Add(element);
                return list;
            }
            default:
                return null;
        }
    }

    private static bool SameValue(object? left, object? right)
    {
        if (left == null || right == null) return left == null && right == null;
        if (left is IList listLeft && right is IList listRight)
        {
            if (listLeft.Count != listRight.Count) return false;
            for (int i = 0; i < listLeft.Count; i++)
                if (!SameValue(listLeft[i], listRight[i])) return false;
            return true;
        }
        // 字段表里也声明了字段的小对象（CountChance / ThingDefCountRangeClass）逐字段比
        if (left is CountChance ca && right is CountChance cb) return ca.count == cb.count && ca.chance == cb.chance;
        if (left is ThingDefCountRangeClass ta && right is ThingDefCountRangeClass tb)
            return ta.thingDef == tb.thingDef && ta.countRange.Equals(tb.countRange);
        return Equals(left, right);
    }

    private static string DescribeValueOf(object? value) => value switch
    {
        null => "<null>",
        Def def => def.defName ?? "<无名 Def>",
        string str => $"\"{str}\"",
        IList list => $"[{list.Count}]",
        _ => value.ToString() ?? "<?>",
    };

    /// <summary>字段值要真的来回一致（不只是"文本一致"）：Def 引用、区间、枚举、嵌套对象列表、字符串列表。</summary>
    private static void testStockGen_FieldValuesRoundTrip()
    {
        var tag = new StockGenerator_Tag
        {
            tradeTag = "ExoticMisc",
            countRange = new IntRange(1, 4),
            totalPriceRange = new FloatRange(10f, 99.5f),
            maxTechLevelBuy = TechLevel.Medieval,
            price = PriceType.Expensive,
            customCountRanges = new List<ThingDefCountRangeClass>
            {
                new(ThingDefOf.Steel, new IntRange(2, 6)),
                new(ThingDefOf.Cloth, new IntRange(7, 7)),
            },
        };
        SetGenField(tag, "thingDefCountRange", new IntRange(3, 5));
        SetGenField(tag, "excludedThingDefs", new List<ThingDef> { ThingDefOf.Steel, ThingDefOf.Cloth });

        var back = RoundTripGen(tag) as StockGenerator_Tag;
        Assert(back != null, "往返后类型应仍是 StockGenerator_Tag");
        Assert(back!.tradeTag == "ExoticMisc", $"tradeTag 应保持，实际 {back.tradeTag}");
        Assert(back.countRange.Equals(new IntRange(1, 4)), "countRange 应保持");
        Assert(back.totalPriceRange.Equals(new FloatRange(10f, 99.5f)), "totalPriceRange 应保持");
        Assert(back.maxTechLevelBuy == TechLevel.Medieval, "maxTechLevelBuy（枚举）应保持");
        Assert(back.price == PriceType.Expensive, "price（枚举）应保持");
        Assert(back.customCountRanges is { Count: 2 }, "customCountRanges 应保持 2 项");
        Assert(back.customCountRanges![0].thingDef == ThingDefOf.Steel, "customCountRanges[0].thingDef 应保持");
        Assert(back.customCountRanges[0].countRange.Equals(new IntRange(2, 6)), "customCountRanges[0].countRange 应保持");
        Assert(back.customCountRanges[1].countRange.Equals(new IntRange(7, 7)), "customCountRanges[1] 应保持");
        Assert((IntRange?)GetGenField(back, "thingDefCountRange") is { } range && range.Equals(new IntRange(3, 5)),
            "私有字段 thingDefCountRange 也应保持");
        var excluded = GetGenField(back, "excludedThingDefs") as List<ThingDef>;
        Assert(excluded is { Count: 2 } && excluded[0] == ThingDefOf.Steel && excluded[1] == ThingDefOf.Cloth,
            "Def 列表 excludedThingDefs 应保持");
        Assert(back.trader == null, "[Unsaved] 的 trader 不应被编码");

        // 字符串列表（StockGenerator_Animals 的 tradeTagsSell 是 private）
        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "AnimalCommon", "AnimalUncommon" });
        SetGenField(animals, "kindCountRange", new IntRange(1, 3));
        SetGenField(animals, "minWildness", 0.25f);
        SetGenField(animals, "checkTemperature", false);
        var animalsBack = RoundTripGen(animals) as StockGenerator_Animals;
        Assert(animalsBack != null, "往返后类型应仍是 StockGenerator_Animals");
        var tagsBack = GetGenField(animalsBack!, "tradeTagsSell") as List<string>;
        Assert(tagsBack is { Count: 2 } && tagsBack[0] == "AnimalCommon" && tagsBack[1] == "AnimalUncommon",
            "字符串列表 tradeTagsSell 应保持");
        Assert((IntRange?)GetGenField(animalsBack!, "kindCountRange") is { } kcr && kcr.Equals(new IntRange(1, 3)),
            "私有字段 kindCountRange 应保持");
        Assert((float?)GetGenField(animalsBack!, "minWildness") == 0.25f, "私有字段 minWildness 应保持");
        Assert((bool?)GetGenField(animalsBack!, "checkTemperature") == false, "私有字段 checkTemperature 应保持");

        // 小对象列表（StockGenerator_Techprints 的 countChances 是 private List<CountChance>）
        var techprints = new StockGenerator_Techprints();
        SetGenField(techprints, "countChances", new List<CountChance>
        {
            new() { count = 2, chance = 0.5f },
            new() { count = 3, chance = 1f },
        });
        var techprintsBack = RoundTripGen(techprints) as StockGenerator_Techprints;
        var chances = techprintsBack == null ? null : GetGenField(techprintsBack, "countChances") as List<CountChance>;
        Assert(chances is { Count: 2 } && chances[0].count == 2 && chances[0].chance == 0.5f && chances[1].count == 3,
            "小对象列表 countChances 应保持");

        // 单个 Def 引用（StockGenerator_SingleDef 的 thingDef 是 private）
        var single = new StockGenerator_SingleDef();
        SetGenField(single, "thingDef", ThingDefOf.Steel);
        var singleBack = RoundTripGen(single) as StockGenerator_SingleDef;
        Assert(singleBack != null && (ThingDef?)GetGenField(singleBack, "thingDef") == ThingDefOf.Steel,
            "单个 Def 引用 thingDef 应保持");
    }

    /// <summary>字段值里出现分隔符（<c>| = [ ] ; \</c>）时也必须原样往返 —— 转义是这套格式唯一的"复杂"之处。</summary>
    private static void testStockGen_EscapingRoundTrip()
    {
        string nasty = "a|b=c;[d]\\e;f~g,h=i";
        var tag = new StockGenerator_Tag { tradeTag = nasty };
        var back = RoundTripGen(tag) as StockGenerator_Tag;
        Assert(back != null && back.tradeTag == nasty, $"含分隔符的字符串应原样往返，实际 {(back == null ? "<null>" : back.tradeTag)}");

        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "a|b", "c;d", "[e]", "f\\g", "" });
        var animalsBack = RoundTripGen(animals) as StockGenerator_Animals;
        var tags = animalsBack == null ? null : GetGenField(animalsBack, "tradeTagsSell") as List<string>;
        Assert(tags is { Count: 4 }, $"空字符串元素跳过（其余 4 个保持），实际 {tags?.Count}");
        Assert(tags!.Contains("a|b") && tags.Contains("c;d") && tags.Contains("[e]") && tags.Contains("f\\g"),
            "含分隔符的列表元素应原样往返");

        // 转义 / 反转义本身互逆
        foreach (var s in new[] { "", "a", nasty, "\\", "[[;;]]", "==" })
            Assert(StockGeneratorCodec.Unescape(StockGeneratorCodec.Escape(s)) == s, $"Escape/Unescape 不互逆：\"{s}\"");
    }

    /// <summary>克隆：改副本不得影响原对象；非原版（字段表外）子类因为字段复制不了，直接沿用原实例。</summary>
    private static void testStockGen_CloneIsDeep()
    {
        var tag = new StockGenerator_Tag { tradeTag = "Foo" };
        tag.customCountRanges = new List<ThingDefCountRangeClass> { new(ThingDefOf.Steel, new IntRange(1, 2)) };

        var clone = StockGeneratorCodec.Clone(tag) as StockGenerator_Tag;
        Assert(clone != null && !ReferenceEquals(clone, tag), "克隆应是新实例");
        Assert(!ReferenceEquals(clone!.customCountRanges, tag.customCountRanges), "嵌套列表应是新实例");
        Assert(clone.customCountRanges![0].thingDef == ThingDefOf.Steel, "嵌套元素内容应保持");
        Assert(!ReferenceEquals(clone.customCountRanges[0], tag.customCountRanges[0]), "嵌套元素也应是新实例");

        clone.tradeTag = "Changed";
        clone.customCountRanges[0].countRange = new IntRange(9, 9);
        Assert(tag.tradeTag == "Foo", "改副本不应影响原对象（标量）");
        Assert(tag.customCountRanges[0].countRange.Equals(new IntRange(1, 2)), "改副本不应影响原对象（嵌套元素）");

        // 列表字段（CountChance 是值类型，装箱即复制）也应独立
        var techprints = new StockGenerator_Techprints();
        SetGenField(techprints, "countChances", new List<CountChance> { new() { count = 1, chance = 1f } });
        var techprintsClone = StockGeneratorCodec.Clone(techprints) as StockGenerator_Techprints;
        var cloneChances = techprintsClone == null ? null : GetGenField(techprintsClone, "countChances") as List<CountChance>;
        Assert(cloneChances is { Count: 1 } && cloneChances[0].count == 1, "小对象列表应被克隆");
        Assert(!ReferenceEquals(cloneChances, GetGenField(techprints, "countChances")), "克隆的列表应是新实例");

        // 字符串列表与 Def 列表也必须跟着克隆（少复制一个字段 = 静默丢数据，克隆宁可失败也不能交出半份副本）
        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "AnimalFarm", "AnimalPet" });
        SetGenField(animals, "tradeTagsBuy", new List<string> { "AnimalExotic" });
        SetGenField(animals, "createMatingPair", new List<string> { "AnimalFarm" });
        var animalsClone = StockGeneratorCodec.Clone(animals) as StockGenerator_Animals;
        Assert(animalsClone != null && !ReferenceEquals(animalsClone, animals), "有字符串列表的生成器也应克隆成新实例");
        foreach (var name in new[] { "tradeTagsSell", "tradeTagsBuy", "createMatingPair" })
        {
            var from = GetGenField(animals, name) as List<string>;
            var to = GetGenField(animalsClone!, name) as List<string>;
            Assert(to != null && !ReferenceEquals(from, to) && to.SequenceEqual(from!),
                $"字符串列表 {name} 应被克隆成内容相同的新实例");
        }
        (GetGenField(animalsClone!, "tradeTagsSell") as List<string>)!.Add("OnlyInClone");
        Assert((GetGenField(animals, "tradeTagsSell") as List<string>)!.Count == 2, "改克隆的字符串列表不应影响原对象");

        var defListGen = new StockGenerator_Tag { tradeTag = "Foo" };
        SetGenField(defListGen, "excludedThingDefs", new List<ThingDef> { ThingDefOf.Steel });
        var defListClone = StockGeneratorCodec.Clone(defListGen) as StockGenerator_Tag;
        var clonedDefs = defListClone == null ? null : GetGenField(defListClone, "excludedThingDefs") as List<ThingDef>;
        Assert(clonedDefs is { Count: 1 } && clonedDefs[0] == ThingDefOf.Steel, "Def 列表应被克隆");
        Assert(!ReferenceEquals(clonedDefs, GetGenField(defListGen, "excludedThingDefs")), "克隆的 Def 列表应是新实例");

        // 字段表外的类型不克隆：它自己的字段复制不了，克隆只会丢数据，所以直接沿用原实例
        var mod = new TestNonVanillaGenerator { tradeTag = "Bar", extraModField = "mod" };
        Assert(ReferenceEquals(StockGeneratorCodec.Clone(mod), mod), "非原版子类应直接沿用原实例（不克隆）");
    }

    /// <summary>
    /// 钉住文本格式：值里的 <c>[ ] ; = | \</c> 都要转义，列表两端必须成对出现。
    /// （曾经的 bug：`new StringBuilder(ListStart)` 命中"容量"构造函数，开头的 <c>[</c> 被静默丢掉，
    /// 于是所有带列表字段的生成器都解析不回来 —— 这条用例就是为了让这种回归一眼可查。）
    /// </summary>
    private static void testStockGen_Format()
    {
        var tag = new StockGenerator_Tag { tradeTag = "Foo" };
        Assert(StockGeneratorCodec.ToItemString(tag) == "StockGenerator_Tag|tradeTag=Foo",
            $"默认值应省略、只留类型与非默认字段，实际：{StockGeneratorCodec.ToItemString(tag)}");

        SetGenField(tag, "excludedThingDefs", new List<ThingDef> { ThingDefOf.Steel, ThingDefOf.Cloth });
        string line = StockGeneratorCodec.ToItemString(tag);
        Assert(line.Contains(@"excludedThingDefs=\[Steel\;Cloth\]"), $@"Def 列表应写成 \[a\;b\]，实际：{line}");

        var animals = new StockGenerator_Animals();
        SetGenField(animals, "tradeTagsSell", new List<string> { "first", "second|x" });
        string animalLine = StockGeneratorCodec.ToItemString(animals);
        Assert(animalLine.Contains(@"tradeTagsSell=\[first\;second\\\|x\]"),
            $@"字符串列表元素要多转义一层，应写成 \[a\;b\\\|c\]，实际：{animalLine}");

        // 编码出来的行必须能解析回来（格式自洽）
        Assert(StockGeneratorCodec.TryParse(line, out var back1) && back1 != null, $"格式自洽性：{line}");
        Assert(StockGeneratorCodec.TryParse(animalLine, out var back2) && back2 != null, $"格式自洽性：{animalLine}");
    }

    private static void testStockGen_EmptyAndNull()
    {
        Assert(StockGeneratorCodec.Serialize(null) == null, "null 列表应编码为 null");
        Assert(StockGeneratorCodec.Serialize(new List<StockGenerator>()) == null, "空列表应编码为 null");
        Assert(StockGeneratorCodec.Deserialize(null) == null, "null 输入应还原为 null");
        Assert(StockGeneratorCodec.Deserialize(new List<string>()) == null, "空输入应还原为 null");
        Assert(StockGeneratorCodec.CloneAll(null) == null, "克隆 null 应得到 null");
        Assert(!StockGeneratorCodec.TryParse(null, out _), "null 行应解析失败");
        Assert(!StockGeneratorCodec.TryParse("", out _), "空行应解析失败");
    }

    private static void testStockGen_BadInputFails()
    {
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_ThisTypeDoesNotExist|a=b", out _), "未知类型应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_Tag|noSuchField=x", out _), "未知字段应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_SingleDef|thingDef=__BT_Not_A_Real_Def__", out _),
            "缺 Def 应解析失败（由调用方保持 Def 现状）");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_Tag|countRange=abc", out _), "区间值非法应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_Tag|tradeTag", out _), "缺 \"=\" 的字段段应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator|tradeTag=x", out _), "抽象基类名应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_MiscItems|countRange=1~2", out _), "抽象类型应解析失败");
        Assert(!StockGeneratorCodec.TryParse("CountChance|count=1", out _), "字段表里的小对象类型不是生成器，应解析失败");
        Assert(!StockGeneratorCodec.TryParse("StockGenerator_Tag|excludedThingDefs=[__BT_Not_A_Real_Def__]", out _),
            "Def 列表里缺 Def 应解析失败");

        var good = StockGeneratorCodec.ToItemString(new StockGenerator_Tag { tradeTag = "Foo" });
        Assert(StockGeneratorCodec.Deserialize(new List<string> { good, "StockGenerator_Tag|noSuchField=x" }) == null,
            "整块里有一条坏行 ⇒ 整块拒绝");
    }

    /// <summary>
    /// 表单文本框里的区间文本：<b>"输到一半"必须被拒</b>。
    /// 判据不能靠 <c>Verse.*.FromString</c> 抛不抛异常 —— <c>IntRange.FromString("1~")</c> 完全不抛，
    /// 它把缺省的上界当 <c>int.MaxValue</c>，于是"在 "~" 之后离开输入框"会把 <c>countRange</c> 写成
    /// 1~2147483647，而库存数量直接取自 <c>countRange.RandomInRange</c>（生成时按这个数量造物品）。
    /// </summary>
    private static void testStockGen_RangeTextValidation()
    {
        // 半成品 / 非数字：一律拒绝（前两个正是 IntRange.FromString 会收下的）
        foreach (var bad in new[] { "1~", "~4", "1~2~3", "abc", "", " ", "1.5", "1~abc" })
            Assert(!StockGeneratorCodec.TryParseRangeText(isIntRange: true, bad, out _),
                $"整数区间不该接受 \"{bad}\"");

        // 单值 = 两端同值：IntRange.ToString() 在 min==max 时只显示一个数，必须能编辑回去
        Assert(StockGeneratorCodec.TryParseRangeText(true, "5", out var single)
            && single is IntRange s && s.min == 5 && s.max == 5, "整数区间应接受单值（= min~max）");
        Assert(StockGeneratorCodec.TryParseRangeText(true, "1~4", out var pair)
            && pair is IntRange p && p.min == 1 && p.max == 4, "整数区间应接受 min~max");

        // 浮点：显示用 G9，可能带指数写法，必须能读回来
        Assert(StockGeneratorCodec.TryParseRangeText(false, "1.5~2.5", out var fr)
            && fr is FloatRange f && f.min == 1.5f && f.max == 2.5f, "浮点区间应接受 min~max");
        Assert(StockGeneratorCodec.TryParseRangeText(false, "1E-05~2", out var exp)
            && exp is FloatRange e && e.min == 1E-05f, "浮点区间应接受 G9 可能产出的指数写法");
        Assert(!StockGeneratorCodec.TryParseRangeText(false, "1~", out _), "浮点区间不该接受 1~");

        // 千位分隔符不算合法数字（NumberStyles.Float 不含 AllowThousands）：
        // 否则逗号小数点环境下 "1.5" 会被裸 float.TryParse 读成 15
        Assert(!StockGeneratorCodec.TryParseRangeText(false, "1,5~2", out _), "浮点区间不该接受带千位分隔符的写法");
    }

    private static void testStockGen_FieldsAndSummary()
    {
        var fields = StockGeneratorCodec.FieldsFor(typeof(StockGenerator_Tag));
        Assert(fields.Any(f => f.Name == "tradeTag" && f.Kind == StockGeneratorCodec.Kind.Text), "tradeTag 应是文本字段");
        Assert(fields.Any(f => f.Name == "countRange" && f.Kind == StockGeneratorCodec.Kind.IntRange), "countRange 应是整数区间字段");
        Assert(fields.Any(f => f.Name == "totalPriceRange" && f.Kind == StockGeneratorCodec.Kind.FloatRange), "totalPriceRange 应是浮点区间字段");
        Assert(fields.Any(f => f.Name == "maxTechLevelBuy" && f.Kind == StockGeneratorCodec.Kind.Enum), "maxTechLevelBuy 应是枚举字段");
        Assert(fields.Any(f => f.Name == "price" && f.Kind == StockGeneratorCodec.Kind.Enum), "price 应是枚举字段");
        Assert(fields.Any(f => f.Name == "excludedThingDefs" && f.Kind == StockGeneratorCodec.Kind.DefList), "excludedThingDefs 应是 Def 列表字段");
        Assert(fields.Any(f => f.Name == "customCountRanges" && f.Kind == StockGeneratorCodec.Kind.ComplexList), "customCountRanges 应是小对象列表字段");
        Assert(!fields.Any(f => f.Name == "trader"), "字段表不应含 [Unsaved] 的 trader");
        Assert(!fields.Any(f => f.Name == "tmpGenerated"), "字段表不应含运行期临时表 tmpGenerated");

        // 小对象列表的元素类型也用同一张字段表（SGComplexListEditorWindow 直接读它）
        Assert(StockGeneratorCodec.FieldsFor(typeof(CountChance)).Any(f => f.Name == "chance" && f.Kind == StockGeneratorCodec.Kind.Float),
            "CountChance.chance 应是浮点字段");
        Assert(StockGeneratorCodec.FieldsFor(typeof(ThingDefCountRangeClass)).Any(f => f.Name == "thingDef" && f.Kind == StockGeneratorCodec.Kind.Def),
            "ThingDefCountRangeClass.thingDef 应是 Def 字段");

        string summary = StockGeneratorCodec.Summarize(new StockGenerator_Tag { tradeTag = "Foo" });
        Assert(summary.Contains("tradeTag=Foo"), $"摘要应含 tradeTag=Foo，实际：\"{summary}\"");
        Assert(StockGeneratorCodec.Summarize(new TestNonVanillaGenerator()) == "", "非原版类型不打摘要");
        Assert(StockGeneratorCodec.TypeLabel(typeof(StockGenerator_Tag)) == "StockGenerator_Tag", "类型显示名应是原版 XML Class 的写法");
    }

    /// <summary>
    /// 原版 / 非原版的判据就是"在不在字段表里"：类型菜单只列表里的具体类型；表外（其它 Mod 新增）
    /// 的子类字段集为空 —— 本工具对它们只做只读展示，不读取也不修改它们的字段。
    /// </summary>
    private static void testStockGen_NonVanillaRules()
    {
        Assert(StockGeneratorCodec.IsVanilla(typeof(StockGenerator_Tag)), "原版 StockGenerator_Tag 应判定为原版");
        Assert(!StockGeneratorCodec.IsVanilla(typeof(TestNonVanillaGenerator)), "字段表外的类型应判定为非原版");

        var vanilla = StockGeneratorCodec.VanillaGeneratorTypes();
        Assert(vanilla.Count > 1, "原版类型列表不应为空");
        Assert(vanilla.All(StockGeneratorCodec.IsVanilla), "类型菜单里的每一项都必须是原版类型");
        Assert(vanilla.All(t => !t.IsAbstract && t != typeof(StockGenerator)), "类型菜单不得含抽象类型与基类");
        Assert(vanilla.Contains(typeof(StockGenerator_Tag)), "StockGenerator_Tag 应在类型菜单里");
        Assert(vanilla.Contains(typeof(StockGenerator_MarketValue)), "官方的 StockGenerator_MarketValue 也应在类型菜单里");
        Assert(!vanilla.Contains(typeof(StockGenerator_MiscItems)), "抽象类 StockGenerator_MiscItems 不应出现在类型菜单里");
        Assert(!vanilla.Contains(typeof(TestNonVanillaGenerator)), "非原版子类不得出现在类型菜单里");

        // 字段表外的类型：本工具只读展示，字段一个都不读不写
        Assert(StockGeneratorCodec.FieldsFor(typeof(TestNonVanillaGenerator)).Count == 0,
            "非原版子类的字段表应为空（本工具不读不写它的字段）");
        Assert(StockGeneratorCodec.Clone(new TestNonVanillaGenerator()) is TestNonVanillaGenerator, "非原版子类应能原样透传");

        // 继承链上的字段也跟着走（表内类型）
        Assert(StockGeneratorCodec.FieldsFor(typeof(StockGenerator_Tomes)).Any(f => f.Name == "thingDef"),
            "StockGenerator_Tomes 应含它从 StockGenerator_SingleDef 继承的 thingDef");
        var marketValue = StockGeneratorCodec.FieldsFor(typeof(StockGenerator_MarketValue)).Select(f => f.Name).ToList();
        Assert(marketValue.Contains("countRange") && marketValue.Contains("tradeTag")
            && marketValue.Contains("weaponTag") && marketValue.Contains("apparelTag"),
            "StockGenerator_MarketValue 应含基类字段与自有的三个标签字段");
    }

    /// <summary>
    /// 非原版（其它 Mod 新增）生成器：只读展示 + 占位标记透传。
    /// 编码只写类型名；回填时按类型名把 Def 里那个原实例原样放回原位 —— 它自己的字段一个都不丢。
    /// </summary>
    private static void testStockGen_NonVanillaPassthrough()
    {
        var mod = new TestNonVanillaGenerator { tradeTag = "Tag", extraModField = "mod" };

        // 编码：只留类型名（连它继承的 tradeTag 都不写 —— 本工具不读不写它的字段）
        string line = StockGeneratorCodec.ToItemString(mod);
        Assert(line == StockGeneratorCodec.TypeLabel(typeof(TestNonVanillaGenerator)),
            $"非原版条目应只写类型名，实际：{line}");
        Assert(!line.Contains('|'), "非原版条目的行里不应有任何字段段");

        // 解码：得到一个同类型的"空壳"（字段不还原，只当占位标记）
        Assert(StockGeneratorCodec.TryParse(line, out var shell) && shell != null, $"类型名占位标记应能解析：{line}");
        Assert(shell!.GetType() == typeof(TestNonVanillaGenerator), "占位标记应还原出同类型实例");
        Assert(StockGeneratorCodec.ToItemString(shell) == line, "占位标记应能稳定地再编码");

        // 回填：非原版条目必须换成 Def 里的原实例（身份与字段都不变），顺序保持在原处
        var defGen1 = new StockGenerator_SingleDef();
        var defMod1 = new TestNonVanillaGenerator { tradeTag = "Tag", extraModField = "keep-1" };
        var defGen2 = new StockGenerator_Tag();
        var defMod2 = new TestNonVanillaGenerator { tradeTag = "Tag", extraModField = "keep-2" };
        var defList = new List<StockGenerator> { defGen1, defMod1, defGen2, defMod2 };

        var merged = StockGeneratorCodec.MergeWithDef(new List<StockGenerator> { shell, defGen2 }, defList);
        Assert(merged is { Count: 3 }, $"应得到 1 个原版克隆 + 2 个原实例 共 3 条，实际 {merged?.Count}");
        Assert(merged![0].GetType() == typeof(StockGenerator_Tag) && !ReferenceEquals(merged[0], defGen2),
            "原版条目应是克隆（不与 Def 共享实例）");
        Assert(merged[1] is TestNonVanillaGenerator m1 && m1.extraModField == "keep-1",
            "非原版条目应按类型名换回 Def 里的原实例（字段不丢）");
        Assert(!ReferenceEquals(merged[1], shell), "绝不能把空壳塞回 Def");
        Assert(merged[2] is TestNonVanillaGenerator m2 && m2.extraModField == "keep-2",
            "Def 里剩下的非原版生成器应原样保留（追加在末尾，绝不丢）");

        // 清空字段（用户显式清掉整个字段）⇒ 不合并，直接清空
        Assert(StockGeneratorCodec.MergeWithDef(new List<StockGenerator>(), defList) == null, "空字段应返回 null（清空库存）");
        Assert(StockGeneratorCodec.MergeWithDef(null, defList) == null, "null 字段应返回 null");
    }

    /// <summary>
    /// 测试用的"非原版"生成器：位于本 Mod 程序集，行为等价于其它 Mod 新增的 <see cref="StockGenerator"/> 子类
    /// （多出一个自有的 <c>extraModField</c>）。直接派生自原版 <see cref="StockGenerator_Tag"/> 是为了免去实现
    /// 抽象成员；它同时覆盖"字段表外的类型只读透传（占位标记 + 按类型名找回原实例）"这条规则。
    /// </summary>
    private class TestNonVanillaGenerator : StockGenerator_Tag
    {
        public string extraModField = "mod";
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