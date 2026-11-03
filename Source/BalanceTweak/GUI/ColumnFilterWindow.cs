using UnityEngine;
using Verse;
using static BalanceTweak.BalanceTweakSettings;
using static BalanceTweak.StatColumnConfig;

namespace BalanceTweak;

public partial class BalanceTweakSettings
{
    /// <summary>
    /// 列过滤窗口：从表头右键菜单打开，只改<b>这一列</b>的过滤条件。
    ///
    /// <para>
    /// 条件类型不由用户选 —— 列的字段类型已经决定了它（<see cref="StatColumnConfig.filterKind"/>）：
    /// 数字列与列表列出「大于 / 小于 / 等于 / 范围」+ 数字（项数）框，文本列只有一个搜索框。
    /// 让用户对着一个 float 列选"包含"只会得到一堆无意义的行。
    /// </para>
    /// <para>
    /// 窗口里改的是草稿，点「确定」才写回 <see cref="CurrentColumnFilters"/>；「取消」/点窗外一律不生效。
    /// </para>
    /// </summary>
    public class ColumnFilterWindow : Window
    {
        private const float RowHeight = 30f;
        private const float LabelWidth = 80f;
        private const float Gap = 4f;
        private const float ButtonRowHeight = 32f;

        private readonly StatColumnConfig config;
        private readonly ColumnFilterKind kind;

        // 草稿状态。number 是「单值或区间下限」，numberMax 是区间上限；列表列也走这两个字段（值就是项数）。
        private ColumnFilterMode mode;
        private float number;
        private float numberMax;
        private string numberBuffer;
        private string maxBuffer;
        private string text;

        public ColumnFilterWindow(StatColumnConfig config)
        {
            this.config = config;
            kind = config.filterKind;

            // 已有条件就接着改（再点一次「过滤…」应当是"编辑"而不是"从头来"）
            if (CurrentColumnFilters.TryGetValue(config.id, out var existing))
            {
                mode = existing.mode;
                number = existing.number;
                numberMax = existing.numberMax;
                text = existing.search.Text;
            }
            else
            {
                mode = ColumnFilterMode.Greater;
                number = 0f;
                numberMax = 0f;
                text = "";
            }
            numberBuffer = BufferOf(number);
            maxBuffer = BufferOf(numberMax);

            doCloseX = true;
            forcePause = false;
            draggable = true;
            closeOnClickedOutside = true;
            optionalTitle = "MST.ColumnFilter".Translate().RawText + " - " + config.label;
        }

