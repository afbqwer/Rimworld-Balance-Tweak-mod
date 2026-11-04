using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static BalanceTweak.StatColumnConfig;
using static BalanceTweak.TweakData;


namespace BalanceTweak;

public partial class BalanceTweakSettings
{
    public static class TweakUtility
    {
        /// <summary>修改一整列</summary>
        /// <remarks>
        /// <b>先算后写，不能边算边写。</b>镜像列（武器行的伤害/穿透，值实际存在关联弹丸上）在写入时会立刻
        /// 刷新所有共用该弹丸的行；边算边写会让排在后面的行拿"已被刷新的值"再算一遍 ——
        /// 共用一颗弹丸的两把枪（原版 Bullet_Shotgun 那样）就会把 +1 叠加成 +2，而只有一把枪的弹丸只 +1。
        /// 先把新值全部算完（此时读到的都是同一份基准）再逐行落盘，各行结果才一致。
        /// </remarks>
        public static void ApplyBatchOperation()
        {
            var config = selectedCol;
            if (config == null) return;
            showChanged = false;

            var pending = new List<(TweakData data, object value)>();
            foreach (var data in filteredData)
            {
                var org = config.GetRawValue(data);
                //Log.Message($"[BalanceTweak]org{org}");
                if (org == null && !showAbstract) continue;
                if (org is FloatRange fr)
                {
                    float newMin = ApplyBatchToValue(fr.min);
                    float newMax = ApplyBatchToValue(fr.max);
                    pending.Add((data, new FloatRange(newMin, newMax)));
                }
                else if (org is IntRange ir)
                {
                    int newMin = (int)ApplyBatchToValue(ir.min);
                    int newMax = (int)ApplyBatchToValue(ir.max);
                    pending.Add((data, new IntRange(newMin, newMax)));
                }
                else
                {
                    float? rawNum = config.GetNumericValue(data);
                    //Log.Message($"[BalanceTweak]rawNum{rawNum}");
                    if (!rawNum.HasValue) { continue; }
                    if (config.getAbstract != null && rawNum == -1f) { continue; }
                    float newVal = ApplyBatchToValue(rawNum.Value);
                    if (org is int)
                    {
                        pending.Add((data, (int)newVal));
                    }
                    else // float
                    {
                        pending.Add((data, newVal));
                    }
                }
            }

            foreach (var (data, value) in pending) config.ApplyValue(data, value);
        }


        /// <summary>重置当前可见数据</summary>
        public static void ApplyCurReset()
        {
            List<TweakData> d = new();
            foreach (var data in filteredData)
            {
                if (!data.tweaked)
                {
                    continue;
                }
                d.Add(data);
            }
            foreach (var item in d)
            {
                RemoveData(item);
            }
        }


        /// <summary>完全重置</summary>
        public static void ApplyFullReset()
        {
            tweakDatas.Clear();
            foreach ((var _, var dict) in cachedData)
            {
                dict.Clear();
            }
            foreach ((var key, var data) in TweakDatabase.AllOriginals)
            {
                var clone = data.Clone();
                clone.ApplyAuthoritative();
                cachedData[key.settingType].SetOrAdd(key, clone);
            }
            filterDirty = true;
        }

        /// <summary>
        /// 首次建表还没建完的行（null = 从未开始或已完成）。
        /// 分行不一次性建，是<see cref="AdvanceInit"/> 的全部目的。
        /// </summary>
        private static List<KeyValuePair<TweakID, TweakData>>? initQueue;
        private static int initCursor;
        private static double initElapsedMs;
        private static int initFrames;

        /// <summary>
        /// 每帧最多克隆多少行。按 L1′ 之后约 4µs/行算，2000 行 ≈ 8ms ——
        /// 卡在半帧之内，进度条才画得动。
        /// </summary>
        private const int RowsPerInitFrame = 2000;

        // === 当前页签的派生值推导（第二段分帧） ===
        // 和建行缓存分开：那段是"一次性的 43422 行克隆"，这段是"每个页签一次的全量 EnsureDisplayValues"。
        private static SettingType deriveType;
        private static List<TweakData>? deriveQueue;
        private static int deriveCursor;
        private static bool deriveDone;
        private static bool deriveDirty = true;
        private static double deriveElapsedMs;
        private static int deriveFrames;

