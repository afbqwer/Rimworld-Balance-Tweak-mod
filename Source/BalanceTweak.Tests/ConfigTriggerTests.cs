using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using UnityEngine;
using Verse;
using Xunit;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak.Tests;

/// <summary>
/// 触发列（<see cref="ColumnStyle.Config"/>）的接线守卫。
///
/// <para>
/// <b>为什么非有不可</b>：这一列的意义就是"新增一种配置不再改任何 switch"——
/// 代价是 <c>Editor</c> 写错、漏写、指向没有 <c>(TweakData)</c> 构造的类型时，
/// 编译期全绿、启动期只有一条 <c>Log.Error</c>，玩家点下去才发现没反应。
/// 这里把那条约定钉成两条断言：全库扫描（漏写）+ 真造一次窗口（写错）。
/// </para>
/// </summary>
public class ConfigTriggerTests
{
    // ── 全库扫描：所有 Config 列都必须能真的点开 ──

    /// <summary>
    /// 扫主程序集里每一个 <c>[TweakField(Style = ColumnStyle.Config)]</c> 字段，断言：
    /// ① 有 <c>Editor</c>；② 是 <see cref="Window"/> 子类；③ 有 <c>(TweakData)</c> 公开构造。
    /// <para>
    /// 纯元数据读取，不碰 <c>DefDatabase</c>，所以能在没有游戏数据的测试进程里跑。
    /// </para>
    /// </summary>
    [Fact]
    public void 每个Config触发列都必须声明可用的Editor()
    {
        var violations = new List<string>();

        foreach (var type in typeof(TweakData).Assembly.GetTypes())
        {
            if (type.IsAbstract || type.ContainsGenericParameters) continue;

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<TweakFieldAttribute>();
                if (attr == null || attr.Style != ColumnStyle.Config) continue;

                string where = $"{type.Name}.{field.Name}";
                if (attr.Editor == null)
                {
                    violations.Add($"{where}：缺少 Editor");
                    continue;
                }
                if (!typeof(Window).IsAssignableFrom(attr.Editor))
                    violations.Add($"{where}：Editor {attr.Editor.Name} 不是 Window 子类");
                else if (attr.Editor.GetConstructor(new[] { typeof(TweakData) }) == null)
                    violations.Add($"{where}：Editor {attr.Editor.Name} 缺少 (TweakData) 构造");
            }
        }

        Assert.True(violations.Count == 0,
            "ColumnStyle.Config 的触发列配置有问题（点了会没反应）：\n  " + string.Join("\n  ", violations));
    }

    // ── 真造一次窗口：验证生产路径（AllColumn → GetFieldConfig → CreateConfigWindow） ──

    /// <summary>只为这条测试存在的数据类。</summary>
    private class ProbeData : TweakData<ProbeData>
    {
        [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Config,
            Editor = typeof(ProbeConfigWindow))]
        public TweakData.TweakID? probeConfig = null;

        [TweakField(NotAColumn = true)]
        public float? probeValue = null;

        public override void Apply() { }
        public override List<string> TypeStrings => new();
        public override int GetPropType() => 0;
    }

    /// <summary>最小的窗口替身：只用来证明 <c>Activator</c> 那条路走得通。</summary>
    public class ProbeConfigWindow : Window
    {
        public readonly TweakData Data;

        public ProbeConfigWindow(TweakData data)
        {
            Data = data;
        }

        public override void DoWindowContents(Rect inRect) { }
    }

    private static (ProbeData Data, StatColumnConfig Column) Arrange()
    {
        var data = new ProbeData { id = new TweakData.TweakID("__BT_ConfigProbe__", SettingType.Gene) };
        return (data, data.AllColumn.Single(c => c.fieldName == nameof(ProbeData.probeConfig)));
    }

    [Fact]
    public void Config列_由字段声明的Editor造窗口()
    {
        var (data, column) = Arrange();

        Assert.Equal(ColumnStyle.Config, column.columnType);
        Assert.Equal(typeof(ProbeConfigWindow), column.configEditorType);

        var window = column.CreateConfigWindow(data);
        Assert.IsType<ProbeConfigWindow>(window);
        Assert.Same(data, ((ProbeConfigWindow)window!).Data);
    }

    /// <summary>
    /// 按钮文字默认取 <c>MST.{字段名}Edit</c>。这里不假设翻译表里有这条键
    /// （测试进程没有 Languages 目录），只断言"取的就是这个键"——<c>Translate()</c> 缺键时原样返回。
    /// </summary>
    [Fact]
    public void Config列_按钮默认用字段名加Edit后缀()
    {
        var (data, column) = Arrange();
        Assert.Equal("MST.probeConfigEdit", column.GetString(data));
    }

    /// <summary>
    /// 触发列的三条公共性质：不进派生值推导、不给列过滤、不参与涂抹。
    /// 前两条由 <see cref="IsTriggerStyle"/> 一个判据管，第三条由 <c>Display</c> 类型挡掉 —— 这里钉住第一条。
    /// </summary>
    [Fact]
    public void Config列_是触发列且没有列过滤()
    {
        Assert.True(IsTriggerStyle(ColumnStyle.Config));

        // filterKind 由私有 ResolveFilterKind 推出，这里读公开字段的结果。
        var (_, column) = Arrange();
        Assert.Equal(ColumnFilterKind.None, column.filterKind);
    }
}
