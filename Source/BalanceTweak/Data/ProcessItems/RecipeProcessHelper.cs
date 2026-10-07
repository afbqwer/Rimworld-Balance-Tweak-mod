using System;
using System.Collections.Generic;
using System.Linq;
using PipeSystem;
using Verse;
using UnityEngine;
using HarmonyLib;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;
namespace BalanceTweak;

/// <summary>
/// 辅助类，隔离所有 PipeSystem.ProcessDef 相关逻辑。
/// 仅在此类中引用 PipeSystem 类型，避免 RecipeData 在 PipeSystem 未加载时因类型解析失败而崩溃。
/// </summary>
public class RecipeProcessHelper
{
    public static bool SetProcessDef(Def def, RecipeData data)
    {
        if (def is not ProcessDef pd) return false;
        data.generated = pd.generated;
        data.isProcess = true;
        data.uiIcon = AccessTools.Field(typeof(ProcessDef), "uiIcon")?.GetValue(pd) as Texture2D;
        if(data.uiIcon == null){
            var t = pd.results.FirstOrDefault()?.thing;
            data.uiIcon = t?.uiIcon;
        }
        data.label = pd.LabelCap;
        data.desc = $"{pd.defName}\n{pd.description}";
        if (data.desc.NullOrEmpty()) { data.desc = pd.defName; }
        data.searchString = data.label + def.defName;
        data.ticks ??= pd.ticks;
        data.ticksQuality ??= pd.ticksQuality;
        data.isLightDependingProcess ??= pd.isLightDependingProcess;
        data.minLight ??= pd.minLight;
        data.maxLight ??= pd.maxLight;
        data.temperatureRuinable ??= pd.temperatureRuinable;
        data.minSafeTemperature ??= pd.minSafeTemperature;
        data.maxSafeTemperature ??= pd.maxSafeTemperature;
        data.maxOutputCount ??= pd.maxOutputCount;

        // 验证：如果原数据变更导致 ingredient/result 项数不匹配，清除所有修改，重新从 ProcessDef 加载
        bool ingredientMismatch = data.processIngredients != null
            && (pd.ingredients == null || data.processIngredients.Count != pd.ingredients.Count);
        bool resultMismatch = data.processResults != null
            && (pd.results == null || data.processResults.Count != pd.results.Count);
        if (ingredientMismatch || resultMismatch)
        {
            data.processIngredients = null;
            data.processResults = null;
        }

        if (pd.ingredients != null)
        {
            if (data.processIngredients == null)
            {
                data.processIngredients = new List<ProcessIngredientItem>(pd.ingredients.Count);
                foreach (var i in pd.ingredients)
                {
                    data.processIngredients.Add(new ProcessIngredientItem
                    {
                        thing = i.thing,
                        thingCategory = i.thingCategory,
                        disallowedThingDefs = i.disallowedThingDefs?.ToList(),
                        countNeeded = i.countNeeded,
                    });
                }
            }
        }
        if (pd.results != null)
        {
            if (data.processResults == null)
            {
                data.processResults = new List<ProcessResultItem>(pd.results.Count);
                foreach (var r in pd.results)
                {
                    data.processResults.Add(new ProcessResultItem
                    {
                        thing = r.thing,
                        count = r.count,
                    });
                }
            }
        }
        return true;
    }

    public static bool ApplyProcessDef(Def def, RecipeData data)
    {
        if (def is not ProcessDef pd) return false;
        if (data.processIngredients != null && pd.ingredients != null)
        {
            int count = Math.Min(data.processIngredients.Count, pd.ingredients.Count);
            for (int i = 0; i < count; i++)
            {
                var src = data.processIngredients[i];
                var dst = pd.ingredients[i];
                dst.thing = src.thing;
                dst.thingCategory = src.thingCategory;
                dst.disallowedThingDefs = src.disallowedThingDefs?.ToList();
                dst.countNeeded = src.countNeeded;
            }
        }
        if (data.ticks.HasValue) { pd.ticks = data.ticks.Value; }
        if (data.ticksQuality != null) { pd.ticksQuality = data.ticksQuality; }
        if (data.processResults != null && pd.results != null)
        {
            int count = Math.Min(data.processResults.Count, pd.results.Count);
            for (int i = 0; i < count; i++)
            {
                var src = data.processResults[i];
                var dst = pd.results[i];
                dst.thing = src.thing;
                dst.count = src.count;
            }
        }
        if (data.isLightDependingProcess.HasValue) { pd.isLightDependingProcess = data.isLightDependingProcess.Value; }
        if (data.minLight.HasValue) { pd.minLight = data.minLight.Value; }
        if (data.maxLight.HasValue) { pd.maxLight = data.maxLight.Value; }
        if (data.temperatureRuinable.HasValue) { pd.temperatureRuinable = data.temperatureRuinable.Value; }
        if (data.minSafeTemperature.HasValue) { pd.minSafeTemperature = data.minSafeTemperature.Value; }
        if (data.maxSafeTemperature.HasValue) { pd.maxSafeTemperature = data.maxSafeTemperature.Value; }
        if (data.maxOutputCount.HasValue) { pd.maxOutputCount = data.maxOutputCount.Value; }
        return true;
    }

