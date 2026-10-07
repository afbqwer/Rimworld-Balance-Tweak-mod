namespace BalanceTweak;

public partial class BalanceTweakSettings
{
    /// <summary>新增成员必须追加在末尾；不可改名/删除/变更已有成员的顺序。</summary>
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
    }
}