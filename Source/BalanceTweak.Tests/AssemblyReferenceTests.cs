using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BalanceTweak;
using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 把「不得静态引用可选 Mod 的类型」这条硬约束变成可执行的断言。
///
/// <para>
/// <b>为什么读元数据而不是读源码</b>：真正决定程序集能不能加载的是 <c>TypeRef</c> 表，而不是源码长相。
/// <c>typeof</c> / <c>is</c> / 泛型参数 / 字段类型 / 方法签名 / 把变量捕获进 lambda（编译器给闭包类
/// 生成一个公共字段）都会在元数据里留下一条挂在外部程序集上的 <c>TypeRef</c>；
/// 该程序集缺席时，RimWorld 启动期对全部类型调 <c>GetFields()</c> 就 <c>TypeLoadException</c>，
/// 整个 Mod 一起失效。字符串写法（<c>"Fortified.Xxx"</c> + 反射）不会留 typeref，正是安全的做法。
/// </para>
///
/// <para>
/// 断言对象是测试实际加载的那个 <c>BalanceTweak.dll</c>（<c>ProjectReference</c> 的产物），
/// 与发布产物同一份源码、同一套引用。
/// </para>
/// </summary>
public class AssemblyReferenceTests
{
    /// <summary>允许依赖的程序集：游戏本体、Unity、Harmony、BCL。可选 Mod 一个都不允许。</summary>
    private static readonly HashSet<string> AllowedExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assembly-CSharp",   // 游戏本体（含 DLC 类型）
        "UnityStubs",        // 1.7 的 Unity 门面
        "Waylith",           // 1.7 新增的游戏程序集
        "0Harmony",
        "mscorlib",
        "netstandard",
        "System",
    };

    /// <summary>按前缀放行：BCL 子程序集与 1.6 拆分的 UnityEngine.* 模块。</summary>
    private static readonly string[] AllowedPrefix = { "System.", "UnityEngine", "Microsoft." };

    private static bool IsAllowed(string assemblyName)
        => AllowedExact.Contains(assemblyName)
        || AllowedPrefix.Any(p => assemblyName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void 不得静态引用可选Mod的类型()
    {
        var path = typeof(TweakData).Assembly.Location;
        using var stream = System.IO.File.OpenRead(path);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var violations = new List<string>();

        foreach (var handle in md.AssemblyReferences)
        {
            string name = md.GetString(md.GetAssemblyReference(handle).Name);
            if (!IsAllowed(name)) violations.Add($"程序集引用 {name}");
        }

        // AssemblyRef 之外再逐个 TypeRef 看解析作用域：报出来的是"哪个类型挂在谁身上"，比只报程序集名好定位。
        foreach (var handle in md.TypeReferences)
        {
            var typeRef = md.GetTypeReference(handle);
            string? owner = ScopeAssemblyName(md, typeRef.ResolutionScope);
            if (owner == null || IsAllowed(owner)) continue;

            string ns = md.GetString(typeRef.Namespace);
            string name = md.GetString(typeRef.Name);
            violations.Add($"{owner}::{ns}{(ns.Length > 0 ? "." : "")}{name}");
        }

        Assert.True(violations.Count == 0,
            $"BalanceTweak.dll 里存在对可选 Mod 的静态类型引用，未安装该 Mod 的玩家会整个程序集加载失败：\n  "
            + string.Join("\n  ", violations.Distinct())
            + "\n改法：不用 typeof / is / 泛型参数 / 字段类型，改按字符串全名反射"
            + "（DataUtility.IsTypeOrSubclassOfName / FindMember / GetByName / OptionalType）。"
            + "\n若确实新增了合法的框架依赖，请加进本测试的 AllowedExact / AllowedPrefix。");
    }

    /// <summary>顺着嵌套类型的解析链往上找，返回它最终归属的程序集名。</summary>
    private static string? ScopeAssemblyName(MetadataReader md, EntityHandle scope)
    {
        // 嵌套类型（如 Dictionary<K,V>.Enumerator）的 ResolutionScope 是外层 TypeRef，需要往下追。
        for (int guard = 0; guard < 32; guard++)
        {
            switch (scope.Kind)
            {
                case HandleKind.AssemblyReference:
                    return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
                case HandleKind.TypeReference:
                    scope = md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
                    continue;
                case HandleKind.ModuleReference:
                    return "<module>";
                default:
                    return null;   // ModuleDefinition 等：本程序集内部，不可能是外部依赖
            }
        }
        return null;
    }
}
