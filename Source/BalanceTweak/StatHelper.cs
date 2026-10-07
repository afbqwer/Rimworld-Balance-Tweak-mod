using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

public static class StatHelper
{
    public static float? TryGetStat(BuildableDef thing, StatDef def)
        => thing.statBases?.Find(s => s.stat == def)?.value;
    public static float? TryGetStat(AbilityDef thing, StatDef def)
        => thing.statBases?.Find(s => s.stat == def)?.value;

    public static float? TryGetStatFromEquippedStat(ThingDef thing, StatDef def)
        => thing.equippedStatOffsets?.Find(s => s.stat == def)?.value;
    public static float? TryGetStatFromStuffOffsetStat(ThingDef thing, StatDef def)
        => thing.stuffProps?.statOffsets?.Find(s => s.stat == def)?.value;
    public static float? TryGetStatFromStuffFactorStat(ThingDef thing, StatDef def)
        => thing.stuffProps?.statFactors?.Find(s => s.stat == def)?.value;

    public static void TrySetStat(BuildableDef def, StatDef stat, float? v, bool toInt = false)
    {
        if (v.HasValue)
        {
            def.statBases ??= new();
            var mod = def.statBases.Find(s => s.stat == stat);
            var val = toInt ? Mathf.Floor(v.Value) : v.Value;
            if (mod != null) mod.value = val;
            else def.statBases.Add(new() { stat = stat, value = val });
        }
        if (v == null && def.statBases != null && def.statBases.ContainsAny(s => s.stat == stat))
            def.statBases.RemoveWhere(s => s.stat == stat);
    }

    public static void TrySetStatFromEquippedStat(ThingDef def, StatDef stat, float? v, bool toInt = false)
    {
        if (v.HasValue)
        {
            def.equippedStatOffsets ??= new();
            var mod = def.equippedStatOffsets.Find(s => s.stat == stat);
            var val = toInt ? Mathf.Floor(v.Value) : v.Value;
            if (mod != null) mod.value = val;
            else def.equippedStatOffsets.Add(new() { stat = stat, value = val });
        }
        if (v == null && def.equippedStatOffsets != null && def.equippedStatOffsets.ContainsAny(s => s.stat == stat))
            def.equippedStatOffsets.RemoveWhere(s => s.stat == stat);
    }

    public static void TrySetStatFromStuffOffsetStat(ThingDef def, StatDef stat, float? v, bool toInt = false)
    {
        if (v.HasValue && def.stuffProps != null)
        {
            def.stuffProps.statOffsets ??= new();
            var mod = def.stuffProps.statOffsets.Find(s => s.stat == stat);
            var val = toInt ? Mathf.Floor(v.Value) : v.Value;
            if (mod != null) mod.value = val;
            else def.stuffProps.statOffsets.Add(new() { stat = stat, value = val });
        }
        if (v == null && def.stuffProps?.statOffsets != null && def.stuffProps.statOffsets.ContainsAny(s => s.stat == stat))
            def.stuffProps.statOffsets.RemoveWhere(s => s.stat == stat);
    }

    public static void TrySetStatFromStuffFactorStat(ThingDef def, StatDef stat, float? v, bool toInt = false)
    {
        if (v.HasValue && def.stuffProps != null)
        {
            def.stuffProps.statFactors ??= new();
            var mod = def.stuffProps.statFactors.Find(s => s.stat == stat);
            var val = toInt ? Mathf.Floor(v.Value) : v.Value;
            if (mod != null) mod.value = val;
            else def.stuffProps.statFactors.Add(new() { stat = stat, value = val });
        }
        if (v == null && def.stuffProps?.statFactors != null && def.stuffProps.statFactors.ContainsAny(s => s.stat == stat))
            def.stuffProps.statFactors.RemoveWhere(s => s.stat == stat);
    }

    public static void TrySetStat(AbilityDef def, StatDef stat, float? v, bool toInt)
    {
        if (v.HasValue)
        {
            def.statBases ??= new();
            var mod = def.statBases.Find(s => s.stat == stat);
            var val = toInt ? Mathf.Floor(v.Value) : v.Value;
            if (mod != null) mod.value = val;
            else def.statBases.Add(new() { stat = stat, value = val });
        }
        if (v == null && def.statBases != null && def.statBases.ContainsAny(s => s.stat == stat))
            def.statBases.RemoveWhere(s => s.stat == stat);
    }
}