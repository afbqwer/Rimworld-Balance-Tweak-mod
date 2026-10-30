using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using RimWorld;
using Verse;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// L0 三态内核（Unset / Set / Cleared）与 BackCompat 字段跟踪 —— 从游戏内
/// <c>TweakDataSerializationTests</c> 搬来的<b>纯逻辑</b>部分。
///
/// <para>
/// <b>为什么这组能搬出来</b>：它们只碰 <see cref="TweakData"/> 的实例状态机
/// （<c>MarkField*</c> / <c>Apply*</c> / <c>StateOfField</c> / <c>EqualsData</c> /
/// <c>DetectRevivedFields</c> / <c>IsBackCompatModified</c>），
/// <b>不查 <c>DefDatabase</c></b>。实测确认：<c>ResolveDefs</c> 对
/// "不在 <c>_savedLoadData</c> 里"和"值为 null / 空列表"的字段都<b>提前返回</b>
/// （见 <c>TweakData.FieldCodecs.cs</c> 的 <c>ResolveList</c> 早退分支），
/// 所以空库下整条 <c>ResolveDefs</c> 不触库。
/// </para>
///
/// <para>
/// <b>这批是 L0 里最该进快速回路的部分</b>：<c>★T1</c>（未动过的列表字段不得被误标
/// ⇒ 否则旧档数据整片丢失）、<c>★T8</c>（状态不同即视为已改，保护清空意图）、
/// <c>★B1</c>、<c>★SC2</c> 全是"错了就静默毁数据"的那一类。
/// </para>
///
/// <para>
/// <b>边界（实测，不是推断）</b>：<see cref="DataUtility.MayRequire"/> 在测试进程里会抛
/// <c>TypeInitializationException</c>（<c>Verse.ModLister</c> 静态初始化失败），
/// 所以<b>凡带 <c>MayRequire</c> 属性的数据类都不能在游戏外构造</b>
/// （<c>RecipeData</c> 就是；<c>GeneData</c> 不是）。依赖真 Def 解析的用例留在游戏内。
/// </para>
/// </summary>
public class TweakDataStateTests
{
    // ═══════════════════════ B 组：Resolve 显式置空 + Apply 门控 ═══════════════════════

    /// <summary>
    /// ★B1：显式清空（Cleared）的列表字段，ResolveList 必须把它还原成<b>空集合</b> ——
    /// 既不能残留 SetDef 回填值，也不能留成 null（原版不区分二者，但 null 集合是运行期的错误状态）。
    /// </summary>
    [Fact]
    public void 已改但清的列表字段读档后是空集合()
    {
        var gene = new GeneData();
        gene.statOffsets = new List<StatModifier>(); // 用户清空（清空的产物就是空集合）
        gene.MarkFieldCleared("statOffsets");
        // 模拟读档：ExposeList 对空/null 列表不写 key，加载后 _savedLoadData 里该字段值为 null
        SetSavedLoadData(gene, new Dictionary<string, object?> { ["statOffsets"] = null });

        Assert.True(gene.ResolveDefs(), "ResolveDefs 应返回 true");
        Assert.NotNull(gene.statOffsets);
        Assert.Empty(gene.statOffsets);
    }

    /// <summary>
    /// 未改字段不触发 setter；已改非空 → 写入；"已改但值空"（未显式清空）⇒ 退化为 Unset，不写。
    /// ApplyS 兼容旧版判据（非 null 即照写）：空串在未 Cleared 时仍照写。
    /// </summary>
    [Fact]
    public void 未改字段不触发setter()
    {
        var g1 = new GeneData();
        bool called = false, listCalled = false;
        g1.ApplyV<float>("selectionWeight", v => called = true);
        g1.ApplyL<StatModifier>("statOffsets", v => listCalled = true);
        Assert.False(called, "未改字段不应触发标量 setter");
        Assert.False(listCalled, "未改字段不应触发列表 setter");

        var g2 = new GeneData { selectionWeight = 3f };
        g2.MarkFieldModified("selectionWeight");
        float captured = 0f;
        g2.ApplyV<float>("selectionWeight", v => captured = v);
        Assert.Equal(3f, captured, 3);

        var g3 = new GeneData { selectionWeight = null };
        g3.MarkFieldModified("selectionWeight");
        bool called3 = false;
        g3.ApplyV<float>("selectionWeight", v => called3 = true);
        Assert.False(called3, "已改但值空（未显式清空）⇒ Unset，不写");

        var g4 = new GeneData { defLabel = "" };
        g4.MarkFieldModified("defLabel");
        string? capS = null;
        bool calledS = false;
        g4.ApplyS("defLabel", v => { capS = v; calledS = true; });
        Assert.True(calledS, "空串在未 Cleared 时仍应照写（与旧版 != null 一致）");
        Assert.Equal("", capS);
    }