        /// <summary>
        /// 高度按<b>最坏情况</b>（区间模式要两行输入）给足，不随模式变 ——
        /// 窗口开起来之后再切模式时高度不会跟着长，留白比溢出好。文本条件只有一个框，矮一截。
        /// 余量要够放大字号（UI 缩放会改字高）。
        /// </summary>
        public override Vector2 InitialSize => new(460f, kind == ColumnFilterKind.Text ? 175f : 235f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            float curY = 0f;
            if (kind != ColumnFilterKind.Text) DrawModeRow(ref curY, inRect.width);
            DrawValueRows(ref curY, inRect.width);

            DrawBottomRow(new Rect(0f, inRect.height - ButtonRowHeight, inRect.width, ButtonRowHeight));

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawModeRow(ref float curY, float width)
        {
            Widgets.Label(new Rect(0f, curY, LabelWidth, RowHeight), "MST.FilterCondition".Translate().RawText);

            var modes = new[]
            {
                ColumnFilterMode.Greater, ColumnFilterMode.Less, ColumnFilterMode.Equal, ColumnFilterMode.Range,
            };
            float btnWidth = (width - LabelWidth - Gap * (modes.Length - 1)) / modes.Length;
            float x = LabelWidth;
            foreach (var m in modes)
            {
                Rect btnRect = new(x, curY, btnWidth, RowHeight - 2f);
                // 选中态用文字颜色区分：ButtonText 自己画底纹，在它下面铺色块是看不见的。
                if (Widgets.ButtonText(btnRect, ModeLabel(m), drawBackground: true, doMouseoverSound: true,
                        textColor: m == mode ? Color.yellow : Color.white))
                {
                    mode = m;
                }
                x += btnWidth + Gap;
            }
            curY += RowHeight + Gap;
        }

        /// <summary>
        /// 值输入：文本列一个搜索框，单值模式一行（「过滤值」），区间模式两行（「下限」「上限」）。
        /// 区间不做"留空即无界" —— 单边条件用「大于 / 小于」表达，免得同一个意思有两条路。
        /// </summary>
        private void DrawValueRows(ref float curY, float width)
        {
            if (kind == ColumnFilterKind.Text)
            {
                DrawTextRow(ref curY, width);
                return;
            }
            if (mode != ColumnFilterMode.Range)
            {
                DrawNumberRow(ref curY, width, "MST.FilterValue", ref number, ref numberBuffer);
                return;
            }
            DrawNumberRow(ref curY, width, "MST.FilterLower", ref number, ref numberBuffer);
            DrawNumberRow(ref curY, width, "MST.FilterUpper", ref numberMax, ref maxBuffer);
        }

        private void DrawTextRow(ref float curY, float width)
        {
            Widgets.Label(new Rect(0f, curY, LabelWidth, RowHeight), "MST.FilterText".Translate().RawText);
            Rect fieldRect = new(LabelWidth, curY, width - LabelWidth, RowHeight);
            // 原版搜索的匹配方式（Trim + 不区分大小写的子串），空串 = 不过滤
            GUI.SetNextControlName("BT_ColumnFilterText");
            text = Widgets.TextField(fieldRect, text);
            TooltipHandler.TipRegion(fieldRect, "MST.FilterTextHint".Translate().RawText);
            curY += RowHeight + Gap;
        }

        private void DrawNumberRow(ref float curY, float width, string labelKey, ref float value, ref string buffer)
        {
            Widgets.Label(new Rect(0f, curY, LabelWidth, RowHeight), labelKey.Translate().RawText);

            Rect fieldRect = new(LabelWidth, curY, width - LabelWidth, RowHeight);
            if (kind == ColumnFilterKind.ListCount)
            {
                // 项数不可能为负；上限给个大数只为满足 TextFieldNumeric 的签名。
                // TextFieldNumeric 的缓冲是文本，所以收回来的整数再写回 float 字段，草稿只存一处。
                int countValue = Mathf.RoundToInt(value);
                Widgets.TextFieldNumeric(fieldRect, ref countValue, ref buffer, 0f, 1_000_000f);
                value = countValue;
            }
            else if (config.columnType == ColumnStyle.Prec)
            {
                // Prec 列屏幕上是百分数（值 × 100），过滤值也按百分数收，所以尾巴上标一个 %，
                // 免得用户对着 "95.00%" 填 0.95。先画输入框再画 %，与单元格编辑器的顺序一致。
                Rect percentField = fieldRect.LeftPart(0.9f);
                Widgets.TextFieldNumeric(percentField, ref value, ref buffer, -1e9f, 1e9f);
                Widgets.Label(new Rect(percentField.xMax, fieldRect.y, fieldRect.width - percentField.width, fieldRect.height), "%");
            }
            else
            {
                Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer, -1e9f, 1e9f);
            }
            curY += RowHeight + Gap;
        }

        private void DrawBottomRow(Rect rect)
        {
            float btnWidth = (rect.width - Gap) / 2f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, btnWidth, rect.height), "MST.OK".Translate().RawText))
            {
                Apply();
            }
            if (Widgets.ButtonText(new Rect(rect.x + btnWidth + Gap, rect.y, btnWidth, rect.height), "MST.Cancel".Translate().RawText))
            {
                Close();
            }
        }

        private void Apply()
        {
            // 文本条件为空白 = 没条件。留着它只会让这一列被涂成橙色却一行都不筛
            // （QuickSearchFilter 对空串一律 match，对纯空格则等于"匹配所有非空值"）。
            if (kind == ColumnFilterKind.Text && string.IsNullOrWhiteSpace(text))
            {
                CurrentColumnFilters.Remove(config.id);
            }
            else
            {
                float lower = number;
                float upper = numberMax;
                if (kind == ColumnFilterKind.ListCount)
                {
                    lower = Mathf.RoundToInt(number);
                    upper = Mathf.RoundToInt(numberMax);
                }

                // 上下限填反了就换过来：用户想写 "5~10" 却先在框里打了 10，不该给他一个空表。
                if (mode == ColumnFilterMode.Range && lower > upper)
                {
                    (lower, upper) = (upper, lower);
                }

                CurrentColumnFilters[config.id] = new ColumnFilter(config, mode, lower, upper, text);
            }
            TweakUtility.MarkFilterDirty();
            Close();
        }

        private string BufferOf(float value)
            => (kind == ColumnFilterKind.ListCount ? Mathf.RoundToInt(value) : value).ToString();

        private static string ModeLabel(ColumnFilterMode mode) => mode switch
        {
            ColumnFilterMode.Greater => "MST.FilterGreater".Translate().RawText,
            ColumnFilterMode.Less => "MST.FilterLess".Translate().RawText,
            ColumnFilterMode.Equal => "MST.FilterEqual".Translate().RawText,
            _ => "MST.FilterRange".Translate().RawText,
        };
    }
}
