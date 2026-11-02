using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BalanceTweak;

/// <summary>
/// <c>List&lt;ExtraDamage&gt;</c> 的列表编辑器，服务 <see cref="WeaponTraitDef.extraDamages"/>
/// （命中时额外附加的一段伤害：燃烧弹、穿甲弹、毒素弹都是这么写的）。
///
/// <para>
/// <b>为什么不复用 <see cref="DamageModListEditorWindow"/></b>：那个编辑的是 <see cref="DamageFactor"/>
/// （伤害类型 + 倍率），这里是"伤害类型 + 绝对值 + 穿甲 + 几率"，字段形状不同，
/// 硬凑会让两边都读起来别扭。元素类型已进字段表，复制 / 粘贴 / 拖拽涂抹仍走
/// <see cref="ListEditorWindow{T}"/> 的默认实现，本类刻意不覆写
/// <c>SerializeList</c> / <c>DeserializeList</c>（契约见 <c>ListEditorWindowContractTests</c>）。
/// </para>
/// <para>
/// <c>armorPenetration</c> 的 <c>-1</c> 是原版语义"按伤害自动折算"（<c>AdjustedArmorPenetration</c>），
/// 不是"没有穿甲"，所以这一格允许负数、不做范围校验。
/// </para>
/// </summary>
public class ExtraDamageListEditorWindow : ListEditorWindow<ExtraDamage>
{
    public ExtraDamageListEditorWindow(StatColumnConfig config, TweakData data) : base(config, data)
    {
        originalList = CloneAll((List<ExtraDamage>?)config.GetRawValue(data));
        workingList = CloneAll(originalList);
    }

    public ExtraDamageListEditorWindow(string title, List<ExtraDamage> initialList, Action<List<ExtraDamage>?> onSave) : base(title, onSave)
    {
        originalList = CloneAll(initialList);
        workingList = CloneAll(originalList);
    }

    /// <summary>逐字段复制：直接引用原对象会让"改到一半点取消"把数据改脏。</summary>
    private static List<ExtraDamage> CloneAll(List<ExtraDamage>? source)
        => source == null
            ? new List<ExtraDamage>()
            : source.Where(e => e != null)
                .Select(e => new ExtraDamage
                {
                    def = e.def,
                    amount = e.amount,
                    armorPenetration = e.armorPenetration,
                    chance = e.chance,
                })
                .ToList();

    protected override string AddButtonLabel => "MST.AddExtraDamage".Translate().RawText;

    // -1 / 1 是原版字段的初值：穿甲"自动折算"、几率 100%。
    protected override void OnAddItem() => workingList.Add(new ExtraDamage
    {
        def = null,
        amount = 1f,
        armorPenetration = -1f,
        chance = 1f,
    });

    /// <summary>没有伤害类型的条目会在弹丸结算时解引用空 def，保存时丢掉（字段里那行还在，补一个就能救回来）。</summary>
    protected override bool IsItemValid(ExtraDamage item) => item.def != null;

    protected override void DrawEntry(Rect rect, int index)
    {
        var entry = workingList[index];
        float curX = rect.x;

        // 先扣掉删除按钮、三组短标签与间距，剩下的按比例分，别写死像素。
        float availableWidth = rect.width - (DeleteButtonWidth + Spacing * 7 + StyleLabelWidth * 3);
        float defButtonWidth = availableWidth * 0.4f;
        float fieldWidth = availableWidth * 0.2f;

        Rect defButtonRect = new(curX, rect.y, defButtonWidth, rect.height);
        string defLabel = entry.def == null
            ? "MST.SelectDamageDef".Translate().RawText
            : $"{entry.def.LabelCap}({entry.def.defName})";
        if (Widgets.ButtonText(defButtonRect, defLabel))
        {
            var allDamageDefs = DefDatabase<DamageDef>.AllDefsListForReading.Cast<Def>().ToList();
            OpenChildWindow(new DefSelectionWindow(
                GetBaseTitle(),
                allDamageDefs,
                entry.def,
                selectedDef => workingList[index].def = (DamageDef?)selectedDef));
        }
        if (entry.def != null && !entry.def.description.NullOrEmpty())
        {
            TooltipHandler.TipRegion(defButtonRect, entry.def.description);
        }
        curX += defButtonWidth + Spacing;

        curX = DrawNumberField(curX, rect, fieldWidth, "MST.Amount", entry.amount, "MST.extraDamageAmountComment",
            v => entry.amount = v);
        curX = DrawNumberField(curX, rect, fieldWidth, "MST.ArmorPenetration", entry.armorPenetration, "MST.extraDamageArmorPenetrationComment",
            v => entry.armorPenetration = v);
        DrawNumberField(curX, rect, fieldWidth, "MST.Chance", entry.chance, "MST.extraDamageChanceComment",
            v => entry.chance = v);

        Rect deleteRect = new(rect.xMax - DeleteButtonWidth, rect.y, DeleteButtonWidth, rect.height);
        if (Widgets.ButtonText(deleteRect, "X"))
        {
            RemoveAtDeferred(index);
        }
    }

    /// <summary>画"短标签 + 输入框"一组，返回下一个可用 x。文本解析不出来就保持原值（不写 0）。</summary>
    private static float DrawNumberField(float curX, Rect rect, float fieldWidth, string labelKey, float value,
        string commentKey, Action<float> set)
    {
        Rect labelRect = new(curX, rect.y, StyleLabelWidth, rect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().RawText);
        Text.Anchor = TextAnchor.UpperLeft;
        curX += StyleLabelWidth;

        Rect fieldRect = new(curX, rect.y, fieldWidth, rect.height);
        string newText = Widgets.TextField(fieldRect, value.ToString("F2"));
        if (float.TryParse(newText, out float parsed)) set(parsed);
        if (commentKey.TryTranslate(out var tip)) TooltipHandler.TipRegion(fieldRect, tip);

        return curX + fieldWidth + Spacing;
    }
}
