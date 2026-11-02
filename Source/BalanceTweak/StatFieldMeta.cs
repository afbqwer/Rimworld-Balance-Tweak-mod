using System;
using System.Collections.Generic;
using System.Reflection;
using BalanceTweak;
using RimWorld;
using Verse;
using static BalanceTweak.StatColumnConfig;


//  字段元数据缓存
public class StatFieldMeta
{
    public FieldInfo Field { get; }
    public TweakFieldAttribute Attr { get; }

    /// <summary>
    /// 字段类型是不是 <c>List&lt;T&gt;</c>。
    /// <para>
    /// 只有 List 需要在克隆时深拷贝（防止 clone 与原始数据共享列表引用）；其余引用类型
    /// （string / Def / TweakID……）都是共享引用。克隆循环据此提前跳过 ——
    /// 省掉的是"先反射读一遍才知道该不该读"，这在一行几十个字段、几万行时是纯浪费。
    /// </para>
    /// </summary>
    public bool IsList { get; }

    /// <summary>
    /// <c>List&lt;T&gt;</c> 的构造泛型类型；非 List 为 null。
    /// 预先算好，避免每次克隆都做一次 <c>MakeGenericType</c>（那比反射读字段还贵）。
    /// </summary>
    public Type? ListType { get; }

    // 延迟加载的 StatDef
    private StatDef? _statDef;
    private bool _statDefResolved = false;
    public bool isEquippedStat = false;
    public bool isStuffFactorStat = false;
    public bool isStuffOffsetStat = false;
    public StatDef? StatDef
    {
        get
        {
            if (!_statDefResolved)
            {
                _statDefResolved = true;
                if (!string.IsNullOrEmpty(Attr.StatDef))
                {
                    _statDef = DefDatabase<StatDef>.GetNamed(Attr.StatDef, false);
                    if (_statDef == null)
                    {
                        Log.Error($"[BalanceTweak] 无法找到 StatDef: {Attr.StatDef}");
                    }
                }
            }
            return _statDef;
        }
    }

    public Type? EnumType => Attr.EnumType;

    public FieldInfo? OptionsListField { get; }

    public IsAvailable? AvailableChecker { get; }

    /// <summary>按行判定留白的谓词（<see cref="TweakFieldAttribute.Hidden"/>），null 表示永远显示。</summary>
    public IsAvailable? HiddenChecker { get; }

    public GetAbstract? GetAbstractMethod { get; }

    public delegate object? DefaultValueGetter(TweakData data);
    public DefaultValueGetter? DefaultGetter { get; }

    public StatFieldMeta(Type type, FieldInfo field, TweakFieldAttribute attr)
    {
        Field = field;
        Attr = attr;

        // 字段类型在类型加载时就固定，算一次即可。这是"每行省十几次反射"的唯一代价。
        if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            IsList = true;
            ListType = field.FieldType;
        }
        isEquippedStat = attr.IsEquippedStat;
        isStuffFactorStat = attr.IsStuffFactorStat;
        isStuffOffsetStat = attr.IsStuffOffsetStat;

