using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.TweakDatabase;

namespace BalanceTweak;

/// <summary>
/// [TweakField] 字段的三态（L0 语义）。
/// Unset   —— 用户没动过，Apply 时不得写 Def。
/// Set     —— 用户显式赋过值，Apply 时写入 Def。
/// Cleared —— 用户显式清空（见 <see cref="TweakData{T}._clearedFieldNames"/>）。
///
/// <para>
/// <b>Cleared 的语义只有一条：从集合里移除一项或全部项。</b>集合（<c>List&lt;T&gt;</c>）⇒ 写空列表；
/// stat 列 ⇒ 把该条 <c>StatModifier</c> 从 <c>statBases</c> / <c>equippedStatOffsets</c> 里移除；
/// 其余（标量 / 枚举 / string / Def 引用）本来就没有"清空"这种状态，编辑器也不给入口，
/// 万一出现按"还原原值"处理。<b>任何情况下都不写 null</b> —— 把字段或 Def 的集合置 null
/// 是早先对"清空"的误解（把"移除全部项"实现成了"把集合整个丢掉"），原版从不区分二者，
/// 而 null 列表在运行期等于错误状态。
/// </para>
///
/// 判据是两个显式集合：<see cref="TweakData{T}._modifiedFieldNames"/>（Set）
/// 与 <see cref="TweakData{T}._clearedFieldNames"/>（Cleared），互斥。
/// <b>绝不用"值 == null / 值为空"反推</b> —— 否则历史数据里被批量标记为"已改"的空值字段
/// 会被整片误清空（见 Docs/NullSemantics_L0_Plan.md）。
/// </summary>
public enum FieldState { Unset, Set, Cleared }

public abstract partial class TweakData : IExposable
{
    public Def? def;
    public TweakID id;
    public int propType = 0;
    public string modPackName = "";
    public string label = "";
    public string searchString = "";
    private Texture2D? _uiIcon;
    private bool _uiIconResolved;
    public Texture2D? uiIcon
    {
        get
        {
            if (!_uiIconResolved)
            {
                _uiIconResolved = true;
                _uiIcon = ResolveUiIcon();
            }
            return _uiIcon;
        }
        set { _uiIcon = value; _uiIconResolved = true; }
    }

    /// <summary>子类可重写：把图标解析推迟到首次访问。默认不延迟。</summary>
    protected virtual Texture2D? ResolveUiIcon() => null;

    public Color uiIconColor = Color.white;

    private string? _desc;
    private bool _descResolved;
    public string desc
    {
        get
        {
            if (!_descResolved)
            {
                _descResolved = true;
                _desc = ResolveDesc();
            }
            return _desc ?? string.Empty;
        }
        set { _desc = value; _descResolved = true; }
    }

    /// <summary>子类可重写：把只用于 tooltip 的描述推迟到首次访问。默认不延迟。</summary>
    protected virtual string ResolveDesc() => string.Empty;

