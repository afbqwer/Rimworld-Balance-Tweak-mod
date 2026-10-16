using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public abstract partial class TweakData<T> : TweakData where T : TweakData<T>
{
    public override bool ResolveDefs()
    {
        if (_savedLoadData == null)
        {
            RecordClearedSnapshot();
            return true;
        }

        // Backup current field values so we can restore on partial failure,
        // preventing inconsistent state where some fields are resolved and others aren't.
        var backup = new Dictionary<string, object?>();
        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            if (_savedLoadData.ContainsKey(meta.Field.Name))
                backup[meta.Field.Name] = meta.Field.GetValue(this);
        }

        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;

            if (!TryResolveField(meta))
            {
                _corruptedFieldNames.Add(meta.Field.Name);
                continue;
            }
            _corruptedFieldNames.Remove(meta.Field.Name);
        }

        // 有损坏字段时：仅回滚损坏字段，保留成功解析的字段
        // 同时保留 _savedLoadData 供保存损坏字段的原始字符串
        if (_corruptedFieldNames.Count > 0)
        {
            foreach (var meta in Fields)
            {
                if (_corruptedFieldNames.Contains(meta.Field.Name) && backup.TryGetValue(meta.Field.Name, out var val))
                    meta.Field.SetValue(this, val);
            }
        }
        else
        {
            _savedLoadData = null;
        }
        RecordClearedSnapshot();
        return _corruptedFieldNames.Count == 0;
    }

    /// <summary>
    /// L0 自检用（运行期字段，不序列化）：ResolveDefs 末尾记下当前显式清空的字段名。
    /// 这些字段的语义是 Cleared，Apply 之后必须仍为空 —— 若被 SetDef / GenValues 的 <c>??=</c>
    /// 回填了，就是漏加守卫（见 Docs/NullSemantics_L0_Plan.md §4.2，由 SC2 校验）。
    /// 判据只看显式清空集合：这些字段本来就该保持空。
    /// </summary>
    private void RecordClearedSnapshot()
    {
        _ = Fields; // 保险：确保 _fieldIndex 已随 Fields 初始化
        _clearedSnapshot = new HashSet<string>(_clearedFieldNames);
    }

    /// <summary>
    /// 读档后还原一个字段 —— 只查一次登记表，不再有类型分派链
    /// （表在 <c>TweakData.FieldCodecs.cs</c>；加新类型只改那里）。
    /// <para>
    /// 三种情况必须分清：<br/>
    /// ① 表里查不到（<c>codec == null</c>）⇒ **漏登记**，是该字段静默失效的根因，报错但不标损坏
    /// （损坏的语义是"存档数据坏了"，与"我们的表漏了"不是一回事）；<br/>
    /// ② <c>Resolve == null</c> ⇒ **已登记、但不参与解析**（标量由 Scribe 在 LoadingVars 阶段直接落值）；<br/>
    /// ③ 有 <c>Resolve</c> ⇒ 正常解析，返回 <c>false</c> 才标损坏。
    /// </para>
    /// </summary>
    private bool TryResolveField(StatFieldMeta meta)
    {
        var codec = FieldCodecs.Find(meta.Field.FieldType);
        if (codec == null)
        {
            Log.Error($"[BalanceTweak] 字段类型未登记，该字段读档后不会还原："
                + $"{meta.Field.DeclaringType?.Name}.{meta.Field.Name}（{meta.Field.FieldType.Name}）"
                + $" —— 请加进 TweakData.FieldCodecs.cs 的 FieldCodecs.Build()。");
            return true;
        }
        if (codec.Resolve == null) return true; // 标量：不参与解析
        return codec.Resolve(this, meta);
    }
}
