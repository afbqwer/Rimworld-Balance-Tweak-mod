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
/// Set     —— 用户改过且值为非空，Apply 时写入 Def。
/// Cleared —— 用户改过但值为空（列表空/过滤器空/字段为 null），Apply 时按字段类别
///            "清空 Def"（集合）或"还原原值"（标量 / Def 引用）。
///
/// 判据只有 <see cref="TweakData{T}._modifiedFieldNames"/> —— 绝不用"值 == null"反推。
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
    public Texture2D? uiIcon;
    public Color uiIconColor = Color.white;

    public string desc = "";
    /// 是否显示重置按钮
    public bool tweaked = false;
    public TweakID? parentTweakId;
    public int? index;
    // key:string value:string/List<string>
    protected Dictionary<string, object?>? _savedLoadData;
    public bool _backCompatResolved;
    protected HashSet<string> _modifiedFieldNames = new();
    protected HashSet<string> _corruptedFieldNames = new();
    public bool forceResetStats;
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
    /// 根据 Def 自动检测归属类型并取回对应的 TweakData。
    ///
    /// 分类完全走 TweakRegistry 的规则表 —— 与 TweakDatabase.Init() 用的是同一份声明，
    /// 不会再出现"两处各写一份分类逻辑然后漂移"的问题（旧实现里 Init() 用 deepCommonality、
    /// 这里用 deepCountPerCell，早就已经不一致了）。
    /// </summary>
    /// <param name="def">目标 Def</param>
    /// <param name="settingType">可选，指定 SettingType。若不指定则自动检测。</param>
    /// <returns>匹配的 TweakData，若未找到则返回 null</returns>
    public static TweakData? GetData(Def def, SettingType? settingType = null)
    {
        if (def == null) return null;

        // 如果指定了 SettingType，直接使用
        if (settingType.HasValue)
            return GetData(new TweakID(def.defName, settingType.Value));

        var rule = FindRuleFor(def);
        return rule == null ? null : GetData(new TweakID(def.defName, rule.Tab));
    }

    /// <summary>
    /// 按规则表找出该 Def 的主类型规则（不含兜底规则，保持"未匹配即 null"的旧语义）。
    /// </summary>
    public static TweakRule? FindRuleFor(Def def)
    {
        var defType = def.GetType();
        return TweakRegistry.Rules
            .Where(r => r.IsAvailable && !r.IsFallback && r.DefType.IsAssignableFrom(defType))
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault(r => r.Matcher == null || r.Matcher(def));
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

    public virtual TweakData Clone()
    {
        var clone = (TweakData)MemberwiseClone();
        clone._modifiedFieldNames = new HashSet<string>(_modifiedFieldNames);
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
            if (!ValuesEqual(c.GetRawValue(this), c.GetRawValue(other)))
                return false;
        }
        return true;
    }

    public void MarkFieldModified(string fieldName)
    {
        _modifiedFieldNames.Add(fieldName);
        _corruptedFieldNames.Remove(fieldName);
        tweaked = true;
    }
    public void RemoveFieldModified(string fieldName) => _modifiedFieldNames.Remove(fieldName);
    public int CountFieldModified() => _modifiedFieldNames.Count;
    public bool IsFieldModified(string fieldName) => _modifiedFieldNames.Contains(fieldName);
    public void ClearModifiedFields() => _modifiedFieldNames.Clear();

    public void MarkFieldCorrupted(string fieldName) => _corruptedFieldNames.Add(fieldName);
    public void RemoveFieldCorrupted(string fieldName) => _corruptedFieldNames.Remove(fieldName);
    public int CountFieldCorrupted() => _corruptedFieldNames.Count;
    public bool IsFieldCorrupted(string fieldName) => _corruptedFieldNames.Contains(fieldName);
    public void ClearCorruptedFields() => _corruptedFieldNames.Clear();

    #endregion

    #region 可继承

    public abstract List<StatColumnConfig> AllColumn { get; }
    public abstract List<string> TypeStrings { get; }
    public virtual int LoadingOrd => 100;
    public abstract int GetPropType();
    public abstract void Apply();
    public abstract bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false);
    public abstract void SetDef(Def def, SettingType type, bool tweaked);
    public virtual bool ResolveDefs() => true;
    public override string ToString() => id.ToString();
    public override bool Equals(object? obj)
        => obj is TweakData data && EqualityComparer<TweakID>.Default.Equals(id, data.id);
    public override int GetHashCode() => HashCode.Combine(id);

    #endregion
}