        /// <summary>每帧留给推导的时间预算（毫秒）。单帧总开销要压在 8ms 以内。</summary>
        private const double DeriveBudgetMs = 6.0;

        /// <summary>当前正在看的页签。派生值推导只关心这一页，别的页签切过去时自然会重推。</summary>
        public static SettingType CurrentType => curType;

        /// <summary>当前页签的派生值作废：下次筛选前重推一遍全量派生值。</summary>
        public static void MarkDerivedDirty() => deriveDirty = true;

        /// <summary>首次建表或当前页签推导还没做完。</summary>
        public static bool InitInProgress => !cached || !deriveDone;

        /// <summary>准备进度 0~1。克隆占九成权重（它才是大头），推导占一成。</summary>
        public static float InitProgress
        {
            get
            {
                if (!cached)
                {
                    if (initQueue == null || initQueue.Count == 0) return 0f;
                    return 0.9f * initCursor / initQueue.Count;
                }
                if (!deriveDone && deriveQueue != null && deriveQueue.Count > 0)
                    return 0.9f + 0.1f * deriveCursor / deriveQueue.Count;
                return 1f;
            }
        }

        public static void InitAndUpdateData()
        {
            if (!cached) AdvanceInit();
            // 没建完就先别筛：此时 cachedData 只有一部分，筛出来的列表会缺行。
            if (!cached) return;

            AdvanceDerive();
            if (!deriveDone) return;

            if (filterDirty)
            {
                long stampFilter = PerfProbe.Stamp();
                UpdateFilters();
                SyncVisibleColumns();
                filterDirty = false;
                PerfProbe.Report($"筛选/排序（{filteredData.Count} 行 / {allColumnSource.Count} 列）", stampFilter);
            }
        }

        /// <summary>
        /// 把<b>当前页签</b>所有行的派生值算完，每帧限时 6ms。
        /// <para>
        /// 为什么要全算而不是按需：按需的方案在"按派生列排序 / 搜索"时会把整页行一次性拉起来 ——
        /// 那是一次不可控的同步停顿，而且正好落在用户点列头那一刻。全算之后排序只是比较已有数字。
        /// </para>
        /// <para>
        /// 每帧预算按<b>时间</b>而不是行数：不同页签的单行成本差两个数量级
        /// （兵种战力命中缓存约 3µs，武器 DPS 要跑 GenDps）。固定行数要么卡帧，要么浪费。
        /// </para>
        /// </summary>
        private static void AdvanceDerive()
        {
            if (deriveDirty || deriveType != curType)
            {
                deriveDirty = false;
                deriveType = curType;
                deriveDone = false;
                deriveQueue = null;
                deriveCursor = 0;
                deriveElapsedMs = 0;
                deriveFrames = 0;
            }

            if (deriveDone) return;

            if (deriveQueue == null)
            {
                var rows = cachedData[curType];
                deriveQueue = new List<TweakData>(rows.Count);
                foreach (var row in rows.Values) deriveQueue.Add(row);

                // 列定义（反射 + 每条列若干次 Translate）是每类型一次的固定开销，约 20~30ms。
                // 它反正要在本次打开期间付，挪到进度条阶段比拼在"表格首次显示前"更不突兀。
                if (deriveQueue.Count > 0) _ = deriveQueue[0].AllColumn;
            }

            long stamp = PerfProbe.Stamp();
            while (deriveCursor < deriveQueue.Count && PerfProbe.ElapsedMs(stamp) < DeriveBudgetMs)
            {
                var row = deriveQueue[deriveCursor++];
                // 展示属性（标题 / 搜索串 / 分类 / 图标 / 图标色）一并预热。
                // 它们同样是懒加载的，但滚动路径上没有分帧预算：先滚进视野的行如果当场解析，
                // 卡顿就落在滚动上。这里算完，绘制与筛选就只剩字段读。
                row.WarmDisplayProps();
                row.EnsureDisplayValues();
            }

            deriveElapsedMs += PerfProbe.ElapsedMs(stamp);
            deriveFrames++;

            if (deriveCursor < deriveQueue.Count) return;

            int total = deriveQueue.Count;
            deriveDone = true;
            deriveQueue = null;

            // 只在真的跨帧或耗时可观时打一条，方便确认"排序不再触发全量计算"。
            if ((deriveFrames > 1 || deriveElapsedMs >= 8.0) && TweakLog.Verbose)
                TweakLog.Message($"[BalanceTweak][perf] 页签派生值推导完成：{curType} {total} 行 / {deriveFrames} 帧，累计 {deriveElapsedMs:F0}ms");
        }

