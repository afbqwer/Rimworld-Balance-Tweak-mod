using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
namespace BalanceTweak;

/// <summary>
/// 角色背景（<see cref="BackstoryDef"/>）。
///
/// <para>
/// <b>覆盖范围</b>：除 <c>requiresSpawnCategory</c>（只读展示列）外的全部字段 ——
/// 字符串 / 列表 / Def 引用 / 布尔。
/// 其中 <c>forcedTraits</c> / <c>disallowedTraits</c>（<c>List&lt;BackstoryTrait&gt;</c>，窗口
/// <see cref="BackstoryTraitListEditorWindow"/>）与 <c>possessions</c>
/// （<c>List&lt;PossessionThingDefCountClass&gt;</c>，窗口 <see cref="PossessionListEditorWindow"/>）
/// 的元素类型已进 <c>PocoCodec</c> 字段表，存档与剪贴板走通用编解码。
/// </para>
/// <para>
/// <b>刻意不开放的四个字段</b>：<c>identifier</c>（只服务翻译导出，改了没有游戏内效果）、
/// <c>nameMaker</c>（起名规则包）、<c>slot</c>（分组键已经由 <see cref="GetPropType"/> 表达，
/// 单独一列只是重复信息）、<c>requiresSpawnCategory</c>（原版只在加载期校验）—— 都不建列。
/// </para>
/// <para>
/// <b>为什么改 <c>description</c> 而不是 <c>baseDesc</c></b>：<c>baseDesc</c> 只在
/// <see cref="BackstoryDef.PostLoad"/> 里用来给 <c>description</c> 兜底，之后全文再没有读者
/// （仅存档 XML 里出现过），改它不会改变任何显示。<c>description</c> 才是
/// <see cref="BackstoryDef.FullDescriptionFor"/> 与工具提示读取的字段。
/// </para>
/// </summary>
[TweakFor(typeof(BackstoryDef), SettingType.Backstory)]
class BackstoryData : TweakData<BackstoryData>
{
    [TweakField(Style = ColumnStyle.String)]
    public string? title = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? titleFemale = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? titleShort = null;
    [TweakField(Style = ColumnStyle.String)]
    public string? titleShortFemale = null;

    /// <summary>背景描述正文（= Def 的 <c>description</c>，角色创建面板里显示的那段）。</summary>
    [TweakField(Style = ColumnStyle.String)]
    public string? description = null;

    /// <summary>技能增益（技能 + 数值）；窗口复用 <c>SkillGainListEditorWindow</c>。</summary>
    [TweakField(Style = ColumnStyle.SkillGainList)]
    public List<SkillGain>? skillGains = null;

    /// <summary>该背景强制赋予的特性（特性 + 程度）。</summary>
    [TweakField(Style = ColumnStyle.BackstoryTraitList)]
    public List<BackstoryTrait>? forcedTraits = null;

    /// <summary>该背景禁止角色拥有的特性（特性 + 程度）。</summary>
    [TweakField(Style = ColumnStyle.BackstoryTraitList)]
    public List<BackstoryTrait>? disallowedTraits = null;

