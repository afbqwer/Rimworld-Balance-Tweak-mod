using System.Collections;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BalanceTweak;

/// <summary>
/// Fortified 多炮塔的全名反射入口（<c>CompPropertiesMultipleTurretGun</c> /
/// <c>CompProperties_VehicleWeapon</c>）。
///
/// <para>
/// 槽位的可编辑字段通过 <see cref="TurretSlotRecord"/> 落盘，Apply 时按槽位 <c>ID</c>
/// 找回 Def 里的 <c>SubTurretProperties</c> 并<b>原地改</b>：渲染配置保持不动，已存在的
/// <c>SubTurret.turretProp</c> 引用也不变，所以标量改动立即生效，无需缓存刷新。
/// </para>
/// </summary>
internal static class FortifiedTurret
{
    internal const string MultipleTurretPropsType = "Fortified.CompPropertiesMultipleTurretGun";
    internal const string VehicleWeaponPropsType = "Fortified.CompProperties_VehicleWeapon";

    private const string SlotsField = "subTurrets";

    internal static object? FindComp(Def? def, string fullTypeName)
    {
        if (def is not ThingDef td || td.comps.NullOrEmpty()) return null;
        foreach (var props in td.comps)
        {
            if (props != null && DataUtility.IsTypeOrSubclassOfName(props.GetType(), fullTypeName))
                return props;
        }
        return null;
    }

    internal static object? FindMultipleTurret(Def? def) => FindComp(def, MultipleTurretPropsType);
    internal static object? FindVehicleWeapon(Def? def) => FindComp(def, VehicleWeaponPropsType);

    /// <summary>是否挂了副炮槽 Comp。每帧每格求值，只扫自己的 comps。</summary>
    internal static bool HasSlots(Def? def) => FindMultipleTurret(def) != null;

    /// <summary>副炮槽或主炮任一存在（种族页触发列用）。</summary>
    internal static bool HasAny(Def? def) => FindMultipleTurret(def) != null || FindVehicleWeapon(def) != null;

    /// <summary>把 Def 上的槽位读成可编辑快照。没有槽位时返回 null。</summary>
    internal static List<TurretSlotRecord>? ReadSlots(object? props)
    {
        var slots = GetSlots(props);
        if (slots == null || slots.Count == 0) return null;

        var result = new List<TurretSlotRecord>(slots.Count);
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            result.Add(new TurretSlotRecord
            {
                id = DataUtility.GetByName(slot, "ID") as string ?? "",
                turret = DataUtility.GetByName(slot, "turret") as ThingDef,
                supportedWeaponTag = DataUtility.GetByName(slot, "supportedWeaponTag") as string ?? "",
                generateWithWeapons = DataUtility.GetByName(slot, "generateWithWeapons") is List<string> list
                    ? new List<string>(list)
                    : new List<string>(),
                idleAngleOffset = DataUtility.AsFloat(DataUtility.GetByName(slot, "IdleAngleOffset")) ?? 0f,
                angleOffset = DataUtility.AsFloat(DataUtility.GetByName(slot, "angleOffset")) ?? 0f,
                autoAttack = DataUtility.GetByName(slot, "autoAttack") as bool? ?? true,
                warmingTime = DataUtility.AsFloat(DataUtility.GetByName(slot, "warmingTime")) ?? 1f,
            });
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// 把编辑后的快照写回 Def 上的对应槽位（按 <c>ID</c> 匹配，原地改）。
    /// 找不到的槽位只告警跳过 —— <b>绝不增删槽位</b>：新增槽需要渲染配置，删槽会让已保存的
    /// <c>SubTurret</c> 找不到 prop 而失效。
    /// </summary>
    internal static void WriteSlots(ThingDef def, object? props, List<TurretSlotRecord>? records)
    {
        if (records == null || records.Count == 0) return;
        var slots = GetSlots(props);
        if (slots == null || slots.Count == 0) return;

        int index = 0;
        foreach (var record in records)
        {
            if (record == null) continue;
            var slot = FindSlot(slots, record.id, index);
            index++;
            if (slot == null)
            {
                WarnMissingSlot(def, record.id);
                continue;
            }

            // 不做任何"替换整个列表 / 整个对象"的动作：保留 renderNodeProperty 与 SubTurret 的引用。
            DataUtility.SetByName(slot, "turret", record.turret);
            DataUtility.SetByName(slot, "supportedWeaponTag", record.supportedWeaponTag ?? "");
            DataUtility.SetByName(slot, "generateWithWeapons", record.generateWithWeapons ?? new List<string>());
            DataUtility.SetByName(slot, "IdleAngleOffset", record.idleAngleOffset);
            DataUtility.SetByName(slot, "angleOffset", record.angleOffset);
            DataUtility.SetByName(slot, "autoAttack", record.autoAttack);
            DataUtility.SetByName(slot, "warmingTime", record.warmingTime);
        }
    }

    private static IList? GetSlots(object? props)
        => props == null ? null : DataUtility.GetByName(props, SlotsField) as IList;

    private static object? FindSlot(IList slots, string? id, int index)
    {
        if (!id.NullOrEmpty())
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && DataUtility.GetByName(slots[i], "ID") as string == id)
                    return slots[i];
            }
            return null;
        }

        // 只有身份为空的记录才允许按顺序兜底（正常记录一定带 ID）。
        return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    private static readonly HashSet<string> warnedMissing = new();

    private static void WarnMissingSlot(ThingDef def, string? id)
    {
        string key = def.defName + "|" + id;
        if (!warnedMissing.Add(key)) return;
        Log.Warning($"[BalanceTweak] {def.defName} 找不到副炮槽 \"{id}\"（槽位 ID 被改名或删除？），该槽的修改已跳过。");
    }
}
