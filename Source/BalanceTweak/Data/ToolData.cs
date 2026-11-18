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
    // 这一格是关联招式类型行（ManeuverData）的镜像：值属于那一行，这里只是副本。
    // IsMirror 让它在 EqualsData 里被跳过 —— 全表共用同一份伤害类型，别的武器改了它，
    // 没改过的这一行"副本跟着变"是常态，参与判定会让脏行常驻已修改表。
    // 编辑它的动作由 Apply → PushToolEditsToManeuver 推给招式类型行，改动归属也落在那边。
    [TweakField(Style = ColumnStyle.DefSelector, IsMirror = true)]
    public DamageDef? damageType = null;
    /// <summary>本招式对应的招式类型行（按 <c>tool.capacities[0]</c> 反查）。Display 字段，不落盘。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Link)]
    public TweakID? Maneuver = null;
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
    private Tool? tool = null;

    /// <summary>本招式对应的招式类型行 id（只按 <c>tool.capacities[0]</c> 反查，不建实例）。</summary>
    internal static TweakID? ManeuverIdOf(Tool? tool)
    {
        var capacity = tool?.capacities?.FirstOrDefault();
        if (capacity == null) return null;
        return TweakDatabase.maneuverDatas.TryGetValue(capacity.defName, out var id) ? id : null;
    }

    /// <summary>
    /// 旧档迁移用：按父项 + <see cref="TweakData.index"/> 找回本行的 <see cref="Tool"/>。
    /// <para>
    /// 迁移跑在每个招式行走 <see cref="SetParentTweak"/> 之前，此刻私有字段 <c>tool</c> 还是 null，
    /// 父项实例的 def 也还没绑定（<c>TryApplyData</c> 才 SetDef）。
    /// </para>
    /// </summary>
    internal Tool? FindTool()
    {
        if (index is not int i || i < 0) return null;
        if (parentTweakId is not TweakID pid) return null;
        // 迁移期间调用方会把 buildingOriginals 置上（见 TweakDatabase.MigrateToolDamageTypes），
        // 所以这里补建出来的父项快照读的是原始层，不会把别的行的现值当原值。
        var parentDef = TweakDatabase.GetOriginal(pid)?.def ?? GetLiveData(pid)?.def;
        if (parentDef is ThingDef d)
            return d.tools != null && i < d.tools.Count ? d.tools[i] : null;
        if (parentDef is HediffDef h)
        {
            var comp = h.CompProps<HediffCompProperties_VerbGiver>();
            return comp?.tools != null && i < comp.tools.Count ? comp.tools[i] : null;
        }
        return null;
    }

    /// <summary>
    /// 把本行接上「招式类型」行：解析链接、同步镜像、登记共享关系。
    /// <para>
    /// 两个 def 分支（<see cref="ThingDef"/> / <see cref="HediffDef"/>）都要走它 —— 招式的身份只由
    /// <c>capacities[0]</c> 决定，与宿主是武器、种族还是异体无关。
    /// </para>
    /// </summary>
    private void ResolveManeuver(Tool tool)
    {
        var capacity = tool.capacities?.FirstOrDefault();
        if (capacity == null) return; // 没有能力 = 没有对应的共享行，属正常情况
        if (ManeuverIdOf(tool) is not TweakID mid)
        {
            WarnManeuverUnresolved(capacity, "索引里没有这个能力");
            return;
        }
        Maneuver = mid;
        // 建快照期间必须读原始层：存档里的修改此刻已经反序列化好了，走活实例会把"改后值"写进快照，
        // 之后 Revert / ResetTo 还原出来的就是改后的值（与 WeaponData.ResolveProjectile 同一个理由）。
        // 窗口打开后补建的父项走不到这里的分支：那时 buildingOriginals 已是 false，读活层才是对的。
        var md = (TweakDatabase.buildingOriginals
            ? TweakDatabase.GetOriginal(mid) ?? GetLiveData(mid)
            : GetLiveData(mid)) as ManeuverData;
        if (md == null)
        {
            // 与"索引里没有"是两件事：索引有这条，但行既没建也补不出来（Def 丢了 / SetDef 抛了）。
            // 分开写是因为排查方向完全不同 —— 前者查 [TweakFor] 与 BuildLookupIndices，后者查补建日志。
            WarnManeuverUnresolved(capacity, $"索引有这一项（{mid}），但取不到它的行");
            return;
        }
        // 必须**赋值**（不是 ??=）：镜像的源在共享行，用 ??= 会让共享行之后的改动永远刷不进来。
        // 只跳过"用户还没推出去的修改"（迁移没搬成功的那种），否则会把他保存的值静默吃掉；
        // 那种值会在 Apply 的 PushToolEditsToManeuver 里推给共享行。
        if (!IsFieldModified(nameof(damageType))) damageType = md.meleeDamageDef;
        // 每次 SetParentTweak 都登记，不能只在"首次解析"时做：
        // 读了存档的行 Maneuver 已由上面的解析重建，走不到任何"只此一次"的分支，漏登记就再也不刷新。
        md.RegisterSharedTool(id);
    }

    /// <summary>已告警过的能力（每个能力每局一次，避免几千行招式刷屏）。</summary>
    private static readonly HashSet<string> _warnedUnresolvedManeuvers = new();
    private static bool _warnedManeuverTableEmpty;

    private static void WarnManeuverUnresolved(ToolCapacityDef capacity, string why)
    {
        if (TweakDatabase.maneuverDatas.Count == 0)
        {
            if (_warnedManeuverTableEmpty) return;
            _warnedManeuverTableEmpty = true;
            Log.Error("[BalanceTweak] 招式类型索引为空：招式行的伤害类型镜像无法同步，在招式行改伤害类型也无处落盘。"
                + "检查 ManeuverData 的 [TweakFor] 是否仍注册，以及 TweakDatabase.BuildLookupIndices 是否还填了 maneuverDatas。");
            return;
        }
        if (!_warnedUnresolvedManeuvers.Add(capacity.defName)) return;
        Log.Warning($"[BalanceTweak] 能力 {capacity.defName} 没有对应的招式类型行（{why}，索引共 {TweakDatabase.maneuverDatas.Count} 项）："
            + "相关招式行的伤害类型不会跟随招式类型页，在招式行改伤害类型也不会生效。");
    }

    /// <summary>
    /// 共享的招式类型（<see cref="ManeuverData"/>）改动后刷新本行：对齐镜像、重算派生值并通知父项。
    /// <para>
    /// 由 <see cref="ManeuverData.RefreshSharedTools"/> 调用。父项必须一起刷：武器的 DPS / 强度
    /// 与种族的近战 DPS 都是按"招式 + 材质"算出来的，只刷招式行等于让上层数字停在旧值。
    /// </para>
    /// </summary>
    internal void RefreshManeuverMirror()
    {
        // 还没绑定 def 的行（初始化期被别的行刷到）：此刻没有可信的父项，宁可留旧值。
        if (def == null) return;
        SyncMirrorFromManeuver();
        // 原始快照是"原值基准"，派生同步不得写它（Readme §4.5）。
        if (GetLiveData(parentTweakId) is WeaponData wd && !ReferenceEquals(TweakDatabase.GetOriginal(wd.id), wd))
            wd.GenValues();
        else if (GetLiveData(parentTweakId) is RaceData rd)
            rd.MeleeDPS = (float?)RaceData.GetDPS(rd);
    }

    /// <summary>
    /// 把镜像对齐到共享行。只动"用户没改过"的那一格 —— 改过的要由 <see cref="Apply"/>
    /// 推给共享行（<see cref="PushToolEditsToManeuver"/>），这里覆盖等于把他的输入静默吃掉。
    /// </summary>
    private void SyncMirrorFromManeuver()
    {
        if (Maneuver is not TweakID mid) return;
        if (GetLiveData(mid) is not ManeuverData md || md.def == null) return;
        if (!IsFieldModified(nameof(damageType))) damageType = md.meleeDamageDef;
    }

    /// <summary>
    /// 这一格是否正被「还原」（右键重置此格 / 重置此列）。<c>ResetTo</c> 先写回原值再清标记，
    /// 那一刻三态已是 <see cref="FieldState.Unset"/> —— 只看三态会把重置误判成"没动过"，
    /// 镜像列的重置于是静默失效（同 <see cref="WeaponData"/>）。
    /// </summary>
    private bool IsFieldBeingReset(string field)
        => !forceWriteAll && forceWriteFields?.Contains(field) == true;

    /// <summary>
    /// 把招式行上的伤害类型（本质是共享招式类型行的镜像）推给招式类型行。
    /// <para>
    /// <b>只在用户显式动过这一格时写</b>：改过就写新值，清空 / 重置就让共享行自己恢复原值，
    /// 没动过就什么都不写 —— 招式行的这一格在 <see cref="RefreshManeuverMirror"/> 里按共享行同步，
    /// 属于派生镜像；若每次 Apply 都照镜像盖回去，任何一把共用同一能力的武器
    /// （所有 Cut 武器共用一个 ManeuverDef）在改自身其它字段时都会把另一处的修改静默撤销。
    /// </para>
    /// <para>
    /// 改动还要登记到共享行那一行：否则它的 <c>ApplyD</c> 会因三态未改而跳过，值只停在内存里。
    /// </para>
    /// </summary>
    private void PushToolEditsToManeuver()
    {
        if (Maneuver is not TweakID mid) return;

        // 先判有没有动过，再取可写实例：没动过时 GetEditableData 会白克隆一份招式类型行
        // （并把克隆塞进 UI 缓存），而这条路径在每次招式行 Apply 都会走一遍。
        bool resetting = IsFieldBeingReset(nameof(damageType))
                      || StateOf(nameof(damageType)) == FieldState.Cleared;
        bool editing = !resetting && StateOf(nameof(damageType)) == FieldState.Set && damageType != null;
        if (!resetting && !editing) return;

        if (GetEditableData(mid) is not ManeuverData md || md.def == null) return;

        if (resetting)
        {
            // 让共享行自己按"显式清空 / 还原"恢复原值（ApplyD 会走 Revert），不必在这里抄原值。
            md.MarkFieldCleared(nameof(ManeuverData.meleeDamageDef));
        }
        else
        {
            md.meleeDamageDef = damageType;
            md.MarkFieldModified(nameof(ManeuverData.meleeDamageDef));
        }

        md.MarkIndirectlyModified();
        // 写完 def 还要刷镜像：同一能力的其它招式行（其它武器 / 种族 / 异体）都读这一份。
        md.SetManeuverProp(md.def, refreshTools: true);

        // 改动归属已经转移到招式类型行，本行不再持有这一格的标记 —— 否则本行会亮"已修改"并给出
        // 一个按"只重置本行"规则不会回写共享行的假重置按钮。
        RemoveFieldModified(nameof(damageType));
        if (CountFieldModified() == 0)
        {
            tweaked = false;
            // 一行都没改了，就别再占一个存档条目 —— 否则读档时 SetParentTweak(tweaked: true) 又会点亮"已修改"。
            tweakDatas.Remove(id);
        }
    }

    public override void EnsureDisplayValues()
    {
        if (displayValuesEnsured) return;
        // 环境没就绪就别置标志：在 def / 共享行缺席时算出的 null 会永久留着。
        if (def == null) return;
        displayValuesEnsured = true;
        // 克隆行是从原始快照来的，镜像带着"建快照那一刻"的值；共享行在那之后被改过就要跟上来。
        if (!ReferenceEquals(TweakDatabase.GetOriginal(id), this))
            SyncMirrorFromManeuver();
    }

    /// <summary>标题是"招式名(武器名)"：单看招式名（Cut / Stab）分不清是哪把武器的哪一招。</summary>
    protected override string ResolveLabel()
        => tool == null ? base.ResolveLabel() : $"{tool.LabelCap}({def?.LabelCap})";

    /// <summary>招式名与武器名都要能搜到（玩家常直接搜武器）。</summary>
    protected override string ResolveSearchString()
        => $"{def?.label}{def?.defName}{tool?.LabelCap}";

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
            id = new(def.defName + $"_{index}", type);
            damageAmountBase ??= tool.power;
            armorPenetrationBase ??= tool.armorPenetration;
            cooldownTime ??= tool.cooldownTime;
            bodypartLabel ??= tool.linkedBodyPartsGroup?.LabelCap;
            ResolveManeuver(tool);
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
            id = new($"{hd.defName}_{index}", type);
            damageAmountBase ??= tool.power;
            armorPenetrationBase ??= tool.armorPenetration;
            cooldownTime ??= tool.cooldownTime;
            bodypartLabel ??= tool.linkedBodyPartsGroup?.LabelCap;
            ResolveManeuver(tool);
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
        ApplyV<float>(nameof(damageAmountBase), v => tool.power = v);
        ApplyV<float>(nameof(armorPenetrationBase), v => tool.armorPenetration = v);
        ApplyV<float>(nameof(cooldownTime), v => tool.cooldownTime = v);
        // damageType 是共享招式类型行的镜像：不再直接写 verb（那会悄悄改掉全表同能力的招式），
        // 只在用户真的动过这一格时把值推给那一行（见 PushToolEditsToManeuver）。
        PushToolEditsToManeuver();
        var ex = tool.extraMeleeDamages;
        if (ex != null && ex.Count > 0)
        {
            ApplyV<float>(nameof(EXdamageAmountBase), v => ex[0].amount = v);
            ApplyV<float>(nameof(EXarmorPenetrationBase), v => ex[0].armorPenetration = v);
            ApplyV<float>(nameof(EXchance), v => ex[0].chance = v);
            ApplyD<DamageDef>(nameof(EXdamageType), v => ex[0].def = v);
        }
        if (ex != null && ex.Count > 1)
        {
            ApplyV<float>(nameof(EX2damageAmountBase), v => ex[1].amount = v);
            ApplyV<float>(nameof(EX2armorPenetrationBase), v => ex[1].armorPenetration = v);
            ApplyV<float>(nameof(EX2chance), v => ex[1].chance = v);
            ApplyD<DamageDef>(nameof(EX2damageType), v => ex[1].def = v);
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
        // Apply 把该算的都算过了（DPS / 穿透摘要都是按当前值现写的），标"已就绪"而不是置脏：
        // 调用方紧接着会 EnsureDisplayValues，置脏等于把同一份派生值再算一遍。
        displayValuesEnsured = true;
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


