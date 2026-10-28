using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public abstract partial class TweakData<T> : TweakData where T : TweakData<T>
{
    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        this.def = def;
        id = new(def.defName, type);
        label = def.LabelCap;
        if (label.NullOrEmpty()) label = def.defName;
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        searchString = label + def.defName;
    }

    public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false)
    {
        this.def = data.def!;
        parentTweakId = data.id;
        modPackName = def.modContentPack?.Name ?? "Unknown";
        this.tweaked = tweaked;
        SetProps(def, type);
        return true;
    }

    protected void SetProps(Def def, SettingType type)
    {
        propType = GetPropType();
        try
        {
            if (def is ThingDef t)
            {
                if (t.uiIcon != null && t.uiIcon != BaseContent.BadTex)
                {
                    uiIcon = t.uiIcon;
                    uiIconColor = t.uiIconColor;
                }
                desc = $"{t.defName}\n{(t.description != null ? t.DescriptionDetailed : "")}";
            }
            else if (def is BuildableDef bd)
            {
                if (bd.uiIcon != null && bd.uiIcon != BaseContent.BadTex)
                {
                    uiIcon = bd.uiIcon;
                    uiIconColor = bd.uiIconColor;
                }
                desc = $"{bd.defName}\n{bd.description ?? ""}";
            }
            else
            {
                desc = $"{def.defName}\n{def.description ?? ""}";
            }
        }
        catch (Exception e)
        {
            if (def is BuildableDef bd)
            {
                if (bd.uiIcon != null && bd.uiIcon != BaseContent.BadTex)
                {
                    uiIcon = bd.uiIcon;
                }
                desc = $"{bd.defName}\n{bd.description ?? ""}";
            }
            Log.Warning(e.ToString());
        }
        InitFieldValue();
    }

    private void InitFieldValue()
    {
        foreach (var meta in DefaultedFields)
        {
            // 用 IsFieldModified（Set ∪ Cleared）：已清空的字段（值为 null）不得被默认值回填复活。
            if (meta.Field.GetValue(this) == null && !IsFieldModified(meta.Field.Name))
                meta.Field.SetValue(this, meta.DefaultGetter!.Invoke(this));
        }
    }

    /// <summary>
    /// 将当前 TweakData 中所有 [TweakField] 标记的 Stat 字段保存到指定 Def 中
    /// 再将 Def 上的最终值同步回字段以保持一致
    /// 由各子类的 Apply() 在持久化其他非 Stat 属性前调用。
    /// </summary>
    protected void ApplyDefStats(Def def)
    {
        bool anyWrite = false;

        // Phase 1: Write List<StatModifier> fields to def IF they were directly modified by the user.
        // Run before Phase 1A so individual StatDef values can overwrite specific entries,
        // giving StatDef fields priority over list fields.
        foreach (var meta in Fields)
        {
            bool isForceField = IsForceWriteField(meta.Field.Name);
            if (meta.Field.FieldType == typeof(List<StatModifier>) && (forceResetStats || isForceField || IsFieldModified(meta.Field.Name)))
            {
                var list = (List<StatModifier>?)meta.Field.GetValue(this);
                if (list == null)
                {
                    // 字段已改但空有两种来源：
                    //   ① forceResetStats：一次重置，需清掉 def 上的脏数据，防 Phase 3 把脏值回读进字段；
                    //   ② L0 Cleared：用户主动清空该列表，语义即为"把 def 上的对应列表也清掉"
                    //      —— 与 Phase 2 对单个 StatDef 字段的"value==null ⇒ 移除"对齐。
                    // 判据用 StateOf(f)==Cleared 而非裸 IsFieldModified：Cleared 只由显式清空写入，
                    // 未显式清空的空列表不会走到这里（Reset 的脏数据由 forceResetStats 兜）。
                    // isForceField：单字段重置该列时，原值可能就是 null，同样要把 def 上的列表清掉。
                    if (forceResetStats || isForceField || StateOf(meta.Field.Name) == FieldState.Cleared)
                    {
                        anyWrite = true;
                        switch (def)
                        {
                            case ThingDef td:
                                switch (meta.Field.Name)
                                {
                                    case "statBases": td.statBases = null; break;
                                    case "equippedStatOffsets": td.equippedStatOffsets = null; break;
                                    case "statOffsets": if (td.stuffProps != null) td.stuffProps.statOffsets = null; break;
                                    case "statFactors": if (td.stuffProps != null) td.stuffProps.statFactors = null; break;
                                }
                                break;
                            case BuildableDef bd:
                                if (meta.Field.Name == "statBases") bd.statBases = null;
                                break;
                            case AbilityDef ad:
                                if (meta.Field.Name == "statBases") ad.statBases = null;
                                break;
                        }
                    }
                    continue;
                }
                anyWrite = true;
                var copy = list.Select(sm => new StatModifier { stat = sm.stat, value = sm.value }).ToList();
                switch (def)
                {
                    case ThingDef td:
                        switch (meta.Field.Name)
                        {
                            case "statBases":
                                td.statBases = copy;
                                break;
                            case "equippedStatOffsets":
                                td.equippedStatOffsets = copy;
                                break;
                            case "statOffsets":
                                if (td.stuffProps != null)
                                    td.stuffProps.statOffsets = copy;
                                break;
                            case "statFactors":
                                if (td.stuffProps != null)
                                    td.stuffProps.statFactors = copy;
                                break;
                        }
                        break;
                    case BuildableDef bd:
                        if (meta.Field.Name == "statBases")
                            bd.statBases = copy;
                        break;
                    case AbilityDef ad:
                        if (meta.Field.Name == "statBases")
                            ad.statBases = copy;
                        break;
                }
            }
        }

        // Phase 2: Apply individual StatDef values from fields to def (overrides list values).
        // Only apply fields that were directly modified by the user,
        // otherwise unmodified fields with old values would undo Phase 1's statBases changes.
        foreach (var meta in Fields)
        {
            // 「本次被重置的就是这个字段」—— 它有权越过下面的 null 守卫（见 forceWriteFields 注释）
            bool isResetTarget = IsForceWriteField(meta.Field.Name);
            if (meta.StatDef != null && (forceResetStats || isResetTarget || IsFieldModified(meta.Field.Name)))
            {
                var value = (float?)meta.Field.GetValue(this);
                // Skip null-valued StatDef fields that were never directly modified.
                // These became null because Phase 3 synced from a list where the stat
                // was removed (e.g. user deleted the entry from statBases), not because
                // the user intentionally set the field to null. Without this guard,
                // TrySetStat(def, stat, null) would remove the stat from the list even
                // when Phase 1 just correctly restored it during a reset.
                //
                // 例外：isResetTarget。这就是"原值为 null 的字段"重置后回不去的那条路 ——
                // 重置已把字段写回 null 并清掉修改标记，守卫于是把这次删除吃掉，
                // 紧接着 Phase 3 又把 def 上的旧值同步回来，重置被自己的 Apply 撤销。
                // 只有被重置的那一个字段可以越过守卫；其它 stale-null 字段仍按上面的理由跳过。
                if (value == null && !IsFieldModified(meta.Field.Name) && !isResetTarget)
                    continue;
                anyWrite = true;
                if (def is ThingDef td)
                {
                    if (meta.Attr.IsEquippedStat)
                        StatHelper.TrySetStatFromEquippedStat(td, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                    else if (meta.Attr.IsStuffOffsetStat)
                        StatHelper.TrySetStatFromStuffOffsetStat(td, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                    else if (meta.Attr.IsStuffFactorStat)
                        StatHelper.TrySetStatFromStuffFactorStat(td, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                    else
                        StatHelper.TrySetStat(td, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                }
                else if (def is BuildableDef bd)
                {
                    StatHelper.TrySetStat(bd, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                }
                else if (def is AbilityDef ad)
                {
                    StatHelper.TrySetStat(ad, meta.StatDef, value, meta.Attr.Style == ColumnStyle.Int);
                }
            }
        }

        // Phase 3: Sync stat modifier lists from def back to fields.
        // Only sync if something was written in Phase 1 or Phase 2.
        // Otherwise the def still holds stale modified values and would
        // overwrite the correct original field values (e.g. after a reset).
        if (!anyWrite && !forceResetStats) return;

        foreach (var meta in Fields)
        {
            if (meta.Field.FieldType == typeof(List<StatModifier>))
            {
                List<StatModifier>? source = def switch
                {
                    ThingDef td => meta.Field.Name switch
                    {
                        "statBases" => td.statBases,
                        "equippedStatOffsets" => td.equippedStatOffsets,
                        "statOffsets" => td.stuffProps?.statOffsets,
                        "statFactors" => td.stuffProps?.statFactors,
                        _ => null
                    },
                    BuildableDef bd => meta.Field.Name switch
                    {
                        "statBases" => bd.statBases,
                        _ => null
                    },
                    AbilityDef ad => meta.Field.Name switch
                    {
                        "statBases" => ad.statBases,
                        _ => null
                    },
                    _ => null
                };
                meta.Field.SetValue(this, source?.Select(sm => new StatModifier { stat = sm.stat, value = sm.value }).ToList());
            }
            else if (meta.StatDef != null)
            {
                if (def is ThingDef td)
                {
                    float? value;
                    if (meta.Attr.IsEquippedStat)
                        value = td.equippedStatOffsets?.Find(s => s.stat == meta.StatDef)?.value;
                    else if (meta.Attr.IsStuffOffsetStat)
                        value = td.stuffProps?.statOffsets?.Find(s => s.stat == meta.StatDef)?.value;
                    else if (meta.Attr.IsStuffFactorStat)
                        value = td.stuffProps?.statFactors?.Find(s => s.stat == meta.StatDef)?.value;
                    else
                        value = td.statBases?.Find(s => s.stat == meta.StatDef)?.value;
                    meta.Field.SetValue(this, value);
                }
                else if (def is BuildableDef bd)
                {
                    float? value = bd.statBases?.Find(s => s.stat == meta.StatDef)?.value;
                    meta.Field.SetValue(this, value);
                }
                else if (def is AbilityDef ad)
                {
                    float? value = ad.statBases?.Find(s => s.stat == meta.StatDef)?.value;
                    meta.Field.SetValue(this, value);
                }
            }
        }
        forceResetStats = false;
        // 注意：这里不能清 forceWriteFields。各数据类的 Apply() 都是先调 ApplyDefStats
        // 再逐个 ApplyV/ApplyN/ApplyL —— 在这清掉，后面的非 stat 字段就又变回"看三态"了。
        // 它只由设置方（ResetTo / ApplyAuthoritative）的 finally 撤销。
    }


    private static List<StatFieldMeta>? _fields;
    public static List<StatFieldMeta> Fields => _fields ??= InitStaticFields();

    /// <summary>Fields 中带 DefaultGetter 的字段（惰性初始化一次），InitFieldValue 只遍历这些。</summary>
    private static List<StatFieldMeta>? _defaultedFields;
    private static List<StatFieldMeta> DefaultedFields => _defaultedFields ??= Fields.Where(f => f.DefaultGetter != null).ToList();

    /// <summary>字段名 → 元数据 的索引，随 <see cref="Fields"/> 的惰性初始化一起建（供 RawVal 用）。</summary>
    private static Dictionary<string, StatFieldMeta>? _fieldIndex;

    private static List<StatColumnConfig>? _allColumn;
    public override List<StatColumnConfig> AllColumn => _allColumn ??= Fields
            .Select(f => new StatColumnConfig(f.Field,
                style: f.Attr.Style,
                type: f.Attr.DataType,
                stat: f.StatDef,
                isAvailable: f.AvailableChecker,
                getAbstract: f.GetAbstractMethod,
                isSPStat: f.isEquippedStat || f.isStuffFactorStat || f.isStuffOffsetStat,
                enumType: f.EnumType,
                optionsListField: f.OptionsListField,
                isMirror: f.Attr.IsMirror
                ))
            .ToList();

    private static List<StatFieldMeta> InitStaticFields()
    {
        _fields = new List<StatFieldMeta>();
        _fieldIndex ??= new Dictionary<string, StatFieldMeta>();
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = field.GetCustomAttribute<TweakFieldAttribute>();
            if (attr == null || !DataUtility.MayRequire(attr.MayRequire)) continue;
            var meta = new StatFieldMeta(typeof(T), field, attr);
            _fields.Add(meta);
            _fieldIndex[field.Name] = meta;
        }
        return _fields;
    }

    #region L0 三态内核（Unset / Set / Cleared）

    /// <summary>
    /// 运行期自检用（不序列化）：ResolveDefs 末尾记录"当前 modified ∧ 空"的字段名，
    /// 供 Apply 之后校验是否被 SetDef / GenValues 回填复活（见 Docs/NullSemantics_L0_Plan.md §4.2）。
    /// </summary>
    protected HashSet<string>? _clearedSnapshot;

    /// <summary>已经告警过的字段（每字段每局只 Log 一次，避免日志刷屏）。</summary>
    private HashSet<string>? _revertWarned;

    /// <summary>
    /// 读字段的原始值，不做任何解释、不做回填。
    /// 与 <c>meta.Field.GetValue(this)</c> 等价，只是走 <see cref="_fieldIndex"/> 的 O(1) 查表，
    /// 免得 B 组的 1000 处替换都要现场遍历 <see cref="Fields"/>。
    /// </summary>
    protected object? RawVal(string f)
    {
        _ = Fields; // 保险：确保 _fieldIndex 已随 Fields 初始化
        return _fieldIndex![f].Field.GetValue(this);
    }

    /// <summary>
    /// 字段三态。判据是两个显式集合（Set=<see cref="_modifiedFieldNames"/>，Cleared=<see cref="_clearedFieldNames"/>），
    /// <b>绝不用"值 == null / 值为空"反推</b> —— Cleared 只由编辑器的清空动作（<see cref="MarkFieldCleared"/>）写入。
    /// </summary>
    protected FieldState StateOf(string f)
    {
        if (_clearedFieldNames.Contains(f)) return FieldState.Cleared;

        return _modifiedFieldNames.Contains(f) && !IsEmptyValue(RawVal(f))
            ? FieldState.Set : FieldState.Unset;
    }

    /// <summary>跨类型只读入口（供 TweakDatabase 的 dry-run 与 TweakSelfCheck 用）。</summary>
    internal override FieldState StateOfField(string f) => StateOf(f);

    /// <summary>
    /// SC2（复活检测）：检查 <see cref="_clearedSnapshot"/> 里的字段在 Apply 之后是否仍为空。
    /// 被 SetDef / GenValues 的 <c>??=</c> 回填 = 复活（漏了 <c>!IsFieldModified</c> 守卫）。
    /// 见 Docs/NullSemantics_L0_Plan.md §4.2。
    /// </summary>
    internal override List<string> DetectRevivedFields()
    {
        if (_clearedSnapshot == null || _clearedSnapshot.Count == 0) return new List<string>();
        var revived = new List<string>();
        foreach (var f in _clearedSnapshot)
        {
            if (!_fieldIndex!.TryGetValue(f, out var meta)) continue;
            if (!IsEmptyValue(meta.Field.GetValue(this))) revived.Add(f);
        }
        return revived;
    }

    /// <summary>
    /// 本次 Apply 中该字段是否按"权威值"写入（不看三态）：
    /// <see cref="forceWriteAll"/> 覆盖所有字段（整行/完全重置），
    /// <see cref="forceWriteFields"/> 只覆盖被重置的那一个字段（单元格重置）。
    /// </summary>
    protected bool IsForceWriteField(string f)
        => forceWriteAll || (forceWriteFields != null && forceWriteFields.Contains(f));

    /// <summary>
    /// L0 三态 helper 的访问级别为 <c>protected internal</c>（原计划为 <c>protected</c>）：
    /// 同程序集的静态辅助类 <see cref="RecipeProcessHelper.ApplyProcessDef"/> 需要统一走这几个入口，
    /// 否则 ProcessDef 的字段写入会绕过门控（B 组执行记录里有说明）。
    /// </summary>
    protected internal void ApplyV<TV>(string f, Action<TV> set)
    {
        // 权威字段（整行/完全重置，或单字段重置）：不看三态，字段值即真相。
        // 目标是非空值类型而字段为 null 时不写 —— 没有可写的值，保持 Def 现状。
        if (IsForceWriteField(f))
        {
            if (RawVal(f) is TV tv) set(tv);
            else if (!typeof(TV).IsValueType) set(default!);
            return;
        }
        switch (StateOf(f))
        {
            case FieldState.Set: set((TV)RawVal(f)!); break;
            case FieldState.Cleared: Revert(f, set); break;
        }
    }

    /// <summary>目标可空（<c>float?</c> / <c>IntRange?</c> / <c>QualityCategory?</c> 等）：Cleared = 置 null。</summary>
    protected internal void ApplyN<TV>(string f, Action<TV?> set) where TV : struct
    {
        if (IsForceWriteField(f)) { set((TV?)RawVal(f)); return; }
        switch (StateOf(f))
        {
            case FieldState.Set: set((TV?)RawVal(f)); break;
            case FieldState.Cleared: set(null); break;
        }
    }

    /// <summary>集合（<c>List&lt;T&gt;</c> / ThingFilter）：Cleared = 清空。</summary>
    protected internal void ApplyL<TV>(string f, Action<List<TV>?> set)
    {
        if (IsForceWriteField(f)) { set(RawVal(f) as List<TV>); return; }
        switch (StateOf(f))
        {
            case FieldState.Set: set((List<TV>)RawVal(f)!); break;
            case FieldState.Cleared: set(null); break;
        }
    }

    /// <summary>Def 引用：Cleared = 还原原引用。</summary>
    protected internal void ApplyD<TV>(string f, Action<TV?> set) where TV : Def
    {
        if (IsForceWriteField(f)) { set(RawVal(f) as TV); return; }
        switch (StateOf(f))
        {
            case FieldState.Set: set((TV?)RawVal(f)); break;
            case FieldState.Cleared: Revert(f, set); break;
        }
    }

    /// <summary>
    /// string 目标。与 <see cref="ApplyV{TV}"/> 的区别：字符串的"空"在旧版里是<b>非 null 的空串</b>，
    /// 旧代码判据是 <c>!= null</c>（空串照样写）⇒ 非 Cleared 时照写，保持与旧版逐字段一致。
    /// <see cref="FieldState.Cleared"/> ⇒ 还原原值（判据只看显式清空集合，不再用"值为空 + 已改"反推）。
    /// 编辑器不对 String 列开放清空，故 Cleared 分支通常只出现在程序化 / 测试场景。
    /// </summary>
    protected internal void ApplyS(string f, Action<string> set)
    {
        // 权威字段：原来是非 null 的字符串写回原串，原来是 null 的也写回 null（原样照抄）。
        if (IsForceWriteField(f)) { set((RawVal(f) as string)!); return; }
        if (RawVal(f) is not string s) return;
        if (StateOf(f) == FieldState.Cleared) { Revert(f, set); return; }
        set(s);
    }

    /// <summary>
    /// ThingFilter 目标。<see cref="GUI.Editor.IngredientFilterEditorWindow"/> 的产物恒为非 null 过滤器，
    /// 旧版判据 <c>!= null</c> 恒真 ⇒ 非 null 即写入。空过滤器（AllowedDefCount == 0）在 RimWorld 里是
    /// "允许项为空"的有效值（＝清空该过滤器的允许项），不是 null 语义的 Clear，故不做 Cleared 分支。
    /// </summary>
    protected internal void ApplyF(string f, Action<ThingFilter> set)
    {
        if (RawVal(f) is ThingFilter tf) set(tf);
    }

    /// <summary>
    /// 还原：从 <c>GetOriginal(id)</c> 取同字段的原值写回。
    /// 原值缺席（老档 / 原始数据被换掉）时不抛异常、不写坏值 —— 保持现状 + 每字段告警一次。
    /// </summary>
    private void Revert<TV>(string f, Action<TV> set)
    {
        if (TweakDatabase.GetOriginal(id) is not TweakData<T> orig) { WarnRevertOnce(f); return; }
        if (orig.RawVal(f) is not TV v) { WarnRevertOnce(f); return; } // 原值缺席：保持现状
        set(v);
    }

    private void WarnRevertOnce(string f)
    {
        _revertWarned ??= new HashSet<string>();
        if (_revertWarned.Add(f))
            Log.Warning($"[BalanceTweak] {id} 的字段 {f} 无法还原（原值缺席），保持当前值。");
    }

    #endregion

    public override TweakData Clone()
    {
        var clone = (TweakData<T>)base.Clone();
        DeepCopyFields(clone);
        return clone;
    }

    /// <summary>
    /// 对所有 [TweakField] 引用类型字段进行深拷贝，
    /// 防止 clone 与 originalData 共享列表引用导致意外修改。
    /// </summary>
    private void DeepCopyFields(TweakData<T> clone)
    {
        foreach (var meta in Fields)
        {
            var original = meta.Field.GetValue(this);
            if (original == null) continue;

            var copied = DeepCopyValue(original, meta.Field.FieldType);
            if (!ReferenceEquals(copied, original))
                meta.Field.SetValue(clone, copied);
        }
    }

    private static object? DeepCopyValue(object value, Type fieldType)
    {
        // List<T>: 创建新 List 防止 Add/Remove/Clear 影响 originalData
        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)value;
            var elementType = fieldType.GetGenericArguments()[0];
            var newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            for (int i = 0; i < list.Count; i++)
                newList.Add(list[i]);
            return newList;
        }

        // 其他引用类型（string、Def 等不可变/单例类型）共享引用
        return value;
    }
}