        /// <summary>
        /// 把首次建表推进一帧的份额。
        /// <para>
        /// 43422 行一次克隆要 400ms，直接卡在打开窗口那一帧上。分成每帧 2000 行之后，
        /// 总量不变，但每帧只占约 8ms，中间还能画进度条 —— 用户看到的是"在准备"而不是"死了"。
        /// 小数据包（&lt; 2000 行）仍然一帧建完，不会看到进度条闪过。
        /// </para>
        /// </summary>
        private static void AdvanceInit()
        {
            long stamp = PerfProbe.Stamp();

            if (initQueue == null)
            {
                initCursor = 0;
                initElapsedMs = 0;
                initFrames = 0;
                initQueue = new List<KeyValuePair<TweakID, TweakData>>(TweakDatabase.AllOriginals.Count);
                foreach (var pair in TweakDatabase.AllOriginals)
                    initQueue.Add(pair);
            }

            int end = Math.Min(initCursor + RowsPerInitFrame, initQueue.Count);
            for (; initCursor < end; initCursor++)
            {
                var pair = initQueue[initCursor];
                var live = tweakDatas!.TryGetValue(pair.Key);
                // 存档里改过的行直接复用那个实例；没改过的克隆一份，避免编辑时写脏原始快照。
                cachedData[pair.Key.settingType].SetOrAdd(pair.Key, live ?? pair.Value.Clone());
            }

            initElapsedMs += PerfProbe.ElapsedMs(stamp);
            initFrames++;

            if (initCursor < initQueue.Count) return;

            int total = initQueue.Count;
            initQueue = null;
            cached = true;
            filterDirty = true;

            TweakLog.Message($"[BalanceTweak][perf] 首次构建行缓存完成：{total} 行 / {initFrames} 帧，累计 {initElapsedMs:F0}ms");
        }

        /// <summary>从数据中移除</summary>
        public static void RemoveData(TweakData data)
        {
            TweakData? originalData = TweakDatabase.GetOriginal(data.id) ?? data;
            if (originalData != null)
            {
                var newdata = originalData.Clone();
                cachedData[data.id.settingType].SetOrAdd(data.id, newdata);
                tweakDatas.Remove(newdata.id);
                filteredData.Replace(data, newdata);
                // 整行重置：克隆体是"原始快照"，必须整体写回 —— stat 列表由 forceResetStats 覆盖，
                // 非 stat 字段靠 ApplyAuthoritative 的 forceWriteAll 才不会被三态判据跳过（见 TweakData.forceWriteAll）。
                newdata.ApplyAuthoritative();
                // 必须在 Def 已被还原之后再算参考值：否则会从"被用户改过的 Def"读取，
                // 让本局内重置后的参考列显示偏差（重启后重新初始化就会自愈）。
                newdata.EnsureDisplayValues();
                editingControlName = null;
            }
            else
            {
                Log.Error("无法重置数据！");
            }
        }
        /// <summary>重置单个数据项的指定列</summary>
        public static void ResetSingleItem(TweakData data, StatColumnConfig config)
        {
            if (!data.tweaked)
            {
                return;
            }
            TweakData? originalData = TweakDatabase.GetOriginal(data.id) ?? data;
            if (originalData != null)
            {
                var originalValue = config.GetRawValue(originalData);
                config.ResetTo(data, originalValue);
                if (data.EqualsData(originalData))
                {
                    RemoveData(data);
                }
            }
            else
            {
                Log.Error($"[BalanceTweak]找不到原始数据：{data}");
            }
        }
        /// <summary>重置一整列</summary>
        public static void ResetSelectedColumn()
        {
            if (selectedCol == null) return;

            var config = selectedCol;
            if (config == null) return;
            // Display 列是只读派生值，没有"重置"语义；且其原值可能延迟计算/为 null，
            // 不能拿它去覆盖运行数据的展示字段。
            if (config.columnDataType != ColumnDataType.Field) return;
            var l = new List<TweakData>();
            foreach (var data in filteredData)
            {
                if (!data.tweaked)
                {
                    continue;
                }
                TweakData? originalData = TweakDatabase.GetOriginal(data.id) ?? data;
                if (originalData != null)
                {
                    var originalValue = config.GetRawValue(originalData);
                    config.ResetTo(data, originalValue);
                    if (data.EqualsData(originalData))
                    {
                        l.Add(data);
                    }
                }
                else
                {
                    Log.Error($"[BalanceTweak]找不到原始数据：{data}");
                }
            }
            foreach (var item in l)
            {
                RemoveData(item);
            }
        }