    /// <summary>丢弃已解析的描述，让下一次访问重新走 ResolveDesc（用于延迟生成昂贵 Tooltip 的子类）。</summary>
    protected void ResetDescCache()
    {
        _desc = null;
        _descResolved = false;
    }
    /// 是否显示重置按钮
    public bool tweaked = false;
    public TweakID? parentTweakId;
    public int? index;
    // key:string value:string/List<string>
    protected Dictionary<string, object?>? _savedLoadData;
    public bool _backCompatResolved;
    protected HashSet<string> _modifiedFieldNames = new();
    /// <summary>
    /// 显式清空的字段（<see cref="FieldState.Cleared"/> 的唯一来源）。
    /// 由编辑器的"清空"动作写入（单元格输入 <c>null</c>），与 <see cref="_modifiedFieldNames"/> 互斥。
    /// <b>不再由"已改且值为空"反推</b> —— 这条反推会把迁移期批量标记的空值字段误判成清空。
    /// </summary>
    protected HashSet<string> _clearedFieldNames = new();
    protected HashSet<string> _corruptedFieldNames = new();
    /// <summary>
    /// 一次性的"强制重写 stat"开关（由各重置流程设置）。
    /// <para>已弃用（L0 起）：它在语义上等价于"把全部 stat 字段标为 Cleared"，而 L0 三态已能表达该意图。
    /// 暂保留以兼容既有重置路径（TweakUtility 的 4 处赋值），待 L0 稳定后另开提交删除。</para>
    /// </summary>
    public bool forceResetStats;
    /// <summary>
    /// 单字段重置用的一次性"权威字段集合"：本次 Apply 中，这些字段无视三态、无视 null 守卫，
    /// 一律按字段当前值写进 def。
    /// <para>
    /// <b>为什么需要它</b>：重置做的事是"把字段写回原值 + 清掉修改标记"，于是该字段变成
    /// <see cref="FieldState.Unset"/>。写作路径完全依赖三态（<c>ApplyV/ApplyN/ApplyL/ApplyD</c>），
    /// Unset ⇒ 什么都不写 —— 只要同行还有别的字段处于 Set，就走不到 <c>RemoveData</c> 的整行兜底，
    /// 重置变成"只改了界面"。stat 字段另有 Phase 2 的 null 守卫要越（null 即"删掉这一项"），
    /// 非 stat 字段只需"照抄值"。两者用同一个集合表达，避免出现两套并列的开关。
    /// </para>
    /// <para>
    /// <b>为什么不能复用 <see cref="forceResetStats"/></b>：那个是整个数据级的开关。若让它越过
    /// Phase 2 的"null 且未修改 ⇒ 跳过"守卫，同一次 Apply 里所有因 Phase 3 同步而为 null 的 stat
    /// 字段都会去删 def 上的条目 —— 重置列表列时就会把 Phase 1 刚恢复的条目再删掉。
    /// 重置单个字段只应影响那一个字段。
    /// </para>
    /// <para>由 <c>StatColumnConfig.ResetTo</c> 设置，<c>ApplyDefStats</c> 末尾与 <c>ResetTo</c> 的 finally 各清一次。</para>
    /// </summary>
    public HashSet<string>? forceWriteFields;
    /// <summary>
    /// 一次性"权威快照"开关：本次 <c>Apply</c> 中，字段的<b>当前值就是唯一真相</b>，一律写进 Def，
    /// 不再看 <see cref="FieldState"/>。
    /// <para>
    /// <b>为什么需要它</b>：整行重置（<c>TweakUtility.RemoveData</c>）与完全重置
    /// （<c>ApplyFullReset</c>）都是"拿原始数据的一份克隆替换掉当前行，再 Apply"。这份克隆一个修改标记
    /// 都没有 ⇒ 所有字段都是 <see cref="FieldState.Unset"/> ⇒ <c>ApplyV/ApplyN/ApplyL/ApplyD</c>
    /// 一个都不会写。stat 字段因为有 <see cref="forceResetStats"/>（Phase 1 整表重写）碰巧是对的，
    /// 非 stat 字段则被静默跳过：界面换成了原始克隆、看起来"重置成功"，Def 上还是改过的值。
    /// </para>
    /// <para>
    /// 语义是"照抄"，不是 <see cref="FieldState.Cleared"/>：<c>Cleared</c> 对集合写空列表、对标量走
    /// <c>Revert</c> 回查原值，而这里快照本身就是原值 —— 列表按原样写回（空列表就是空列表），
    /// 原快照里为 null 的列表也写成空列表（<c>ApplyL</c> 保证不给 Def 留 null 集合）。
    /// 与 <see cref="forceResetStats"/> 一起由 <see cref="ApplyAuthoritative"/> 设置并在 finally 里撤销，
    /// 不会漏到下一次 Apply。
    /// </para>
    /// </summary>
    public bool forceWriteAll;
    protected string? _idTag; // 缓存 id.ToString()

