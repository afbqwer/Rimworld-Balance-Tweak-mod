using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.TweakData;
namespace BalanceTweak;

/// <summary>
/// 辅助类，隔离所有 PipeSystem.ProcessDef 相关逻辑。
///
/// PipeSystem 是**可选 Mod**：这里一律按全名反射访问其类型与成员，绝不静态引用
/// （包括 <c>typeof</c> / 泛型参数 / 把该类型的变量捕获进 lambda 闭包 —— 后者会把类型
/// 编成闭包类的公共字段，未加载时启动期 <c>GetFields()</c> 直接 TypeLoadException，
/// 见 <see cref="DataUtility"/> 顶部注释）。
/// 闭包里只允许出现 <c>object</c> / <c>MemberInfo</c> 这类 BCL 类型。
/// </summary>
public class RecipeProcessHelper
{
    private const string ProcessDefType = "PipeSystem.ProcessDef";
    private const string AdvancedProcessorCompType = "PipeSystem.CompProperties_AdvancedResourceProcessor";

    private static bool IsProcessDef(Def? def) => def != null && DataUtility.IsTypeOrSubclassOfName(def.GetType(), ProcessDefType);

    private static object? G(object? o, string name) => DataUtility.GetByName(o, name);
    private static void S(object? o, string name, object? v) => DataUtility.SetByName(o, name, v);

    private static object? FirstItem(object? collection)
    {
        if (collection is IEnumerable e)
            foreach (var o in e) return o;
        return null;
    }

    private static int CountOf(object? collection)
        => collection is ICollection c ? c.Count : collection is IEnumerable e ? e.Cast<object>().Count() : 0;

    /// <summary>走 ApplyV 门控 + 反射写入；闭包只捕获 object 与 MemberInfo，不引入 PipeSystem 类型。</summary>
    private static void ApplyMember<TV>(RecipeData data, object target, string memberName, string tweakField) where TV : struct
    {
        var member = DataUtility.FindMember(target.GetType(), memberName);
        if (member == null) return;
        data.ApplyV<TV>(tweakField, v => DataUtility.SetMember(target, member, v));
    }

    public static bool SetProcessDef(Def def, RecipeData data)
    {
        if (!IsProcessDef(def)) return false;
        object pd = def!;

        data.generated = DataUtility.AsBool(G(pd, "generated"));
        data.isProcess = true;
        data.uiIcon = G(pd, "uiIcon") as Texture2D;
        if (data.uiIcon == null)
        {
            var t = G(FirstItem(G(pd, "results")), "thing") as ThingDef;
            data.uiIcon = t?.uiIcon;
        }
        data.label = def.LabelCap;
        data.desc = $"{def.defName}\n{def.description}";
        if (data.desc.NullOrEmpty()) { data.desc = def.defName; }
        data.searchString = data.label + def.defName;
        data.ticks ??= DataUtility.AsInt(G(pd, "ticks"));
        data.ticksQuality ??= DataUtility.AsIntList(G(pd, "ticksQuality"));
        data.isLightDependingProcess ??= DataUtility.AsBool(G(pd, "isLightDependingProcess"));
        data.minLight ??= DataUtility.AsFloat(G(pd, "minLight"));
        data.maxLight ??= DataUtility.AsFloat(G(pd, "maxLight"));
        data.temperatureRuinable ??= DataUtility.AsBool(G(pd, "temperatureRuinable"));
        data.minSafeTemperature ??= DataUtility.AsFloat(G(pd, "minSafeTemperature"));
        data.maxSafeTemperature ??= DataUtility.AsFloat(G(pd, "maxSafeTemperature"));
        data.maxOutputCount ??= DataUtility.AsFloat(G(pd, "maxOutputCount"));

        // 验证：如果原数据变更导致 ingredient/result 项数不匹配，清除所有修改，重新从 ProcessDef 加载
        bool ingredientMismatch = data.processIngredients != null && CountOf(G(pd, "ingredients")) != data.processIngredients.Count;
        bool resultMismatch = data.processResults != null && CountOf(G(pd, "results")) != data.processResults.Count;
        if (ingredientMismatch || resultMismatch)
        {
            data.processIngredients = null;
            data.processResults = null;
        }

        if (data.processIngredients == null && G(pd, "ingredients") is IEnumerable ingredients)
        {
            data.processIngredients = new List<ProcessIngredientItem>();
            foreach (var i in ingredients.Cast<object>())
            {
                data.processIngredients.Add(new ProcessIngredientItem
                {
                    thing = G(i, "thing") as ThingDef,
                    thingCategory = G(i, "thingCategory") as ThingCategoryDef,
                    disallowedThingDefs = (G(i, "disallowedThingDefs") as IEnumerable)?.Cast<ThingDef>().ToList(),
                    countNeeded = DataUtility.AsFloat(G(i, "countNeeded")) ?? 0f,
                });
            }
        }
        if (data.processResults == null && G(pd, "results") is IEnumerable results)
        {
            data.processResults = new List<ProcessResultItem>();
            foreach (var r in results.Cast<object>())
            {
                data.processResults.Add(new ProcessResultItem
                {
                    thing = G(r, "thing") as ThingDef,
                    count = DataUtility.AsInt(G(r, "count")) ?? 0,
                });
            }
        }
        return true;
    }