    public static int GetProcessDefPropType(Def def)
    {
        if (def is ProcessDef pd)
        {
            return pd.isFactoryProcess
                ? (int)RecipeData.RecipeCategory.Factory
                : (int)RecipeData.RecipeCategory.Process;
        }
        return -1;
    }

    /// <summary>
    /// ProcessDef.defName → 初始扫描时拥有的建筑列表（不可变，用于还原）。
    /// </summary>
    private static Dictionary<string, List<ThingDef>>? _processOriginalCache;

    public static void PopulateProcessUsers()
    {
        _processOriginalCache = new Dictionary<string, List<ThingDef>>();

        foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
        {
            if (thingDef.category != ThingCategory.Building) continue;

            var comp = thingDef.GetCompProperties<CompProperties_AdvancedResourceProcessor>();
            if (comp == null) continue;

            foreach (var processDef in comp.processes)
            {
                // 填充缓存
                if (!_processOriginalCache.TryGetValue(processDef.defName, out var origList))
                {
                    origList = new List<ThingDef>();
                    _processOriginalCache[processDef.defName] = origList;
                }
                origList.Add(thingDef);

                // 填充 RecipeData 的 display 字段
                var id = new TweakID(processDef.defName, SettingType.Recipe);
                if (TweakDatabase.TryGetOriginal(id, out var data) && data is RecipeData rd)
                {
                    rd.processUser = origList.Count == 1
                        ? origList[0].LabelCap
                        : null; // 仅在首次遇到时设置，后续跳过
                }
            }
        }

        // 第二轮：为所有 Process 补上 processUser 显示值
        foreach (var kvp in _processOriginalCache)
        {
            var id = new TweakID(kvp.Key, SettingType.Recipe);
            if (TweakDatabase.TryGetOriginal(id, out var data) && data is RecipeData rd && rd.processUser == null)
            {
                rd.processUser = kvp.Value.Count > 0
                    ? kvp.Value[0].LabelCap
                    : "—";
            }
        }
    }

    /// <summary>
    /// 根据 hidden 开关移除/还原 ProcessDef 在所有建筑 comp 中的引用。
    /// hidden=true  → 从所有原建筑中移除该 process，更新当前缓存为空。
    /// hidden=false → 还原到所有原建筑中，更新当前缓存 = 原缓存。
    /// </summary>
    public static void ApplyProcessHidden(Def def, bool hidden)
    {
        if (def is not ProcessDef pd) return;
        if (_processOriginalCache == null) return;

        if (hidden)
        {
            // 移除：从所有原建筑中删除此 process
            if (_processOriginalCache.TryGetValue(pd.defName, out var originalBuildings))
            {
                foreach (var thingDef in originalBuildings)
                {
                    var comp = thingDef.GetCompProperties<CompProperties_AdvancedResourceProcessor>();
                    if (comp != null && comp.processes.Contains(pd))
                        comp.processes.Remove(pd);
                }
            }
        }
        else
        {
            // 还原：恢复到所有原建筑
            if (_processOriginalCache.TryGetValue(pd.defName, out var originalBuildings))
            {
                foreach (var thingDef in originalBuildings)
                {
                    var comp = thingDef.GetCompProperties<CompProperties_AdvancedResourceProcessor>();
                    if (comp != null && !comp.processes.Contains(pd))
                        comp.processes.Add(pd);
                }
            }
        }
    }
}