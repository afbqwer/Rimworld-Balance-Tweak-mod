using System.Collections.Generic;
using Verse;

namespace BalanceTweak;

/// <summary>
/// 一个副炮槽（<c>Fortified.SubTurretProperties</c>）的可编辑快照。
///
/// <para>
/// <b>只存可编辑字段</b>：渲染配置（<c>renderNodeProperty</c> / <c>renderNodeProperties</c>）
/// 永远留在 Def 上，由 <see cref="FortifiedTurret.WriteSlots"/> 原地写回时保持不动。
/// 槽位身份 <see cref="id"/> 随记录一起落盘，Apply 时按它找回 Def 里的同一槽位。
/// </para>
///
/// <para>
/// 字段初值与 Fortified 的 <c>SubTurretProperties</c> 一致（<c>autoAttack = true</c>、
/// <c>warmingTime = 1</c>），这样字段表可以省略默认值，存档文本更短。
/// </para>
/// </summary>
public class TurretSlotRecord
{
    public string id = "";
    public ThingDef? turret = null;
    public string supportedWeaponTag = "";
    public List<string> generateWithWeapons = new();
    /// <summary>表现层角度：仍随记录落盘并原样写回（不改 Def 原值），但编辑窗口不开放。</summary>
    public float idleAngleOffset;
    public float angleOffset;
    public bool autoAttack = true;
    public float warmingTime = 1f;

    public TurretSlotRecord Clone() => new()
    {
        id = id,
        turret = turret,
        supportedWeaponTag = supportedWeaponTag,
        generateWithWeapons = new List<string>(generateWithWeapons ?? new List<string>()),
        idleAngleOffset = idleAngleOffset,
        angleOffset = angleOffset,
        autoAttack = autoAttack,
        warmingTime = warmingTime,
    };

    public static List<TurretSlotRecord> CloneAll(List<TurretSlotRecord>? source)
    {
        var result = new List<TurretSlotRecord>();
        if (source == null) return result;
        foreach (var item in source)
            if (item != null) result.Add(item.Clone());
        return result;
    }
}