        /// <summary>筛选并排序</summary>
        public static void UpdateFilters()
        {
            // 研究解锁索引有两条失效路径，这里是"兜底"的那条：
            // ① 正常编辑/重置走 ApplyValue / ApplyClear / ApplyAuthoritative，它们按 def 前后槽位的并集
            //    精确清缓存并丢索引（见 ResearchUnlocks.Capture/Commit），一格改动只碰 1~2 个项目；
            // ② 这里在每次重建列表时再丢一次，覆盖"绕过上面三条路径"的写入（第三方 mod 运行时改前置、
            //    跨存档残留等）。代价是一次全库正向扫描（约 1ms），重建本身是懒的：
            //    当前页签没有研究行就不会触发。
            ResearchUnlocks.Invalidate();

            filteredData.Clear();
            allColumnSource.Clear();
            editingControlName = null;
            availableModPacks.Clear();
            availableModPacks.Add("All");

            // 列过滤：本页签当前生效的条件。绘制层的橙色底只认 filteredColumnIds，
            // 每格一次字典查询换成一次集合命中 —— 数据区是行/列双虚拟化的，这里省下的是每帧几百次。
            var activeColumnFilters = new List<ColumnFilter>();
            filteredColumnIds.Clear();
            foreach (var (id, filter) in CurrentColumnFilters)
            {
                if (!filter.IsActive) continue;
                activeColumnFilters.Add(filter);
                filteredColumnIds.Add(id);
            }

            var datas = cachedData[curType];
            if (datas == null)
            {
                filterModPack = "All";
                selectedCol = null;
                sortCol = null;
            }
            else
            {
                var tempPacks = new HashSet<string>();
                TweakData? firstData = null;
                var addedPinnedIds = new HashSet<TweakID>();

                // 单次遍历：收集符合筛选条件的数据、modPack 名称、固定项标记
                foreach (var data in datas.Values)
                {
                    if (data.propType != filterPropType) continue;
                    // 这里**不能**调 EnsureDisplayValues：
                    // 这是全表遍历（几千行），而绘制是行 / 列双虚拟化的，真正要看的一屏不过几十行。
                    // 一旦在这里补算，"按需推导"就名存实亡 —— 打开页签的瞬间整表算完，
                    // 卡顿正好落在玩家最敏感的"刚点开"那一刻。
                    // 现在的约定：值由 GetString / GetNumericValue / GetRawValue 的调用方按需触发
                    // （见 StatColumnConfig.EnsureForDisplay），排序与搜索也走同一条路。
                    firstData ??= data;
                    if (!string.IsNullOrEmpty(data.modPackName))
                        tempPacks.Add(data.modPackName);

                    if (filterModPack != "All" && data.modPackName != filterModPack) continue;
                    // 列过滤：多条条件取"与"。放在搜索之前 —— 数字比较比字符串匹配便宜，
                    // 先挡掉行能让后面的搜索少跑一次（QuickSearchFilter 的缓存按字符串命中，省下的正是它的 key 计算）。
                    if (!MatchesColumnFilters(data, activeColumnFilters)) continue;
                    // 搜索
                    if (!searchFilter.Matches(data.searchString))
                    {
                        if (sortCol == null) continue;
                        var s = sortCol.GetString(data);
                        if (s.NullOrEmpty() || !searchFilter.Matches(s))
                            continue;
                    }
                    filteredData.Add(data);
                    
                    if (pinnedIds.Contains(data.id))
                        addedPinnedIds.Add(data.id);
                }

                // 构建可用 ModPack 列表
                availableModPacks.AddRange(tempPacks.OrderBy(s => s));
                // 调整 filterModPack：若当前选中项不在可用列表中，则回退到 "All"
                if (filterModPack != "All" && !availableModPacks.Contains(filterModPack))
                    filterModPack = "All";

                // 从首个数据项设置列源
                if (firstData != null)
                {
                    filterStr = firstData.TypeStrings;
                    // 列定义是静态惰性构建的：首次打开某个页签要做一遍反射 + 每条列 4 次 Translate。
                    long stampCols = PerfProbe.Stamp();
                    var columnsOfFirst = firstData.AllColumn;
                    PerfProbe.Report($"首次构建列定义（{columnsOfFirst.Count} 列）", stampCols);
                    foreach (var config in columnsOfFirst)
                    {
                        if (config.isAvailableDelegate == null || config.isAvailableDelegate(firstData))
                            allColumnSource.Add(config);
                    }
                    if (selectedCol != null && !allColumnSource.Contains(selectedCol))
                        selectedCol = null;
                    if (sortCol != null && !allColumnSource.Contains(sortCol))
                        sortCol = null;
                }

                // 确保固定项始终在列表中（不受筛选条件影响）
                if (addedPinnedIds.Count < pinnedIds.Count)
                {
                    foreach (var pinnedId in pinnedIds)
                    {
                        if (addedPinnedIds.Contains(pinnedId)) continue;
                        if (datas.TryGetValue(pinnedId, out var pinnedData) && pinnedData.propType == filterPropType)
                            filteredData.Add(pinnedData);
                    }
                }

                // 进行排序
                if (sortCol != null && curSortMode != SortModeType.NoSort)
                {
                    bool asc = curSortMode == SortModeType.Ascending;
                    filteredData.Sort((a, b) =>
                    {
                        int cmp = sortCol.SortFieldType switch
                        {
                            FieldType.String or FieldType.TweakID => string.Compare(
                                sortCol.GetString(a) ?? "", sortCol.GetString(b) ?? ""),
                            FieldType.List => ((sortCol.GetRawValue(a) as System.Collections.IList)?.Count ?? 0)
                                .CompareTo((sortCol.GetRawValue(b) as System.Collections.IList)?.Count ?? 0),
                            FieldType.Def => string.Compare(
                                (sortCol.GetRawValue(a) as Def)?.defName ?? "",
                                (sortCol.GetRawValue(b) as Def)?.defName ?? ""),
                            FieldType.ThingFilter => ((sortCol.GetRawValue(a) as ThingFilter)?.AllowedDefCount ?? 0)
                                .CompareTo((sortCol.GetRawValue(b) as ThingFilter)?.AllowedDefCount ?? 0),
                            _ => Comparer<float>.Default.Compare(
                                sortCol.GetNumericValue(a) ?? (asc ? float.MaxValue : float.MinValue),
                                sortCol.GetNumericValue(b) ?? (asc ? float.MaxValue : float.MinValue)),
                        };
                        return asc ? cmp : -cmp;
                    });
                }
                else if (sortCol == null)
                {
                    filteredData.SortBy(d => d.searchString);
                }

                // 固定项始终优先：单次遍历分离固定项与非固定项
                if (addedPinnedIds.Count > 0)
                {
                    var pinnedItems = new List<TweakData>(addedPinnedIds.Count);
                    var nonPinnedItems = new List<TweakData>(filteredData.Count - addedPinnedIds.Count);
                    foreach (var d in filteredData)
                    {
                        if (pinnedIds.Contains(d.id))
                            pinnedItems.Add(d);
                        else
                            nonPinnedItems.Add(d);
                    }
                    filteredData.Clear();
                    filteredData.AddRange(pinnedItems);
                    filteredData.AddRange(nonPinnedItems);
                }
            }

            // 设置垂直滚动位置到目标行
            if (jumpData != null)
            {
                int index = filteredData.IndexOf(jumpData);
                jumpData = null;
                if (index >= 0)
                    jumpdataScrollY = Mathf.Max(index * LineHeight, 0f);
            }
        }

