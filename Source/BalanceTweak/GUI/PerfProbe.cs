using System;
using System.Collections.Generic;
using System.Diagnostics;
using Verse;

namespace BalanceTweak
{
    /// <summary>
    /// 一次性的耗时探针：只在某一段超过阈值时打一条日志，每个标签每秒最多一条。
    /// <para>
    /// 用途是定位"打开窗口卡一下"究竟落在哪一段 —— 窗口打开时的一次性工作有好几处
    /// （克隆行缓存、建列定义、筛选排序、建装备池），没有数据就只能猜。
    /// </para>
    /// <para>
    /// 日常运行零输出、零分配：只有超过阈值的调用才会走到字符串拼接。
    /// </para>
    /// </summary>
    internal static class PerfProbe
    {
        /// <summary>低于这个耗时不值得记 —— 一帧 16ms，8ms 以下用户基本感觉不到。</summary>
        private const double ThresholdMs = 8.0;

        /// <summary>每 60 帧（约 1 秒）每标签最多一条，避免掉帧时把日志刷爆。</summary>
        private static readonly Dictionary<string, long> lastReportAt = [];

        internal static long Stamp() => Stopwatch.GetTimestamp();

        internal static double ElapsedMs(long stamp)
            => (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency;

        internal static void Report(string tag, long stamp)
        {
            double ms = ElapsedMs(stamp);
            if (ms < ThresholdMs) return;

            long now = Stopwatch.GetTimestamp();
            if (lastReportAt.TryGetValue(tag, out long last) && now - last < Stopwatch.Frequency) return;
            lastReportAt[tag] = now;

            TweakLog.Message($"[BalanceTweak][perf] {tag}: {ms:F1}ms");
        }

        /// <summary>测试用：清掉限流状态，让下一次调用一定打日志。</summary>
        internal static void Reset() => lastReportAt.Clear();
    }
}
