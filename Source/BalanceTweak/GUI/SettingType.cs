namespace BalanceTweak;

public partial class BalanceTweakSettings
{
    /// <summary>
    /// 页签标识。存档里每条修改的身份是 `(数据类 FullName, defName, 枚举名)`，按<b>名称</b>写入，
    /// 因此成员可以随意重排（保留各自的显式数值即可），但**不可改名 / 删除**。
    /// <para>
    /// 本文件的声明顺序<b>不代表</b>页签的显示顺序 —— 下拉里的顺序只看 <see cref="TabOrder"/>。
    /// 新成员照惯例追加在末尾，想让它排在哪儿由 <see cref="TabOrder"/> 决定。
    /// </para>
    /// </summary>
    public enum SettingType
    {
        None = 0,
        Race = 1,
        Apparel = 2,
        Weapon = 3,
        MeleeTool = 4,
        Ability = 5,
        Stuff = 6,
        Building = 7,
        Projectile = 8,
        Thought = 9,
        ThoughtStage = 10,
        PawnKind = 11,
        Gene = 12,
        Meme = 13,
        Incident = 14,
        Research = 15,
        Weather = 16,
        Faction = 17,
        Food = 18,
        Hediff = 19,
        HediffStage = 20,
        Trait = 21,
        TraitDegree = 22,
        Terrain = 23,
        Recipe = 24,
        Damage = 25,
        Misc = 26,
        BodyPart = 27,
        BodyDef = 28,
        Biome = 29,
        Trader = 30,
        Xenotype = 31,
        ArrivalMode = 32,
        Backstory = 33,
        RaidStrategy = 34,
        Landmark = 35,
        WeaponTrait = 36,
        HeavyEquipment = 37,
        Quest = 38,
    }

    /// <summary>
    /// 页签下拉的显示顺序 —— <b>唯一来源</b>。调整页签顺序只改这里，与枚举声明顺序无关。
    /// <para>
    /// 约束：新增 <see cref="SettingType"/> 成员必须在此追加一项，否则该页签不会出现在下拉里
    /// （`BalanceTweak.Tests/SettingTypeOrderTests` 会在 `dotnet test` 时报红）。
    /// 顺序列表里出现重复项同样会被该测试拦下。
    /// </para>
    /// <para>
    /// 「装没装某个 Mod」不在这里管：可选 Mod 专属页签（如 <see cref="SettingType.HeavyEquipment"/>）
    /// 仍由 `BalanceTweakGUI.IsTabVisible` 在缺 Mod 时整项隐藏，那与顺序是两件事。
    /// </para>
    /// </summary>
    public static readonly SettingType[] TabOrder =
    [
        SettingType.Race,
        SettingType.Apparel,
        SettingType.Weapon,
        SettingType.MeleeTool,
        SettingType.Ability,
        SettingType.Stuff,
        SettingType.Building,
        SettingType.Projectile,
        SettingType.Thought,
        SettingType.ThoughtStage,
        SettingType.PawnKind,
        SettingType.Gene,
        SettingType.Meme,
        SettingType.Incident,
        SettingType.Quest,
        SettingType.Research,
        SettingType.Weather,
        SettingType.Faction,
        SettingType.Food,
        SettingType.Hediff,
        SettingType.HediffStage,
        SettingType.Trait,
        SettingType.TraitDegree,
        SettingType.Terrain,
        SettingType.Recipe,
        SettingType.Damage,
        SettingType.Misc,
        SettingType.BodyPart,
        SettingType.BodyDef,
        SettingType.Biome,
        SettingType.Trader,
        SettingType.ArrivalMode,
        SettingType.Backstory,
        SettingType.RaidStrategy,
        SettingType.Xenotype, // biotech DLC
        SettingType.Landmark, // odyssey DLC
        SettingType.WeaponTrait, // royal DLC
        SettingType.HeavyEquipment, // FFF mod
    ];
}
