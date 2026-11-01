using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>一件服装对某个部位的护甲贡献。护甲按<b>最高值</b>取，不叠层 —— 见 <see cref="ArmorSet"/>。</summary>
internal readonly struct ArmorPiece
{
    public readonly List<BodyPartGroupDef> groups;
    public readonly float sharp;
    public readonly float blunt;

    public ArmorPiece(List<BodyPartGroupDef> groups, float sharp, float blunt)
    {
        this.groups = groups;
        this.sharp = sharp;
        this.blunt = blunt;
    }

    /// <summary>同 <c>ApparelProperties.CoversBodyPart</c>：部位的分组与服装的分组有交集即覆盖。</summary>
    public bool Covers(BodyPartRecord part)
    {
        var groupsOfPart = part.groups;
        if (groupsOfPart == null) return false;
        for (int i = 0; i < groupsOfPart.Count; i++)
        {
            if (groups.Contains(groupsOfPart[i])) return true;
        }
        return false;
    }
}

/// <summary>
/// 一整套服装给出的「部位 → 护甲」映射。
/// <para>
/// <b>取最高值而不是叠层</b>：原版 <c>ArmorUtility.GetPostArmorDamage</c> 是逐层对剩余伤害调用
/// <c>ApplyArmor</c>，穿透值不随层衰减，于是里层的实际收益极低；而原版层数越往里护甲也越低。
/// 两者相乘的结果就是：能决定结果的只有护甲最高的那一件。所以这里只保留最大值。
/// </para>
/// </summary>
internal sealed class ArmorSet
{
    public static readonly ArmorSet Empty = new([]);

    private readonly ArmorPiece[] pieces;

    public ArmorSet(ArmorPiece[] pieces) => this.pieces = pieces;

    public bool IsEmpty => pieces.Length == 0;

    /// <summary>该部位能拿到的最高锐器护甲。</summary>
    public float Sharp(BodyPartRecord part) => Highest(part, sharp: true);

    /// <summary>该部位能拿到的最高钝器护甲。</summary>
    public float Blunt(BodyPartRecord part) => Highest(part, sharp: false);

    private float Highest(BodyPartRecord part, bool sharp)
    {
        float best = 0f;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!pieces[i].Covers(part)) continue;
            float value = sharp ? pieces[i].sharp : pieces[i].blunt;
            if (value > best) best = value;
        }
        return best;
    }
}

/// <summary>
/// 一个兵种的装备画像。
/// <para>
/// <see cref="Ranged"/> 与 <see cref="Melee"/> 都是<b>武器固有价值</b>，不含射手精度与命中率 ——
/// 那些取决于技能，由 <c>CombatPower.Offense</c> 统一乘上去，这样同一个画像能被不同技能的兵种复用。
/// </para>
/// </summary>
internal readonly struct Gear
{
    public readonly float Ranged;
    public readonly float Melee;
    public readonly ArmorSet Armor;

    /// <summary>画像签名，同时是缓存键。防御值按 (race, 签名) 缓存。</summary>
    public readonly string Key;

    public Gear(float ranged, float melee, ArmorSet armor, string key)
    {
        Ranged = ranged;
        Melee = melee;
        Armor = armor;
        Key = key;
    }
}

/// <summary>
/// 装备画像的唯一入口：给一个兵种，算出它「大概会拿什么、穿什么」。
/// <para>
/// 非人形只有一条路：<c>weaponTags</c> + <c>weaponMoney</c> 锁死一两类武器，没有服装。
/// 人形走期望值：候选集比机械大一到两个数量级，所以用标签索引 + 画像缓存，
/// 把「兵种数 × 武器数」压成「画像数 × 候选数」。
/// </para>
/// </summary>
internal static class CombatGear
{
    private sealed class WeaponEntry
    {
        public ThingDef def = null!;
        public float price;
        public float weight;
        public float ranged;
        /// <summary>近战招式价值；-1 表示还没算（惰性，避免建池时就对每种材料各跑一次）。</summary>
        public float melee = -1f;
        public ThingDef? stuff;
        public int id;
    }

    private sealed class ApparelEntry
    {
        public ThingDef def = null!;
        public float price;
        public float sharp;
        public float blunt;
        public int id;
    }

    /// <summary>人形兵种没有声明技能时的默认值。原版射击/近战都走背故事生成，均值落在这附近。</summary>
    private const int DefaultHumanlikeSkill = 5;

    /// <summary>预算取样档数：<c>RandomInRange</c> 是每次生成独立掷的，取几档平均比取 max 更贴近原版。</summary>
    private const int BudgetSamples = 5;

