using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;


/// <summary>
/// 随机任务脚本（<see cref="QuestScriptDef"/>）。本页签只收"这个任务多久出现一次 / 多大难度"的旋钮，
/// 也就是 <see cref="NaturalRandomQuestChooser"/> 拿来算权重的那些字段，外加 <c>QuestGen</c> 读的挑战等级。
///
/// <para>
/// <b>抽取链路</b>（改完立即生效，这一层没有任何缓存）：
/// <c>NaturalRandomQuestChooser.ChooseNaturalRandomQuest</c> 先按人口分流
/// （<c>rootIncreasesPopulation</c>），再对候选集按 <c>GetNaturalRandomSelectionWeight</c> 加权随机。
/// 权重 = <c>rootSelectionWeight</c> × <c>rootSelectionWeightFactorFromPointsCurve</c>(点数)
/// × 近期重复惩罚 × 皇家恩惠因子；但先过五道门槛（任一不过即归零）：
/// 权重 ≤ 0、点数 &lt; <c>rootMinPoints</c>、天数 &lt; <c>rootEarliestDay</c>、
/// 进度 &lt; <c>rootMinProgressScore</c>、该任务仍在 <c>minRefireDays</c> 冷却内。
/// 所以"某任务总也不出"通常是门槛把它归零了，而不是权重本身太小。
/// </para>
/// <para>
/// <b>三个布尔列不是装饰</b>：<c>randomlySelectable</c> 是 <c>IsRootRandomSelected</c> 的一半判据
/// （权重非零 + 它），关掉等于把该任务从随机池里摘出去；<c>rootIncreasesPopulation</c> 决定它落在
/// "增员 / 非增员"哪个池（两个池按 <c>QuestTuning.IncreasesPopQuestChanceByPopIntentCurve</c> 分别抽，
/// 池间不互相竞争）；<c>canGiveRoyalFavor</c> 在玩家想要皇家恩惠时会把非恩惠任务的权重乘上一条随时间回升的惩罚曲线。
/// </para>
/// <para>
/// <b>写入时机与例外</b>：以上字段都在"每次抽任务时现读"。唯一的缓存是 <c>CanRun</c> 的
/// <c>lastCheckCanRunTick</c>（按 tick + 点数缓一帧级的结果），但它只缓存 <c>root.TestRun</c> 的结论，
/// 不参与上面的权重公式，所以改这些字段不需要额外刷新。
/// </para>
/// <para>
/// <b><c>autoAccept</c> 与 <c>expireDaysRange</c> 是互斥的</b>：两者同时生效时，原版
/// <c>QuestScriptDef.ConfigErrors</c> 会报"autoAccept but there is an expireDaysRange set"；
/// 反过来权重 &gt; 0 而接受期限没设，会报"rootSelectionWeight &gt; 0 but expireDaysRange not set"。
/// 这里<b>不做任何拦截或自动纠正</b> —— 报错只写日志、不影响运行，是否让这两条出现由用户自己权衡。
/// </para>
/// <para>
/// <b>刻意不开放的字段</b>（要么与平衡无关，要么改错会直接炸生成期）：
/// </para>
/// <list type="bullet">
/// <item><c>root</c>（<c>QuestNode</c> 树）：任务的全部内容（生成什么、要求什么、给什么奖励）都在这里，
/// 不是"一个数值"，没有可编辑的列形态；改它等于重写任务。</item>
/// <item><c>hideOnCleanup</c> / <c>sendAvailableLetter</c> / <c>defaultHidden</c> /
/// <c>nameMustBeUnique</c> / <c>epic</c> / <c>epicParent</c> / <c>endOnColonyMove</c> /
/// <c>hideInvolvedFactionsInfo</c> / <c>questAvailableLetterTextIsDescription</c>：流程与表现开关，
/// 不改变"出现频率 / 难度 / 奖励量"。</item>
/// <item><c>isRootSpecial</c>：由事件 / 书籍 / 仪式直接指定的任务（不走随机池），改了分组也不会让它变成随机任务。</item>
/// <item><c>givenBy</c>（<see cref="QuestGiverTag"/> 列表）、<c>layerWhitelist</c> / <c>layerBlacklist</c> /
/// <c>canOccurOnAllPlanetLayers</c> / <c>everAcceptableInSpace</c> / <c>neverPossibleInSpace</c>：
/// 来源与地图层白名单，属于"能不能拿到"而不是"多久拿到一次"；
/// 太空层三个字段还依赖 Odyssey，缺 DLC 时全是默认值。</item>
/// <item><c>affectedByPoints</c> / <c>affectedByPopulation</c>：<b>只被调试窗口的筛选器读</b>
/// （<c>DebugActionsQuests</c>），对正常抽取没有任何影响，放开只会误导。</item>
/// <item><c>questDescriptionRules</c> / <c>questNameRules</c> / <c>questContentRules</c> / <c>questSubjectRules</c>：
/// 文案语法包，改错会让任务名 / 描述在生成期抛异常。</item>
/// </list>
/// </summary>
[TweakFor(typeof(QuestScriptDef), SettingType.Quest)]
class QuestData : TweakData<QuestData>
{
    /// <summary>参与随机抽取的基础权重（按权重随机，不是概率）。0 = 不进随机池。</summary>
    [TweakField()]
    public float? rootSelectionWeight = null;