        /// <summary>
        /// 一条行记录是否通过<b>全部</b>列过滤条件（取"与"）。
        /// 无生效条件时 <paramref name="filters"/> 为空表，这里直接返回 true，不额外判空。
        /// </summary>
        private static bool MatchesColumnFilters(TweakData data, List<ColumnFilter> filters)
        {
            foreach (var filter in filters)
            {
                if (!filter.Matches(data)) return false;
            }
            return true;
        }

        /// <summary>
        /// 列过滤条件变了：只需要重筛，不必重推派生值（<see cref="MarkDerivedDirty"/> 是另一件事）。
        /// 编辑窗口 / 右键菜单 / 取消过滤都走这里。
        /// </summary>
        public static void MarkFilterDirty() => filterDirty = true;

        /// <summary>跳转到数据</summary>
        public static void JumpToData(TweakData target)
        {
            if (target == null) return;

            // 每次跳转记一条历史：标题行的「后退」靠它还原离开时的筛选 / 搜索词 / 滚动位置
            PushNavigationHistory();

            // 设置筛选条件
            curType = target.id.settingType;
            // 列过滤会挡住目标行（按下标定位行号时 IndexOf 返回 -1，表现为"点了跳转没反应"），
            // 与清空搜索词同理一并撤掉；历史里已经存好，后退能原样恢复。
            CurrentColumnFilters.Clear();
            selectedCol = null;
            sortCol = null;
            var mp = string.IsNullOrEmpty(target.modPackName) ? "All" : target.modPackName;
            if (filterModPack != mp && filterModPack != "All") filterModPack = mp;
            filterPropType = target.propType;
            searchFilter.Text = "";
            curSortMode = SortModeType.NoSort;
            // 更新筛选列表
            filterDirty = true;
            jumpData = target;
            // 设置跳转高亮
            jumpHighlightData = target;
            jumpHighlightFrame = 90;
        }