    /// <summary>随身物品（物品 + 数量区间）：角色生成时直接放进背包。</summary>
    [TweakField(Style = ColumnStyle.PossessionList)]
    public List<PossessionThingDefCountClass>? possessions = null;

    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(WorkTags))]
    public WorkTags? workDisables = null;
    [TweakField(Style = ColumnStyle.Flags, EnumType = typeof(WorkTags))]
    public WorkTags? requiredWorkTags = null;

    [TweakField(Style = ColumnStyle.StringList)]
    public List<string>? spawnCategories = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public BodyTypeDef? bodyTypeGlobal = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public BodyTypeDef? bodyTypeFemale = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public BodyTypeDef? bodyTypeMale = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? shuffleable = null;

    /// <summary>是否需要生成分类才可使用，只读：原版只在加载期校验，运行中改了只会让 Def 自相矛盾。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool)]
    public bool? requiresSpawnCategory = null;

    /// <summary>
    /// <see cref="BackstoryDef.DisabledWorkTypes"/> 的懒加载缓存（无失效机制）：
    /// 改完 <c>workDisables</c> 必须置 null，否则运行中的"禁用工作"列表不刷新。
    /// </summary>
    private static readonly FieldInfo? cachedDisabledWorkTypesField =
        Reflect.Field(typeof(BackstoryDef), "cachedDisabledWorkTypes");

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);
        if (this.def is BackstoryDef d)
        {
            // Def.label 默认是 defName，列表里可读性太差 —— 用背景标题（与原版 UI 同源）。
            label = d.title.NullOrEmpty() ? d.defName : d.TitleCapFor(Gender.None);
            searchString = label + d.defName + d.titleShort;

            title ??= d.title;
            titleFemale ??= d.titleFemale;
            titleShort ??= d.titleShort;
            titleShortFemale ??= d.titleShortFemale;
            description ??= d.description;
            if (!IsFieldModified(nameof(skillGains))) skillGains ??= d.skillGains;
            if (!IsFieldModified(nameof(forcedTraits))) forcedTraits ??= d.forcedTraits;
            if (!IsFieldModified(nameof(disallowedTraits))) disallowedTraits ??= d.disallowedTraits;
            if (!IsFieldModified(nameof(possessions))) possessions ??= d.possessions;
            workDisables ??= d.workDisables;
            requiredWorkTags ??= d.requiredWorkTags;
            if (!IsFieldModified(nameof(spawnCategories))) spawnCategories ??= d.spawnCategories;
            bodyTypeGlobal ??= d.bodyTypeGlobal;
            bodyTypeFemale ??= d.bodyTypeFemale;
            bodyTypeMale ??= d.bodyTypeMale;
            shuffleable ??= d.shuffleable;
            requiresSpawnCategory ??= d.requiresSpawnCategory;
        }
    }

    public override void Apply()
    {
        if (this.def is not BackstoryDef def)
        {
            Log.Error($"[BalanceTweak]{this}的def为{this.def}!");
            return;
        }
        ApplyS(nameof(title), v => def.title = v);
        ApplyS(nameof(titleFemale), v => def.titleFemale = v);
        ApplyS(nameof(titleShort), v => def.titleShort = v);
        ApplyS(nameof(titleShortFemale), v => def.titleShortFemale = v);
        ApplyS(nameof(description), v => def.description = v);
        // requiresSpawnCategory 是 Display 列（只读），不写回 Def。
        // 列表清空时一律写空列表而非 null：原版对这些字段大量无条件解引用
        // （FullDescriptionFor 遍历 skillGains、按 spawnCategories 查冥想焦点、
        //  StartingPawnUtility 遍历 possessions，ConfigErrors 里也直接 .Contains），
        // null 会让"查看角色生平 / 生成角色"直接 NRE。
        ApplyL<SkillGain>(nameof(skillGains), v => def.skillGains = v ?? new List<SkillGain>());
        ApplyL<BackstoryTrait>(nameof(forcedTraits), v => def.forcedTraits = v ?? new List<BackstoryTrait>());
        ApplyL<BackstoryTrait>(nameof(disallowedTraits), v => def.disallowedTraits = v ?? new List<BackstoryTrait>());
        ApplyL<PossessionThingDefCountClass>(nameof(possessions), v => def.possessions = v ?? new List<PossessionThingDefCountClass>());
        ApplyV<WorkTags>(nameof(workDisables), v => def.workDisables = v);
        ApplyV<WorkTags>(nameof(requiredWorkTags), v => def.requiredWorkTags = v);
        ApplyL<string>(nameof(spawnCategories), v => def.spawnCategories = v ?? new List<string>());
        ApplyD<BodyTypeDef>(nameof(bodyTypeGlobal), v => def.bodyTypeGlobal = v);
        ApplyD<BodyTypeDef>(nameof(bodyTypeFemale), v => def.bodyTypeFemale = v);
        ApplyD<BodyTypeDef>(nameof(bodyTypeMale), v => def.bodyTypeMale = v);
        ApplyV<bool>(nameof(shuffleable), v => def.shuffleable = v);

        // 缓存失效：DisabledWorkTypes 只在首次访问时按 workDisables 算一次，之后永不重算。
        if (StateOf(nameof(workDisables)) != FieldState.Unset || IsForceWriteField(nameof(workDisables)))
            cachedDisabledWorkTypesField?.SetValue(def, null);
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(BackstoryCategory).GetEnumNames().Select(s => "MST." + s).ToList();

    // 分类键 = 背景槽位（童年 / 成年），与原版角色创建页的两段划分一致。
    private enum BackstoryCategory
    {
        Childhood,
        Adulthood,
    }

    public override int GetPropType()
    {
        if (this.def is BackstoryDef def && def.slot == BackstorySlot.Adulthood)
            return (int)BackstoryCategory.Adulthood;
        return (int)BackstoryCategory.Childhood;
    }
}
