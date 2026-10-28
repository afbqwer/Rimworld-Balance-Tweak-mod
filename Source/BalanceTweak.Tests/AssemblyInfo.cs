using Xunit;

// 这些测试直接改**全局状态**：BalanceTweakSettings.tweakDatas / cachedData / DefDatabase 全是静态字典。
// 两个测试类并行跑就会撞在同一个字典上 —— 实测报
//   "Operations that change non-concurrent collections must have exclusive access"
// （StatColumnConfig.SetRawValue → tweakDatas.SetOrAdd）。
// 全套不过 250ms，串行没有代价，比按类维护 [Collection] 可靠得多。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