        /// <summary>跳转到ID</summary>
        public static void JumpToData(TweakID? id)
        {
            if (id == null) return;
            var data = cachedData[id.Value.settingType][id.Value];
            JumpToData(data);
        }

        public static void SyncVisibleColumns()
        {
            editingControlName = null;
            editingBuffer = null;
            if (allColumnSource.Count == 0) return;
            visibleColumns.Clear();

            // 在同步时应用排序
            foreach (var col in allColumnSource.OrderBy(c => GetColumnSortOrder(c.id)))
            {
                if (!CurrentHiddenColumns.Contains(col.id))
                {
                    visibleColumns.Add(col);
                }
            }

            if (selectedCol != null && !visibleColumns.Contains(selectedCol))
            {
                selectedCol = null;
            }
            if (sortCol != null && !visibleColumns.Contains(sortCol))
            {
                sortCol = null;
            }
        }

        public static int GetColumnSortOrder(string columnId)
        {
            if (columnOrders.TryGetValue(curType, out var typeOrders) && typeOrders.TryGetValue(columnId, out int order))
            {
                return order;
            }
            return 1000 + allColumnSource.Count;
        }

        #region 数据清理方法

        /// <summary>
        /// 丢弃一条错误项，并把它在 Def 与 UI 上的残留一起收拾干净。
        /// </summary>
        /// <remarks>
        /// <b>不能只做 <c>tweakDatas.Remove</c> + 从 <c>cachedData</c> 删 id</b>（旧实现），两条路都会留下死角：
        /// <list type="number">
        /// <item>
        /// <c>cachedData</c> 是本会话唯一的行源（<see cref="UpdateFilters"/> 只遍历它），而它只在
        /// <see cref="InitAndUpdateData"/> 里建一次、<c>cached</c> 置 true 后**没有**任何重建入口。
        /// 所以删 id 等于这一行在本局内永久消失 —— 只有重启才能让它回来。
        /// 而 id 在 <see cref="TweakDatabase.AllOriginals"/> 里真实存在的情形并不罕见：
        /// 解析失败（<see cref="TweakDatabase.failedResolveItems"/>）的 Def 本身好好的；
        /// 延迟项里"父项在、但 label 对不上"（<c>HediffStageData.SetParentTweak</c> / <c>ToolData.SetParentTweak</c>
        /// 返回 false）的那一类，其 id 正是当前那一阶段/招式的 id。
        /// </item>
        /// <item>
        /// 解析失败的条目在 <c>TweakDatabase.Init</c> 的第二轮循环里**照样被 Apply 过**
        /// （<c>skipIds</c> 那段是注释掉的），Def 上已经有"能解析的字段已落盘"的半成品修改。
        /// 只删记录、不回滚，这份修改就失去了唯一能还原它的入口（Def 带着改动，表里却没有这一行），
        /// 直到重启重新读 XML 才回到原版。
        /// </item>
        /// </list>
        /// 因此按"Def 还在不在"分叉：有原始快照 ⇒ 用原始克隆替换该行（与整行重置同一条路径，
        /// 见 <see cref="RemoveData"/>），<paramref name="rollbackDef"/> 为 true 时再把 Def 一起写回原值；
        /// 找不到原始快照（Def 真的没了）⇒ 才连行一起删。
        /// </remarks>
        /// <param name="rollbackDef">
        /// 是否需要把 Def 回滚到原始快照。<b>只有 <see cref="TweakDatabase.failedResolveItems"/> 需要</b> ——
        /// 那类条目在 <c>TweakDatabase.Init</c> 的第二轮循环里被 Apply 过（<c>skipIds</c> 是注释掉的），
        /// Def 上已有半成品修改。直接项与延迟项都在 Apply 之前就 <c>continue</c> 了，Def 没被动过，
        /// 对它们写回原值不但多余，还可能盖掉同一 Def 上另一个并行数据实例（<c>Exclusive = false</c>）的改动。
        /// </param>
        private static void DropFailedItem(TweakData data, bool rollbackDef)
        {
            var id = data.id;

            // 同键已经换人了：用户在点「清理」之前重新编辑过这一行 —— 编辑会把行上的实例写回同一个键
            // （StatColumnConfig.SetRawValue 的 tweakDatas.SetOrAdd），清理列表里那份只是悬空引用。
            // 「父项结构已变」尤其容易撞上：它的 id 就是"当前那一阶段/招式"的 id，用户完全可能先编辑再清理。
            // 这时什么都不做：删键会把新编辑一起丢，回滚还会把新编辑在 Def 上的改动抹掉。
            if (tweakDatas.TryGetValue(id, out var current) && !ReferenceEquals(current, data))
                return;

            tweakDatas.Remove(id);

            if (TweakDatabase.GetOriginal(id) is not { } original)
            {
                // Def 已不存在：这一行本来就不在 cachedData 里（行源是 AllOriginals），删了也只是兜底。
                foreach (var dict in cachedData.Values)
                    dict.Remove(id);
                return;
            }

            var fresh = original.Clone();
            if (rollbackDef)
            {
                try
                {
                    // 与 RemoveData 一致：克隆体是"原始快照"，必须整体写回 —— 三态判据会让 Unset 字段一个都不写。
                    fresh.ApplyAuthoritative();
                }
                catch (Exception e)
                {
                    // 移除动作不该在这里中断：记录已删，Def 保持现状，至少不留半途状态。
                    Log.Error($"[BalanceTweak] 移除错误项时回滚 Def 失败，id={id}: {e}");
                }
                // 必须在 Def 已被还原之后再算参考值（同 RemoveData）。
                fresh.EnsureDisplayValues();
            }
            cachedData[id.settingType].SetOrAdd(id, fresh);
            filteredData.Replace(data, fresh);
            editingControlName = null;
        }