    public static bool ApplyProcessDef(Def def, RecipeData data)
    {
        if (!IsProcessDef(def)) return false;
        object pd = def!;

        if (data.processIngredients != null && G(pd, "ingredients") is IEnumerable pdIngredients)
        {
            var list = pdIngredients.Cast<object>().ToList();
            int count = Math.Min(data.processIngredients.Count, list.Count);
            for (int i = 0; i < count; i++)
            {
                var src = data.processIngredients[i];
                var dst = list[i];
                S(dst, "thing", src.thing);
                S(dst, "thingCategory", src.thingCategory);
                S(dst, "disallowedThingDefs", src.disallowedThingDefs?.ToList());
                S(dst, "countNeeded", src.countNeeded);
            }
        }
        ApplyMember<int>(data, pd, "ticks", nameof(data.ticks));
        data.ApplyL<int>(nameof(data.ticksQuality), v => S(pd, "ticksQuality", v));
        if (data.processResults != null && G(pd, "results") is IEnumerable pdResults)
        {
            var list = pdResults.Cast<object>().ToList();
            int count = Math.Min(data.processResults.Count, list.Count);
            for (int i = 0; i < count; i++)
            {
                var src = data.processResults[i];
                var dst = list[i];
                S(dst, "thing", src.thing);
                S(dst, "count", src.count);
            }
        }
        ApplyMember<bool>(data, pd, "isLightDependingProcess", nameof(data.isLightDependingProcess));
        ApplyMember<float>(data, pd, "minLight", nameof(data.minLight));
        ApplyMember<float>(data, pd, "maxLight", nameof(data.maxLight));
        ApplyMember<bool>(data, pd, "temperatureRuinable", nameof(data.temperatureRuinable));
        ApplyMember<float>(data, pd, "minSafeTemperature", nameof(data.minSafeTemperature));
        ApplyMember<float>(data, pd, "maxSafeTemperature", nameof(data.maxSafeTemperature));
        ApplyMember<float>(data, pd, "maxOutputCount", nameof(data.maxOutputCount));
        return true;
    }

    public static int GetProcessDefPropType(Def def)
    {
        if (!IsProcessDef(def)) return -1;
        return DataUtility.AsBool(G(def, "isFactoryProcess"))
            ? (int)RecipeData.RecipeCategory.Factory
            : (int)RecipeData.RecipeCategory.Process;
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

            var comp = FindAdvancedProcessorComp(thingDef);
            if (comp == null) continue;
            if (G(comp, "processes") is not IEnumerable processes) continue;

            foreach (var processDef in processes.Cast<object>())
            {
                string defName = processDef is Def d ? d.defName : DataUtility.GetByName(processDef, "defName") as string ?? "";
                if (defName.Length == 0) continue;

                // 填充缓存
                if (!_processOriginalCache.TryGetValue(defName, out var origList))
                {
                    origList = new List<ThingDef>();
                    _processOriginalCache[defName] = origList;
                }
                origList.Add(thingDef);

                // 填充 RecipeData 的 display 字段
                var id = new TweakID(defName, SettingType.Recipe);
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
        if (!IsProcessDef(def) || _processOriginalCache == null) return;
        object pd = def!;
        if (!_processOriginalCache.TryGetValue(def.defName, out var originalBuildings)) return;

        foreach (var thingDef in originalBuildings)
        {
            var comp = FindAdvancedProcessorComp(thingDef);
            if (G(comp, "processes") is not IList processes) continue;

            if (hidden)
            {
                if (processes.Contains(pd)) processes.Remove(pd);
            }
            else
            {
                if (!processes.Contains(pd)) processes.Add(pd);
            }
        }
    }

    private static object? FindAdvancedProcessorComp(ThingDef thingDef)
        => thingDef.comps?.FirstOrDefault(c => c != null && DataUtility.IsTypeOrSubclassOfName(c.GetType(), AdvancedProcessorCompType));
}
