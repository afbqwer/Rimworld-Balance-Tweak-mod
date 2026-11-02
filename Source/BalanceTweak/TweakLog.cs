using Verse;

namespace BalanceTweak
{
    /// <summary>
    /// 诊断类日志的统一出口：只有「总体设置 → 冗杂日志」打开时才写。
    /// <para>
    /// 这类内容（Def 统计、初始化耗时、性能探针、非原版数据的处理说明）每次启动都会输出，
    /// 对普通玩家没有意义，出问题时反而会淹掉真正有用的行。
    /// </para>
    /// <para>
    /// <b>真正的错误与警告不走这里</b>：<c>Log.Error</c> / <c>Log.Warning</c> 永远输出，
    /// 否则关掉日志就等于关掉了报错。
    /// </para>
    /// </summary>
    internal static class TweakLog
    {
        internal static bool Verbose => BalanceTweakSettings.verboseLog;

        internal static void Message(string message)
        {
            if (BalanceTweakSettings.verboseLog) Log.Message(message);
        }
    }
}