    /// <summary>
    /// 编辑器的「清空」落在集合列上：字段变<b>空集合</b>（不是 null）并登记为显式 Cleared，
    /// 清空后的状态可重复清（幂等，不会反复产生新状态）。
    /// </summary>
    [Fact]
    public void 清空集合列_字段变空集合且登记Cleared()
    {
        var id = new TweakData.TweakID("__BT_Test_ClearColumn__", BalanceTweakSettings.SettingType.Gene);
        var gene = new GeneData
        {
            id = id,
            def = new GeneDef { defName = "__BT_Test_ClearColumn__" },
            statOffsets = new List<StatModifier>
            {
                new() { stat = new StatDef { defName = "MoveSpeed" }, value = 0.2f },
            },
        };
        try
        {
            var col = gene.AllColumn.Single(c => c.fieldName == nameof(GeneData.statOffsets));
            Assert.True(col.CanClear(gene), "有内容的集合列应当可清空");

            col.ClearValue(gene);

            Assert.True(gene.IsFieldCleared(nameof(GeneData.statOffsets)));
            Assert.NotNull(gene.statOffsets);
            Assert.Empty(gene.statOffsets);
            Assert.Empty(((GeneDef)gene.def!).statOffsets); // Def 侧同样是空列表，绝不是 null
            Assert.False(col.CanClear(gene), "已经空了就没有可清的东西");
        }
        finally { BalanceTweakSettings.tweakDatas.Remove(id); }
    }

    /// <summary>
    /// 清空的语义只有"从集合里移除一项或全部项"：
    /// 集合 ⇒ setter 收到<b>空集合</b>（不是 null —— 原版不区分二者，但 null 集合会 NRE）；
    /// 可空标量根本没有"清空"这种状态 ⇒ 走还原，不写 null（null 在本工程里等于"重置"，功能重复）。
    /// </summary>
    [Fact]
    public void 清空后集合收到空列表()
    {
        var g1 = new GeneData();
        g1.statOffsets = new List<StatModifier>(); // 用户清空
        g1.MarkFieldCleared("statOffsets");
        List<StatModifier>? capL = null;
        bool calledL = false;
        g1.ApplyL<StatModifier>("statOffsets", v => { capL = v; calledL = true; });
        Assert.True(calledL);
        Assert.NotNull(capL);
        Assert.Empty(capL);
    }

    /// <summary>可空标量的 Cleared ⇒ 还原原值，<b>不</b>写 null（不制造"清空 = 重置"的重复语义）。</summary>
    [Fact]
    public void 可空标量清空走还原而非置null()
    {
        var id = new TweakData.TweakID("__BT_Test_NullableClear__", BalanceTweakSettings.SettingType.Gene);
        try
        {
            var orig = new GeneData { id = id, selectionWeight = 7f };
            TweakDatabase.AllOriginals[id] = orig;

            var g = new GeneData { id = id, selectionWeight = null };
            g.MarkFieldCleared("selectionWeight");
            float? captured = 1f;
            bool called = false;
            g.ApplyN<float>("selectionWeight", v => { captured = v; called = true; });
            Assert.True(called);
            Assert.Equal(7f, captured!.Value, 3);

            // 原值也缺席时保持现状（Revert 兜底），同样不会写 null
            TweakDatabase.AllOriginals.Remove(id);
            float? captured2 = 1f;
            bool called2 = false;
            g.ApplyN<float>("selectionWeight", v => { captured2 = v; called2 = true; });
            Assert.False(called2, "原值缺席：保持现状、不写、不抛异常");
        }
        finally { TweakDatabase.AllOriginals.Remove(id); }
    }