    /// <summary>按殖民地点数缩放上一条权重：横轴点数、纵轴倍率。</summary>
    [TweakField(Style = ColumnStyle.Curve)]
    public SimpleCurve? rootSelectionWeightFactorFromPointsCurve = null;

    /// <summary>低于这个点数权重直接归零。</summary>
    [TweakField()]
    public float? rootMinPoints = null;

    /// <summary>低于这个进度分权重直接归零（<c>StorytellerUtility.GetProgressScore</c>）。</summary>
    [TweakField()]
    public float? rootMinProgressScore = null;

    /// <summary>开局后最早第几天才可能抽到。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? rootEarliestDay = null;

    /// <summary>同名任务两次出现之间的最短间隔（天）。</summary>
    [TweakField()]
    public float? minRefireDays = null;

    /// <summary>接受期限（天，随机取区间内一值）。<c>-1~-1</c> = 不过期。</summary>
    [TweakField(Style = ColumnStyle.Range)]
    public FloatRange? expireDaysRange = null;

    /// <summary>
    /// 任务出现即自动接受，跳过"玩家点接受"这一步（<c>QuestGen</c> 里 <c>autoAccept</c> 的任务不给拒绝机会）。
    /// <para>
    /// <b>与 <see cref="expireDaysRange"/> 互斥</b>：原版 <c>QuestScriptDef.ConfigErrors</c> 对
    /// 「autoAccept 且 expireDaysRange 有效」和「权重 &gt; 0 但 expireDaysRange 未设」两种情况都会报一条配置错误。
    /// 是否让这两条报错出现由用户自己决定 —— 报错只写日志，不影响运行，但要清楚那是用户选择的结果。
    /// </para>
    /// </summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? autoAccept = null;

    /// <summary>敕令抽取权重（Royalty 的"敕令"走 <c>GetNaturalDecreeSelectionWeight</c>，与随机池互不影响）。</summary>
    [TweakField()]
    public float? decreeSelectionWeight = null;

    /// <summary>任务挑战等级，决定人数上限与负面事件的档位。<c>-1</c> = 未设置（沿用生成过程中的默认值）。</summary>
    [TweakField(Style = ColumnStyle.Int)]
    public int? defaultChallengeRating = null;

    /// <summary>是否允许被随机抽取。<c>false</c> 时即使权重非零也不进池（<c>IsRootRandomSelected</c> 的另一半判据）。</summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? randomlySelectable = null;

    /// <summary>是否算"增员任务"：决定它落在哪个抽取池，两个池的抽取概率互相独立。</summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? rootIncreasesPopulation = null;

    /// <summary>玩家想要皇家恩惠时，该任务是否算"给恩惠的"（否则其权重按天数惩罚曲线衰减）。</summary>
    [TweakField(Style = ColumnStyle.Bool)]
    public bool? canGiveRoyalFavor = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (def is not QuestScriptDef d) return;