        /// <summary>移除解析失败的直接项（未找到对应 Def）</summary>
        public static void RemoveFailedDirectItems()
        {
            foreach (var data in TweakDatabase.failedDirectItems)
                DropFailedItem(data, rollbackDef: false);
            TweakDatabase.failedDirectItems.Clear();
            filterDirty = true;
        }

        /// <summary>移除解析失败的延迟项（父项不存在 / 父项结构已变）</summary>
        public static void RemoveFailedDelayedItems()
        {
            foreach (var (data, _) in TweakDatabase.failedDelayedItems)
                DropFailedItem(data, rollbackDef: false);
            TweakDatabase.failedDelayedItems.Clear();
            filterDirty = true;
        }

        /// <summary>移除内部解析失败的数据（ResolveDefs 失败）</summary>
        public static void RemoveFailedResolveItems()
        {
            foreach (var data in TweakDatabase.failedResolveItems)
                DropFailedItem(data, rollbackDef: true);
            TweakDatabase.failedResolveItems.Clear();
            filterDirty = true;
        }

        #endregion

        /// <summary>移除所有错误项（直接项+延迟项+解析失败）</summary>
        public static void RemoveAllFailedItems()
        {
            RemoveFailedDirectItems();
            RemoveFailedDelayedItems();
            RemoveFailedResolveItems();
        }
    }
}