    /// <summary>标量 / Def 引用 / string 的 Cleared ⇒ 还原 GetOriginal(id) 的同字段原值。</summary>
    [Fact]
    public void 清空后还原原值()
    {
        var id = new TweakData.TweakID("__BT_Test_Original__", BalanceTweakSettings.SettingType.Gene);
        try
        {
            var orig = new GeneData { id = id };
            orig.passionModSkill = new SkillDef { defName = "Shooting" };
            orig.selectionWeight = 7f;
            orig.defLabel = "OrigLabel";
            TweakDatabase.AllOriginals[id] = orig;

            var g1 = new GeneData { id = id, passionModSkill = null };
            g1.MarkFieldCleared("passionModSkill");
            SkillDef? capD = null;
            bool calledD = false;
            g1.ApplyD<SkillDef>("passionModSkill", v => { capD = v; calledD = true; });
            Assert.True(calledD);
            Assert.Same(orig.passionModSkill, capD);

            var g2 = new GeneData { id = id, selectionWeight = null };
            g2.MarkFieldCleared("selectionWeight");
            float capV = 0f;
            bool calledV = false;
            g2.ApplyV<float>("selectionWeight", v => { capV = v; calledV = true; });
            Assert.True(calledV);
            Assert.Equal(7f, capV, 3);

            var g3 = new GeneData { id = id, defLabel = "" };
            g3.MarkFieldCleared("defLabel");
            string? capS = null;
            bool calledS = false;
            g3.ApplyS("defLabel", v => { capS = v; calledS = true; });
            Assert.True(calledS);
            Assert.Equal("OrigLabel", capS);
        }
        finally { TweakDatabase.AllOriginals.Remove(id); }
    }

    /// <summary>原值缺席（老档 / 原始数据被换掉）：保持现状、不抛异常、不写坏值（Revert 兜底）。</summary>
    [Fact]
    public void 原值缺席时不写也不抛()
    {
        var id = new TweakData.TweakID("__BT_Test_NoOriginal__", BalanceTweakSettings.SettingType.Gene);
        TweakDatabase.AllOriginals.Remove(id); // 确保无原值
        var g = new GeneData { id = id, selectionWeight = null };
        g.MarkFieldCleared("selectionWeight");
        bool called = false;
        g.ApplyV<float>("selectionWeight", v => called = true); // 不应抛异常
        Assert.False(called, "原值缺席：保持现状、不写、不抛异常");
    }

    // ═══════════════════════ C 组：三态 / 复活检测 ═══════════════════════

    /// <summary>跨类型只读入口 StateOfField：Cleared 只来自显式清空集合。</summary>
    [Fact]
    public void 三态判据跨类型一致()
    {
        var g = new GeneData { selectionWeight = null };
        g.MarkFieldCleared("selectionWeight");
        Assert.Equal(FieldState.Cleared, g.StateOfField("selectionWeight"));
        Assert.Equal(FieldState.Unset, g.StateOfField("biostatMet"));

        var g2 = new GeneData { selectionWeight = 2f };
        g2.MarkFieldModified("selectionWeight");
        Assert.Equal(FieldState.Set, g2.StateOfField("selectionWeight"));
    }

    /// <summary>
    /// ★T8：EqualsData 先比状态、再比值 —— 状态不同（Set vs Unset）即视为"已改"，
    /// 保护用户的修改/清空意图不被重置流程误删。
    /// </summary>
    [Fact]
    public void 相等判定先比状态再比值()
    {
        var modified = new GeneData { selectionWeight = 1f };
        modified.MarkFieldModified("selectionWeight");
        var untouched = new GeneData { selectionWeight = 1f };
        Assert.False(modified.EqualsData(untouched),
            "Set vs Unset ⇒ 视为已改（值虽相同，仍保护用户意图）");
    }

    /// <summary>
    /// ★SC2 判据：ResolveDefs 末尾记录的"modified ∧ 空"字段，回填前不报复活、被回填后必报。
    /// </summary>
    [Fact]
    public void 复活检测判据()
    {
        var gene = new GeneData();
        gene.MarkFieldCleared("statOffsets");
        SetSavedLoadData(gene, new Dictionary<string, object?> { ["statOffsets"] = null });
        Assert.True(gene.ResolveDefs());

        Assert.Empty(gene.DetectRevivedFields());

        // 模拟 SetDef / GenValues 的 ??= 回填（漏了 !IsFieldModified 守卫的那种）
        gene.statOffsets = new List<StatModifier> { new() { stat = new StatDef { defName = "MoveSpeed" }, value = 1f } };
        Assert.Contains("statOffsets", gene.DetectRevivedFields());
    }

