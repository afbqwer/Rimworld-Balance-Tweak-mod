// 程序集级特性。
//
// 为什么手写而不是用 csproj 的 <InternalsVisibleTo> item：
// BalanceTweak.csproj 里设了 GenerateAssemblyInfo=false（避免自动生成一堆元数据），
// 而那个 item 正是靠自动生成的 AssemblyInfo 落地的 —— 两者一起用会静默失效。
//
// 仅 Debug 授权：发布产物不给测试工程开 internal 访问。

#if DEBUG
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BalanceTweak.Tests")]
#endif
