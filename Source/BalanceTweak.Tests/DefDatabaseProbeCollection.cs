using Xunit;

namespace BalanceTweak.Tests;

/// <summary>
/// 会往 <see cref="Verse.DefDatabase{T}"/> 注册探针 StatDef 的测试所在的 xunit 集合。
///
/// <para>
/// <c>DefDatabase&lt;StatDef&gt;.Add</c> 是往静态字典里写，xunit 默认让不同测试类并行跑 ——
/// 多个类同时 <c>GetNamedSilentFail</c> + <c>Add</c> 会撞在同一个字典上（表现是偶发失败，
/// 单跑却全绿）。这些类共享同一个集合名即可被 xunit 串行化。
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public class DefDatabaseProbeCollection
{
    public const string Name = "DefDatabase 探针（串行）";
}