    public struct TweakID(string defName, SettingType settingType) : IExposable
    {
        public string defName = defName;
        public SettingType settingType = settingType;
        public override string ToString() => $"{defName}:{settingType}";
        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName", "");
            Scribe_Values.Look(ref settingType, "settingType");
        }
    }

    // 延迟计算的 id 字符串缓存，供 GUI 使用
    internal string IdTag => _idTag ??= id.ToString();

    public virtual void ExposeData()
    {
        _idTag = null;
        Scribe_Deep.Look(ref id, "id");
        Scribe_Deep.Look(ref parentTweakId, "parentTweakId");
        Scribe_Values.Look(ref index, "index");
        // Serialize modified field names
        {
            List<string>? modifiedList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
                modifiedList = _modifiedFieldNames.ToList();
            Scribe_Collections.Look(ref modifiedList, "modifiedFieldNames", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && modifiedList != null)
                _modifiedFieldNames = new HashSet<string>(modifiedList);
        }
        // Serialize explicitly-cleared field names（L0 显式清空）
        {
            List<string>? clearedList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
                clearedList = _clearedFieldNames.ToList();
            Scribe_Collections.Look(ref clearedList, "clearedFieldNames", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && clearedList != null)
                _clearedFieldNames = new HashSet<string>(clearedList);
        }
        // Note: _backCompatResolved is intentionally NOT serialized.
        // It is a runtime-only flag used during loading for backward compatibility
        // with saves from before _modifiedFieldNames was introduced.
        // Persisting it per-entry would cause unnecessary save file bloat.
        // Serialize corrupted field names
        {
            List<string>? corruptedList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
                corruptedList = _corruptedFieldNames.ToList();
            Scribe_Collections.Look(ref corruptedList, "corruptedFieldNames", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && corruptedList != null)
                _corruptedFieldNames = new HashSet<string>(corruptedList);
        }
    }

    #region 工具方法

    /// <summary>
    /// 空值判定：null / 空字符串 / 空集合 / 无任何允许项的 ThingFilter。
    /// 注意 <see cref="ThingFilter"/> 的分支必须排在 ICollection 之前。
    /// 放在非泛型基类，供 LoadAllFieldsForBackCompat、三态内核与运行期自检共用。
    /// </summary>
    public static bool IsEmptyValue(object? v) => v switch
    {
        null => true,
        string s => s.Length == 0,
        ThingFilter tf => tf.AllowedDefCount == 0,
        System.Collections.ICollection c => c.Count == 0,
        _ => false,
    };

    /// <summary>
    /// A1 判据：backcompat（代际 A 存档）阶段判定某字段是否算"改过"。
    ///
    /// - <paramref name="value"/> 非 null：简单类型（float?/int?/bool? 等）由 ExposeSimple 在
    ///   LoadingVars 阶段就 SetValue 到字段上了。
    /// - <paramref name="savedLoadData"/> 命中且值非空：复杂类型（Def / List&lt;&gt; / ThingFilter 等）
    ///   由 ExposeDef/ExposeList/ExposeStringList/ExposeIntList/ExposeIngredientFilter 在 LoadingVars
    ///   阶段写进 _savedLoadData（字段本身要等 ResolveDefs 才 SetValue）。
    ///   这些加载分支**无条件写 key**（值为 null / 空列表时也写），所以这里只认非空值 ——
    ///   否则会把所有未动过的列表字段误标成"已改"，L0 新语义一开就会被整片清空。
    /// </summary>
    internal static bool IsBackCompatModified(object? value, string fieldName,
        Dictionary<string, object?>? savedLoadData)
        => value != null
           || (savedLoadData?.TryGetValue(fieldName, out var saved) == true && !IsEmptyValue(saved));

    /// <summary>
    /// 所有具体的 TweakData 子类（自检 / 统计共用，缓存一次）。
    /// 口径：非抽象、非基类自身、且继承自 <see cref="TweakData"/>。
    /// </summary>
    public static IReadOnlyList<Type> AllConcreteDataTypes => _allConcreteDataTypes ??= typeof(TweakData).Assembly
        .GetTypes()
        .Where(t => !t.IsAbstract && t != typeof(TweakData) && typeof(TweakData).IsAssignableFrom(t))
        .OrderBy(t => t.Name)
        .ToList();

    private static IReadOnlyList<Type>? _allConcreteDataTypes;

    public static TweakData? GetData(TweakID? id)
    {
        if (id == null) return null;
        var d = cachedData[id.Value.settingType];
        if (d.TryGetValue(id.Value, out var data)) return data;
        return GetOriginal(id.Value);
    }

    public static List<TweakData> GetAllData(TweakID? id)
    {
        var l = new List<TweakData>();
        if (id == null) return l;
        var d = cachedData[id.Value.settingType];
        if (d.TryGetValue(id.Value, out var data)) l.Add(data);
        if (TryGetOriginal(id.Value, out var o)) l.Add(o!);
        if (tweakDatas.TryGetValue(id.Value, out var t)) l.Add(t);
        return l;
    }

    /// <summary>
    /// 取"当前生效"的实例：UI 缓存（克隆）优先 → 存档里的修改实例 → 原始快照。
    /// <para>
    /// 与 <see cref="GetData(TweakID?)"/> 的区别只有一个，但很致命：那个不查 <c>tweakDatas</c>。
    /// 初始化期 UI 缓存还是空的，它于是返回原始快照 —— 读到的是改动前的值。
    /// 跨行读依赖（武器读弹丸）必须用这个。
    /// </para>
    /// </summary>
    public static TweakData? GetLiveData(TweakID? id)
    {
        if (id == null) return null;
        var d = cachedData[id.Value.settingType];
        if (d.TryGetValue(id.Value, out var data) && data != null) return data;
        if (tweakDatas.TryGetValue(id.Value, out var t) && t != null) return t;
        return GetOriginal(id.Value);
    }

    /// <summary>
    /// 取一个<b>可以安全写入</b>的实例：保证不是原始快照。
    /// <para>
    /// 跨行写（在武器行改属于弹丸的伤害/穿透）时，弹丸那一行可能还没被 UI 建过缓存，
    /// 此时直接拿 <see cref="GetOriginal"/> 会写脏原始数据 —— 之后"还原原值"就会还原成改后的值。
    /// 这里克隆一份登记进缓存再返回。
    /// </para>
    /// </summary>
    public static TweakData? GetEditableData(TweakID? id)
    {
        if (id == null) return null;
        var d = cachedData[id.Value.settingType];
        if (d.TryGetValue(id.Value, out var data) && data != null) return data;
        if (tweakDatas.TryGetValue(id.Value, out var t) && t != null) return t;
        var original = GetOriginal(id.Value);
        if (original == null) return null;
        var clone = original.Clone();
        d.SetOrAdd(id.Value, clone);
        return clone;
    }

    /// <summary>
    /// 根据 Def 自动检测归属类型并取回对应的 TweakData。
    ///
    /// 分类完全走 TweakRegistry 的规则表 —— 与 TweakDatabase.Init() 用的是同一份声明，
    /// 不会再出现"两处各写一份分类逻辑然后漂移"的问题（旧实现里 Init() 用 deepCommonality、
    /// 这里用 deepCountPerCell，早就已经不一致了）。
    /// </summary>
    /// <param name="def">目标 Def；传 null 返回 null。</param>
    /// <param name="settingType">可选，指定 SettingType。若不指定则自动检测。</param>
    /// <param name="includeFallback">
    /// 自动检测时是否把兜底规则（<see cref="TweakRule.IsFallback"/>）也算进来。
    /// 默认 <c>false</c>，保持"未匹配即 null"的旧语义；UI 的「跳转到该 Def 的数据」需传 <c>true</c> ——
    /// 兜底收编的 Def（如 <c>MiscThingData</c> 覆盖的物品）在表里同样可编辑，
    /// 只认非兜底规则会把它们误判成"无对应数据"。
    /// </param>
    /// <returns>匹配的 TweakData，若未找到则返回 null</returns>
    public static TweakData? GetData(Def? def, SettingType? settingType = null, bool includeFallback = false)
    {
        if (def == null) return null;

        // 如果指定了 SettingType，直接使用
        if (settingType.HasValue)
            return GetData(new TweakID(def.defName, settingType.Value));

        var rule = FindRuleFor(def, includeFallback);
        return rule == null ? null : GetData(new TweakID(def.defName, rule.Tab));
    }

    /// <summary>
    /// 按规则表找出该 Def 的主类型规则。
    /// 非兜底规则按 Priority 降序取首个命中者；<paramref name="includeFallback"/> 为 true 时，
    /// 前者全不命中再退回兜底规则（仍须过其 Matcher 收容范围，与 <c>TweakDatabase.ClassifyAll</c> 一致）。
    /// </summary>
    public static TweakRule? FindRuleFor(Def def, bool includeFallback = false)
    {
        var defType = def.GetType();
        var candidates = TweakRegistry.Rules
            .Where(r => r.IsAvailable && r.DefType.IsAssignableFrom(defType))
            .OrderByDescending(r => r.Priority)
            .ToList();

        var primary = candidates.FirstOrDefault(r => !r.IsFallback && (r.Matcher == null || r.Matcher(def)));
        if (primary != null || !includeFallback) return primary;

        return candidates.FirstOrDefault(r => r.IsFallback && (r.Matcher == null || r.Matcher(def)));
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null || b == null) return a == b;
        return a switch
        {
            int i => Math.Abs(i - (int)b) == 0,
            float f => !FloatChanged(f, (float)b, 0.0001f),
            bool bv => bv == (bool)b,
            string s => s == (string)b,
            FloatRange fr => fr.Equals((FloatRange)b),
            IntRange ir => ir.Equals((IntRange)b),
            Enum e => e.Equals(b),
            _ => false
        };
    }

    public static string GenJumpString(TweakData data)
        => $"{"MST.JumpToDesc".Translate().RawText}{data.label}({data.id.settingType.ToString().Translate().RawText})";

    /// <summary>
    /// 显示值缓存是否已算过。Display 列不落盘、也不参与 Apply，只是 UI 的派生展示值，
    /// 所以统一在"第一次被读"时按需计算（见各数据类的 <see cref="EnsureDisplayValues"/>）。
    /// </summary>
    protected bool displayValuesEnsured;

    public virtual TweakData Clone()
    {
        var clone = (TweakData)MemberwiseClone();
        // 克隆出来的是新实例，缓存标记不能跟着抄：否则 UI 从原始快照克隆的行会直接跳过按需计算，
        // 屏幕上留下原始快照那一刻的值。
        clone.displayValuesEnsured = false;
        clone._modifiedFieldNames = new HashSet<string>(_modifiedFieldNames);
        clone._clearedFieldNames = new HashSet<string>(_clearedFieldNames);
        clone._corruptedFieldNames = new HashSet<string>(_corruptedFieldNames);
        clone._savedLoadData = _savedLoadData != null
            ? new Dictionary<string, object?>(_savedLoadData)
            : null;
        return clone;
    }
    public bool EqualsData(TweakData other)
    {
        foreach (var c in AllColumn)
        {
            // Display 列是派生展示值（可能延迟计算、原值也可能还没算过），不参与"是否已恢复原样"的判定。
            if (c.columnDataType == ColumnDataType.Display) continue;
            // 镜像列的值属于另一行（武器行的伤害/穿透来自弹丸）。源行一改，共享它的每一行副本都会被同步刷新，
            // 于是"我这一行什么都没改、但副本值与原值不同"成了常态 —— 若让它参与判定，这些行永远不等于原始数据，
            // ResetSingleItem / ResetSelectedColumn 末尾的 RemoveData 就不再触发，脏行常驻已修改表。
            // 用户真的改过镜像时，改动会被转移到源行（见 WeaponData.PushWeaponEditsToProjectile），
            // 本行不留标记，因此跳过它不会漏掉任何"用户意图"。
            if (c.isMirror) continue;
            // 三态：先比状态、再比值。否则"Cleared 字段的值恰好与原值相同"会被判成"未改"，
            // 让 ResetSingleItem / ResetSelectedColumn 误删用户的清空意图。
            if (StateOfField(c.fieldName) != other.StateOfField(c.fieldName))
                return false;
            if (!ValuesEqual(c.GetRawValue(this), c.GetRawValue(other)))
                return false;
        }
        return true;
    }

    /// <summary>显式赋值（Set）。与"清空"互斥。</summary>
    public void MarkFieldModified(string fieldName)
    {
        _modifiedFieldNames.Add(fieldName);
        _clearedFieldNames.Remove(fieldName);
        _corruptedFieldNames.Remove(fieldName);
        tweaked = true;
    }

    /// <summary>显式清空（Cleared）。由编辑器"清空"动作写入，与"赋值"互斥。</summary>
    public void MarkFieldCleared(string fieldName)
    {
        _clearedFieldNames.Add(fieldName);
        _modifiedFieldNames.Remove(fieldName);
        _corruptedFieldNames.Remove(fieldName);
        tweaked = true;
    }

    /// <summary>回到"未动过"：两个显式集合都清掉。</summary>
    public void RemoveFieldModified(string fieldName)
    {
        _modifiedFieldNames.Remove(fieldName);
        _clearedFieldNames.Remove(fieldName);
    }

    /// <summary>
    /// 把"被间接改动"的这条数据登记为修改项（<see cref="BalanceTweakSettings.tweakDatas"/>）。
    /// <para>
    /// 典型场景：武器行上的伤害/穿透其实是关联弹丸的镜像，在武器行改这两格 = 改弹丸。
    /// 改动必须落在弹丸那条数据上才会被 Scribe 保存，否则重启游戏就丢。
    /// </para>
    /// <para>
    /// 原始快照（<see cref="TweakDatabase.GetOriginal"/> 返回的那份）不得登记 ——
    /// 它一旦进了修改表就会被序列化，之后"还原原值"拿到的也是被改过的值。
    /// 调用方须先用 <see cref="GetEditableData"/> 拿到可写的实例。
    /// </para>
    /// </summary>
    public void MarkIndirectlyModified()
    {
        if (CountFieldModified() == 0) return;
        if (ReferenceEquals(TweakDatabase.GetOriginal(id), this)) return;
        tweakDatas.SetOrAdd(id, this);
    }
    /// <summary>Set ∪ Cleared 的字段数（<c>tweaked</c> 的判据）。</summary>
    public int CountFieldModified() => _modifiedFieldNames.Count + _clearedFieldNames.Count;
    /// <summary>是否被用户动过（Set 或 Cleared 皆算）。回填守卫必须用它 —— 否则已清空字段会被 <c>??=</c> 复活。</summary>
    public bool IsFieldModified(string fieldName) => _modifiedFieldNames.Contains(fieldName) || _clearedFieldNames.Contains(fieldName);
    public bool IsFieldCleared(string fieldName) => _clearedFieldNames.Contains(fieldName);
    public void ClearModifiedFields()
    {
        _modifiedFieldNames.Clear();
        _clearedFieldNames.Clear();
    }

    public void MarkFieldCorrupted(string fieldName) => _corruptedFieldNames.Add(fieldName);
    public void RemoveFieldCorrupted(string fieldName) => _corruptedFieldNames.Remove(fieldName);
    public int CountFieldCorrupted() => _corruptedFieldNames.Count;
    public bool IsFieldCorrupted(string fieldName) => _corruptedFieldNames.Contains(fieldName);
    public void ClearCorruptedFields() => _corruptedFieldNames.Clear();

    #endregion

    #region 可继承

    /// <summary>
    /// L0 自检 / 迁移报告用的只读入口：字段三态（见 Docs/NullSemantics_L0_Plan.md §4）。
    /// 实现在 <see cref="TweakData{T}"/>（<c>Fields</c> 在泛型侧），基类只给签名 ——
    /// 这样"只持有 <c>TweakData</c> 引用"的调用方（TweakDatabase / TweakSelfCheck）也能取状态，
    /// 而不必各自复制一份三态判据。
    /// </summary>
    internal virtual FieldState StateOfField(string fieldName) => FieldState.Unset;

    /// <summary>
    /// SC2（复活检测）：返回 Apply 之后被 SetDef / GenValues 回填复活的 Cleared 字段名（空 = 未复活）。
    /// 见 Docs/NullSemantics_L0_Plan.md §4.2。
    /// </summary>
    internal virtual List<string> DetectRevivedFields() => new();

    public abstract List<StatColumnConfig> AllColumn { get; }
    public abstract List<string> TypeStrings { get; }
    public virtual int LoadingOrd => 100;
    /// <summary>按需计算只用于 UI 显示的派生值；默认无操作。</summary>
    public virtual void EnsureDisplayValues() { }
    public abstract int GetPropType();
    public abstract void Apply();

    /// <summary>
    /// 把当前实例当作"权威快照"整体写回 Def（整行重置 / 完全重置用）：
    /// stat 列表 + 每个非 stat 字段都按实例里的值落盘，包括 null。
    /// <para>两个一次性开关都只在这一次 <c>Apply</c> 内有效，finally 保证异常路径也不残留。</para>
    /// </summary>
    public void ApplyAuthoritative()
    {
        bool prevWriteAll = forceWriteAll;
        forceWriteAll = true;
        forceResetStats = true;
        try
        {
            Apply();
        }
        finally
        {
            forceWriteAll = prevWriteAll;
            forceResetStats = false;
            forceWriteFields = null;
        }
    }
    public abstract bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false);
    public abstract void SetDef(Def def, SettingType type, bool tweaked);
    public virtual bool ResolveDefs() => true;
    public override string ToString() => id.ToString();
    public override bool Equals(object? obj)
        => obj is TweakData data && EqualityComparer<TweakID>.Default.Equals(id, data.id);
    public override int GetHashCode() => HashCode.Combine(id);

    #endregion
}