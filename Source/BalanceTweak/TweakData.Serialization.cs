using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public abstract partial class TweakData<T> : TweakData where T : TweakData<T>
{
    public override void ExposeData()
    {
        base.ExposeData();

        // Backward compatibility: if _modifiedFieldNames is empty and _backCompatResolved
        // is false, it might be an old save from before the field tracking was introduced.
        // Load all fields and record which ones are non-null.
        if (!_backCompatResolved && _modifiedFieldNames.Count == 0 && _clearedFieldNames.Count == 0 &&
            Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.PostLoadInit)
        {
            LoadAllFieldsForBackCompat();
            return;
        }

        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            if (!_modifiedFieldNames.Contains(meta.Field.Name)) continue;

            if (Scribe.mode == LoadSaveMode.Saving &&
                _corruptedFieldNames.Contains(meta.Field.Name) &&
                _savedLoadData != null &&
                _savedLoadData.TryGetValue(meta.Field.Name, out var rawValue))
            {
                // 损坏字段：从 _savedLoadData 保存原始字符串数据
                if (rawValue is string str)
                    Scribe_Values.Look(ref str!, meta.Field.Name);
                else if (rawValue is List<string> list)
                    Scribe_Collections.Look(ref list, meta.Field.Name, LookMode.Value);
                else
                    Log.Error($"[BalanceTweak] 损坏字段 {meta.Field.Name}（{id}）的 _savedLoadData 包含不支持的类型: {rawValue?.GetType()}");
            }
            else
            {
                ExposeField(meta); // 正常保存/加载
            }
        }
    }

    /// <summary>
    /// 落盘 / 读档一个字段 —— 只查一次登记表，不再有类型分派链
    /// （表在 <c>TweakData.FieldCodecs.cs</c>；加新类型只改那里）。
    /// </summary>
    private void ExposeField(StatFieldMeta meta)
    {
        var codec = FieldCodecs.Find(meta.Field.FieldType);
        if (codec == null)
        {
            // 未登记 ⇒ 该字段既不会落盘也不会读回（静默丢失）。开发期由 SC10 断言拦截；
            // 这里是运行期兜底 —— 宁可吵，也不要静默。
            Log.Error($"[BalanceTweak] 字段类型未登记，该字段不会落盘："
                + $"{meta.Field.DeclaringType?.Name}.{meta.Field.Name}（{meta.Field.FieldType.Name}）"
                + $" —— 请加进 TweakData.FieldCodecs.cs 的 FieldCodecs.Build()。");
            return;
        }
        codec.Expose(this, meta);
    }

    private void LoadAllFieldsForBackCompat()
    {
        foreach (var meta in Fields)
        {
            if (meta.Attr.DataType != ColumnDataType.Field) continue;
            ExposeField(meta);
        }

        // Only modify _modifiedFieldNames in PostLoadInit to avoid state mutation during LoadingVars,
        // which could cause issues if ExposeData is called again.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            foreach (var meta in Fields)
            {
                if (meta.Attr.DataType != ColumnDataType.Field) continue;
                var value = meta.Field.GetValue(this);
                // 判据见 TweakData.IsBackCompatModified：只认"值非 null"或"key 存在且值非空"。
                // 旧实现用 _savedLoadData.ContainsKey 判定，而那些 Expose* 的加载分支会
                // 无条件写 key（值是 null / 空列表时也写）⇒ 所有未动过的列表字段都被误标成"已改"，
                // L0 新语义一开就会被整片清空，故必须排除空值。
                if (IsBackCompatModified(value, meta.Field.Name, _savedLoadData))
                    _modifiedFieldNames.Add(meta.Field.Name);
            }
            _backCompatResolved = true;
        }
    }
}
