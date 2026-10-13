using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 手工反射的<b>一次性解析 + 显式报错</b>入口（见 RimWorld1.7模组迁移指南 §4.4.2）。
///
/// 跨版本时最危险的不是"编译不过"，而是<b>反射静默失配</b>：<c>AccessTools.Field</c> 拿不到就返回 null，
/// 调用点随后一句 <c>.GetValue()</c> 直接 NRE；或者更糟 —— 被某个 <c>if (x != null)</c> 吞掉，功能悄悄消失，
/// 日志里一个字都没有。这里把"有没有解析到"前移到<b>日志</b>：缺失就打一条可 grep 的 <c>Log.Error</c>，
/// 写明缺了哪个成员。
///
/// 语义与 <c>AccessTools.*</c> 完全一致（找不到仍返回 null，后续由调用点决定），
/// 所以这是<b>纯诊断增强、零行为变化</b>：唯一的区别是启动日志里多一条明确的错误，而不是等运行到某处才炸。
/// </summary>
internal static class Reflect
{
    /// <summary>解析私有/公有字段；找不到时记一条 <c>Log.Error</c> 并仍返回 null。</summary>
    public static FieldInfo Field(Type type, string name)
    {
        FieldInfo? field = AccessTools.Field(type, name);
        if (field == null) ReportMissing(type, name, "字段");
        return field!;
    }

    private static void ReportMissing(Type type, string name, string kind)
        => Log.Error($"[BalanceTweak] 反射{kind} {type.FullName}.{name} 未找到 —— 依赖它的功能将失效"
                   + "（游戏版本改名 / 成员形态变化？）");
}
