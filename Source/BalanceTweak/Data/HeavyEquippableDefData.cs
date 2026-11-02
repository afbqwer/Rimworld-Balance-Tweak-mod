using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

/// <summary>
/// Fortified 的重型装备档位（<c>Fortified.HeavyEquippableDef</c>）。
///
/// <para>
/// 一个档位描述"这件装备需要多强的身体 / 什么装备才能用"：
/// 体型门槛 + 四组白名单（种族 / 护甲 / Hediff / 基因）。武器通过
/// <c>HeavyEquippableExtension.EquippableDef</c> 指向其中一个档位，那一侧在武器行上编辑
/// （<see cref="WeaponData.heavyTier"/>）。
/// </para>
///
/// <para>
/// <b>判定语义</b>（<c>HeavyEquippableExtension.CanEquippedBy</c>）：四组白名单是 <b>OR</b>，任意一条命中直接放行；
/// 都不命中时才比较体型（<c>pawn.BodySize &gt;= EquippableBaseBodySize</c>）。
/// 体型为负数表示"机载专用"（<c>IsMountedWeapon</c>）：不比较体型，只认白名单。
/// 机械族还有第二道门 <c>MechWeaponExtension</c>（武器标签 / 科技 / 类别过滤），在种族行上编辑。
/// </para>
///
/// <para>
/// <b>空列表安全</b>：Fortified 读取这些列表时用 <c>NullOrEmpty</c> / <c>Contains</c>，
/// 所以 <c>ApplyL</c> 的"清空 = 写空列表"不会出问题；只是 <c>EquippableByRace</c> 在
/// <c>CanEquippedBy(ThingDef)</c> 里是裸 <c>.Contains</c>，绝不能给 null —— 这正好是本工程的既定语义。
/// </para>
/// </summary>
[TweakFor(typeof(Def), SettingType.HeavyEquipment,
          TypeName = FortifiedHeavy.TierDefType,
          MayRequire = TweakDatabase.FFF)]
public sealed class HeavyEquippableDefData : TweakData<HeavyEquippableDefData>
{
    private const string BodySizeField = "EquippableBaseBodySize";
    private const string ByRaceField = "EquippableByRace";
    private const string WithApparelField = "EquippableWithApparel";
    private const string WithHediffField = "EquippableWithHediff";
    private const string WithGeneField = "EquippableWithGene";

    /// <summary>装备所需的最低体型。<b>负数 = 机载专用</b>（不比较体型，只认白名单），不要 clamp。</summary>
    [TweakField]
    public float? equippableBaseBodySize = null;

    /// <summary>种族白名单：这些种族无条件可用（OR 关系）。</summary>
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? equippableByRace = null;

    /// <summary>护甲白名单：穿着任意一件即可用（OR 关系）。</summary>
    [TweakField(Style = ColumnStyle.DefList)]
    public List<ThingDef>? equippableWithApparel = null;

    /// <summary>Hediff 白名单：拥有任意一个即可用（OR 关系，原版仿生臂 / 阿基泰手臂就走这条）。</summary>
    [TweakField(Style = ColumnStyle.DefList)]
    public List<HediffDef>? equippableWithHediff = null;

    /// <summary>基因白名单：拥有任意一个即可用（OR 关系，Biotech）。</summary>
    [TweakField(Style = ColumnStyle.DefList, MayRequire = "ludeon.rimworld.biotech")]
    public List<GeneDef>? equippableWithGene = null;

    /// <summary>只读：体型 &lt; 0，表示这个档位只能装在载具 / 炮塔上（<c>IsMountedWeapon</c>）。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.Bool)]
    public bool? isMountedWeapon = null;

    /// <summary>只读反查：正在使用这个档位的武器 / 装备（点击查看并跳转）。</summary>
    [TweakField(DataType = ColumnDataType.Display, Style = ColumnStyle.WeaponLinks)]
    public TweakID? usedByWeapons = null;

    public override void SetDef(Def def, SettingType type, bool tweaked = false)
    {
        base.SetDef(def, type, tweaked);

        equippableBaseBodySize ??= DataUtility.AsFloat(DataUtility.GetByName(def, BodySizeField));
        // 列表字段必须带守卫：已清空的字段读档后不能被 Def 的现列表"复活"（L0 语义，SC2 自检会报）。
        if (!IsFieldModified(nameof(equippableByRace)))
            equippableByRace ??= DataUtility.GetByName(def, ByRaceField) as List<ThingDef>;
        if (!IsFieldModified(nameof(equippableWithApparel)))
            equippableWithApparel ??= DataUtility.GetByName(def, WithApparelField) as List<ThingDef>;
        if (!IsFieldModified(nameof(equippableWithHediff)))
            equippableWithHediff ??= DataUtility.GetByName(def, WithHediffField) as List<HediffDef>;
        if (!IsFieldModified(nameof(equippableWithGene)))
            equippableWithGene ??= DataUtility.GetByName(def, WithGeneField) as List<GeneDef>;

        isMountedWeapon = (equippableBaseBodySize ?? 0f) < 0f;
        // 基类 SetProps 在字段回填之前算过一次 propType（那时一律是 BodySize），机载档位要在这里修正。
        propType = GetPropType();
    }

    public override void Apply()
    {
        if (def == null) return;

        ApplyV<float>(nameof(equippableBaseBodySize),
            v => DataUtility.SetByName(def, BodySizeField, v));
        ApplyL<ThingDef>(nameof(equippableByRace),
            v => DataUtility.SetByName(def, ByRaceField, v));
        ApplyL<ThingDef>(nameof(equippableWithApparel),
            v => DataUtility.SetByName(def, WithApparelField, v));
        ApplyL<HediffDef>(nameof(equippableWithHediff),
            v => DataUtility.SetByName(def, WithHediffField, v));
        ApplyL<GeneDef>(nameof(equippableWithGene),
            v => DataUtility.SetByName(def, WithGeneField, v));

        isMountedWeapon = (equippableBaseBodySize ?? 0f) < 0f;
    }

    public override List<string> TypeStrings => typeStrings;
    private static readonly List<string> typeStrings =
        typeof(HeavyTierKind).GetEnumNames().Select(s => "MST." + s).ToList();

    private enum HeavyTierKind
    {
        BodySize = 0,
        Mounted = 1,
    }

    public override int GetPropType() => (equippableBaseBodySize ?? 0f) < 0f
        ? (int)HeavyTierKind.Mounted
        : (int)HeavyTierKind.BodySize;
}
