using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BalanceTweak;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// <c>ListEditorWindow&lt;T&gt;</c> 的 <c>SerializeList</c> / <c>DeserializeList</c> 由
/// <b><c>abstract</c> 改成 <c>virtual</c></b>（默认走 <see cref="PocoCodec"/> 字段表）之后的
/// <b>契约守卫</b>（见 <c>Docs/Architecture_Improvement_Plan.md</c> §3.9 的 A 档）。
///
/// <para>
/// <b>为什么必须守着</b>：<c>abstract</c> 的强项是"必须表态" —— 新增一个列表窗口时编译器逼你想一次。
/// 改成 <c>virtual</c> 后，若新窗口的元素类型没进字段表、又忘了覆写，默认实现会返回 <c>null</c>：
/// 剪贴板复制与 Shift+拖拽涂抹就"按了没反应"，且**没有任何日志**。这不是数据损坏（可见的失效），
/// 但它正是 §3.9 要消灭的那类"漏改不响"。
/// </para>
/// <para>
/// 契约：<b>「<c>T</c> 已进字段表」与「该窗口自己覆写了 <c>SerializeList</c>」至少成立一个</b>。
/// 两个都不成立就是上面那个静默失效。
/// </para>
/// <para>
/// 两边都成立是<b>允许</b>的，但必须在 <see cref="IntentionalOverrides"/> 里登记并写明理由 ——
/// 那是"默认实现不够用"的信号，每多一个都值得停下来想一次：真需要，还是又把转发写回来了？
/// </para>
/// </summary>
public class ListEditorWindowContractTests
{
    /// <summary>
    /// 「元素类型已进字段表」却<b>仍然覆写</b>的窗口。允许存在，但必须写下理由 —— 否则
    /// 当初那 17 对纯转发就是这样长回来的。
    /// </summary>
    private static readonly Dictionary<string, string> IntentionalOverrides = new()
    {
        ["StockGeneratorListEditorWindow"] =
            "文本要带类型标记：StockGeneratorCodec.Serialize 写的是 stockGenerators 的格式"
            + "（每行带类型名，供非原版子类透传），PocoCodec.Encode 不带类型前缀。"
            + "另外粘贴走 OnPasteData 整体校验，DeserializeList 刻意留空。",
    };

    /// <summary>元素类型的窗口基类实例（<c>ListEditorWindow&lt;T&gt;</c> 是泛型定义，子类从它继承）。</summary>
    private static Type? ListWindowBaseOf(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ListEditorWindow<>)) return t;
        }
        return null;
    }

    /// <summary>全部具体的 <c>ListEditorWindow&lt;T&gt;</c> 子类。</summary>
    private static List<(Type Window, Type ElementType)> ConcreteListWindows()
    {
        return typeof(ListEditorWindow<>).Assembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .Select(t => (Window: t, Base: ListWindowBaseOf(t)))
            .Where(x => x.Base != null)
            .Select(x => (x.Window, ElementType: x.Base!.GetGenericArguments()[0]))
            .OrderBy(x => x.Window.Name)
            .ToList();
    }

    private static bool OverridesSerialize(Type window)
        => window.GetMethod("SerializeList",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null;

    [Fact]
    public void 契约_每个窗口都能序列化()
    {
        // 判据依赖声明表，必须先确保它建好了（PocoCodec 不会自己去拉 FieldCodecs）。
        // 少了这一句，本测试会随"其它测试是否先跑过"而时红时绿。
        TweakData.FieldCodecs.EnsureBuilt();

        var windows = ConcreteListWindows();
        Assert.True(windows.Count >= 23, $"只找到 {windows.Count} 个列表窗口，反射口径可能不对");

        var violations = new List<string>();
        foreach (var (window, elementType) in windows)
        {
            bool declared = PocoCodec.IsDeclared(elementType);
            bool overrides = OverridesSerialize(window);

            if (!declared && !overrides)
                violations.Add($"{window.Name}（T={elementType.Name}）：元素类型未进字段表，又没有覆写 "
                    + "SerializeList ⇒ 剪贴板/拖拽涂抹会静默失效（无日志）");
            else if (declared && overrides && !IntentionalOverrides.ContainsKey(window.Name))
                violations.Add($"{window.Name}（T={elementType.Name}）：元素类型已进字段表，却仍覆写了 "
                    + "SerializeList —— 若只是转发就删掉它；若确实需要（格式特殊等），"
                    + "加进 IntentionalOverrides 并写明理由");
        }

        Assert.True(violations.Count == 0,
            $"ListEditorWindow 契约被破坏（{violations.Count} 处）：\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// <see cref="IntentionalOverrides"/> 不许过期：登记过的窗口必须**仍然**是"已声明 + 覆写"。
    /// 否则它就成了遮掩——将来有人把覆写删了（或元素类型退出了字段表），名单却还挂着。
    /// </summary>
    [Fact]
    public void 有意覆写的登记不许过期()
    {
        TweakData.FieldCodecs.EnsureBuilt();
        var windows = ConcreteListWindows();

        foreach (var (name, reason) in IntentionalOverrides)
        {
            var hit = windows.FirstOrDefault(w => w.Window.Name == name);
            Assert.True(hit.Window != null, $"IntentionalOverrides 里的 {name} 已不存在");

            bool stillApplies = PocoCodec.IsDeclared(hit.ElementType) && OverridesSerialize(hit.Window);
            Assert.True(stillApplies,
                $"{name} 已不再满足「已声明 + 覆写」，请把它从 IntentionalOverrides 移除。"
                + $"当初的登记理由是：{reason}");
        }
    }

    /// <summary>
    /// 基类那两个方法必须仍然是"可覆写的 <c>virtual</c>"（而不是 <c>abstract</c> 或非虚）。
    /// 顺便钉住：它们确实是 <c>protected</c> —— 改可见性会让所有子类的覆写失去意义。
    /// </summary>
    [Fact]
    public void 基类默认实现可覆写且非抽象()
    {
        var baseType = typeof(ListEditorWindow<>);
        foreach (var name in new[] { "SerializeList", "DeserializeList" })
        {
            var method = baseType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True(method!.IsVirtual, $"{name} 应为 virtual");
            Assert.False(method.IsAbstract, $"{name} 不应退回 abstract（会让 17 个窗口再写一遍转发）");
            Assert.True(method.IsFamily, $"{name} 应为 protected");
        }
    }

    /// <summary>
    /// 反向核对：确实存在"元素类型未声明、靠自己覆写"的窗口 —— 说明上面那条契约不是在空跑
    /// （若哪天全都声明了，这条会失败，提醒把守卫简化掉）。
    /// </summary>
    [Fact]
    public void 仍存在必须自行覆写的窗口()
    {
        TweakData.FieldCodecs.EnsureBuilt();

        var needing = ConcreteListWindows()
            .Where(x => !PocoCodec.IsDeclared(x.ElementType) && OverridesSerialize(x.Window))
            .Select(x => $"{x.Window.Name}<{x.ElementType.Name}>")
            .ToList();

        Assert.True(needing.Count > 0,
            "已不存在需要自行覆写的列表窗口 —— 请复核本守卫是否还有意义");
    }
}
