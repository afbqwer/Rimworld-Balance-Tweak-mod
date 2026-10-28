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

        public static void InitAndUpdateData()
        {
            if (!cached)
            {

                Log.Message("开始读取TweakDatabase");
                foreach ((var key, var data) in TweakDatabase.AllOriginals)
                {
                    var tdata = tweakDatas!.TryGetValue(key);
                    if (tdata != null)
                    {
                        cachedData[key.settingType].SetOrAdd(key, tdata);
                    }
                    else
                    {
                        cachedData[key.settingType].SetOrAdd(key, data.Clone());
                    }
                }
                cached = true;
                filterDirty = true;
            }

            if (filterDirty)
            {
                UpdateFilters();
                SyncVisibleColumns();
                filterDirty = false;
            }
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
            filteredData.Clear();
            allColumnSource.Clear();
            editingControlName = null;
            availableModPacks.Clear();
            availableModPacks.Add("All");

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
                    data.EnsureDisplayValues();
                    firstData ??= data;
                    if (!string.IsNullOrEmpty(data.modPackName))
                        tempPacks.Add(data.modPackName);

                    if (filterModPack != "All" && data.modPackName != filterModPack) continue;
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
                    foreach (var config in firstData.AllColumn)
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
        /// <summary>跳转到数据</summary>
        public static void JumpToData(TweakData target)
        {
            if (target == null) return;

            // 每次跳转记一条历史：标题行的「后退」靠它还原离开时的筛选 / 搜索词 / 滚动位置
            PushNavigationHistory();

            // 设置筛选条件
            curType = target.id.settingType;
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

        /// <summary>移除解析失败的直接项（未找到对应 Def）</summary>
        public static void RemoveFailedDirectItems()
        {
            foreach (var data in TweakDatabase.failedDirectItems)
            {
                tweakDatas.Remove(data.id);
                foreach (var dict in cachedData.Values)
                {
                    dict.Remove(data.id);
                }
            }
            TweakDatabase.failedDirectItems.Clear();
            filterDirty = true;
        }

        /// <summary>移除解析失败的延迟项（未找到父项）</summary>
        public static void RemoveFailedDelayedItems()
        {
            foreach (var data in TweakDatabase.failedDelayedItems)
            {
                tweakDatas.Remove(data.id);
                foreach (var dict in cachedData.Values)
                {
                    dict.Remove(data.id);
                }
            }
            TweakDatabase.failedDelayedItems.Clear();
            filterDirty = true;
        }

        /// <summary>移除内部解析失败的数据（ResolveDefs 失败）</summary>
        public static void RemoveFailedResolveItems()
        {
            foreach (var data in TweakDatabase.failedResolveItems)
            {
                tweakDatas.Remove(data.id);
                foreach (var dict in cachedData.Values)
                {
                    dict.Remove(data.id);
                }
            }
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