        if (!string.IsNullOrEmpty(attr.OptionsList))
        {
            OptionsListField = type.GetField(attr.OptionsList, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (OptionsListField == null)
            {
                Log.Error($"[BalanceTweak]无法找到OptionsList字段: {attr.OptionsList}");
            }
        }

        if (!attr.MayRequire.NullOrEmpty() && !field.FieldType.IsClass && Nullable.GetUnderlyingType(field.FieldType) == null)
        {
            Log.Error($"[BalanceTweak]MayRequire field must be nullable:{type}|{field}");
            return;
        }
        if (!string.IsNullOrEmpty(attr.Available))
        {
            var method = type.GetMethod(attr.Available, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? typeof(DataUtility).GetMethod(attr.Available, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
                AvailableChecker = (IsAvailable)Delegate.CreateDelegate(typeof(IsAvailable), method);
            else
                Log.Error($"[BalanceTweak]无法找到{attr.Available}");
        }
        if (!string.IsNullOrEmpty(attr.Hidden))
        {
            var method = type.GetMethod(attr.Hidden, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? typeof(DataUtility).GetMethod(attr.Hidden, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
                HiddenChecker = (IsAvailable)Delegate.CreateDelegate(typeof(IsAvailable), method);
            else
                Log.Error($"[BalanceTweak]无法找到{attr.Hidden}");
        }
        if (!string.IsNullOrEmpty(attr.GetAbstract))
        {
            var method = type.GetMethod(attr.GetAbstract, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
                GetAbstractMethod = (GetAbstract)Delegate.CreateDelegate(typeof(GetAbstract), method);
            else
                Log.Error($"[BalanceTweak]无法找到{attr.GetAbstract}");
        }
        if (!string.IsNullOrEmpty(attr.StatDef))
        {
            if (DefaultGetter != null) Log.Error($"[BalanceTweak]DefaultValueGetter{DefaultGetter}与{attr.StatDef}不可同时存在");
            DefaultGetter = data =>
            {
                var stat = StatDef;
                var def = data.def;
                if (def is AbilityDef ad && stat != null)
                {
                    return StatHelper.TryGetStat(ad, stat);
                }
                if (def is TerrainDef td && stat != null)
                {
                    return StatHelper.TryGetStat(td, stat);
                }
                if (stat == null || def is not ThingDef d) return null;
                if (attr.IsEquippedStat)
                {
                    return StatHelper.TryGetStatFromEquippedStat(d, stat);
                }
                else if (attr.IsStuffOffsetStat)
                {
                    return StatHelper.TryGetStatFromStuffOffsetStat(d, stat);
                }
                else if (attr.IsStuffFactorStat)
                {
                    return StatHelper.TryGetStatFromStuffFactorStat(d, stat);
                }
                else
                {
                    return StatHelper.TryGetStat(d, stat);
                }
            };
        }
    }
}

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class TweakFieldAttribute : Attribute
{
    public string? StatDef { get; set; }
    public string? MayRequire { get; set; }
    public bool IsStuffOffsetStat { get; set; } = false;
    public bool IsEquippedStat { get; set; } = false;

    public bool IsStuffFactorStat { get; set; } = false;
    // ColumnStyle { Float, Prec, Int, String, Link, Bool, Enum, Curve }
    // ColumnDataType { Field, Display }
    public ColumnStyle Style { get; set; } = ColumnStyle.Float;
    public ColumnDataType DataType { get; set; } = ColumnDataType.Field;
    /// <summary>
    /// 镜像字段：值属于<b>另一行</b>（如武器行的伤害/穿透来自关联弹丸），本行只是它的副本。
    /// <para>
    /// 它仍是 <see cref="ColumnDataType.Field"/> —— 可编辑、参与三态，编辑它等于编辑源行。
    /// （不开放"清空"：镜像是从源行同步过来的副本，清空后下一次 <c>EnsureDisplayValues</c> 又会把它同步回来，
    /// 点了等于没点 —— 见 <see cref="StatColumnConfig.canClear"/>。）
    /// 唯一被区别对待的是 <see cref="TweakData.EqualsData"/>：镜像<b>不参与</b>"是否已恢复原样"的判定。
    /// </para>
    /// <para>
    /// 为什么必须排除：源行一改，共享该源行的每一行副本都会被派生同步刷新（原版 <c>Bullet_Shotgun</c>
    /// 供泵动与链式霰弹枪）。若镜像参与相等判定，那些<b>本来就没改过</b>的行会因为副本值与原值不同
    /// 而永远"不等于原始数据"，<c>ResetSingleItem</c> / <c>ResetSelectedColumn</c> 末尾的
    /// <c>RemoveData</c> 就再也不会触发，脏行常驻已修改表（白占存档条目，每次读档还要重放一遍）。
    /// </para>
    /// </summary>
    public bool IsMirror { get; set; }
    /// <summary>检测是否显示时调用的方法名称（决定<b>整列</b>是否出现，按页签第一行求值一次）</summary>
    public string? Available { get; set; }

    /// <summary>
    /// 只落盘、不出现在表格里的字段。
    /// <para>
    /// 用于"一组字段由一个自定义编辑窗口统一编辑、没有逐列展示价值"的可选 Mod 数据
    /// （如 Fortified 的机械武器过滤）。它们照常参与三态 / 存档 / 克隆 / 重置，
    /// 只是从 <c>AllColumn</c> 里排除；窗口通过 <c>TweakData.GetFieldConfig</c> 按字段名取配置读写。
    /// </para>
    /// </summary>
    public bool NotAColumn { get; set; }
    /// <summary>
    /// 按<b>行</b>判定这一格要不要留白的方法名称（返回 true = 不显示）。
    /// <para>
    /// 和 <see cref="Available"/> 的区别：那个是列级（整列存在与否），这个只影响单行。
    /// 典型用法是"这一类行不适用" —— 例如人形种族的战斗力列，留白而不是显示 Null，
    /// 因为 Null 的语义是"算不出来"，而不是"这一列跟它无关"。
    /// </para>
    /// </summary>
    public string? Hidden { get; set; }
    /// <summary>数据被改变时调用的方法名称（已弃用，改为在 Apply 中触发）</summary>
    [Obsolete("OnChange 已弃用，请将逻辑移至 Apply 方法中直接调用。")]
    public string? OnChange { get; set; }
    /// <summary>获取默认值的方法名称（已弃用，请将默认值逻辑移至 SetDef 方法中直接调用）</summary>
    [Obsolete("Default 已弃用，请将默认值逻辑移至 SetDef 方法中直接调用。")]
    public string? Default { get; set; }
    /// <summary>获取默认的抽象值的方法名称</summary>
    public string? GetAbstract { get; set; }
    /// <summary>Enum类型，用于下拉菜单</summary>
    public Type? EnumType { get; set; }
    /// <summary>选项列表字段名称，用于自定义下拉菜单选项</summary>
    public string? OptionsList { get; set; }
}