        rootSelectionWeight ??= d.rootSelectionWeight;
        rootSelectionWeightFactorFromPointsCurve ??= d.rootSelectionWeightFactorFromPointsCurve;
        rootMinPoints ??= d.rootMinPoints;
        rootMinProgressScore ??= d.rootMinProgressScore;
        rootEarliestDay ??= d.rootEarliestDay;
        minRefireDays ??= d.minRefireDays;
        expireDaysRange ??= d.expireDaysRange;
        autoAccept ??= d.autoAccept;
        decreeSelectionWeight ??= d.decreeSelectionWeight;
        defaultChallengeRating ??= d.defaultChallengeRating;
        randomlySelectable ??= d.randomlySelectable;
        rootIncreasesPopulation ??= d.rootIncreasesPopulation;
        canGiveRoyalFavor ??= d.canGiveRoyalFavor;
    }

    /// <summary>
    /// 行标题取 <c>questNameRules</c> 拼出的任务名（见 <see cref="QuestNameResolver"/>）：
    /// 原版的任务脚本几乎都没有 <c>&lt;label&gt;</c>，直接显示就是 <c>OpportunitySite_BanditCamp</c>
    /// 这种类名，一屏几十行根本认不出是哪个任务。
    /// <para>拼不出来（规则里带 <c>[asker_nameDef]</c> 这类运行时变量）时回落 Def.label，不硬凑。</para>
    /// </summary>
    protected override string ResolveLabel()
    {
        if (def is not QuestScriptDef q) return base.ResolveLabel();
        // 作者显式写了 label（≠ defName）就尊重作者，不去猜。
        if (!q.label.NullOrEmpty() && q.label != q.defName) return base.ResolveLabel();
        return QuestNameResolver.TryResolve(q) ?? base.ResolveLabel();
    }

    public override void Apply()
    {
        if (this.def is not QuestScriptDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyV<float>(nameof(rootSelectionWeight), v => def.rootSelectionWeight = v);
        ApplyV<SimpleCurve>(nameof(rootSelectionWeightFactorFromPointsCurve), v => def.rootSelectionWeightFactorFromPointsCurve = v);
        ApplyV<float>(nameof(rootMinPoints), v => def.rootMinPoints = v);
        ApplyV<float>(nameof(rootMinProgressScore), v => def.rootMinProgressScore = v);
        ApplyV<int>(nameof(rootEarliestDay), v => def.rootEarliestDay = v);
        ApplyV<float>(nameof(minRefireDays), v => def.minRefireDays = v);
        ApplyV<FloatRange>(nameof(expireDaysRange), v => def.expireDaysRange = v);
        ApplyV<bool>(nameof(autoAccept), v => def.autoAccept = v);
        ApplyV<float>(nameof(decreeSelectionWeight), v => def.decreeSelectionWeight = v);
        ApplyV<int>(nameof(defaultChallengeRating), v => def.defaultChallengeRating = v);
        ApplyV<bool>(nameof(randomlySelectable), v => def.randomlySelectable = v);
        ApplyV<bool>(nameof(rootIncreasesPopulation), v => def.rootIncreasesPopulation = v);
        ApplyV<bool>(nameof(canGiveRoyalFavor), v => def.canGiveRoyalFavor = v);
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(QuestCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    /// <summary>
    /// 分组按"这个任务从哪儿来"判断，判据全是原版自己算 <c>IsRootRandomSelected</c> / <c>IsRootDecree</c> /
    /// <c>IsRootAny</c> 的方式，不按 defName 或 DLC 猜。
    /// </summary>
    private enum QuestCategory
    {
        /// <summary>随机池（<c>rootSelectionWeight != 0</c>）。</summary>
        Random,
        /// <summary>皇权敕令（<c>decreeSelectionWeight != 0</c>）。</summary>
        Decree,
        /// <summary>由事件 / 书籍 / 仪式直接指定的根任务（<c>isRootSpecial</c>）。</summary>
        Special,
        /// <summary>其余 —— 基本都是挂在本体任务下的子任务，只能被父任务生成。</summary>
        Other,
    }

    public override int GetPropType()
    {
        if (this.def is not QuestScriptDef def) return (int)QuestCategory.Other;

        // 注意用 rootSelectionWeight 而不是 IsRootRandomSelected：后者把 randomlySelectable 也算进来，
        // 一旦用户为了"临时关掉"把开关关了就再也找不到这一行（会被归到"其它"里）。
        if (def.rootSelectionWeight != 0f) return (int)QuestCategory.Random;
        if (def.decreeSelectionWeight != 0f) return (int)QuestCategory.Decree;
        if (def.isRootSpecial) return (int)QuestCategory.Special;
        return (int)QuestCategory.Other;
    }
}