    // ═══════════════════════ BackCompat PostLoadInit 字段跟踪 ═══════════════════════

    /// <summary>
    /// 模拟 BackCompat PostLoadInit：简单类型字段（float? / int? / bool?）靠"值非 null"被跟踪
    /// —— 它们在 LoadingVars 里已由 <c>ExposeSimple</c> 直接落值，不进 <c>_savedLoadData</c>。
    /// </summary>
    [Fact]
    public void 跟踪简单类型字段()
    {
        var gene = new GeneData
        {
            selectionWeight = 1.5f,   // 模拟 LoadingVars 已通过 ExposeSimple 设置值
            biostatMet = 2,
            canGenerateInGeneSet = true,
        };
        SetSavedLoadData(gene, new Dictionary<string, object?>());

        SimulateBackCompatPostLoadInit(gene);

        Assert.True(gene.IsFieldModified("selectionWeight"));
        Assert.True(gene.IsFieldModified("biostatMet"));
        Assert.True(gene.IsFieldModified("canGenerateInGeneSet"));
        Assert.True(gene.CountFieldModified() >= 3);
    }

    /// <summary>
    /// 模拟 BackCompat PostLoadInit：Def 类型字段靠 <c>_savedLoadData</c> 的 key 被跟踪
    /// —— LoadingVars 阶段 <c>ExposeDef</c> 不 SetValue（值由 ResolveDefs 设），字段本身仍是 null。
    /// </summary>
    [Fact]
    public void 跟踪Def字段()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?> { ["passionModSkill"] = "Shooting" });

        SimulateBackCompatPostLoadInit(gene);

        Assert.True(gene.IsFieldModified("passionModSkill"),
            "字段值为 null，但 _savedLoadData 有 key，必须被跟踪");
    }

    /// <summary>模拟 BackCompat PostLoadInit：列表类型字段同样靠 <c>_savedLoadData</c> 的 key 被跟踪。</summary>
    [Fact]
    public void 跟踪列表字段()
    {
        var gene = new GeneData();
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["statOffsets"] = Encoded(new StatModifier { stat = DefRef<StatDef>("MoveSpeed"), value = 0.2f }),
            ["abilities"] = new List<string> { "EntitySkip" },
            ["capMods"] = Encoded(new PawnCapacityModifier
            {
                capacity = DefRef<PawnCapacityDef>("Consciousness"),
                setMax = 0.5f,
            }),
            ["damageFactors"] = Encoded(new DamageFactor { damageDef = DefRef<DamageDef>("Arrow"), factor = 0.5f }),
        });

        SimulateBackCompatPostLoadInit(gene);

        Assert.True(gene.IsFieldModified("statOffsets"));
        Assert.True(gene.IsFieldModified("abilities"));
        Assert.True(gene.IsFieldModified("capMods"));
        Assert.True(gene.IsFieldModified("damageFactors"));
        Assert.True(gene.CountFieldModified() >= 4);
    }

    /// <summary>混合类型全部被跟踪：简单类型靠"值非 null"，Def/列表靠 key，互不遗漏。</summary>
    [Fact]
    public void 混合类型全部被跟踪()
    {
        var gene = new GeneData { selectionWeight = 1.0f };
        SetSavedLoadData(gene, new Dictionary<string, object?>
        {
            ["passionModSkill"] = "Shooting",
            ["statOffsets"] = Encoded(new StatModifier { stat = DefRef<StatDef>("MoveSpeed"), value = 0.2f }),
            ["abilities"] = new List<string> { "EntitySkip" },
        });

        SimulateBackCompatPostLoadInit(gene);

        Assert.True(gene.IsFieldModified("selectionWeight"));
        Assert.True(gene.IsFieldModified("passionModSkill"));
        Assert.True(gene.IsFieldModified("statOffsets"));
        Assert.True(gene.IsFieldModified("abilities"));
        Assert.True(gene.CountFieldModified() >= 4);
    }

    /// <summary>
    /// ★T1（反破坏红线）：代际 A 存档（没有 modifiedFieldNames 节点）+ 用户<b>从未动过</b>的
    /// 列表 / Def 字段 ⇒ 绝不能进 <c>_modifiedFieldNames</c>。
    ///
    /// <para>
    /// 背景：代际 A 的 <c>ExposeList</c> / <c>ExposeDefList</c> / <c>ExposeStringList</c> /
    /// <c>ExposeIntList</c> / <c>ExposeIngredientFilter</c> 在 LoadingVars 分支里<b>无条件写 key</b>
    /// （值为 null / 空列表时也写）。旧实现用 <c>_savedLoadData.ContainsKey</c> 判定"改过"，
    /// 于是所有未动过的列表字段都被误标，L0 新语义一开就会把它们整片清空 → 旧档数据丢失。
    /// 这是 A 组的最高风险项。
    /// </para>
    /// </summary>
    [Fact]
    public void 未动过的列表字段不得被误标()
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

        Assert.False(gene.IsFieldModified("statOffsets"), "未动过的 List<StatModifier> 不得进 _modifiedFieldNames（红线）");
        Assert.False(gene.IsFieldModified("abilities"), "未动过的 List<AbilityDef> 不得进 _modifiedFieldNames（红线）");
        Assert.False(gene.IsFieldModified("capMods"), "未动过的 List<PawnCapacityModifier> 不得进 _modifiedFieldNames（红线）");
        Assert.False(gene.IsFieldModified("damageFactors"), "未动过的 List<DamageFactor> 不得进 _modifiedFieldNames（红线）");
        Assert.False(gene.IsFieldModified("passionModSkill"), "空 defName 的 Def 字段不得进 _modifiedFieldNames");
        Assert.Equal(0, gene.CountFieldModified());

        // 反向校验：真正改过的字段仍必须被跟踪（别把启发式收过头）
        var gene2 = new GeneData();
        SetSavedLoadData(gene2, new Dictionary<string, object?>
        {
            ["abilities"] = new List<string> { "EntitySkip" },
            ["passionModSkill"] = "Shooting",
        });
        SimulateBackCompatPostLoadInit(gene2);
        Assert.True(gene2.IsFieldModified("abilities"), "非空列表必须被跟踪");
        Assert.True(gene2.IsFieldModified("passionModSkill"), "有效 defName 的 Def 字段必须被跟踪");
    }

    // ═══════════════════════ 辅助方法 ═══════════════════════

    /// <summary>
    /// 模拟 BackCompat PostLoadInit 阶段的字段跟踪逻辑。
    /// <c>LoadAllFieldsForBackCompat()</c> 的前半段（逐个 <c>ExposeField</c>）在无 <c>Scribe</c>
    /// 的进程里跑不了，所以这里只重演 <c>PostLoadInit</c> 那半边；<b>判据本身调用生产代码</b>
    /// （<see cref="TweakData.IsBackCompatModified"/>），不在测试里再抄一份、免得日后漂移。
    /// </summary>
    private static void SimulateBackCompatPostLoadInit(TweakData instance)
    {
        var saved = SavedLoadDataOf(instance);

        foreach (var meta in FieldsOf(instance))
        {
            var value = meta.Field.GetValue(instance);
            if (TweakData.IsBackCompatModified(value, meta.Field.Name, saved))
                instance.MarkFieldModified(meta.Field.Name);
        }
    }

    /// <summary>取该实例具体类型的 <c>Fields</c> 静态属性（静态属性不沿继承链搜索，需 FlattenHierarchy）。</summary>
    private static List<StatFieldMeta> FieldsOf(TweakData instance)
    {
        var prop = instance.GetType().GetProperty("Fields",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
        return (List<StatFieldMeta>)prop.GetValue(null)!;
    }

    private static Dictionary<string, object?>? SavedLoadDataOf(TweakData instance)
    {
        var field = typeof(TweakData).GetField("_savedLoadData",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Dictionary<string, object?>?)field.GetValue(instance);
    }

    private static void SetSavedLoadData(TweakData instance, Dictionary<string, object?> data)
    {
        var field = typeof(TweakData).GetField("_savedLoadData",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(instance, data);
    }

    /// <summary>用<b>真实编码器</b>产出 <c>_savedLoadData</c> 里的文本（与写进存档的东西一致）。</summary>
    private static List<string> Encoded<T>(params T[] items) where T : class
        => items.Select(PocoCodec.Encode).ToList();

    /// <summary>只有 defName 的合成 Def：编码只需要 defName，不查库。</summary>
    private static T DefRef<T>(string defName) where T : Def, new() => new() { defName = defName };
}