    private static readonly Dictionary<string, Gear> gearCache = [];

    private static List<WeaponEntry>? weaponEntries;
    private static Dictionary<string, List<int>>? weaponByTag;
    private static List<ApparelEntry>? apparelEntries;
    private static Dictionary<string, List<int>>? apparelByTag;
    private static Dictionary<string, ApparelEntry>? apparelByDef;

    /// <summary>去重用的暂存（同一个 pair 可能命中多个标签）。</summary>
    private static readonly HashSet<int> tmpSeen = [];

    /// <summary>防御值按 (种族, 画像) 缓存 —— 一次要遍历几十个部位，不该每行都跑。</summary>
    private static readonly Dictionary<string, float> defenseCache = [];

    public static void Clear()
    {
        gearCache.Clear();
        defenseCache.Clear();
        weaponEntries = null;
        weaponByTag = null;
        apparelEntries = null;
        apparelByTag = null;
        apparelByDef = null;
        tmpSeen.Clear();
    }

    public static float CachedDefense(string raceName, Gear gear, System.Func<float> compute)
    {
        string key = raceName + "#" + gear.Key;
        if (defenseCache.TryGetValue(key, out float cached)) return cached;
        float value = compute();
        defenseCache[key] = value;
        return value;
    }

    #region 画像

    public static Gear Of(PawnKindDef kind)
    {
        string key = Signature(kind);
        if (gearCache.TryGetValue(key, out var gear)) return gear;
        gear = kind.race?.race?.Humanlike == true ? Humanlike(kind, key) : Locked(kind, key);
        gearCache[key] = gear;
        return gear;
    }

    /// <summary>
    /// 画像签名。只用会影响结果的字段：武器标签/预算/材料锁定、服装标签/预算/头盔概率。
    /// 预算取整到 1 银，避免浮点尾数把同一档拆成两个缓存键。
    /// </summary>
    private static string Signature(PawnKindDef kind)
    {
        var sb = new StringBuilder(72);
        AppendTags(sb, kind.weaponTags);
        sb.Append('|').Append(Mathf.RoundToInt(kind.weaponMoney.min)).Append('~')
          .Append(Mathf.RoundToInt(kind.weaponMoney.max));
        sb.Append('|').Append(kind.weaponStuffOverride?.defName ?? "-");
        sb.Append('|');
        AppendTags(sb, kind.apparelTags);
        sb.Append('|').Append(Mathf.RoundToInt(kind.apparelMoney.min)).Append('~')
          .Append(Mathf.RoundToInt(kind.apparelMoney.max));
        sb.Append('|').Append(Mathf.RoundToInt(kind.apparelAllowHeadgearChance * 100f));
        return sb.ToString();
    }

    private static void AppendTags(StringBuilder sb, List<string>? tags)
    {
        if (tags == null || tags.Count == 0) return;
        for (int i = 0; i < tags.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(tags[i]);
        }
    }

    /// <summary>非人形：标签 + 预算锁死候选，按原版权重取期望值，没有服装。</summary>
    private static Gear Locked(PawnKindDef kind, string key)
        => new(CombatPower.ExpectedGunValue(kind), 0f, ArmorSet.Empty, key);

    /// <summary>人形：武器与服装都按候选集求期望。</summary>
    private static Gear Humanlike(PawnKindDef kind, string key)
    {
        float ranged = ExpectedRanged(kind);
        float melee = ExpectedMelee(kind);
        var armor = ExpectedArmor(kind);
        return new Gear(ranged, melee, armor, key);
    }

    #endregion

    #region 武器池

    private static void EnsureWeapons()
    {
        if (weaponEntries != null) return;

        weaponEntries = [];
        weaponByTag = [];

        var pairs = PawnWeaponGenerator.AllWeaponPairs;
        if (pairs == null) return;

        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i];
            var def = pair.thing;
            if (def == null || def.equipmentType != EquipmentType.Primary) continue;
            if (def.weaponTags.NullOrEmpty()) continue;
            // 与锁定装备那条路一致：永不自然生成的独特变体（ChargeLance_Unique 之类）不参与期望。
            if (def.generateAllowChance < 1f) continue;

            float ranged = CombatPower.RangedIntrinsic(def);
            bool hasTools = !def.tools.NullOrEmpty();
            if (ranged <= 0f && !hasTools) continue;

