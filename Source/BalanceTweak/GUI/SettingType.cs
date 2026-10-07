namespace BalanceTweak;

public partial class BalanceTweakSettings
{
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

        /// <summary>
        /// ThingDef 兜底类型（MiscThingData）。只能追加在枚举末尾：
        /// 存档里存的是成员**名**（Scribe_Values 写 ToString()），改名/删除会让旧数据失效，
        /// 且 curSettingTypeStr[(int)curType] 要求序号从 0 起连续。
        /// </summary>
        Misc = 26,

        /// <summary>身体部位（BodyPartDef）。新增成员必须追加在末尾，不可改名/删除。</summary>
        BodyPart = 27,

        /// <summary>整套身体（BodyDef）。新增成员必须追加在末尾，不可改名/删除。</summary>
        BodyDef = 28,
    }
}