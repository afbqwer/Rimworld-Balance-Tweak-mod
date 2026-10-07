using System.Collections.Generic;
using System.Linq;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;


class ToolData : TweakData<ToolData>
{
    // 字段定义
    [TweakField(DataType = ColumnDataType.Field, Style = ColumnStyle.String, Available = nameof(NotAvailable))]
    public string? toolLabel = null;

    [TweakField(Style = ColumnStyle.String, DataType = ColumnDataType.Display, Available = nameof(AvailableIfRace))]
    public string? bodypartLabel = null;

    [TweakField()]
    public float? damageAmountBase = null;
    [TweakField(GetAbstract = nameof(GetArmorPenetrationAbstract))]
    public float? armorPenetrationBase = null;// -1
    [TweakField(DataType = ColumnDataType.Display)]
    public float? normalarmorPenetrationBase = null;
    [TweakField()]
    public float? cooldownTime = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? damageType = null;
    [TweakField(DataType = ColumnDataType.Display)]
    public float? DPS = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Int)]
    public int? EXdamageNum = null;
    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? EXdamageType = null;
    [TweakField()]
    public float? EXdamageAmountBase = null;
    [TweakField()]
    public float? EXarmorPenetrationBase = null;// -1
    [TweakField(Style = ColumnStyle.Prec)]
    public float? EXchance = null;

    [TweakField(Style = ColumnStyle.DefSelector)]
    public DamageDef? EX2damageType = null;
    [TweakField()]
    public float? EX2damageAmountBase = null;
    [TweakField()]
    public float? EX2armorPenetrationBase = null;
    [TweakField(Style = ColumnStyle.Prec)]
    public float? EX2chance = null;

    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool)]
    public bool? haveSurpriseAttack = null;


    public override int LoadingOrd => 400;
    public DamageDef? damage = null;
    private Tool? tool = null;

    private static float GetArmorPenetrationAbstract(TweakData? data)
    {
        if (data is ToolData td)
        {
            if (td.armorPenetrationBase == null || td.damageAmountBase == null) { return -1f; }
            if (td.armorPenetrationBase == -1) { return td.damageAmountBase.Value * 0.015f; }
            return td.armorPenetrationBase.Value;
        }
        return -1f;
    }

    public override bool SetParentTweak(TweakData data, SettingType type, bool tweaked = false)
    {
        base.SetParentTweak(data, type, tweaked);

        if (data.def is ThingDef def && index.HasValue)
        {
            tool = def.tools[index.Value];
            if (!toolLabel.NullOrEmpty() && tool.untranslatedLabel != toolLabel)
            {
                Log.Warning($"[BalanceTweak]{def} tool的名称错误：{toolLabel}，{tool.untranslatedLabel}");
                return false;
            }
            toolLabel = tool.untranslatedLabel;
            var lab = tool.LabelCap;
            id = new(def.defName + $"_{index}", type);
            label = $"{lab}({def.LabelCap})";
            searchString = $"{def.label}{def.defName}{lab}";
            damageAmountBase ??= tool.power;
            armorPenetrationBase ??= tool.armorPenetration;
            cooldownTime ??= tool.cooldownTime;
            bodypartLabel ??= tool.linkedBodyPartsGroup?.LabelCap;
            if (tool.capacities?.FirstOrDefault() is ToolCapacityDef tc)
            {
                TweakDatabase.allMeleeVerb.TryGetValue(tc, out var v);
                damage = v?.meleeDamageDef;
            }
            damageType ??= damage;
            haveSurpriseAttack ??= tool.surpriseAttack == null;
            var ex = tool.extraMeleeDamages;
            EXdamageNum ??= ex?.Count ?? 0;
            DPS ??= damageAmountBase / cooldownTime;
            normalarmorPenetrationBase ??= tool.armorPenetration == -1 ? tool.power * 0.015f : tool.armorPenetration;
            if (EXdamageNum > 0)
            {
                EXdamageType ??= ex![0].def;
                EXdamageAmountBase ??= ex![0].amount;
                EXarmorPenetrationBase ??= ex![0].armorPenetration;
                EXchance ??= ex![0].chance;
            }
            if (EXdamageNum > 1)
            {
                EX2damageType ??= ex![1].def;
                EX2damageAmountBase ??= ex![1].amount;
                EX2armorPenetrationBase ??= ex![1].armorPenetration;
                EX2chance ??= ex![1].chance;
            }
        }
        else if (data.def is HediffDef hd && index.HasValue)
        {
            var comp = hd.CompProps<HediffCompProperties_VerbGiver>();
            if (comp?.tools == null || index.Value >= comp.tools.Count)
            {
                Log.Error($"[BalanceTweak]{hd}的VerbGiver Comp未找到tool，index={index}");
                return false;
            }
            tool = comp.tools[index.Value];
            if (!toolLabel.NullOrEmpty() && tool.untranslatedLabel != toolLabel)
            {
                Log.Warning($"[BalanceTweak]{hd} tool的名称错误：{toolLabel}，{tool.untranslatedLabel}");
                return false;
            }
            toolLabel = tool.untranslatedLabel;
            var lab = tool.LabelCap;
            id = new($"{hd.defName}_{index}", type);
            label = $"{lab}({hd.LabelCap})";
            searchString = $"{hd.label}{hd.defName}{lab}";
            damageAmountBase ??= tool.power;
            armorPenetrationBase ??= tool.armorPenetration;
            cooldownTime ??= tool.cooldownTime;
            bodypartLabel ??= tool.linkedBodyPartsGroup?.LabelCap;
            if (tool.capacities?.FirstOrDefault() is ToolCapacityDef tc)
            {
                TweakDatabase.allMeleeVerb.TryGetValue(tc, out var v);
                damage ??= v?.meleeDamageDef;
            }
            damageType ??= damage;
            haveSurpriseAttack ??= tool.surpriseAttack == null;
            var ex = tool.extraMeleeDamages;
            EXdamageNum ??= ex?.Count ?? 0;
            DPS ??= damageAmountBase / cooldownTime;
            normalarmorPenetrationBase ??= tool.armorPenetration == -1 ? tool.power * 0.015f : tool.armorPenetration;
            if (EXdamageNum > 0)
            {
                EXdamageType ??= ex![0].def;
                EXdamageAmountBase ??= ex![0].amount;
                EXarmorPenetrationBase ??= ex![0].armorPenetration;
                EXchance ??= ex![0].chance;
            }
            if (EXdamageNum > 1)
            {
                EX2damageType ??= ex![1].def;
                EX2damageAmountBase ??= ex![1].amount;
                EX2armorPenetrationBase ??= ex![1].armorPenetration;
                EX2chance ??= ex![1].chance;
            }
        }
        else
        {
            Log.Error($"[BalanceTweak]{data}的def为{data.def}，目前寻找的index为{index}");
            return false;
        }
        if (data is RaceData rd)
        {
            var ap = GetAllData(parentTweakId);
            foreach (RaceData a in ap)
            {
                a.RaceTool ??= id;
            }
        }
        if (data is WeaponData wd)
        {
            if (wd.WeaponTool == null || (wd.WeaponTool != null && GetData(wd.WeaponTool) is ToolData td && td.DPS < DPS))
            {
                var ap = GetAllData(parentTweakId);
                foreach (WeaponData a in ap)
                {
                    a.WeaponTool ??= id;
                }
            }
        }
        if (data is HediffData hdData)
        {
            var ap = GetAllData(parentTweakId);
            foreach (HediffData a in ap)
            {
                a.VerbGiverTool ??= id;
            }
        }
        return true;
    }

    public override void Apply()
    {
        if (this.tool is not Tool tool)
        {
            Log.Error($"[BalanceTweak]{this}的tool为{this.tool}!");
            return;
        }
        if (damageAmountBase.HasValue) tool.power = damageAmountBase.Value;
        if (armorPenetrationBase.HasValue) tool.armorPenetration = armorPenetrationBase.Value;
        if (cooldownTime.HasValue) tool.cooldownTime = cooldownTime.Value;
        if (tool.capacities?.FirstOrDefault() is ToolCapacityDef tc2 && TweakDatabase.allMeleeVerb.TryGetValue(tc2, out var verb) && damageType != null)
            verb.meleeDamageDef = damageType;
        var ex = tool.extraMeleeDamages;
        if (ex != null && ex.Count > 0)
        {
            if (EXdamageAmountBase.HasValue) { ex[0].amount = EXdamageAmountBase.Value; }
            if (EXarmorPenetrationBase.HasValue) { ex[0].armorPenetration = EXarmorPenetrationBase.Value; }
            if (EXchance.HasValue) { ex[0].chance = EXchance.Value; }
            if (EXdamageType != null) { ex[0].def = EXdamageType; }
        }
        if (ex != null && ex.Count > 1)
        {
            if (EX2damageAmountBase.HasValue) { ex[1].amount = EX2damageAmountBase.Value; }
            if (EX2armorPenetrationBase.HasValue) { ex[1].armorPenetration = EX2armorPenetrationBase.Value; }
            if (EX2chance.HasValue) { ex[1].chance = EX2chance.Value; }
            if (EX2damageType != null) { ex[1].def = EX2damageType; }
        }
        normalarmorPenetrationBase = tool.armorPenetration == -1 ? tool.power * 0.015f : tool.armorPenetration;
        DPS = damageAmountBase / cooldownTime;

        var d = GetData(parentTweakId);
        if (d is RaceData rd)
        {
            rd.MeleeDPS = (float?)RaceData.GetDPS(rd);
        }
        if (d is WeaponData wd)
        {
            wd.GenValues();
        }
    }
    public static bool NotAvailable(TweakData data) => data.propType switch
    {
        _ => false,
    };

    public static bool AvailableIfRace(TweakData data) => data.propType switch
    {
        (int)ToolType.RaceTool => true,
        _ => false,
    };

    // 类型枚举和类型字符串
    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings = typeof(ToolType).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum ToolType
    {
        RaceTool,
        MeleeWeaponTool,
        RangedWeaponTool,
        HediffTool,
    }

    public override int GetPropType()
    {
        switch (parentTweakId!.Value.settingType)
        {
            case SettingType.Race:
                return (int)ToolType.RaceTool;
            case SettingType.Weapon:
                if (GetData(parentTweakId)!.propType == 0) { return (int)ToolType.MeleeWeaponTool; }
                return (int)ToolType.RangedWeaponTool;
            case SettingType.Hediff:
                return (int)ToolType.HediffTool;
        }
        return (int)ToolType.RaceTool;
    }

}