            var entry = new WeaponEntry
            {
                def = def,
                stuff = pair.stuff,
                price = pair.Price,
                // 原版 PawnWeaponGenerator.GetCommonality = Commonality × Price，价格本身就是权重的一部分。
                weight = Mathf.Max(pair.Commonality, 0.0001f) * Mathf.Max(pair.Price, 0.0001f),
                ranged = ranged,
                id = weaponEntries.Count,
            };
            weaponEntries.Add(entry);

            for (int t = 0; t < def.weaponTags.Count; t++)
                Bucket(weaponByTag, def.weaponTags[t]).Add(entry.id);
        }
    }

    /// <summary>
    /// 候选：标签交集 + 预算 + <c>generateAllowChance</c> + 材料锁定。
    /// 预算按 <c>RandomInRange</c> 取样 <see cref="BudgetSamples"/> 档，每档按原版权重求期望后平均。
    /// </summary>
    private static float ExpectedRanged(PawnKindDef kind)
    {
        if (kind.weaponTags.NullOrEmpty()) return 0f;
        EnsureWeapons();
        if (weaponEntries == null || weaponByTag == null) return 0f;

        var candidates = Matching(kind.weaponTags, weaponByTag);
        if (candidates.Count == 0) return 0f;

        float lo = kind.weaponMoney.min, hi = kind.weaponMoney.max;
        float total = 0f;
        int samples = 0;
        for (int i = 0; i < BudgetSamples; i++)
        {
            float budget = BudgetSamples == 1 ? hi : Mathf.Lerp(lo, hi, i / (float)(BudgetSamples - 1));
            float sumWeight = 0f, sumWeighted = 0f;
            for (int c = 0; c < candidates.Count; c++)
            {
                var e = weaponEntries[candidates[c]];
                if (e.ranged <= 0f) continue;
                if (e.price > budget) continue;
                if (kind.weaponStuffOverride != null && e.stuff != kind.weaponStuffOverride) continue;

                sumWeight += e.weight;
                sumWeighted += e.weight * e.ranged;
            }
            if (sumWeight <= 0f) continue;
            total += sumWeighted / sumWeight;
            samples++;
        }
        return samples > 0 ? total / samples : 0f;
    }

    /// <summary>人形近战：同一次选武器，只是取它的招式价值。没有近战招式就是 0（空手由种族招式兜底）。</summary>
    private static float ExpectedMelee(PawnKindDef kind)
    {
        if (kind.weaponTags.NullOrEmpty()) return 0f;
        EnsureWeapons();
        if (weaponEntries == null || weaponByTag == null) return 0f;

        var candidates = Matching(kind.weaponTags, weaponByTag);
        if (candidates.Count == 0) return 0f;

        float budget = (kind.weaponMoney.min + kind.weaponMoney.max) * 0.5f;
        float sumWeight = 0f, sumWeighted = 0f, best = 0f;
        for (int c = 0; c < candidates.Count; c++)
        {
            var e = weaponEntries[candidates[c]];
            if (e.price > budget) continue;
            if (kind.weaponStuffOverride != null && e.stuff != kind.weaponStuffOverride) continue;

            float melee = MeleeValueOf(e);
            if (melee <= 0f) continue;

            sumWeight += e.weight;
            sumWeighted += e.weight * melee;
            if (melee > best) best = melee;
        }
        return sumWeight > 0f ? sumWeighted / sumWeight : best;
    }

    /// <summary>近战招式价值惰性求值 —— 建池时不算，第一次有人真的查近战候选时才算。</summary>
    private static float MeleeValueOf(WeaponEntry entry)
    {
        if (entry.melee < 0f)
            entry.melee = CombatPower.MeleeIntrinsic(entry.def, entry.stuff);
        return entry.melee;
    }

    /// <summary>把若干标签桶取并集（按 id 去重）。桶内是 id 列表，顺序无关。</summary>
    private static List<int> Matching(List<string> tags, Dictionary<string, List<int>> index)
    {
        var result = new List<int>();
        tmpSeen.Clear();
        for (int i = 0; i < tags.Count; i++)
        {
            if (!index.TryGetValue(tags[i], out var bucket)) continue;
            for (int b = 0; b < bucket.Count; b++)
            {
                if (tmpSeen.Add(bucket[b])) result.Add(bucket[b]);
            }
        }
        tmpSeen.Clear();
        return result;
    }

    private static List<int> Bucket(Dictionary<string, List<int>> index, string tag)
    {
        if (!index.TryGetValue(tag, out var list))
        {
            list = [];
            index[tag] = list;
        }
        return list;
    }

    #endregion

    #region 服装池

    private static void EnsureApparel()
    {
        if (apparelEntries != null) return;

        apparelEntries = [];
        apparelByTag = [];
        apparelByDef = [];

        var defs = DefDatabase<ThingDef>.AllDefsListForReading;
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def.apparel == null || def.apparel.tags.NullOrEmpty()) continue;

            var stuff = DataUtility.GetDefaultStuffFromDef(def);
            float sharp = def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp, stuff);
            float blunt = def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt, stuff);
            if (sharp <= 0f && blunt <= 0f) continue;

            var entry = new ApparelEntry
            {
                def = def,
                price = DataUtility.GetDefaultMarketValue(def),
                sharp = sharp,
                blunt = blunt,
                id = apparelEntries.Count,
            };
            apparelEntries.Add(entry);
            apparelByDef[def.defName] = entry;

            for (int t = 0; t < def.apparel.tags.Count; t++)
                Bucket(apparelByTag, def.apparel.tags[t]).Add(entry.id);
        }
    }

    /// <summary>
    /// 服装：按原版「标签交集 + 预算」筛候选，再按护甲降序贪心穿到穿不下为止。
    /// <para>
    /// 不复刻原版的随机贪心（<c>GenerateWorkingPossibleApparelSetFor</c>），只保证「一层一冲突」：
    /// 同层且覆盖部位重叠就不能同时穿。这样预算充足时得到的是「这套预算能穿到的最好一套」。
    /// </para>
    /// </summary>
    private static ArmorSet ExpectedArmor(PawnKindDef kind)
    {
        if (kind.apparelTags.NullOrEmpty()) return ArmorSet.Empty;
        EnsureApparel();
        if (apparelEntries == null || apparelByTag == null || apparelByDef == null) return ArmorSet.Empty;

        float budget = (kind.apparelMoney.min + kind.apparelMoney.max) * 0.5f;
        bool allowHeadgear = kind.apparelAllowHeadgearChance >= 0.5f;

        var chosen = new List<ApparelEntry>();
        var chosenDefs = new List<ThingDef>();

        // 强制服装先占预算（原版 GenerateWorkingPossibleApparelSetFor 也是先穿 required）。
        if (kind.apparelRequired != null)
        {
            for (int i = 0; i < kind.apparelRequired.Count; i++)
            {
                var required = kind.apparelRequired[i];
                if (required == null || !apparelByDef.TryGetValue(required.defName, out var entry)) continue;
                if (!allowHeadgear && PawnApparelGenerator.IsHeadgear(required)) continue;

                chosen.Add(entry);
                chosenDefs.Add(required);
                budget -= entry.price;
            }
        }

        var candidates = Matching(kind.apparelTags, apparelByTag);
        candidates.Sort((a, b) => Score(apparelEntries[b]).CompareTo(Score(apparelEntries[a])));

        for (int c = 0; c < candidates.Count; c++)
        {
            var entry = apparelEntries[candidates[c]];
            if (!allowHeadgear && PawnApparelGenerator.IsHeadgear(entry.def)) continue;
            if (entry.price > budget) continue;
            if (Overlaps(chosenDefs, entry.def)) continue;

            chosen.Add(entry);
            chosenDefs.Add(entry.def);
            budget -= entry.price;
        }

        if (chosen.Count == 0) return ArmorSet.Empty;

        var pieces = new ArmorPiece[chosen.Count];
        for (int i = 0; i < chosen.Count; i++)
        {
            var apparel = chosen[i].def.apparel;
            pieces[i] = new ArmorPiece(apparel.bodyPartGroups, chosen[i].sharp, chosen[i].blunt);
        }
        return new ArmorSet(pieces);
    }

    /// <summary>只按护甲总量排序；保暖、防护等其它维度与战力无关。</summary>
    private static float Score(ApparelEntry entry) => entry.sharp + entry.blunt;

    /// <summary>同层且部位重叠 → 不能同时穿（原版 <c>PairOverlapsAnything</c> 的简化版）。</summary>
    private static bool Overlaps(List<ThingDef> chosen, ThingDef candidate)
    {
        var candidateLayers = candidate.apparel.layers;
        var candidateGroups = candidate.apparel.bodyPartGroups;
        for (int i = 0; i < chosen.Count; i++)
        {
            var other = chosen[i].apparel;
            if (!Shares(candidateLayers, other.layers)) continue;
            if (!Shares(candidateGroups, other.bodyPartGroups)) continue;
            return true;
        }
        return false;
    }

    private static bool Shares<T>(List<T> a, List<T> b)
    {
        for (int i = 0; i < a.Count; i++)
        {
            if (b.Contains(a[i])) return true;
        }
        return false;
    }

    #endregion
}
