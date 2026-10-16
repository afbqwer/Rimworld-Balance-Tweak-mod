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
        // ─── StockGenerator 编解码 ───
        // 机制测试（转义 / 格式 / 克隆 / 区间文本 / 字段表 / 非原版透传 / 空值 / 坏输入）已转到**游戏外**：
        //   Source/BalanceTweak.Tests/StockGeneratorCodecTests.cs —— `dotnet test` 秒级跑完，不启动游戏。
        // 这里只留**必须真 DefDatabase** 的三条：它们的价值在于"真数据"，游戏外无法等价替代。
        RunTest("Trader stockGenerators 往返：全部已加载商人类型（真数据整表）", () => testStockGen_RoundTripAllDefs(), ref passed, ref failed);
        RunTest("Trader stockGenerators 往返：Def / Def 列表字段填真 Def", () => testStockGen_DefFieldsRoundTrip(), ref passed, ref failed);
        RunTest("Trader stockGenerators 往返：Def 引用（单个 / 列表 / 嵌套元素）", () => testStockGen_DefRefsRoundTrip(), ref passed, ref failed);

        // ─── SC11a：全字段 Scribe 往返自检（见 Docs/Architecture_Improvement_Plan.md §3.1）───
        // 行为验证：给每个可测字段灌非默认样本值 → 真实 Scribe 存/读往返 → 逐字段比对。
        // 这是 SC1–SC9 之外唯一验证"字段真能落盘、又能读回"的检查。
        RunTest("★SC11a 全字段 Scribe 往返（漏登记/编解码错误都会让值读不回来）",
            () => TweakFieldScribeRoundTripTests.Run(), ref passed, ref failed);

        // ─── 备份/回滚测试 ───
        RunTest("ResolveDefs 单字段失败 → 回滚所有字段", () => testBackupRestore_SingleFieldFail(), ref passed, ref failed);
        RunTest("ResolveDefs 多类型字段，单个失败 → 全部回滚", () => testBackupRestore_MixedTypes(), ref passed, ref failed);
        RunTest("ResolveDefs 无失败 → 正常保留已解析字段", () => testBackupRestore_NoFailure(), ref passed, ref failed);

        // ─── L0 三态内核 / BackCompat 字段跟踪 ───
        // 那 13 条（B 组 / C 组三态 / ★T1 / ★T8 / ★SC2 / IsEmptyValue）**不查 DefDatabase**，
        // 已搬到游戏外秒级回路：BalanceTweak.Tests/TweakDataStateTests.cs。
        // 这里只留 RunAll —— 它跑的是真 Def 数据上的 SC1–SC11 自检，必须在游戏内。
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

    /// <summary>ResolveStatModifierList: 正常数据（defName 都解析得到）→ 应正确还原</summary>
    private static void testResolveStatModList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["statOffsets"] = Encoded(
                new StatModifier { stat = DefRef<StatDef>("MoveSpeed"), value = 0.2f },
                new StatModifier { stat = DefRef<StatDef>("MeleeDamageFactor"), value = 1.5f })
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

    /// <summary>ResolveStatModifierList: Def 引用缺失（例如相关模组被移除）→ 返回 false</summary>
    private static void testResolveStatModList_InvalidStat()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["statOffsets"] = Encoded(new StatModifier { stat = DefRef<StatDef>("NonExistentStat_ABC"), value = 0.5f })
        });
        bool ok = hediff.ResolveDefs();
        Assert(!ok, "Def 引用缺失时应返回 false");
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

    /// <summary>ResolvePawnCapacityList: 只写了 capacity / setMax → 其余字段取新实例初值</summary>
    private static void testResolvePawnCapList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = Encoded(new PawnCapacityModifier
            {
                capacity = DefRef<PawnCapacityDef>("Consciousness"),
                setMax = 0.5f,
            })
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "ResolveDefs 应返回 true");
        Assert(hediff.capMods != null, "capMods 不应为 null");
        Assert(hediff.capMods?.Count == 1, $"应包含 1 项，实际 {hediff.capMods?.Count}");
        Assert(hediff.capMods?[0].capacity.defName == "Consciousness", $"capacity 应为 Consciousness，实际 {hediff.capMods![0].capacity.defName}");
        Assert(Math.Abs(hediff.capMods[0].offset) < 0.001f, "offset 应为 0");
        Assert(Math.Abs(hediff.capMods[0].setMax - 0.5f) < 0.001f, "setMax 应为 0.5");
        Assert(Math.Abs(hediff.capMods[0].postFactor - 1f) < 0.001f, "postFactor 应为 1（默认）");
        Assert(hediff.capMods[0].statFactorMod == null, "statFactorMod 应为 null（未写）");
        Assert(hediff.capMods[0].setMaxCurveEvaluateStat == null, "setMaxCurveEvaluateStat 应为 null（未写）");
    }

    /// <summary>ResolvePawnCapacityList: Def 引用缺失 → 返回 false</summary>
    private static void testResolvePawnCapList_Invalid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = Encoded(new PawnCapacityModifier
            {
                capacity = DefRef<PawnCapacityDef>("NonExistentCapacity_XYZ"),
                setMax = 0.5f,
            })
        });
        bool ok = hediff.ResolveDefs();
        Assert(!ok, "Def 引用缺失时应返回 false");
    }

    /// <summary>ResolvePawnCapacityList: 全部 6 个字段都写上（含 statFactorMod + setMaxCurveEvaluateStat）</summary>
    private static void testResolvePawnCapList_FullFormat()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["capMods"] = Encoded(new PawnCapacityModifier
            {
                capacity = DefRef<PawnCapacityDef>("Moving"),
                offset = 0.2f,
                setMax = -1f,
                postFactor = 0.8f,
                statFactorMod = DefRef<StatDef>("MoveSpeed"),
                setMaxCurveEvaluateStat = DefRef<StatDef>("ShootingAccuracyPawn"),
            })
        });
        bool ok = hediff.ResolveDefs();
        Assert(ok, "完整字段 ResolveDefs 应返回 true");
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

    /// <summary>ResolveDamageFactorList: 正常数据 → 应正确还原</summary>
    private static void testResolveDamageFactorList_Valid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["damageFactors"] = Encoded(
                new DamageFactor { damageDef = DefRef<DamageDef>("Arrow"), factor = 0.5f },
                new DamageFactor { damageDef = DefRef<DamageDef>("Bullet"), factor = 0.75f })
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

    /// <summary>ResolveDamageFactorList: Def 引用缺失 → 返回 false</summary>
    private static void testResolveDamageFactorList_Invalid()
    {
        var hediff = new HediffStageData();
        SetSavedLoadData(hediff, new Dictionary<string, object?>
        {
            ["damageFactors"] = Encoded(new DamageFactor { damageDef = DefRef<DamageDef>("NonExistentDamage_XYZ"), factor = 0.5f })
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
            ["skillGains"] = Encoded(
                new SkillGain { skill = DefRef<SkillDef>("Shooting"), amount = 5 },
                new SkillGain { skill = DefRef<SkillDef>("Cooking"), amount = 3 })
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
            ["skillGains"] = Encoded(new SkillGain { skill = DefRef<SkillDef>("NonExistentSkill_ZZZ"), amount = 5 })
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
            ["aptitudes"] = Encoded(
                new Aptitude { skill = DefRef<SkillDef>("Shooting"), level = 2 },
                new Aptitude { skill = DefRef<SkillDef>("Crafting"), level = 1 })
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
            ["aptitudes"] = Encoded(new Aptitude { skill = DefRef<SkillDef>("NonExistentSkill_ZZZ"), level = 1 })
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
            ["forcedTraits"] = Encoded(
                new TraitRequirement { def = DefRef<TraitDef>("Kind"), degree = 0 },
                new TraitRequirement { def = DefRef<TraitDef>("Industriousness"), degree = 2 })
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
            ["forcedTraits"] = Encoded(new TraitRequirement { def = DefRef<TraitDef>("NonExistentTrait_ZZZ"), degree = 0 })
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
            ["products"] = Encoded(
                new ThingDefCountClass { thingDef = DefRef<ThingDef>("Steel"), count = 10 },
                new ThingDefCountClass { thingDef = DefRef<ThingDef>("Cloth"), count = 5 })
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
            ["products"] = Encoded(new ThingDefCountClass
            {
                thingDef = DefRef<ThingDef>("Plasteel"),
                stuff = DefRef<ThingDef>("Steel"),
                count = 5,
            })
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
            ["products"] = Encoded(new ThingDefCountClass { thingDef = DefRef<ThingDef>("NonExistentThing_XYZ"), count = 10 })
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
            ["skillRequirements"] = Encoded(
                new SkillRequirement { skill = DefRef<SkillDef>("Crafting"), minLevel = 8 },
                new SkillRequirement { skill = DefRef<SkillDef>("Medicine"), minLevel = 6 })
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
            ["skillRequirements"] = Encoded(new SkillRequirement { skill = DefRef<SkillDef>("NonExistentSkill_ZZZ"), minLevel = 5 })
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
            ["processIngredients"] = Encoded(
                new ProcessIngredientItem { thing = DefRef<ThingDef>("Steel"), countNeeded = 5f },
                new ProcessIngredientItem { thingCategory = DefRef<ThingCategoryDef>("ResourcesRaw"), countNeeded = 3f })
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
            ["processIngredients"] = Encoded(new ProcessIngredientItem
            {
                thing = DefRef<ThingDef>("Steel"),
                disallowedThingDefs = new List<ThingDef> { DefRef<ThingDef>("WoodLog"), DefRef<ThingDef>("Cloth") },
                countNeeded = 10f,
            })
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
            ["processResults"] = Encoded(
                new ProcessResultItem { thing = DefRef<ThingDef>("Steel"), count = 5 },
                new ProcessResultItem { thing = DefRef<ThingDef>("Plasteel"), count = 3 })
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
            ["statOffsets"] = Encoded(                                              // 有效
                new StatModifier { stat = DefRef<StatDef>("MoveSpeed"), value = 0.5f },
                new StatModifier { stat = DefRef<StatDef>("MeleeDamageFactor"), value = 2.0f }),
            ["aptitudes"] = Encoded(new Aptitude { skill = DefRef<SkillDef>("Shooting"), level = 3 }),  // 有效
            ["abilities"] = new List<string> { "NonExistentAbility_ZZZ" }           // 无效
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

    // ═══════════════════════ 运行期自检 ═══════════════════════

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
    /// 只测 <b>Def / DefList 字段</b>的往返 —— 这部分<b>必须</b>在游戏内跑：解码要 <c>DefDatabase</c>
    /// 把 defName 查回真 Def 实例，测试进程里做不到。
    /// <para>
    /// 生成器的其它字段保持默认值（不写进文本），所以这条只回答"Def 引用能不能原样往返"。
    /// 非 Def 字段的同类测试在游戏外：<c>StockGeneratorCodecTests.全部原版类型的非Def字段往返</c>。
    /// </para>
    /// </summary>
    private static void testStockGen_DefFieldsRoundTrip()
    {
        var problems = new List<string>();
        int coveredFields = 0;

        foreach (var type in StockGeneratorCodec.VanillaGeneratorTypes())
        {
            var gen = (StockGenerator)Activator.CreateInstance(type)!;
            int filled = 0;
            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                if (field.Kind != PocoCodec.Kind.Def && field.Kind != PocoCodec.Kind.DefList) continue;
                var sample = SampleValueOf(field, StockGeneratorCodec.ReadField(field, gen));
                if (sample == null) continue; // 该 Def 类型本次加载里没有任何实例（可选 Mod 未装等）
                StockGeneratorCodec.WriteField(field, gen, sample);
                filled++;
            }
            if (filled == 0) continue;

            string line = StockGeneratorCodec.ToItemString(gen);
            Assert(!line.Contains('\n'), $"{type.Name}：一行不得含换行");
            Assert(StockGeneratorCodec.TryParse(line, out var back) && back != null,
                $"{type.Name}：Def 字段解析不了自己编码出来的行：{line}");
            if (back!.GetType() != type) { problems.Add($"{type.Name}: 往返后类型变成 {back.GetType().Name}"); continue; }
            if (StockGeneratorCodec.ToItemString(back) != line)
            {
                problems.Add($"{type.Name}: 再编码不一致\n{line}\n{StockGeneratorCodec.ToItemString(back)}");
                continue;
            }

            foreach (var field in StockGeneratorCodec.FieldsFor(type))
            {
                if (field.Kind != PocoCodec.Kind.Def && field.Kind != PocoCodec.Kind.DefList) continue;
                var expected = StockGeneratorCodec.ReadField(field, gen);
                var actual = StockGeneratorCodec.ReadField(field, back);
                if (!SameValue(expected, actual))
                    problems.Add($"{type.Name}.{field.Name}: {DescribeValueOf(expected)} != {DescribeValueOf(actual)}");
                coveredFields++;
            }
        }

        Assert(coveredFields > 0, "没有覆盖到任何 Def 字段（DefDatabase 为空？）");
        Assert(problems.Count == 0, $"{problems.Count} 处 Def 字段没往返一致（列出前 6 条）：\n{string.Join("\n", problems.Take(6))}");
    }

    /// <summary>按字段表的种类造一个"非默认"的样本值（<paramref name="baselineValue"/> 是新实例上的初始值）。</summary>
    private static object? SampleValueOf(PocoCodec.Field field, object? baselineValue)
    {
        switch (field.Kind)
        {
            case PocoCodec.Kind.Float: return 7.25f;
            case PocoCodec.Kind.Int: return 3;
            case PocoCodec.Kind.Bool: return !(baselineValue is bool b && b);
            case PocoCodec.Kind.Text: return "示例|标签=值;[x]\\y";
            case PocoCodec.Kind.IntRange: return new IntRange(2, 5);
            case PocoCodec.Kind.FloatRange: return new FloatRange(1.5f, 9.75f);
            case PocoCodec.Kind.Enum:
            {
                foreach (var value in Enum.GetValues(field.EnumType!))
                    if (!Equals(value, baselineValue)) return value;
                return null;
            }
            case PocoCodec.Kind.Def:
                return GenDefDatabase.GetAllDefsInDatabaseForDef(field.ElementType!).FirstOrDefault();
            case PocoCodec.Kind.DefList:
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(field.ElementType!))!;
                var def = GenDefDatabase.GetAllDefsInDatabaseForDef(field.ElementType!).FirstOrDefault();
                if (def != null) list.Add(def);
                return list.Count > 0 ? list : null;
            }
            case PocoCodec.Kind.StringList:
                return new List<string> { "first", "second|x" };
            case PocoCodec.Kind.ComplexList:
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

    /// <summary>
    /// 只测 <b>Def 引用</b>的往返：单个 Def、Def 列表、以及<b>嵌套在小对象列表元素里</b>的 Def。
    /// 三者都要真 <c>DefDatabase</c> 才能解码，所以留在游戏内。
    ///
    /// <para>
    /// 标量 / 区间 / 枚举 / 字符串列表 / 小对象列表的往返在游戏外覆盖
    /// （<c>StockGeneratorCodecTests.标量与嵌套字段往返</c>）。
    /// </para>
    /// <para>
    /// 第三条值得留意：<c>customCountRanges</c> 的元素类型自带 <c>thingDef</c> —— 它是
    /// "列表元素<b>内部</b>的 Def 字段"，游戏外那种"跳过顶层 Def 字段"的写法拦不住它，
    /// 解码照样要去查库。这正是它必须留在游戏内的原因。
    /// </para>
    /// </summary>
    private static void testStockGen_DefRefsRoundTrip()
    {
        // 单个 Def 引用（StockGenerator_SingleDef 的 thingDef 是 private）
        var single = new StockGenerator_SingleDef();
        SetGenField(single, "thingDef", ThingDefOf.Steel);
        var singleBack = RoundTripGen(single) as StockGenerator_SingleDef;
        Assert(singleBack != null && (ThingDef?)GetGenField(singleBack, "thingDef") == ThingDefOf.Steel,
            "单个 Def 引用 thingDef 应保持");

        // Def 列表
        var tag = new StockGenerator_Tag { tradeTag = "ExoticMisc", countRange = new IntRange(1, 4) };
        SetGenField(tag, "excludedThingDefs", new List<ThingDef> { ThingDefOf.Steel, ThingDefOf.Cloth });
        var tagBack = RoundTripGen(tag) as StockGenerator_Tag;
        var excluded = tagBack == null ? null : GetGenField(tagBack, "excludedThingDefs") as List<ThingDef>;
        Assert(excluded is { Count: 2 } && excluded[0] == ThingDefOf.Steel && excluded[1] == ThingDefOf.Cloth,
            "Def 列表 excludedThingDefs 应保持");

        // 嵌套在小对象列表元素里的 Def（ThingDefCountRangeClass.thingDef）
        var ranges = new StockGenerator_Tag
        {
            customCountRanges = new List<ThingDefCountRangeClass>
            {
                new(ThingDefOf.Steel, new IntRange(2, 6)),
                new(ThingDefOf.Cloth, new IntRange(7, 7)),
            },
        };
        var rangesBack = RoundTripGen(ranges) as StockGenerator_Tag;
        Assert(rangesBack != null && rangesBack.customCountRanges is { Count: 2 }, "customCountRanges 应保持 2 项");
        Assert(rangesBack!.customCountRanges![0].thingDef == ThingDefOf.Steel
            && rangesBack.customCountRanges[0].countRange.Equals(new IntRange(2, 6)),
            "嵌套元素里的 Def 引用应保持");
        Assert(rangesBack.customCountRanges[1].thingDef == ThingDefOf.Cloth
            && rangesBack.customCountRanges[1].countRange.Equals(new IntRange(7, 7)),
            "嵌套元素 [1] 里的 Def 引用应保持");
    }

    // ═══════════════════════ 辅助方法 ═══════════════════════

    /// <summary>
    /// 用<b>真实编码器</b>产出 <c>_savedLoadData</c> 里的文本 —— 与 <c>ExposeXxxList</c> 写进存档的东西一致。
    /// <para>
    /// ④ 第二步改了文本格式（<c>值|值</c> → <c>字段名=值</c>），这些测试随之不再手写格式字符串：
    /// 手写既容易写错，也偏离了"存档里真实长什么样"。格式本身由游戏外测试钉住
    /// （<c>SerializationCodecsTests</c>）。
    /// </para>
    /// </summary>
    private static List<string> Encoded<T>(params T[] items) where T : class
        => items.Select(item => PocoCodec.Encode(item)).ToList();

    /// <summary>
    /// 只有 defName 的合成 Def：用来构造"引用了某个 defName"的输入。
    /// 它只参与<b>编码</b>（编码只需要 defName），解析仍走真 <c>DefDatabase</c> ——
    /// 所以"故意写一个不存在的 defName"这类用例依然成立。
    /// </summary>
    private static T DefRef<T>(string defName) where T : Def, new() => new() { defName = defName };

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