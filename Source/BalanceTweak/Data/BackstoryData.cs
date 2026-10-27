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
/// 本类只覆盖**不需要新增编辑窗口**的字段：标量 / 字符串 / 枚举 / 已有窗口的列表
/// （SkillGain / Flags / StringList / DefSelector）。
/// </para>
/// <para>
/// <b>尚未覆盖</b>（都需要新建列表编辑器，见 Readme §7.4 C）：
/// <c>forcedTraits</c> / <c>disallowedTraits</c>（<c>List&lt;BackstoryTrait&gt;</c>）、
/// <c>possessions</c>（<c>List&lt;PossessionThingDefCountClass&gt;</c>）。
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

    /// <summary>翻译用的稳定标识；只影响语言包导出/导入，不影响游戏内表现。</summary>
    [TweakField(Style = ColumnStyle.String)]
    public string? identifier = null;

    [TweakField(Style = ColumnStyle.Enum, EnumType = typeof(BackstorySlot))]
    public BackstorySlot? slot = null;

    /// <summary>技能增益（技能 + 数值）；窗口复用 <c>SkillGainListEditorWindow</c>。</summary>
    [TweakField(Style = ColumnStyle.SkillGainList)]
    public List<SkillGain>? skillGains = null;

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

    /// <summary>名字生成器规则包（决定该背景的角色起名风格）。</summary>
    [TweakField(Style = ColumnStyle.DefSelector)]
    public RulePackDef? nameMaker = null;

    [TweakField(Style = ColumnStyle.Bool)]
    public bool? shuffleable = null;
    [TweakField(Style = ColumnStyle.Bool)]
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
            identifier ??= d.identifier;
            slot ??= d.slot;
            if (!IsFieldModified(nameof(skillGains))) skillGains ??= d.skillGains;
            workDisables ??= d.workDisables;
            requiredWorkTags ??= d.requiredWorkTags;
            if (!IsFieldModified(nameof(spawnCategories))) spawnCategories ??= d.spawnCategories;
            bodyTypeGlobal ??= d.bodyTypeGlobal;
            bodyTypeFemale ??= d.bodyTypeFemale;
            bodyTypeMale ??= d.bodyTypeMale;
            nameMaker ??= d.nameMaker;
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
        ApplyS(nameof(identifier), v => def.identifier = v);
        ApplyV<BackstorySlot>(nameof(slot), v => def.slot = v);
        // 这两个列表原版是无条件解引用的（FullDescriptionFor 遍历 skillGains、按 spawnCategories
        // 查冥想焦点，ConfigErrors 里也直接 .Contains），清空时必须给空列表而不是 null，否则看角色生平直接 NRE。
        ApplyL<SkillGain>(nameof(skillGains), v => def.skillGains = v ?? new List<SkillGain>());
        ApplyV<WorkTags>(nameof(workDisables), v => def.workDisables = v);
        ApplyV<WorkTags>(nameof(requiredWorkTags), v => def.requiredWorkTags = v);
        ApplyL<string>(nameof(spawnCategories), v => def.spawnCategories = v ?? new List<string>());
        ApplyD<BodyTypeDef>(nameof(bodyTypeGlobal), v => def.bodyTypeGlobal = v);
        ApplyD<BodyTypeDef>(nameof(bodyTypeFemale), v => def.bodyTypeFemale = v);
        ApplyD<BodyTypeDef>(nameof(bodyTypeMale), v => def.bodyTypeMale = v);
        ApplyD<RulePackDef>(nameof(nameMaker), v => def.nameMaker = v);
        ApplyV<bool>(nameof(shuffleable), v => def.shuffleable = v);
        ApplyV<bool>(nameof(requiresSpawnCategory), v => def.requiresSpawnCategory = v);

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
